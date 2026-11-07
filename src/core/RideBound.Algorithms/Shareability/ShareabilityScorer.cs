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
    /// <summary>The emulator's rate interval (sscore.js IV_MS); scoring uses the rates' own interval.</summary>
    public const double IntervalMilliseconds = ZoneDemandRates.DefaultIntervalMilliseconds;

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

    /// <summary>
    /// Scores the target rider of the context in four steps: main nodes, section shadows, the instants on the rest
    /// of the current arc, then the instants of every section up to the target's drop.
    /// </summary>
    public ShareabilityScore Score(ShareabilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Stops);

        var m = context.Stops.Count;
        if (context.Bin < 0)
        {
            throw new ArgumentException("Negative bin.", nameof(context));
        }

        if ((uint)context.PickupIndex >= (uint)m || (uint)context.DropIndex >= (uint)m)
        {
            throw new ArgumentException("Target stops are not on the route.", nameof(context));
        }

        // driving time per free-flow second in this bin (ms)
        var g = cell.Kappa * cell.Factors[Math.Min(context.Bin, cell.Factors.Count - 1)] * 1000;
        var main = FindMainNodes(context, cell.GlobalOf);
        CheckTimes(main, context.EvaluationTime);
        var shadow = new RouteShadow(network, cell, context, main, g);
        var integral = new Integral(context.EvaluationTime);
        var arcInstants = context.Arc is null ? 0 : IntegrateArc(context, main, shadow, integral);
        IntegrateSections(main, shadow, integral);

        var (mainNode, _, departure, _, kP, kK) = main;
        if (Math.Abs(integral.WeightSum - (departure[kK] - context.EvaluationTime)) > 1e-6)
        {
            throw new InvalidOperationException("The instant weights do not add up to drop - evaluation time.");
        }

        return new ShareabilityScore(
            integral.WaitingSum + integral.DrivingSum,
            integral.WaitingSum,
            integral.DrivingSum,
            integral.Instants,
            kP,
            kK,
            mainNode.Count,
            arcInstants);
    }

    /// <summary>
    /// Main nodes of a route: Node, Arrival, Departure and FirstStop (-1 for the vehicle start) per main node, and
    /// the main nodes PickupMain (kP) and DropMain (kK) of the target's pickup and drop.
    /// </summary>
    private sealed record MainNodes(
        List<int> Node,
        List<double> Arrival,
        List<double> Departure,
        List<int> FirstStop,
        int PickupMain,
        int DropMain);

    // main nodes (SPEC 4.3): the vehicle start, then the route stops, consecutive stops at one node merged
    private static MainNodes FindMainNodes(ShareabilityContext context, IReadOnlyList<int> globalOf)
    {
        var stops = context.Stops;
        var m = stops.Count;
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

        return new MainNodes(mainNode, arrival, departure, firstStop, kP, kK);
    }

    // the vehicle leaves its start at or after the evaluation time, and main-node times never decrease
    private static void CheckTimes(MainNodes main, double evaluationTime)
    {
        var (_, arrival, departure, _, _, kK) = main;
        if (!(departure[0] >= evaluationTime))
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
    }

    /// <summary>
    /// The sections of one route up to the target's drop main node kK (section k = main node k-1 -> k, section 0 =
    /// rest of the arc being driven) with their headroom, load and ellipse, and from them the instant value S(t)
    /// of the vehicle at a main node or at an intermediate node.
    /// </summary>
    private sealed class RouteShadow
    {
        private readonly int n;
        private readonly ushort[] d;
        private readonly ushort[] dt;
        private readonly int[] zoneIndex;
        private readonly int zones;
        private readonly double g;
        private readonly double omega;
        private readonly int kK;
        private readonly List<int> mainNode;
        private readonly List<double> departure;
        private readonly int[] load;
        private readonly int capacity;
        private readonly double dPickup;
        private readonly double[] dMaxSection;
        private readonly byte[][] masks;
        private readonly int[][] lists;
        private readonly int[] maxSection;
        private readonly int[][] unionCount;
        private readonly ZoneDemandRates rates;
        private readonly double interval;
        private readonly bool oracle;
        private readonly ZoneWeights? causalWeights;
        private readonly int[] pickupCount;
        private readonly int[] dropCount;

        public RouteShadow(
            ShareabilityNetwork network,
            ShareabilityCell cell,
            ShareabilityContext context,
            MainNodes main,
            double g)
        {
            n = network.NodeCount;
            d = network.D;
            dt = network.DT;
            zoneIndex = network.ZoneIndex;
            zones = network.ZoneCount;
            this.g = g;
            omega = cell.PickupWindow;
            kK = main.DropMain;
            mainNode = main.Node;
            departure = main.Departure;

            var headroom = SectionHeadroom(context.Stops, main.FirstStop, kK);
            load = SectionLoads(context.Stops, context.InitialLoad, main.FirstStop, kK);
            capacity = context.Capacity;
            dPickup = DMaxMul(g, omega);
            dMaxSection = new double[kK + 2];

            // full-section ellipses R_k (mask + node list)
            masks = new byte[kK + 2][];
            lists = new int[kK + 2][];
            lists[kK + 1] = [];
            var scratch = new int[n];
            for (var k = 1; k <= kK; k++)
            {
                dMaxSection[k] = DMaxMul(g, headroom[k]);
                (masks[k], lists[k]) = Ellipse(mainNode[k - 1], mainNode[k], dMaxSection[k], scratch);
            }

            // section 0: ellipse of the whole arc a0 -> M[0] when the vehicle is mid-arc
            var arc = context.Arc;
            if (arc is not null)
            {
                var a0 = cell.GlobalOf[arc.From];
                if (arc.Permille is < 1 or > 999)
                {
                    throw new ArgumentException("The arc progress must be 1..999 permille.", nameof(context));
                }

                if (a0 == mainNode[0])
                {
                    throw new InvalidOperationException("The arc start equals the arc end.");
                }

                dMaxSection[0] = DMaxMul(g, headroom[0]);
                (masks[0], lists[0]) = Ellipse(a0, mainNode[0], dMaxSection[0], scratch);
            }

            maxSection = LargestSections();
            unionCount = SuffixUnionCounts();

            // The rate interval is the one the rates were binned with (the emulator's 600 s).
            rates = cell.Rates;
            interval = rates.IntervalMilliseconds;
            oracle = cell.Oracle;
            causalWeights = oracle ? null : rates.Causal((long)Math.Floor(context.EvaluationTime / interval));
            pickupCount = new int[zones];
            dropCount = new int[zones];
        }

        // vehicle at main node M[k] after service (k < kK): sigma1 = section k+1, sigma2 = section k+2
        public double AtMain(int k, double t)
        {
            var w = WeightsAt(t);
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
        public double AtIntermediate(int k, int x, double t)
        {
            var w = WeightsAt(t);
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

        // section headroom H[k]: suffix minimum of the stop headrooms, from the first stop of main node k on
        private static double[] SectionHeadroom(IReadOnlyList<ShareabilityStop> stops, List<int> firstStop, int kK)
        {
            var m = stops.Count;
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

            return headroom;
        }

        // section loads: load on section k after serving every stop before main node k
        private static int[] SectionLoads(
            IReadOnlyList<ShareabilityStop> stops, int initialLoad, List<int> firstStop, int kK)
        {
            var load = new int[kK + 2];
            var running = initialLoad;
            load[0] = running;
            for (int k = 1, q = 0; k <= kK; k++)
            {
                for (; q < firstStop[k]; q++)
                {
                    running += stops[q].IsPickup ? stops[q].Party : -stops[q].Party;
                }

                load[k] = running;
            }

            return load;
        }

        // ellipse of the section a -> b: the nodes whose detour D[a][node] + D[node][b] - D[a][b] is at most dm
        private (byte[] Mask, int[] Nodes) Ellipse(int a, int b, double dm, int[] scratch)
        {
            var rowA = a * n;
            var rowB = b * n;
            int dab = d[rowA + b];
            var mask = new byte[n];
            var count = 0;
            for (var node = 0; node < n; node++)
            {
                if (d[rowA + node] + dt[rowB + node] - dab <= dm)
                {
                    mask[node] = 1;
                    scratch[count++] = node;
                }
            }

            return (mask, scratch[..count]);
        }

        // largest[node] = largest section containing node
        private int[] LargestSections()
        {
            var largest = new int[n];
            for (var k = 1; k <= kK; k++)
            {
                foreach (var node in lists[k])
                {
                    largest[node] = k;
                }
            }

            return largest;
        }

        // per-zone node counts of the suffix unions U[k] = sections k..kK (U[kK + 1] is empty)
        private int[][] SuffixUnionCounts()
        {
            var inUnion = new bool[n];
            var counts = new int[kK + 2][];
            counts[kK + 1] = new int[zones];
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

                counts[k] = (int[])runningCount.Clone();
            }

            return counts;
        }

        // zone weights at time t: the causal ones of the evaluation time, or the oracle ones of t's rate interval
        private ZoneWeights WeightsAt(double t) =>
            oracle ? rates.Oracle((long)Math.Floor(t / interval)) : causalWeights!;

        private static double ZoneSum(IReadOnlyList<double> weights, int[] counts)
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
    }

    /// <summary>
    /// Running integral of S(t) over the instants: each instant adds S(t) * (t - previous instant) / 1000 to the
    /// waiting part (up to the target's pickup main node) or to the driving part.
    /// </summary>
    private sealed class Integral
    {
        public Integral(double evaluationTime)
        {
            Previous = evaluationTime;
        }

        public double WaitingSum { get; private set; }

        public double DrivingSum { get; private set; }

        public double Previous { get; private set; }

        public int Instants { get; private set; }

        public double WeightSum { get; private set; }

        public void Add(double value, double t, bool waiting)
        {
            var dtInstant = t - Previous;
            var contribution = value * dtInstant / 1000;
            if (waiting)
            {
                WaitingSum += contribution;
            }
            else
            {
                DrivingSum += contribution;
            }

            WeightSum += dtInstant;
            Previous = t;
            Instants++;
        }
    }

    // instants at the nodes of the current arc still ahead of the vehicle (section 0); returns their number
    private int IntegrateArc(ShareabilityContext context, MainNodes main, RouteShadow shadow, Integral integral)
    {
        var (mainNode, arrival, _, _, _, _) = main;
        var arc = context.Arc!;
        var n = network.NodeCount;
        var d = network.D;
        var stride = cell.StrideMilliseconds;
        var a0 = cell.GlobalOf[arc.From];
        var arcInstants = 0;

        var b0 = mainNode[0];
        var path = network.PathOf(a0, b0);
        int dab = d[a0 * n + b0];
        var permille = arc.Permille;
        var span = arrival[0] - context.EvaluationTime;
        if (path[0] != a0 || path[^1] != b0)
        {
            throw new InvalidOperationException("Path ends.");
        }

        var kept = integral.Previous;
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

            integral.Add(shadow.AtIntermediate(0, path[j], t), t, true);
            kept = t;
            arcInstants++;
        }

        return arcInstants;
    }

    // instants at main node M[0], then at the intermediate nodes and the end main node of sections 1..kK;
    // instants up to the pickup main node kP count as waiting
    private void IntegrateSections(MainNodes main, RouteShadow shadow, Integral integral)
    {
        var (mainNode, arrival, departure, _, kP, kK) = main;
        var n = network.NodeCount;
        var d = network.D;
        var stride = cell.StrideMilliseconds;

        integral.Add(shadow.AtMain(0, departure[0]), departure[0], 0 <= kP);
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

            var kept = integral.Previous;
            for (var j = 1; j < path.Length - 1; j++)
            {
                var t = departure[k - 1] + (arrival[k] - departure[k - 1]) * d[a * n + path[j]] / dab;
                if (stride != 0 && t - kept < stride)
                {
                    continue;
                }

                integral.Add(shadow.AtIntermediate(k, path[j], t), t, k <= kP);
                kept = t;
            }

            integral.Add(shadow.AtMain(k, departure[k]), departure[k], k <= kP);
        }
    }
}
