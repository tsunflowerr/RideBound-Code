using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Commitments;

/// <summary>
/// Refactor safety net S3: under the final commitment policies (SHRC71 = rule C budget, SHHZ71 = relaxed
/// deadline; G2 and their insertion-only twins), the kept route (the no-op: every vehicle keeps its route) always
/// passes the commitment validator, whatever the world did since the last decision. This is why
/// <c>commitmentRecovery: no-worse-than-reference-v1</c> never acts in the final runs: the kept candidate is the
/// exogenous projection, so no lock has a witness, the relaxed fence exempts it, and its decision-induced delta
/// is zero, so no budget grows.
/// Random small worlds: 1-2 vehicles with 1-3 riders each on 4 nodes; then 6 decisions, each after a random
/// clock advance, a random slow-down or speed-up of every arc, random booking confirmations and random
/// stop arrivals (boarding no earlier than the published pickup, alighting).
/// </summary>
public sealed class KeptRouteValidationPropertyTests
{
    private const string PolicyId = "wp6-synthetic-policy-overlay-v1";
    private const int Seeds = 60;
    private const int Steps = 6;
    private static readonly NodeId[] Nodes = [new("n-0"), new("n-1"), new("n-2"), new("n-3")];

    [Theory]
    [InlineData("SHRC71", true, false)]
    [InlineData("SHHZ71", false, false)]
    [InlineData("INS-SHRC", true, true)]
    [InlineData("INS-SHHZ", false, true)]
    public void The_kept_route_always_validates_and_never_spends_budget(string arm, bool ruleC, bool insertionOnly)
    {
        var policy = FinalPolicy(ruleC, insertionOnly);
        var validator = new CommitmentDecisionValidator();
        var decisions = 0;
        var promised = 0;
        var boarded = 0;

        for (var seed = 1; seed <= Seeds; seed++)
        {
            var random = new Random(seed);
            var state = OpenWorld(random, policy, validator);

            for (var step = 1; step <= Steps; step++)
            {
                var (reduced, boardings) = WorldMoves(random, state);
                var result = validator.Validate(new CommitmentValidationContext(
                    state,
                    reduced,
                    reduced,
                    new CommitmentPolicyCatalog([policy]),
                    CommitmentValidatorFixtures.EmptyDistances.Instance,
                    $"kept-{seed}-{step}",
                    state.NextEventSequence,
                    InitialPromiseTrigger: InitialPromiseTrigger.BookingConfirmation));

                Assert.True(
                    result.IsValid,
                    $"{arm} seed {seed} step {step}: "
                    + string.Join("; ", result.Witnesses.Select(w => $"{w.Stage} {w.Code} {w.RequestId?.Value} {w.Rule} {w.Message}")));
                Assert.Empty(result.ForcedExemptions);
                Assert.Empty(result.ForcedBreaches);
                state = WithShadowClasses(random, result.ValidatedState!, result.Publications);
                decisions++;
                boarded += boardings;
            }

            foreach (var history in state.Commitments.Histories.Values)
            {
                promised++;
                Assert.All(history.Entries, entry => Assert.Equal(CommitmentVector.Zero, entry.BudgetAfter));
            }
        }

        // The worlds must really exercise promises, revisions and the onboard phase.
        Assert.Equal(Seeds * Steps, decisions);
        Assert.True(promised >= Seeds, $"{arm}: only {promised} promised riders");
        Assert.True(boarded >= Seeds / 2, $"{arm}: only {boarded} boardings");
    }

    /// <summary>The commitment configs of the final runs (Runner.Tests/Final/Fixtures/commitment/*.json).</summary>
    private static CommitmentPolicy FinalPolicy(bool ruleC, bool insertionOnly)
    {
        const CommitmentPhase BeforePickup = CommitmentPhase.Accepted | CommitmentPhase.WaitingPickup;
        var limits = CommitmentDimensionVocabulary.Ordered.Select(dimension => new CommitmentDimensionLimit(
            dimension,
            dimension is CommitmentDimension.PickupStopSwitchCount or CommitmentDimension.DropStopSwitchCount
                || (insertionOnly && dimension == CommitmentDimension.VehicleSwitchCount)
                ? 0
                : null,
            dimension is CommitmentDimension.PickupStopRelocationMm
                or CommitmentDimension.PickupStopSwitchCount
                or CommitmentDimension.PrePickupInsertedStopCount
                ? BeforePickup
                : CommitmentPhase.AllActive));

        return new CommitmentPolicy(
            PolicyId,
            CommitmentBudgetBasis.DecisionInduced,
            limits,
            new MaterialRevisionRule(1_000, 60_000),
            finalConfirmationLocks: PromiseLock.PickupStop | PromiseLock.PickupEta
                | (insertionOnly ? PromiseLock.Vehicle : PromiseLock.None),
            ratchetLocks: insertionOnly ? PromiseLock.None : PromiseLock.PickupEta,
            dropEtaDeadlineRelaxed: !ruleC,
            dropEtaBudgetSlackPermille: ruleC ? 1000 : null,
            dropEtaDeadlineSlackPermille: ruleC ? null : 1000,
            waitingRiderRelocation: !insertionOnly,
            dropEtaShadowAllocation: true);
    }

