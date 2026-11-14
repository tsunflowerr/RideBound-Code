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
/// Random small worlds: 1-2 vehicles with 1-3 riders each on 4 nodes; then 8 decisions. Before each decision the
/// clock advances 0-120 s, every arc gets a new travel time (0.5x-3x), random bookings are confirmed and random
/// vehicles reach their next stop (the rider boards or alights).
/// The world also goes wrong after the promises are made, as it does at real scale:
/// - jams: one step in three, every arc is 10x slower, so many kept drops end up past their first promise's
///   deadline (the case where a strict fence would reject the kept route);
/// - late boardings: one boarding in three comes 1-60 min after the published pickup, often after the latest
///   pickup of the booking window (boarded with <c>allowLatePickup</c>, as the final <c>lateBoarding: record-v1</c>
///   does).
/// Early boarding is left out on purpose: a boarding earlier than the published pickup by more than the rest of the
/// ride makes the projected drop precede the published pickup, which PromiseProjection refuses. That is a known
/// limit of the frozen baseline, not part of this property.
/// </summary>
public sealed class KeptRouteValidationPropertyTests
{
    private const string PolicyId = "wp6-synthetic-policy-overlay-v1";
    private const int Seeds = 60;
    private const int Steps = 8;
    private const int JamFactor = 10;
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
        var seen = new Coverage();

        for (var seed = 1; seed <= Seeds; seed++)
        {
            var random = new Random(seed);
            var state = OpenWorld(random, policy, validator);

            for (var step = 1; step <= Steps; step++)
            {
                var reduced = WorldMoves(random, state, seen);
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
                seen.Decisions++;
                CountRidersPastTheirDeadline(state, seed, seen);
            }

            foreach (var history in state.Commitments.Histories.Values)
            {
                seen.Promised++;
                Assert.All(history.Entries, entry => Assert.Equal(CommitmentVector.Zero, entry.BudgetAfter));
            }
        }

