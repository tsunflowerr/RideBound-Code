using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Candidates;

public sealed record CandidateScheduleEvaluationResult
{
    private CandidateScheduleEvaluationResult(
        CandidateSchedule? schedule,
        string? code,
        string? message)
    {
        Schedule = schedule;
        Code = code;
        Message = message;
    }

    public bool IsSuccess => Schedule is not null;

    public CandidateSchedule? Schedule { get; }

    public string? Code { get; }

    public string? Message { get; }

    public static CandidateScheduleEvaluationResult Success(
        CandidateSchedule schedule) =>
        new(schedule, null, null);

    public static CandidateScheduleEvaluationResult Failure(
        string code,
        string message) =>
        new(null, code, message);
}

public sealed class CandidateScheduleEvaluator
{
    private readonly RouteScheduleProjector _projector;

    public CandidateScheduleEvaluator(RouteScheduleProjector? projector = null)
    {
        _projector = projector ?? new RouteScheduleProjector();
    }

    public CandidateScheduleEvaluationResult Evaluate(
        OnlineState state,
        VehicleState vehicle,
        RoutePlan candidateRoute,
        ITravelTimeLookup travelTimes,
        SimTime evaluationTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(candidateRoute);
        ArgumentNullException.ThrowIfNull(travelTimes);

        var projection = _projector.Project(
            state.Run,
            vehicle,
            candidateRoute,
            travelTimes,
            evaluationTime);

        if (!projection.IsSuccess)
        {
            return Failure(projection.Failure!.Message);
        }

        return CandidateScheduleEvaluationResult.Success(
            new CandidateSchedule(
                projection.Schedule!.Stops
                    .Select(
                        stop => new ScheduledStop(
                            stop.StopId,
                            stop.ArrivalTime,
                            stop.ServiceStartTime,
                            stop.DepartureTime))
                    .ToArray(),
                projection.Schedule.OperationalCost));
    }

    private static CandidateScheduleEvaluationResult Failure(string message) =>
        CandidateScheduleEvaluationResult.Failure(
            CandidateGenerationFailureCodes.ScheduleEvaluationFailed,
            message);
}