    /// <summary>Riders accepted onto their vehicles' routes; some bookings confirmed; first promises opened.</summary>
    private static OnlineState OpenWorld(Random random, CommitmentPolicy policy, CommitmentDecisionValidator validator)
    {
        const long start = 900_000;
        var run = RideBoundRun.Create(new RunIdentifier("kept-run"), new ScenarioIdentifier("kept-scenario"), new SimTime(start));
        var vehicles = random.Next(1, 3);
        var riders = new List<(VehicleId Vehicle, RideRequest Request)>();

        for (var v = 0; v < vehicles; v++)
        {
            var vehicleId = new VehicleId($"v-{v}");
            var stops = new List<RouteStop>();

            for (var r = 0; r < random.Next(1, 4); r++)
            {
                var origin = random.Next(Nodes.Length);
                var destination = (origin + random.Next(1, Nodes.Length)) % Nodes.Length;
                var request = RideRequest.CreatePending(
                    new RequestId($"r-{v}-{r}"),
                    new SimTime(start),
                    Nodes[origin],
                    Nodes[destination],
                    new SimTime(start),
                    new SimTime(start + random.Next(1_800, 3_600) * 1_000L),
                    new Duration(random.Next(1_800, 3_600) * 1_000L),
                    random.Next(1, 3),
                    "standard",
                    PolicyId).Value!;
                riders.Add((vehicleId, request));
                // pickup at a random place, drop at a random later place
                var pickupAt = random.Next(stops.Count + 1);
                stops.Insert(pickupAt, new RouteStop(new StopId($"p-{v}-{r}"), Nodes[origin], RouteStopKind.Pickup, request.Id, new Duration(0)));
                stops.Insert(random.Next(pickupAt + 1, stops.Count + 1), new RouteStop(new StopId($"d-{v}-{r}"), Nodes[destination], RouteStopKind.DropOff, request.Id, new Duration(0)));
            }

            var route = RoutePlan.Create(new PlanVersion(0), 0, [], stops).Value!;
            run = run.BootstrapVehicle(VehicleState.Create(vehicleId, 8, 0, new NodePosition(Nodes[random.Next(Nodes.Length)]), [], [], route, 1).Value!).Value!;
        }

        foreach (var (vehicle, request) in riders)
        {
            run = run.AddRequest(request).Value!;
            run = run.AcceptRequest(request.Id, vehicle).Value!;
        }

        // Booking confirmations arrive in the decision's event batch: Accepted before, WaitingPickup after.
        var travel = Travel(random, version: 1);
        var before = new OnlineState(run.AdvanceEpoch(1, new SimTime(start)).Value!, travel, 2, travel.SnapshotHash, CommitmentLedger.Empty);
        var confirmed = before.Run;

        foreach (var (_, request) in riders)
        {
            if (random.Next(3) != 0)
            {
                confirmed = confirmed.ConfirmWaitingPickup(request.Id).Value!;
            }
        }

        var reduced = before with { Run = confirmed.AdvanceEpoch(2, new SimTime(start)).Value!, NextEventSequence = 3 };
        var opened = validator.Validate(new CommitmentValidationContext(
            before,
            reduced,
            reduced,
            new CommitmentPolicyCatalog([policy]),
            CommitmentValidatorFixtures.EmptyDistances.Instance,
            "open",
            2,
            InitialPromiseTrigger: InitialPromiseTrigger.BookingConfirmation));
        Assert.True(opened.IsValid, string.Join("; ", opened.Witnesses.Select(w => w.Message)));
        return WithShadowClasses(random, opened.ValidatedState!, opened.Publications);
    }

