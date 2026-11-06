using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;
using RideBound.Runner.Online;

namespace RideBound.Runner.Tests.Online;

public sealed class PlanPoolCheckpointTests
{
    [Fact]
    public void Canonical_plan_pool_round_trips_with_distinguished_and_alternative()
    {
        var fixture = CreateStateWithPool();
        using var document = JsonDocument.Parse(
            OnlineStateCanonicalizer.Canonicalize(fixture.State));

        var decoded = OnlineStateCheckpointCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess, decoded.Error);
        Assert.Equal(1, decoded.State!.PlanPool.Version);
        Assert.Equal(2, decoded.State.PlanPool.Plans.Count);
        Assert.Equal(
            fixture.State.PlanPool.DistinguishedPlanId,
            decoded.State.PlanPool.DistinguishedPlanId);
        Assert.Equal(
            OnlineStateCanonicalizer.CalculateHash(fixture.State),
            OnlineStateCanonicalizer.CalculateHash(decoded.State));
    }

    [Fact]
    public void Forged_semantic_plan_id_is_rejected_on_restore()
    {
        var fixture = CreateStateWithPool();
        var node = JsonNode.Parse(
            OnlineStateCanonicalizer.Canonicalize(fixture.State))!;
        node["planPool"]!["plans"]![0]!["planId"] = new string('a', 64);
        using var document = JsonDocument.Parse(node.ToJsonString());

        var decoded = OnlineStateCheckpointCodec.Decode(document.RootElement);

        Assert.False(decoded.IsSuccess);
        Assert.Contains("exact fleet-route semantics", decoded.Error);
    }

    [Fact]
    public void Alternative_cannot_be_forged_as_distinguished_without_matching_run()
    {
        var fixture = CreateStateWithPool();
        var node = JsonNode.Parse(
            OnlineStateCanonicalizer.Canonicalize(fixture.State))!;
        node["planPool"]!["distinguishedPlanId"] = fixture.AlternativePlanId;
        using var document = JsonDocument.Parse(node.ToJsonString());

        var decoded = OnlineStateCheckpointCodec.Decode(document.RootElement);

        Assert.False(decoded.IsSuccess);
        Assert.Contains("distinguished plan", decoded.Error);
    }

    [Fact]
    public void Policy_created_alternatives_are_executable_after_distinguished_publish()
    {
        var state = CreatePolicyState();
        var decision = new MultiplePlanConsensusPolicy().Decide(
            state,
            CandidateGenerationOptions.ExactSmall,
            new MultiplePlanPoolOptions(4, 1_000_000, true));

        Assert.True(decision.IsSuccess, decision.Witness?.Message);
        Assert.True(decision.Decision!.PlanPool.Plans.Count > 1);
        using var document = JsonDocument.Parse(
            OnlineStateCanonicalizer.Canonicalize(
                decision.Decision.DistinguishedDecision.ProposedState));

        var restored = OnlineStateCheckpointCodec.Decode(document.RootElement);

        Assert.True(restored.IsSuccess, restored.Error);
        Assert.Equal(
            decision.Decision.PlanPool.DistinguishedPlanId,
            restored.State!.PlanPool.DistinguishedPlanId);
    }

    private static (OnlineState State, string AlternativePlanId)
        CreateStateWithPool()
    {
        var runId = new RunIdentifier("plan-pool-run");
        var scenarioId = new ScenarioIdentifier("plan-pool-scenario");
        var vehicleId = new VehicleId("vehicle-1");
        var origin = new NodeId("origin");
        var nodeA = new NodeId("node-a");
        var nodeB = new NodeId("node-b");
        var currentRoute = WaypointRoute("current-stop", nodeA);
        var alternativeRoute = currentRoute.ReplaceMutableSuffix(
            [
                new RouteStop(
                    new StopId("alternative-stop"),
                    nodeB,
                    RouteStopKind.Waypoint,
                    null,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            vehicleId,
            4,
            0,
            new NodePosition(origin),
            [],
            [],
            currentRoute,
            1).Value!;
        var run = RideBoundRun.Create(runId, scenarioId, new SimTime(0));
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AdvanceEpoch(1, new SimTime(0)).Value!;
        var travel = TravelTimeSnapshot.Create(
            1,
            new string('b', 64),
            [
                new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(origin, nodeA),
                    new Duration(10)),
                new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(origin, nodeB),
                    new Duration(20)),
            ]).Value!;
        var current = CanonicalFleetPlan.Create(
            1,
            [new CanonicalVehiclePlan(vehicleId, currentRoute)]);
        var alternative = CanonicalFleetPlan.Create(
            1,
            [new CanonicalVehiclePlan(vehicleId, alternativeRoute)]);
        var pool = VersionedPlanPool.CreateNext(
            VersionedPlanPool.Empty,
            1,
            current.PlanId,
            [alternative, current]);
        var state = new OnlineState(
            run,
            travel,
            1,
            travel.SnapshotHash,
            CommitmentLedger.Empty)
        {
            PlanPool = pool,
        };

        return (state, alternative.PlanId);
    }

    private static RoutePlan WaypointRoute(string stopId, NodeId nodeId) =>
        RoutePlan.Create(
            new PlanVersion(1),
            0,
            [],
            [
                new RouteStop(
                    new StopId(stopId),
                    nodeId,
                    RouteStopKind.Waypoint,
                    null,
                    new Duration(0)),
            ]).Value!;

    private static OnlineState CreatePolicyState()
    {
        var nodes = new[]
        {
            new NodeId("origin"),
            new NodeId("pickup-a"),
            new NodeId("pickup-b"),
            new NodeId("drop"),
        };
        var run = RideBoundRun.Create(
            new RunIdentifier("policy-plan-pool-run"),
            new ScenarioIdentifier("policy-plan-pool-scenario"),
            new SimTime(0));
        var requests = new[]
        {
            Pending("request-a", nodes[1], nodes[3]),
            Pending("request-b", nodes[2], nodes[3]),
        };

        foreach (var request in requests)
        {
            run = run.AddRequest(request).Value!;
        }

        var route = RoutePlan.Create(new PlanVersion(0), 0, [], []).Value!;
        var vehicle = VehicleState.Create(
            new VehicleId("vehicle-1"),
            4,
            0,
            new NodePosition(nodes[0]),
            [],
            [],
            route,
            1).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AdvanceEpoch(1, new SimTime(0)).Value!;
        var travel = TravelTimeSnapshot.Create(
            1,
            new string('c', 64),
            from fromNode in nodes
            from to in nodes
            where fromNode != to
            select new KeyValuePair<TravelArc, Duration>(
                new TravelArc(fromNode, to),
                new Duration(100))).Value!;

        return new OnlineState(
            run,
            travel,
            1,
            travel.SnapshotHash,
            CommitmentLedger.Empty);
    }

    private static RideRequest Pending(
        string id,
        NodeId pickup,
        NodeId drop) =>
        RideRequest.CreatePending(
            new RequestId(id),
            new SimTime(0),
            pickup,
            drop,
            new SimTime(0),
            new SimTime(10_000),
            new Duration(10_000),
            1,
            "standard",
            "uniform-v1").Value!;
}
