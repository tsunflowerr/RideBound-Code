using RideBound.Algorithms.Candidates;
using RideBound.Domain.Routes;

namespace RideBound.Algorithms.Tests.Candidates;

/// <summary>Bridge step 1.3: the dwell of the pickup and drop stops the generator inserts.</summary>
public sealed class StopServiceDurationTests
{
    [Fact]
    public void Inserted_stops_carry_the_configured_dwell_and_it_delays_the_rest_of_the_route()
    {
        // Vehicle at node 0 at t = 1000; the rider goes from node 1 to node 2 (110 ms per hop).
        var state = AlgorithmTestData.CreateState(
            [AlgorithmTestData.PendingRequest()],
            [AlgorithmTestData.Vehicle()]);
        var generator = new InsertionCandidateGenerator();

        InsertionCandidate Insert(CandidateGenerationOptions options)
        {
            var result = generator.Generate(state, options);
            Assert.True(result.IsSuccess, result.Witness?.Message);
            return Assert.Single(
                result.VehicleCandidates!.Single().Candidates,
                candidate => !candidate.IsNoOp);
        }

        var byDefault = Insert(new CandidateGenerationOptions(100, 1, exactSmallMode: true));
        var zero = Insert(new CandidateGenerationOptions(100, 1, exactSmallMode: true, stopServiceDurationMs: 0));
        var dwell = Insert(new CandidateGenerationOptions(100, 1, exactSmallMode: true, stopServiceDurationMs: 24));

        Assert.Equal(byDefault.CandidateId, zero.CandidateId);
        Assert.All(zero.Route.MutableSuffix, stop => Assert.Equal(0, stop.ServiceDuration.Milliseconds));
        Assert.Equal(
            [RouteStopKind.Pickup, RouteStopKind.DropOff],
            dwell.Route.MutableSuffix.Select(stop => stop.Kind));
        Assert.All(dwell.Route.MutableSuffix, stop => Assert.Equal(24, stop.ServiceDuration.Milliseconds));
        Assert.NotEqual(zero.CandidateId, dwell.CandidateId);

        // Pickup service starts on arrival (1110) either way; the drop is 24 ms later with the dwell.
        Assert.Equal(1_110, zero.Schedule.Stops[0].ServiceStartTime.Milliseconds);
        Assert.Equal(1_110, dwell.Schedule.Stops[0].ServiceStartTime.Milliseconds);
        Assert.Equal(1_134, dwell.Schedule.Stops[0].DepartureTime.Milliseconds);
        Assert.Equal(
            zero.Schedule.Stops[1].ServiceStartTime.Milliseconds + 24,
            dwell.Schedule.Stops[1].ServiceStartTime.Milliseconds);
        Assert.Equal(
            dwell.Schedule.Stops[1].ServiceStartTime.Milliseconds + 24,
            dwell.Schedule.Stops[1].DepartureTime.Milliseconds);
    }

    [Fact]
    public void A_negative_dwell_is_refused_and_the_repair_cap_keeps_the_dwell()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CandidateGenerationOptions(100, 1, exactSmallMode: false, stopServiceDurationMs: -1));
        Assert.Equal(0, new CandidateGenerationOptions(100, 1, exactSmallMode: false).StopServiceDuration.Milliseconds);

        var options = new CandidateGenerationOptions(100, 1, exactSmallMode: false, stopServiceDurationMs: 24_000);

        Assert.Equal(24_000, options.StopServiceDuration.Milliseconds);
        Assert.Equal(24_000, options.WithRepairRequestCap(2).StopServiceDuration.Milliseconds);
    }
}
