using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

public sealed class SelectionApplier
{
    private readonly InsertionCandidateGenerator _generator;
    private readonly CandidateFleetSelector _selector;
    private readonly PhysicalPlanValidator _validator;

    public SelectionApplier(
        InsertionCandidateGenerator? generator = null,
        CandidateFleetSelector? selector = null,
        PhysicalPlanValidator? validator = null)
    {
        _generator = generator ?? new InsertionCandidateGenerator();
        _selector = selector ?? new CandidateFleetSelector();
        _validator = validator ?? new PhysicalPlanValidator();
    }

    public DispatchDecisionResult Decide(
        OnlineState state,
        CandidateGenerationOptions options,
        CommitmentCandidateFilter? commitmentFilter = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);

        var generated = _generator.Generate(state, options);

        if (!generated.IsSuccess)
        {
            return DispatchDecisionResult.Failure(
                new RollingCostWitness(
                    DecisionFailureCodes.CandidateGenerationFailed,
                    generated.Witness!.Message,
                    generated.Witness.VehicleId,
                    generated.Witness.RequestId,
                    Dimension: generated.Witness.Dimension));
        }

        var candidates = commitmentFilter is null
            ? generated.VehicleCandidates!
            : commitmentFilter.Filter(state, generated.VehicleCandidates!);
        var selection = _selector.Select(candidates);

        if (!selection.IsSuccess)
        {
            return DispatchDecisionResult.Failure(selection.Witness!);
        }

        var validationFailure = ValidateSelection(
            state,
            selection.Selection!,
            _validator);

        if (validationFailure is not null)
        {
            return DispatchDecisionResult.Failure(validationFailure);
        }

        var applied = ApplySelection(
            state,
            selection.Selection!,
            candidates);

