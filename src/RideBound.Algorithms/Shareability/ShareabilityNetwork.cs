namespace RideBound.Algorithms.Shareability;

/// <summary>
/// Free-flow network of the time-integrated shareability score of Elmi, Moylan, Alonso-Mora
/// and Fielbaum (2026), as the comparison emulator defines it (wf15 SPEC 2.3 and 4.2;
/// netfull.js): all-pairs free-flow seconds D (row-major from -> to, closed integer APSP),
/// its transpose, the directed edge list in CSR form for shortest-path reconstruction, and
/// the zone of every node. Pure data: no map provider, no file format.
/// </summary>
public sealed class ShareabilityNetwork
{
    private const int PathCacheCapacity = 50_000;
    private readonly Dictionary<long, int[]> paths = new();

    private ShareabilityNetwork(
        int nodeCount,
        int zoneCount,
        ushort[] distances,
        ushort[] transposed,
        int[] csrStart,
        int[] csrTo,
        int[] csrWeight,
        int[] zoneIndex,
        int[] zoneSize)
    {
        NodeCount = nodeCount;
        ZoneCount = zoneCount;
        D = distances;
        DT = transposed;
        CsrStart = csrStart;
        CsrTo = csrTo;
        CsrWeight = csrWeight;
        ZoneIndex = zoneIndex;
        ZoneSizes = zoneSize;
    }

    public int NodeCount { get; }

    public int ZoneCount { get; }

    /// <summary>Nodes per zone (|N_z|), indexed by zone index.</summary>
    public IReadOnlyList<int> ZoneSizes { get; }

    internal ushort[] D { get; }

    internal ushort[] DT { get; }

    internal int[] CsrStart { get; }

    internal int[] CsrTo { get; }

    internal int[] CsrWeight { get; }

    internal int[] ZoneIndex { get; }

    /// <summary>Zone index of a network node.</summary>
    public int ZoneOf(int node) => ZoneIndex[node];

    /// <param name="nodeCount">Number of nodes N.</param>
    /// <param name="distances">N*N free-flow seconds, row-major from -> to.</param>
    /// <param name="csrStart">N+1 offsets of each node's out-edges.</param>
    /// <param name="csrTo">Edge heads in CSR order.</param>
    /// <param name="csrWeight">Positive integer edge weights (seconds) in CSR order.</param>
    /// <param name="zoneIndex">Zone index 0..zoneCount-1 of every node.</param>
    /// <param name="zoneCount">Number of zones; every zone must hold at least one node.</param>
    public static ShareabilityNetwork Create(
        int nodeCount,
        ushort[] distances,
        int[] csrStart,
        int[] csrTo,
        int[] csrWeight,
        int[] zoneIndex,
        int zoneCount)
    {
        ArgumentNullException.ThrowIfNull(distances);
        ArgumentNullException.ThrowIfNull(csrStart);
        ArgumentNullException.ThrowIfNull(csrTo);
        ArgumentNullException.ThrowIfNull(csrWeight);
        ArgumentNullException.ThrowIfNull(zoneIndex);

        if (nodeCount < 1 || distances.Length != (long)nodeCount * nodeCount)
        {
            throw new ArgumentException("The distance matrix must be N*N.", nameof(distances));
        }

        if (csrStart.Length != nodeCount + 1
            || csrStart[0] != 0
            || csrStart[nodeCount] != csrTo.Length
            || csrTo.Length != csrWeight.Length)
        {
            throw new ArgumentException("The edge list is not a CSR of N nodes.", nameof(csrStart));
        }

        for (var node = 0; node < nodeCount; node++)
        {
            if (csrStart[node + 1] < csrStart[node])
            {
                throw new ArgumentException("CSR offsets must not decrease.", nameof(csrStart));
            }
        }

        for (var edge = 0; edge < csrTo.Length; edge++)
        {
            if ((uint)csrTo[edge] >= (uint)nodeCount || csrWeight[edge] <= 0)
            {
                throw new ArgumentException("Edges need a valid head and a positive weight.", nameof(csrTo));
            }
        }

        if (zoneIndex.Length != nodeCount || zoneCount < 1)
        {
            throw new ArgumentException("Every node needs a zone.", nameof(zoneIndex));
        }

        var zoneSize = new int[zoneCount];
        foreach (var zone in zoneIndex)
        {
            if ((uint)zone >= (uint)zoneCount)
            {
                throw new ArgumentException("Zone index out of range.", nameof(zoneIndex));
            }

            zoneSize[zone]++;
        }

        if (Array.IndexOf(zoneSize, 0) >= 0)
        {
            throw new ArgumentException("Every zone must hold at least one node.", nameof(zoneCount));
        }

        var transposed = new ushort[distances.Length];
        for (var from = 0; from < nodeCount; from++)
        {
            var row = from * nodeCount;
            for (var to = 0; to < nodeCount; to++)
            {
                transposed[to * nodeCount + from] = distances[row + to];
            }
        }

        return new ShareabilityNetwork(
            nodeCount,
            zoneCount,
            (ushort[])distances.Clone(),
            transposed,
            (int[])csrStart.Clone(),
            (int[])csrTo.Clone(),
            (int[])csrWeight.Clone(),
            (int[])zoneIndex.Clone(),
            zoneSize);
    }

    /// <summary>
    /// One shortest free-flow path a -> b: a greedy step to the out-neighbour u with
    /// w(cur, u) + D[u][b] == D[cur][b], the smallest u on ties (SPEC 4.7; sscore.js pathOf).
    /// Exact for integer weights and the closed integer APSP. The edge sum is checked
    /// against D. Cached; the cache never changes a result.
    /// </summary>
    internal int[] PathOf(int a, int b)
    {
        var n = NodeCount;
        var key = (long)a * n + b;
        if (paths.TryGetValue(key, out var hit))
        {
            return hit;
        }

        var output = new List<int> { a };
        var current = a;
        var weightSum = 0;
        for (var guard = 0; current != b; guard++)
        {
            if (guard > n)
            {
                throw new InvalidOperationException("Shareability path reconstruction loops.");
            }

            var remaining = D[current * n + b];
            var best = -1;
            var bestWeight = 0;
            for (var edge = CsrStart[current]; edge < CsrStart[current + 1]; edge++)
            {
                var u = CsrTo[edge];
                if (CsrWeight[edge] + D[u * n + b] == remaining && (best < 0 || u < best))
                {
                    best = u;
                    bestWeight = CsrWeight[edge];
                }
            }

            if (best < 0)
            {
                throw new InvalidOperationException($"No shortest-path step {current} -> {b}.");
            }

            output.Add(best);
            weightSum += bestWeight;
            current = best;
        }

        if (weightSum != D[a * n + b])
        {
            throw new InvalidOperationException(
                $"Path edge sum {weightSum} differs from D {D[a * n + b]} ({a} -> {b}).");
        }

        var path = output.ToArray();
        if (paths.Count >= PathCacheCapacity)
        {
            paths.Clear();
        }

        paths[key] = path;
        return path;
    }
}
