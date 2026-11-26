using System.Text.Json;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Events;
using RideBound.Application.Optimization;
using RideBound.Contracts.Protocol;
using RideBound.Runner.Mapping;
using RideBound.Runner.StateAudit;

namespace RideBound.Runner.Protocol;

// The dispatch decision for one event batch: map and reduce the events, run the dispatch step
// (Algorithms/Dispatch/DispatchStep.cs: the policy, the second validation, exogenous breaches and
// Shadow classes), hash and stage its state, and build the certificate and the actions.
public sealed partial class RunnerSession
{
    /// <summary>
    /// The configured planning forecast rides on every travel snapshot the adapter
    /// sends (and on a restored one, see ProcessRestore). One instance per snapshot, so the
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
            publicationScope,
            sourceEventSequence);

        // Decide, validate again, record exogenous breaches and give new riders their Shadow class.
        var dispatched = _dispatchStep.Run(
            context,
            _wp4Configuration.CandidateGeneration,
            _useManifestSolverSeed
                ? _wp4Configuration.CreateSolverPolicyOptionsForRun(
                    _identity!.Manifest.MasterSeed)
                : _wp4Configuration.SolverPolicyOptions);

        if (!dispatched.IsSuccess)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                dispatched.FailureMessage!);
        }

        var decision = dispatched.Decision!;
        var stateToStage = dispatched.StateToStage!;
        var publications = dispatched.Publications!;

        // One canonicalization and one hash of the staged state per epoch.
        CanonicalSegments? stagedCanonical = null;
        var stagedHash = HashStagedState(stateToStage, lightStateAfterHash, ref stagedCanonical);

        // A validated decision keeps no route that a commitment gate rejected, so it is always normal operation
        // and carries no breach witness.
        var certificate = new CertificateShell(
            CertificateStatus.Produced,
            "VALIDATED",
            new CommitmentCertificateBody(
                "1.0.0",
                "commitment-validator-v1",
                true,
                _stateHash,
                stagedHash,
                publications.Select(value => value.PublicationId).ToArray(),
                stateToStage.Run.Vehicles.Count,
                stateToStage.Run.Requests.Values.Count(
                    value => value.IsAcceptedActive),
                []));

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
                decision,
                beforeEventState)
            .Concat(OnlineDecisionActionMapper.MapPublications(publications))
            .ToArray();
        var reasonCode = decision.RequestActions.Any(
            value => value.Outcome == RequestDecisionOutcome.Accepted)
            || decision.RequestActions.Count == 0
                ? DecisionReasonCodes.Accepted
                : DecisionReasonCodes.NoFeasibleInsertion;
        var solverStatus = decision.SelectionExecution?.SolveResult.Status
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
