using System.Security.Cryptography;
using System.Text;
using RideBound.Algorithms.Candidates;
using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Validation;

namespace RideBound.Runner.Online;

public sealed record ExogenousServiceQualityBreachBridgeResult(
    OnlineState? State,
    string? Error)
{
    public bool IsSuccess => State is not null;

    public static ExogenousServiceQualityBreachBridgeResult Success(
        OnlineState state) =>
        new(state, null);

    public static ExogenousServiceQualityBreachBridgeResult Failure(
        string error) =>
        new(null, error);
}

/// <summary>
/// Persists candidate-generation observations that describe an already-broken
/// service deadline on the unchanged route. The bridge deliberately projects
/// the reduced (pre-decision) state again, uses that projection for both the
/// exogenous and safety sides, and leaves the rider's decision budget untouched.
/// </summary>
public sealed class ExogenousServiceQualityBreachBridge
{
    private readonly RouteScheduleProjector _scheduleProjector;
    private readonly PromiseProjector _promiseProjector;
    private readonly PromiseDeltaCalculator _deltaCalculator;

    public ExogenousServiceQualityBreachBridge(
        RouteScheduleProjector? scheduleProjector = null,
        PromiseProjector? promiseProjector = null,
        PromiseDeltaCalculator? deltaCalculator = null)
    {
        _scheduleProjector = scheduleProjector ?? new RouteScheduleProjector();
        _promiseProjector = promiseProjector ?? new PromiseProjector();
        _deltaCalculator = deltaCalculator ?? new PromiseDeltaCalculator();
    }

    public ExogenousServiceQualityBreachBridgeResult Apply(
        OnlineState reducedState,
        OnlineState validatedState,
        CandidateGenerationDiagnostics diagnostics,
        ICommitmentPolicyProvider policies,
        IStopDistanceLookup stopDistances,
        long sourceEventSequence)
    {
        ArgumentNullException.ThrowIfNull(reducedState);
        ArgumentNullException.ThrowIfNull(validatedState);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(stopDistances);

        var ledger = validatedState.Incidents;
        var groups = diagnostics.ExogenousServiceQualityBreaches
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .ThenBy(value => value.RequestId.Value, StringComparer.Ordinal)
            .ThenBy(value => value.Code, StringComparer.Ordinal)
            .ThenBy(value => value.Dimension, StringComparer.Ordinal)
            .GroupBy(value => (value.VehicleId, value.RequestId));

        foreach (var group in groups)
        {
            var vehicleId = group.Key.VehicleId;
            var requestId = group.Key.RequestId;

            // A provisional booking has no published promise yet and therefore
            // cannot have a commitment breach, even though generation may still
            // report a contractual service-quality overrun for its active route.
            if (!reducedState.Commitments.Histories.TryGetValue(
                    requestId,
                    out var history))
            {
                continue;
            }

            if (!reducedState.Run.Requests.TryGetValue(requestId, out var request)
                || !request.IsAcceptedActive
                || request.AssignedVehicleId != vehicleId
                || !reducedState.Run.Vehicles.TryGetValue(vehicleId, out var vehicle)
                || reducedState.TravelTimes is null)
            {
                return Failure(
                    vehicleId,
                    requestId,
                    "Breach evidence has no matching reduced active assignment.");
            }

            if (!policies.TryGetPolicy(request.CommitmentPolicyId, out var policy)
                || !string.Equals(
                    policy.PolicyId,
                    request.CommitmentPolicyId,
                    StringComparison.Ordinal))
            {
                return Failure(
                    vehicleId,
                    requestId,
                    "Breach evidence has no exact commitment policy.");
            }

            var schedule = _scheduleProjector.Project(
                reducedState.Run,
                vehicle,
                vehicle.Route,
                reducedState.TravelTimes,
                reducedState.Run.SimulationTime);

            if (!schedule.IsSuccess)
            {
                return Failure(
                    vehicleId,
                    requestId,
                    schedule.Failure!.Message);
            }

            var exogenous = _promiseProjector.Project(
                reducedState.Run,
                vehicle,
                vehicle.Route,
                schedule.Schedule!,
                requestId,
                history.Current.PublishedPromise.Projection);

            if (!exogenous.IsSuccess)
            {
                return Failure(
                    vehicleId,
                    requestId,
                    exogenous.Failure!.Message);
            }

            var deltas = _deltaCalculator.Calculate(
                history.Current.PublishedPromise,
                exogenous.Value!,
                exogenous.Value!,
                policy.MaterialRevisionRule,
                stopDistances);

            if (!deltas.IsSuccess)
            {
                return Failure(
                    vehicleId,
                    requestId,
                    deltas.Failure!.Message);
            }

            var witnesses = group
                .Select(
                    value => new ServiceQualityBreach(
                        value.RequestId,
                        value.Code,
                        value.Dimension,
                        value.ContractualMilliseconds,
                        value.ExogenousMilliseconds))
                .ToArray();
            var breach = CommitmentBreachRecord.CreateExogenousServiceQuality(
                CreateBreachId(
                    reducedState,
                    vehicleId,
                    requestId,
                    sourceEventSequence,
                    witnesses),
                requestId,
                history.Current.PublishedPromise,
                exogenous.Value!,
                exogenous.Value!,
                deltas.Deltas!,
                history.Current.BudgetAfter,
                history.Current.BudgetAfter,
                witnesses.Select(value => value.Code),
                witnesses,
                sourceEventSequence,
                reducedState.Run.AppliedEpoch,
                reducedState.Run.SimulationTime);
            var appended = ledger.AppendBreach(breach);

            if (!appended.IsSuccess)
            {
                return Failure(
                    vehicleId,
                    requestId,
                    appended.Failure!.Message);
            }

            ledger = appended.Ledger!;
        }

        return ExogenousServiceQualityBreachBridgeResult.Success(
            validatedState with { Incidents = ledger });
    }

    private static string CreateBreachId(
        OnlineState state,
        VehicleId vehicleId,
        RequestId requestId,
        long sourceEventSequence,
        IReadOnlyList<ServiceQualityBreach> witnesses)
    {
        var identity = string.Join(
            "\0",
            state.Run.Id.Value,
            state.Run.ScenarioId.Value,
            state.Run.AppliedEpoch,
            sourceEventSequence,
            vehicleId.Value,
            requestId.Value,
            string.Join(
                ";",
                witnesses.Select(
                    value => $"{value.Code}|{value.Dimension}|" +
                        $"{value.ContractualMilliseconds}|" +
                        value.ExogenousMilliseconds)));
        var hash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return $"exogenous-{hash}";
    }

    private static ExogenousServiceQualityBreachBridgeResult Failure(
        VehicleId vehicleId,
        RequestId requestId,
        string message) =>
        ExogenousServiceQualityBreachBridgeResult.Failure(
            $"Exogenous breach bridge failed for vehicle '{vehicleId.Value}', " +
            $"request '{requestId.Value}': {message}");
}