        // The worlds must really exercise promises, the onboard phase, late boardings and jams past the deadline.
        var counts = $"{arm}: {seen}";
        Assert.Equal(Seeds * Steps, seen.Decisions);
        Assert.True(seen.Promised >= Seeds, counts);
        Assert.True(seen.Boardings >= Seeds, counts);
        Assert.True(seen.BoardingsAfterPromise >= Seeds / 2, counts);
        Assert.True(seen.BoardingsAfterWindow >= Seeds / 2, counts);
        Assert.True(seen.GuaranteedPastDeadline.Count >= Seeds / 2, counts);
        Assert.True(seen.AllowedPastDeadline.Count >= Seeds / 2, counts);
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
        var travel = Travel(random, version: 1, jam: false);
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
    private static OnlineState WorldMoves(Random random, OnlineState state, Coverage seen)
    {
        var run = state.Run;
        var now = run.SimulationTime.Milliseconds + (random.Next(0, 13) * 10_000L);

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
                // Board no earlier than the published pickup (see the class comment), and one time in three late.
                var publishedPickup = state.Commitments.Histories[rider.Id].Current.PublishedPromise.Projection
                    .PickupEta.Milliseconds;
                var lateBy = random.Next(3) == 0 ? random.Next(1, 61) * 60_000L : 0;
                now = Math.Max(now, publishedPickup + lateBy);
                run = run.ReachStop(vehicle.Id, stop.StopId, vehicle.Route.Version, new NodePosition(stop.NodeId), run.AppliedEpoch).Value!;
                var boarded = run.Board(vehicle.Id, rider.Id, vehicle.Route.Version, new SimTime(now), allowLatePickup: true);
                Assert.True(boarded.IsSuccess, $"{rider.Id.Value} boards at {now} ms: {boarded.Failure?.Message}");
                run = boarded.Value!;
                seen.Boardings++;
                seen.BoardingsAfterPromise += now > publishedPickup ? 1 : 0;
                seen.BoardingsAfterWindow += now > rider.LatestPickup.Milliseconds ? 1 : 0;
            }
            else if (stop.Kind == RouteStopKind.DropOff && rider.Lifecycle == RequestLifecycle.Onboard)
            {
                run = run.ReachStop(vehicle.Id, stop.StopId, vehicle.Route.Version, new NodePosition(stop.NodeId), run.AppliedEpoch).Value!;
                run = run.Alight(vehicle.Id, rider.Id, vehicle.Route.Version).Value!;
            }
        }

        var travel = Travel(random, state.TravelTimes!.Version + 1, jam: random.Next(3) == 0);
        return state with
        {
            Run = run.AdvanceEpoch(run.AppliedEpoch + 1, new SimTime(now)).Value!,
            TravelTimes = travel,
            NextEventSequence = state.NextEventSequence + 1,
        };
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

    /// <summary>
    /// Counts the riders whose current promise (the kept route's projection) is past the deadline of their first
    /// promise: its drop plus the rider's 1000-permille slack share, which is 0 for a guaranteed rider. Under SHHZ71
    /// this is the deadline a strict fence would enforce; under SHRC71 it is the rider's rule C budget, which this
    /// drift does not spend.
    /// </summary>
    private static void CountRidersPastTheirDeadline(OnlineState state, int seed, Coverage seen)
    {
        foreach (var history in state.Commitments.Histories.Values)
        {
            var first = history.Entries[0].PublishedPromise.Projection;
            var current = history.Current.PublishedPromise.Projection;
            var shadowClass = state.Commitments.ShadowAllocationOf(history.RequestId)!.Class;
            var maximumRideTime = state.Run.Requests[history.RequestId].MaxRideTime;
            var slack = shadowClass == ShadowAllocationClass.Guaranteed
                ? 0
                : CommitmentPolicy.SlackProportionalLimit(first, maximumRideTime, 1000);

            if (current.DropEta.Milliseconds - first.DropEta.Milliseconds > slack)
            {
                var riders = shadowClass == ShadowAllocationClass.Guaranteed
                    ? seen.GuaranteedPastDeadline
                    : seen.AllowedPastDeadline;
                riders.Add($"{seed}/{history.RequestId.Value}");
            }
        }
    }

    /// <summary>
    /// Complete graph on the 4 nodes; each arc 30-300 s, scaled by a world factor of 0.5x to 3x, and 10x more in a jam.
    /// </summary>
    private static TravelTimeSnapshot Travel(Random random, long version, bool jam)
    {
        var arcs = new List<KeyValuePair<TravelArc, Duration>>();

        for (var from = 0; from < Nodes.Length; from++)
        {
            for (var to = 0; to < Nodes.Length; to++)
            {
                if (from != to)
                {
                    var seconds = random.Next(30, 301) * random.Next(5, 31) / 10 * (jam ? JamFactor : 1);
                    arcs.Add(new(new TravelArc(Nodes[from], Nodes[to]), new Duration(Math.Max(1, seconds) * 1_000L)));
                }
            }
        }

        return TravelTimeSnapshot.Create(version, new string("abcdef"[(int)(version % 6)], 64), arcs).Value!;
    }

    /// <summary>What the random worlds of one arm exercised.</summary>
    private sealed class Coverage
    {
        public int Decisions { get; set; }

        public int Promised { get; set; }

        public int Boardings { get; set; }

        public int BoardingsAfterPromise { get; set; }

        public int BoardingsAfterWindow { get; set; }

        public HashSet<string> GuaranteedPastDeadline { get; } = [];

        public HashSet<string> AllowedPastDeadline { get; } = [];

        public override string ToString() =>
            $"{Decisions} decisions, {Promised} promised riders, {Boardings} boardings ({BoardingsAfterPromise} after the "
            + $"published pickup, {BoardingsAfterWindow} after the latest pickup), riders past the deadline of their first "
            + $"promise: {GuaranteedPastDeadline.Count} guaranteed, {AllowedPastDeadline.Count} allowed";
    }
}