    /// <summary>What the world does between two decisions; the routes are never touched by the dispatcher.</summary>
    private static (OnlineState Reduced, int Boardings) WorldMoves(Random random, OnlineState state)
    {
        var run = state.Run;
        var now = run.SimulationTime.Milliseconds + (random.Next(0, 13) * 10_000L);
        var boardings = 0;

        foreach (var request in run.Requests.Values.Where(value => value.Lifecycle == RequestLifecycle.Accepted).ToArray())
        {
            if (random.Next(3) == 0)
            {
                run = run.ConfirmWaitingPickup(request.Id).Value!;
            }
        }

        foreach (var vehicle in run.Vehicles.Values.OrderBy(value => value.Id.Value, StringComparer.Ordinal).ToArray())
        {
            if (vehicle.Route.MutableSuffix.Count == 0 || random.Next(5) < 2)
            {
                continue;
            }

            var stop = vehicle.Route.MutableSuffix[0];
            var rider = run.Requests[stop.RequestId!.Value];

            if (stop.Kind == RouteStopKind.Pickup
                && rider.Lifecycle == RequestLifecycle.WaitingPickup
                && state.Commitments.Histories.ContainsKey(rider.Id))
            {
                // Board no earlier than the published pickup. A boarding that is earlier than the promise by more
                // than the rest of the ride is a separate, known limit (the projected drop would precede the
                // published pickup, which PromiseProjection refuses); it is outside this property.
                now = Math.Max(now, state.Commitments.Histories[rider.Id].Current.PublishedPromise.Projection.PickupEta.Milliseconds);
                run = run.ReachStop(vehicle.Id, stop.StopId, vehicle.Route.Version, new NodePosition(stop.NodeId), run.AppliedEpoch).Value!;
                run = run.Board(vehicle.Id, rider.Id, vehicle.Route.Version, new SimTime(now), allowLatePickup: true).Value!;
                boardings++;
            }
            else if (stop.Kind == RouteStopKind.DropOff && rider.Lifecycle == RequestLifecycle.Onboard)
            {
                run = run.ReachStop(vehicle.Id, stop.StopId, vehicle.Route.Version, new NodePosition(stop.NodeId), run.AppliedEpoch).Value!;
                run = run.Alight(vehicle.Id, rider.Id, vehicle.Route.Version).Value!;
            }
        }

        var travel = Travel(random, state.TravelTimes!.Version + 1);
        return (state with
        {
            Run = run.AdvanceEpoch(run.AppliedEpoch + 1, new SimTime(now)).Value!,
            TravelTimes = travel,
            NextEventSequence = state.NextEventSequence + 1,
        }, boardings);
    }

    /// <summary>The Shadow class of each newly promised rider, as ShadowAllocator would record it.</summary>
    private static OnlineState WithShadowClasses(Random random, OnlineState state, IReadOnlyList<PromisePublication> publications)
    {
        var ledger = state.Commitments;

        foreach (var publication in publications.Where(value => value.Entry.Kind == CommitmentLedgerEntryKind.InitialPromise))
        {
            var requestId = publication.Entry.PublishedPromise.Projection.RequestId;
            var allocation = random.Next(2) == 0
                ? new ShadowAllocation(ShadowAllocationClass.Guaranteed, "0")
                : new ShadowAllocation(ShadowAllocationClass.Allowed, "100");
            ledger = ledger.WithShadowAllocation(requestId, allocation);
        }

        return state with { Commitments = ledger };
    }

    /// <summary>Complete graph on the 4 nodes; each arc 30-300 s, scaled by a world factor of 0.5x to 3x.</summary>
    private static TravelTimeSnapshot Travel(Random random, long version)
    {
        var arcs = new List<KeyValuePair<TravelArc, Duration>>();

        for (var from = 0; from < Nodes.Length; from++)
        {
            for (var to = 0; to < Nodes.Length; to++)
            {
                if (from != to)
                {
                    var seconds = random.Next(30, 301) * random.Next(5, 31) / 10;
                    arcs.Add(new(new TravelArc(Nodes[from], Nodes[to]), new Duration(Math.Max(1, seconds) * 1_000L)));
                }
            }
        }

        return TravelTimeSnapshot.Create(version, new string("abcdef"[(int)(version % 6)], 64), arcs).Value!;
    }
}
