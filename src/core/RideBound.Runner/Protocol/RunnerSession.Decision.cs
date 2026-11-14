using System.Text.Json;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.Events;
using RideBound.Application.Optimization;
using RideBound.Contracts.Protocol;
using RideBound.Domain.Commitments;
using RideBound.Runner.Mapping;
using RideBound.Runner.StateAudit;

namespace RideBound.Runner.Protocol;

// The dispatch decision for one event batch: map and reduce the events, run the policy, validate its result,
// record exogenous breaches, give new riders their Shadow class and build the certificate.
public sealed partial class RunnerSession
{
    /// <summary>
    /// ADR-089 GĐ4: the configured planning forecast rides on every travel snapshot the adapter
    /// sends (and on a restored one, see ProcessCheckpoint). One instance per snapshot, so the
    /// reference comparisons of snapshots keep their meaning; without a forecast the batch is
    /// returned as it is.
    /// </summary>
    private InternalEventBatch WithPlanningForecast(InternalEventBatch batch)
    {
        if (_wp4Configuration.PlanningForecast is not { } forecast
            || !batch.Events.Any(value => value is TravelTimesUpdated))
        {
            return batch;
        }

        return batch with
        {
            Events = batch.Events
                .Select(value => value is TravelTimesUpdated travel
                    ? travel with { Snapshot = travel.Snapshot.WithForecast(forecast) }
                    : value)
                .ToArray(),
        };
    }

    private OnlineDecisionBuildResult BuildOnlineDecision(
        ProtocolEnvelope envelope,
        string publicationScope,
        Sha256Hex? lightStateAfterHash)
    {
        if (_onlineCoordinator is null)
        {
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                "Online coordinator was not initialized.");
        }

        var beforeEventState = _onlineCoordinator.CommittedState;
        var mapped = _onlineEventMapper.Map(envelope);

        if (!mapped.IsSuccess)
        {
            return OnlineDecisionBuildResult.Fail(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                mapped.Witness!.Message);
        }

        var batch = WithPlanningForecast(mapped.Batch!);
        var reduced = _onlineCoordinator.Propose(batch);

        if (!reduced.IsSuccess)
        {
            return OnlineDecisionBuildResult.Fail(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                reduced.Witness!.Message);
        }

        var sourceEventSequence = batch.Events[^1].EventSequence;
        var context = new CommitmentMechanismContext(
            beforeEventState,
            reduced.ProposedState!,
            _commitmentPolicies,
            _stopDistances,
            publicationScope,
            sourceEventSequence);

        var solved = _solverBackedPolicy!.Decide(
            context,
            _wp4Configuration.CandidateGeneration,
            _useManifestSolverSeed
                ? _wp4Configuration.CreateSolverPolicyOptionsForRun(
                    _identity!.Manifest.MasterSeed)
                : _wp4Configuration.SolverPolicyOptions!);