        return applied.IsSuccess
            ? applied
            : DispatchDecisionResult.Failure(applied.Witness!);
    }

    /// <summary>
    /// The selected waiting-rider move (G2): null when no selected plan has a relocation role,
    /// otherwise exactly one donor and one receiver of the same rider, the donor being the
    /// rider's vehicle. Anything else is a malformed selection.
    /// </summary>
    internal static (WaitingRiderRelocation? Relocation, RollingCostWitness? Witness)
        SelectedRelocation(OnlineState state, FleetSelection selection)
    {
        var donors = selection.VehiclePlans
            .Where(plan => plan.Candidate.RelocationRole == RelocationRole.Donor)
            .ToArray();
        var receivers = selection.VehiclePlans
            .Where(plan => plan.Candidate.RelocationRole == RelocationRole.Receiver)
            .ToArray();

        if (donors.Length == 0 && receivers.Length == 0)
        {
            return (null, null);
        }

        if (donors.Length != 1
            || receivers.Length != 1
            || donors[0].Candidate.RelocatedRequestId is not RequestId requestId
            || receivers[0].Candidate.RelocatedRequestId != requestId
            || !state.Run.Requests.TryGetValue(requestId, out var request)
            || request.AssignedVehicleId != donors[0].VehicleId
            || receivers[0].VehicleId == donors[0].VehicleId)
        {
            return (
                null,
                new RollingCostWitness(
                    DecisionFailureCodes.SelectedCandidateInvalid,
                    "A selected relocation needs exactly one donor (the rider's vehicle) and one " +
                    "receiver of the same waiting rider.",
                    donors.FirstOrDefault()?.VehicleId ?? receivers[0].VehicleId,
                    donors.FirstOrDefault()?.Candidate.RelocatedRequestId
                        ?? receivers.FirstOrDefault()?.Candidate.RelocatedRequestId,
                    donors.FirstOrDefault()?.Candidate.CandidateId ?? receivers[0].Candidate.CandidateId,
                    "relocation"));
        }

        return (
            new WaitingRiderRelocation(requestId, donors[0].VehicleId, receivers[0].VehicleId),
            null);
    }

    internal static RollingCostWitness? ValidateSelection(
        OnlineState state,
        FleetSelection selection,
        PhysicalPlanValidator validator)
    {
        var (relocation, relocationWitness) = SelectedRelocation(state, selection);

        if (relocationWitness is not null)
        {
            return relocationWitness;
        }

        foreach (var plan in selection.VehiclePlans)
        {
            var validation = validator.ValidateWithExogenousRelief(
                state.Run,
                plan.VehicleId,
                plan.Candidate.Route,
                state.TravelTimes!,
                state.Run.SimulationTime,
                relocation);

            if (!validation.IsFeasible)
            {
                return new RollingCostWitness(
                    DecisionFailureCodes.SelectedCandidateInvalid,
                    validation.Witness!.Message,
                    plan.VehicleId,
                    validation.Witness.RequestId,
                    plan.Candidate.CandidateId,
                    validation.Witness.Dimension);
            }
        }

        return null;
    }

    /// <summary>
    /// Applies a selection with one waiting-rider move: every changed route first (the donor
    /// without the rider, the receiver with it), then the atomic reassignment, then the new
    /// requests vehicle by vehicle, all in ordinal order.
    /// </summary>
    private static (RideBoundRun? Run, RollingCostWitness? Witness) ApplyRelocatedSelection(
        RideBoundRun run,
        FleetSelection selection,
        WaitingRiderRelocation relocation)
    {
        var plans = selection.VehiclePlans
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .ToArray();

        foreach (var plan in plans.Where(value => !value.Candidate.IsNoOp))
        {
            var route = run.UpdateVehicleRoute(plan.VehicleId, plan.Candidate.Route);

            if (!route.IsSuccess)
            {
                return (null, ApplyFailure(route.Failure!, plan.VehicleId, plan.Candidate.CandidateId).Witness);
            }

            run = route.Value!;
        }

        var reassigned = run.ReassignWaitingRequest(relocation.RequestId, relocation.ReceiverVehicleId);

        if (!reassigned.IsSuccess)
        {
            return (
                null,
                ApplyFailure(
                    reassigned.Failure!,
                    relocation.ReceiverVehicleId,
                    plans.Single(value => value.VehicleId == relocation.ReceiverVehicleId).Candidate.CandidateId,
                    relocation.RequestId).Witness);
        }

        run = reassigned.Value!;

        foreach (var plan in plans)
        {
            foreach (var requestId in plan.Candidate.NewRequestIds.OrderBy(
                         value => value.Value,
                         StringComparer.Ordinal))
            {
                var accepted = run.AcceptRequest(requestId, plan.VehicleId);

                if (!accepted.IsSuccess)
                {
                    return (
                        null,
                        ApplyFailure(accepted.Failure!, plan.VehicleId, plan.Candidate.CandidateId, requestId).Witness);
                }

                run = accepted.Value!;
            }
        }

        return (run, null);
    }

    internal static DispatchDecisionResult ApplySelection(
        OnlineState state,
        FleetSelection selection,
        IReadOnlyList<VehicleCandidateSet> generated)
    {
        var run = state.Run;
        var selectedRequests = selection.VehiclePlans
            .SelectMany(value => value.Candidate.NewRequestIds)
            .ToHashSet();
        var feasibleRequests = generated
            .SelectMany(value => value.Candidates)
            .SelectMany(value => value.NewRequestIds)
            .ToHashSet();
        var pruned = generated
            .SelectMany(value => value.PrunedCandidates)
            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToArray();
        var (relocation, relocationWitness) = SelectedRelocation(state, selection);

        if (relocationWitness is not null)
        {
            return DispatchDecisionResult.Failure(relocationWitness);
        }

        if (relocation is not null)
        {
            var moved = ApplyRelocatedSelection(run, selection, relocation);

            if (moved.Witness is not null)
            {
                return DispatchDecisionResult.Failure(moved.Witness);
            }

            run = moved.Run!;
        }

        // Without a move this is the archived per-vehicle loop; with one, it was applied above.
        foreach (var plan in (relocation is not null
                         ? Array.Empty<SelectedVehiclePlan>()
                         : selection.VehiclePlans)
                     .OrderBy(
                         value => value.VehicleId.Value,
                         StringComparer.Ordinal))
        {
            if (!plan.Candidate.IsNoOp)
            {
                var route = run.UpdateVehicleRoute(
                    plan.VehicleId,
                    plan.Candidate.Route);

                if (!route.IsSuccess)
                {
                    return ApplyFailure(
                        route.Failure!,
                        plan.VehicleId,
                        plan.Candidate.CandidateId);
                }

                run = route.Value!;
            }

            foreach (var requestId in plan.Candidate.NewRequestIds.OrderBy(
                         value => value.Value,
                         StringComparer.Ordinal))
            {
                var accepted = run.AcceptRequest(requestId, plan.VehicleId);

                if (!accepted.IsSuccess)
                {
                    return ApplyFailure(
                        accepted.Failure!,
                        plan.VehicleId,
                        plan.Candidate.CandidateId,
                        requestId);
                }

                run = accepted.Value!;
            }
        }

        var actions = new List<RequestDecisionAction>();
        var pending = state.Run.Requests.Values
            .Where(value => value.Lifecycle == RequestLifecycle.Pending)
            .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
            .ToArray();

        foreach (var request in pending)
        {
            if (selectedRequests.Contains(request.Id))
            {
                var selectedPlan = selection.VehiclePlans.Single(
                    value => value.Candidate.NewRequestIds.Contains(request.Id));
                actions.Add(
                    new RequestDecisionAction(
                        request.Id,
                        RequestDecisionOutcome.Accepted,
                        RollingCostReasonCodes.Accepted,
                        selectedPlan.VehicleId,
                        selectedPlan.Candidate.CandidateId));
                continue;
            }

            if (feasibleRequests.Contains(request.Id))
            {
                actions.Add(
                    new RequestDecisionAction(
                        request.Id,
                        RequestDecisionOutcome.Deferred,
                        RollingCostReasonCodes.FleetSelectionConflict));
                continue;
            }

            var relatedPrunes = pruned
                .Where(value => value.NewRequestIds.Contains(request.Id))
                .ToArray();

            if (relatedPrunes.Length == 0)
            {
                actions.Add(
                    new RequestDecisionAction(
                        request.Id,
                        RequestDecisionOutcome.Deferred,
                        "CANDIDATE_BOUND"));
                continue;
            }

            var rejected = run.RejectRequest(request.Id);

            if (!rejected.IsSuccess)
            {
                return ApplyFailure(rejected.Failure!, requestId: request.Id);
            }

            run = rejected.Value!;
            actions.Add(
                new RequestDecisionAction(
                    request.Id,
                    RequestDecisionOutcome.Rejected,
                    FindRejectionReason(request.Id, relatedPrunes)));
        }

        return DispatchDecisionResult.Success(
            new DispatchDecision(
                state with { Run = run },
                selection.VehiclePlans,
                actions.AsReadOnly(),
                pruned,
                selection.AcceptedRequestCount,
                selection.OperationalCost));
    }

    private static string FindRejectionReason(
        RequestId requestId,
        IReadOnlyList<CandidatePruneWitness> pruned)
    {
        var reasonCodes = pruned
            .Where(value => value.NewRequestIds.Contains(requestId))
            .SelectMany(
                value => value.CommitmentWitnesses is { Count: > 0 }
                    ? value.CommitmentWitnesses.Select(witness => witness.Code)
                    : [value.Code])
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();

        return reasonCodes.Length == 1
            ? reasonCodes[0]
            : RollingCostReasonCodes.NoFeasibleInsertion;
    }

    private static DispatchDecisionResult ApplyFailure(
        DomainFailure failure,
        VehicleId? vehicleId = null,
        string? candidateId = null,
        RequestId? requestId = null) =>
        DispatchDecisionResult.Failure(
            new RollingCostWitness(
                DecisionFailureCodes.DecisionApplyFailed,
                failure.Message,
                vehicleId,
                requestId,
                candidateId,
                failure.Dimension));
}
