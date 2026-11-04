namespace RideBound.Algorithms.Shareability;

/// <summary>One stop of the vehicle route at the rider's first promise.</summary>
/// <param name="Node">Local (cell) node.</param>
/// <param name="IsPickup">Pickup (true) or drop (false).</param>
/// <param name="ProjectedTime">Projected service time T (ms).</param>
/// <param name="Headroom">Headroom h of the stop (ms), C_v(s_i) of Eq. 3-4.</param>
/// <param name="Party">Riders boarding or alighting.</param>
public sealed record ShareabilityStop(int Node, bool IsPickup, double ProjectedTime, double Headroom, int Party);

/// <summary>The arc the vehicle is driving: its start node (local) and progress in permille (1..999).</summary>
public sealed record ShareabilityArc(int From, int Permille);

/// <summary>Context of one score: the vehicle and its route when the target rider is first promised.</summary>
public sealed record ShareabilityContext(
    double EvaluationTime,
    int Bin,
    int StartNode,
    double StartTime,
    ShareabilityArc? Arc,
    int InitialLoad,
    int Capacity,
    IReadOnlyList<ShareabilityStop> Stops,
    int PickupIndex,
    int DropIndex,
    double Dwell);

/// <summary>
/// Cell parameters: full-network scale kappa * f_b (ms per free-flow second / 1000), the cell's
/// local -> network node map, the pickup window Omega_p (ms), the instant stride (ms, 0 = none),
/// the zone demand rates, and causal or oracle use of them.
/// </summary>
public sealed record ShareabilityCell(
    double Kappa,
    IReadOnlyList<double> Factors,
    IReadOnlyList<int> GlobalOf,
    double PickupWindow,
    double StrideMilliseconds,
    ZoneDemandRates Rates,
    bool Oracle);

/// <summary>Score S = waiting part S^w + driving part S^d, with the instant counts.</summary>
public sealed record ShareabilityScore(
    double Score,
    double Waiting,
    double Driving,
    int Instants,
    int PickupMainNode,
    int DropMainNode,
    int MainNodes,
    int ArcInstants);

/// <summary>
/// Time-integrated shareability score of a rider at its first promise (Elmi, Moylan,
/// Alonso-Mora and Fielbaum 2026, Eq. 3-4, 9, 11), ported from the comparison emulator's
/// production scorer (sscore.js makeScorer, wf15 SPEC 4) operation for operation, so that the
/// IEEE double results are identical:
/// <list type="bullet">
/// <item>main nodes: vehicle start + route stops, consecutive coincident stops merged;</item>
/// <item>section k = main node k-1 -> k; section 0 = rest of the arc being driven;</item>
/// <item>headroom H[k] = minimum stop headroom from the first stop of main node k on;</item>
/// <item>drop shadow: union of the remaining section ellipses; pickup shadow: the next two
/// sections, reachable within Omega_p;</item>
/// <item>instant value S(t) = sum_z lambdaO_z |N_PU in z| / |N_z| + sum_z lambdaD_z |N_DO in z| / |N_z|,
/// integrated over main and intermediate node instants; instants up to the pickup main node
/// count as waiting.</item>
/// </list>
/// Pure and passive: reads the context, never changes a decision.
/// </summary>
public sealed class ShareabilityScorer
{
    private readonly ShareabilityNetwork network;
    private readonly ShareabilityCell cell;

    public ShareabilityScorer(ShareabilityNetwork network, ShareabilityCell cell)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(cell.Factors);
        ArgumentNullException.ThrowIfNull(cell.GlobalOf);
        ArgumentNullException.ThrowIfNull(cell.Rates);

        if (cell.Factors.Count == 0)
        {
            throw new ArgumentException("A cell needs at least one bin factor.", nameof(cell));
        }

        foreach (var node in cell.GlobalOf)
        {
            if ((uint)node >= (uint)network.NodeCount)
            {
                throw new ArgumentException("A local node maps outside the network.", nameof(cell));
            }
        }