        if (!solved.IsSuccess)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                solved.Witness!.Message);
        }

        var decision = DispatchDecisionResult.Success(
            solved.Decision!.Decision);
        var validationPolicies = solved.Decision.EffectivePolicies;

        if (!decision.IsSuccess)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                decision.Witness!.Message);
        }

        var validation = _commitmentValidator.Validate(
            new CommitmentValidationContext(
                beforeEventState,
                reduced.ProposedState!,
                decision.Decision!.ProposedState,
                validationPolicies,
                _stopDistances,
                publicationScope,
                sourceEventSequence,
                ForcedReferenceVehicles:
                    decision.Decision.ForcedReferenceVehicles,
                ForcedNoWorse:
                    _wp4Configuration.SolverPolicyOptions?.ForcedNoWorseRecovery == true));

        if (!validation.IsValid)
        {
            // The policy already validated this decision, so a rejection here is an
            // internal inconsistency. The commitment code is not a protocol error
            // code, and encoding it used to throw; it is kept in the message.
            _onlineCoordinator.DiscardPendingProposal();
            var witness = validation.Witnesses[0];
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                $"{witness.Code}: {witness.Message}");
        }

        var stateToStage = validation.ValidatedState!;
        IReadOnlyList<PromisePublication> publications = validation.Publications;

        if (decision.Decision.GenerationDiagnostics is { } diagnostics
            && diagnostics.ExogenousServiceQualityBreaches.Count != 0)
        {
            var bridged = _breachBridge.Apply(
                reduced.ProposedState!,
                stateToStage,
                diagnostics,
                validationPolicies,
                _stopDistances,
                sourceEventSequence);

            if (!bridged.IsSuccess)
            {
                _onlineCoordinator.DiscardPendingProposal();
                return OnlineDecisionBuildResult.Fail(
                    "INTERNAL_ERROR",
                    ProtocolFailureDisposition.FailSession,
                    $"EXOGENOUS_BREACH_BRIDGE_FAILED: {bridged.Error}");
            }

            stateToStage = bridged.State!;
        }

        // ADR-089 GĐ4: Shadow Control. Every rider whose first promise this decision publishes
        // gets its frozen class now, from this staged state, so the next decision reads it and
        // the state hash and checkpoint carry it (engine17.js:742-776).
        if (_shadowAllocator is not null)
        {
            var opened = publications
                .Where(value => value.Entry.Kind == CommitmentLedgerEntryKind.InitialPromise)
                .Select(value => value.Entry.PublishedPromise.Projection.RequestId)
                .ToArray();
            stateToStage = _shadowAllocator.Allocate(
                stateToStage,
                opened,
                stateToStage.Run.SimulationTime);
        }

        // One canonicalization and one hash of the staged state per epoch.
        CanonicalSegments? stagedCanonical = null;
        var stagedHash = HashStagedState(stateToStage, lightStateAfterHash, ref stagedCanonical);
        var breachWitnesses = ForcedBreachWitnesses(validation.ForcedExemptions);
        var certificate = new CertificateShell(
            CertificateStatus.Produced,
            "VALIDATED",
            new CommitmentCertificateBody(
                "1.0.0",
                "commitment-validator-v1",
                breachWitnesses.Length == 0,
                _stateHash,
                stagedHash,
                publications.Select(value => value.PublicationId).ToArray(),
                stateToStage.Run.Vehicles.Count,
                stateToStage.Run.Requests.Values.Count(
                    value => value.IsAcceptedActive),
                breachWitnesses));

        var staged = _onlineCoordinator.StageDecisionState(
            reduced.ProposedState!,
            stateToStage);

        if (!staged.IsSuccess)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                staged.Witness!.Message);
        }

        _stagedCanonical = stagedCanonical is null
            ? null
            : new CanonicalStateBytes(stateToStage, stagedCanonical);

        var actions = OnlineDecisionActionMapper.Map(
                decision.Decision,
                beforeEventState)
            .Concat(OnlineDecisionActionMapper.MapPublications(publications))
            .ToArray();
        var reasonCode = decision.Decision.RequestActions.Any(
            value => value.Outcome == RequestDecisionOutcome.Accepted)
            || decision.Decision.RequestActions.Count == 0
                ? DecisionReasonCodes.Accepted
                : DecisionReasonCodes.NoFeasibleInsertion;
        var solverStatus = decision.Decision.SelectionExecution?.SolveResult.Status
            switch
        {
            CandidateSelectionSolveStatus.SafeFallback =>
                SolverStatus.SafeFallback,
            CandidateSelectionSolveStatus.Optimal
                or CandidateSelectionSolveStatus.Feasible =>
                SolverStatus.Completed,
            _ => SolverStatus.NotRun,
        };
        return OnlineDecisionBuildResult.Success(
            stagedHash,
            reasonCode,
            actions,
            certificate,
            new SolverStatusShell(solverStatus));
    }

    /// <summary>
    /// ADR-075: every decision that keeps a gate-rejected route is certified as
    /// non-normal operation, with one witness per breached gate code of each rider,
    /// including a decision that repeats an exemption without a new breach record.
    /// Under no-worse recovery a rider exempted on a changed route is certified with the stage
    /// `forcedNoWorse`, so a kept (frozen) vehicle stays distinguishable from one still serving.
    /// </summary>
    private static CertificateWitnessContract[] ForcedBreachWitnesses(
        IReadOnlyList<ForcedReferenceExemption> exemptions) =>
        exemptions
            .OrderBy(value => value.RequestId.Value, StringComparer.Ordinal)
            .SelectMany(
                exemption => exemption.WitnessCodes.Select(
                    code => new CertificateWitnessContract(
                        exemption.NoWorse ? "forcedNoWorse" : "forcedReference",
                        code,
                        exemption.VehicleId.Value,
                        exemption.RequestId.Value)))
            .ToArray();

    private sealed record OnlineDecisionBuildResult(
        Sha256Hex? StateAfterHash,
        string? ReasonCode,
        IReadOnlyList<JsonElement>? Actions,
        CertificateShell? Certificate,
        SolverStatusShell? Solver,
        OnlineDecisionBuildError? Error)
    {
        public static OnlineDecisionBuildResult Success(
            Sha256Hex stateAfterHash,
            string reasonCode,
            IReadOnlyList<JsonElement> actions,
            CertificateShell certificate,
            SolverStatusShell solver) =>
            new(stateAfterHash, reasonCode, actions, certificate, solver, null);

        public static OnlineDecisionBuildResult Fail(
            string code,
            ProtocolFailureDisposition disposition,
            string message) =>
            new(
                null,
                null,
                null,
                null,
                null,
                new OnlineDecisionBuildError(code, disposition, message));
    }

    private sealed record OnlineDecisionBuildError(
        string Code,
        ProtocolFailureDisposition Disposition,
        string Message);
}