        this.network = network;
        this.cell = cell;
    }

    /// <summary>Largest integer d with fl(g*d) &lt;= H; -1 if none; +Infinity if H is +Infinity.</summary>
    internal static double DMaxMul(double g, double h)
    {
        if (double.IsPositiveInfinity(h))
        {
            return double.PositiveInfinity;
        }

        if (!(h >= 0))
        {
            return -1;
        }

        var d = Math.Floor(h / g);
        if (d > 1e7)
        {
            return double.PositiveInfinity;
        }

        while (g * (d + 1) <= h)
        {
            d++;
        }

        while (d >= 0 && g * d > h)
        {
            d--;
        }

        return d;
    }

    /// <summary>Largest integer d with fl(rem + fl(g*d)) &lt;= Omega; -1 if none.</summary>
    internal static double DMaxAdd(double g, double remaining, double omega)
    {
        if (!(remaining <= omega))
        {
            return -1;
        }

        var d = Math.Floor((omega - remaining) / g);
        if (d > 1e7)
        {
            return double.PositiveInfinity;
        }

        while (remaining + g * (d + 1) <= omega)
        {
            d++;
        }

        while (d >= 0 && remaining + g * d > omega)
        {
            d--;
        }

        return d;
    }

    public ShareabilityScore Score(ShareabilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Stops);

        var n = network.NodeCount;
        var d = network.D;
        var dt = network.DT;
        var zoneIndex = network.ZoneIndex;
        var zones = network.ZoneCount;
        var globalOf = cell.GlobalOf;
        var omega = cell.PickupWindow;
        var stride = cell.StrideMilliseconds;
        var stops = context.Stops;
        var m = stops.Count;

        if (context.Bin < 0)
        {
            throw new ArgumentException("Negative bin.", nameof(context));
        }

        if ((uint)context.PickupIndex >= (uint)m || (uint)context.DropIndex >= (uint)m)
        {
            throw new ArgumentException("Target stops are not on the route.", nameof(context));
        }

        var g = cell.Kappa * cell.Factors[Math.Min(context.Bin, cell.Factors.Count - 1)] * 1000;

        // main nodes (SPEC 4.3)
        var mainNode = new List<int> { globalOf[context.StartNode] };
        var arrival = new List<double> { context.StartTime };
        var departure = new List<double> { context.StartTime };
        var firstStop = new List<int> { -1 };
        var mainOf = new int[m];
        for (var q = 0; q < m; q++)
        {
            var stop = stops[q];
            var node = globalOf[stop.Node];
            var last = mainNode.Count - 1;
            if (node == mainNode[last])
            {
                if (firstStop[last] < 0)
                {
                    firstStop[last] = q;
                }

                departure[last] = stop.ProjectedTime + context.Dwell;
            }
            else
            {
                mainNode.Add(node);
                arrival.Add(stop.ProjectedTime);
                departure.Add(stop.ProjectedTime + context.Dwell);
                firstStop.Add(q);
            }

            mainOf[q] = mainNode.Count - 1;
        }

        var kP = mainOf[context.PickupIndex];
        var kK = mainOf[context.DropIndex];
        if (!(kK >= 1) || kP > kK)
        {
            throw new InvalidOperationException($"Bad target main nodes kP {kP} kK {kK}.");
        }

        if (!(departure[0] >= context.EvaluationTime))
        {
            throw new InvalidOperationException("Start time before the evaluation time.");
        }

        for (var k = 1; k <= kK; k++)
        {
            if (!(arrival[k] >= departure[k - 1]) || !(departure[k] >= arrival[k]))
            {
                throw new InvalidOperationException("Main-node times decrease.");
            }
        }

        // section headroom: suffix minimum of the stop headrooms
        var suffix = new double[m + 1];
        suffix[m] = double.PositiveInfinity;
        for (var q = m - 1; q >= 0; q--)
        {
            suffix[q] = Math.Min(stops[q].Headroom, suffix[q + 1]);
        }

        var headroom = new double[kK + 2];
        headroom[0] = suffix[0];
        for (var k = 1; k <= kK; k++)
        {
            headroom[k] = suffix[firstStop[k]];
        }

        // section loads: load on section k after serving every stop before main node k
        var load = new int[kK + 2];
        var running = context.InitialLoad;
        load[0] = running;
        for (int k = 1, q = 0; k <= kK; k++)
        {
            for (; q < firstStop[k]; q++)
            {
                running += stops[q].IsPickup ? stops[q].Party : -stops[q].Party;
            }

            load[k] = running;
        }

        var capacity = context.Capacity;
        var dPickup = DMaxMul(g, omega);
        var dMaxSection = new double[kK + 2];

        // full-section ellipses R_k (mask + node list)
        var masks = new byte[kK + 2][];
        var lists = new int[kK + 2][];
        lists[kK + 1] = [];
        var scratch = new int[n];
        for (var k = 1; k <= kK; k++)
        {
            var a = mainNode[k - 1];
            var b = mainNode[k];
            var rowA = a * n;
            var rowB = b * n;
            int dab = d[rowA + b];
            var dm = dMaxSection[k] = DMaxMul(g, headroom[k]);
            var mask = masks[k] = new byte[n];
            var count = 0;
            for (var node = 0; node < n; node++)
            {
                if (d[rowA + node] + dt[rowB + node] - dab <= dm)
                {
                    mask[node] = 1;
                    scratch[count++] = node;
                }
            }

            lists[k] = scratch[..count];
        }

        // section 0: ellipse of the whole arc a0 -> M[0] when the vehicle is mid-arc
        var arc = context.Arc;
        var a0 = -1;
        if (arc is not null)
        {
            a0 = globalOf[arc.From];
            if (arc.Permille is < 1 or > 999)
            {
                throw new ArgumentException("The arc progress must be 1..999 permille.", nameof(context));
            }

            if (a0 == mainNode[0])
            {
                throw new InvalidOperationException("The arc start equals the arc end.");
            }

            var rowA = a0 * n;
            var rowB = mainNode[0] * n;
            int dab = d[rowA + mainNode[0]];
            var dm = dMaxSection[0] = DMaxMul(g, headroom[0]);
            var mask = masks[0] = new byte[n];
            var count = 0;
            for (var node = 0; node < n; node++)
            {
                if (d[rowA + node] + dt[rowB + node] - dab <= dm)
                {
                    mask[node] = 1;
                    scratch[count++] = node;
                }
            }

            lists[0] = scratch[..count];
        }

        // maxSection[node] = largest section containing node; per-zone counts of the suffix unions U[k]
        var maxSection = new int[n];
        for (var k = 1; k <= kK; k++)
        {
            foreach (var node in lists[k])
            {
                maxSection[node] = k;
            }
        }

        var inUnion = new bool[n];
        var unionCount = new int[kK + 2][];
        unionCount[kK + 1] = new int[zones];
        var runningCount = new int[zones];
        for (var k = kK; k >= 1; k--)
        {
            foreach (var node in lists[k])
            {
                if (!inUnion[node])
                {
                    inUnion[node] = true;
                    runningCount[zoneIndex[node]]++;
                }
            }

            unionCount[k] = (int[])runningCount.Clone();
        }

        // The rate interval is the one the rates were binned with (the emulator's 600 s).
        var rates = cell.Rates;
        var interval = rates.IntervalMilliseconds;
        var causalWeights = cell.Oracle
            ? null
            : rates.Causal((long)Math.Floor(context.EvaluationTime / interval));
        ZoneWeights WeightsAt(double t) =>
            cell.Oracle ? rates.Oracle((long)Math.Floor(t / interval)) : causalWeights!;

        var pickupCount = new int[zones];
        var dropCount = new int[zones];

        static double ZoneSum(IReadOnlyList<double> weights, int[] counts)
        {
            var sum = 0.0;
            for (var z = 0; z < counts.Length; z++)
            {
                if (counts[z] != 0)
                {
                    sum += weights[z] * counts[z];
                }
            }

            return sum;
        }

        // vehicle at main node M[k] after service (k < kK): sigma1 = section k+1, sigma2 = section k+2
        double AtMain(int k, double t, ZoneWeights w)
        {
            if (k >= kK || load[k + 1] >= capacity)
            {
                return 0;
            }

            var rowX = mainNode[k] * n;
            var first = lists[k + 1];
            var hasSecond = k + 2 <= kK;
            var d2 = -1.0;
            var rowX2 = 0;
            byte[]? secondMask = null;
            if (hasSecond)
            {
                d2 = DMaxAdd(g, departure[k + 1] - t, omega);
                rowX2 = mainNode[k + 1] * n;
                secondMask = masks[k + 2];
            }

            Array.Clear(pickupCount);
            foreach (var node in first)
            {
                if (d[rowX + node] <= dPickup
                    && !(hasSecond && secondMask![node] == 1 && d[rowX2 + node] <= d2))
                {
                    pickupCount[zoneIndex[node]]++;
                }
            }

            if (hasSecond && d2 >= 0)
            {
                foreach (var node in lists[k + 2])
                {
                    if (d[rowX2 + node] <= d2)
                    {
                        pickupCount[zoneIndex[node]]++;
                    }
                }
            }

            Array.Copy(unionCount[k + 1], dropCount, zones);
            return ZoneSum(w.Origin, pickupCount) + ZoneSum(w.Destination, dropCount);
        }

        // vehicle at intermediate node x of section k: sigma1 = (x -> M[k]) with H[k], sigma2 = section k+1
        // (k = 0: x on the rest of the current arc, sigma1 = (x -> M[0]) with H[0], sigma2 = section 1)
        double AtIntermediate(int k, int x, double t, ZoneWeights w)
        {
            if (load[k] >= capacity)
            {
                return 0;
            }

            var b = mainNode[k];
            var rowX = x * n;
            var rowB = b * n;
            int dxb = d[rowX + b];
            var dm = dMaxSection[k];
            var hasSecond = k + 1 <= kK;
            var d2 = -1.0;
            byte[]? secondMask = null;
            if (hasSecond)
            {
                d2 = DMaxAdd(g, departure[k] - t, omega);
                secondMask = masks[k + 1];
            }

            Array.Clear(pickupCount);
            Array.Copy(unionCount[k + 1], dropCount, zones);
            foreach (var node in lists[k])
            {
                int dxn = d[rowX + node];
                if (dxn + dt[rowB + node] - dxb <= dm)
                {
                    if (maxSection[node] == k)
                    {
                        dropCount[zoneIndex[node]]++;
                    }

                    if (dxn <= dPickup
                        && !(hasSecond && secondMask![node] == 1 && d[rowB + node] <= d2))
                    {
                        pickupCount[zoneIndex[node]]++;
                    }
                }
            }

            if (hasSecond && d2 >= 0)
            {
                foreach (var node in lists[k + 1])
                {
                    if (d[rowB + node] <= d2)
                    {
                        pickupCount[zoneIndex[node]]++;
                    }
                }
            }

            return ZoneSum(w.Origin, pickupCount) + ZoneSum(w.Destination, dropCount);
        }

        var waitingSum = 0.0;
        var drivingSum = 0.0;
        var previous = context.EvaluationTime;
        var instants = 0;
        var weightSum = 0.0;

        void Add(double value, double t, bool waiting)
        {
            var dtInstant = t - previous;
            var contribution = value * dtInstant / 1000;
            if (waiting)
            {
                waitingSum += contribution;
            }
            else
            {
                drivingSum += contribution;
            }

            weightSum += dtInstant;
            previous = t;
            instants++;
        }

        var arcInstants = 0;
        if (arc is not null)
        {
            // nodes of the current arc still ahead of the vehicle (section 0)
            var b0 = mainNode[0];
            var path = network.PathOf(a0, b0);
            int dab = d[a0 * n + b0];
            var permille = arc.Permille;
            var span = arrival[0] - context.EvaluationTime;
            if (path[0] != a0 || path[^1] != b0)
            {
                throw new InvalidOperationException("Path ends.");
            }

            var kept = previous;
            for (var j = 1; j < path.Length - 1; j++)
            {
                // > 0 exactly when the free-flow fraction of path[j] is beyond permille / 1000
                var numerator = 1000 * d[a0 * n + path[j]] - permille * dab;
                if (numerator <= 0)
                {
                    continue;
                }

                var t = context.EvaluationTime + span * numerator / ((1000 - permille) * dab);
                if (stride != 0 && t - kept < stride)
                {
                    continue;
                }

                Add(AtIntermediate(0, path[j], t, WeightsAt(t)), t, true);
                kept = t;
                arcInstants++;
            }
        }

        Add(AtMain(0, departure[0], WeightsAt(departure[0])), departure[0], 0 <= kP);
        for (var k = 1; k <= kK; k++)
        {
            var a = mainNode[k - 1];
            var b = mainNode[k];
            var path = network.PathOf(a, b);
            int dab = d[a * n + b];
            if (path[0] != a || path[^1] != b)
            {
                throw new InvalidOperationException("Path ends.");
            }

            var kept = previous;
            for (var j = 1; j < path.Length - 1; j++)
            {
                var t = departure[k - 1] + (arrival[k] - departure[k - 1]) * d[a * n + path[j]] / dab;
                if (stride != 0 && t - kept < stride)
                {
                    continue;
                }

                Add(AtIntermediate(k, path[j], t, WeightsAt(t)), t, k <= kP);
                kept = t;
            }

            Add(AtMain(k, departure[k], WeightsAt(departure[k])), departure[k], k <= kP);
        }

        if (Math.Abs(weightSum - (departure[kK] - context.EvaluationTime)) > 1e-6)
        {
            throw new InvalidOperationException("The instant weights do not add up to drop - evaluation time.");
        }

        return new ShareabilityScore(
            waitingSum + drivingSum,
            waitingSum,
            drivingSum,
            instants,
            kP,
            kK,
            mainNode.Count,
            arcInstants);
    }
}
