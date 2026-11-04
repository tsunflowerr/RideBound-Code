namespace RideBound.Algorithms.Shareability;

/// <summary>Zone weights lambda_z / |N_z| for origins and destinations, indexed by zone.</summary>
public sealed record ZoneWeights(IReadOnlyList<double> Origin, IReadOnlyList<double> Destination);

/// <summary>
/// Per-cell zone demand rates lambda_z (requests per second) of the shareability score, as the
/// comparison emulator defines them (wf15 SPEC 4.5; lambda.js), in the same floating-point order:
/// <list type="bullet">
/// <item>observations obs^i[z]: requests with arrival in [i*I, (i+1)*I) and origin (destination) zone z,
/// rejected ones included; zero after the last observed interval;</item>
/// <item>prior lambda^0[z] = phi * nPrev[z] / 3600, or (sampled / 7200) * |N_z| / N when the prior is empty;</item>
/// <item>smoothing lambda^i[z] = alpha * obs^{i-1}[z] / (I/1000) + (1 - alpha) * lambda^{i-1}[z];</item>
/// <item>causal weights for interval i use lambda^i; oracle weights use obs^i / (I/1000).</item>
/// </list>
/// </summary>
public sealed class ZoneDemandRates
{
    public const double DefaultIntervalMilliseconds = 600_000;

    private readonly int zoneCount;
    private readonly int[] zoneSize;
    private readonly double alpha;
    private readonly double intervalSeconds;
    private readonly double[][] observedOrigin;
    private readonly double[][] observedDestination;
    private readonly double[] zero;
    private readonly List<double[]> hatOrigin;
    private readonly List<double[]> hatDestination;
    private readonly Dictionary<long, ZoneWeights> causal = new();
    private readonly Dictionary<long, ZoneWeights> oracle = new();

    private ZoneDemandRates(
        int zoneCount,
        int[] zoneSize,
        double alpha,
        double intervalMilliseconds,
        double[][] observedOrigin,
        double[][] observedDestination,
        double[] priorOrigin,
        double[] priorDestination,
        int originFallbacks,
        int destinationFallbacks)
    {
        this.zoneCount = zoneCount;
        this.zoneSize = zoneSize;
        this.alpha = alpha;
        IntervalMilliseconds = intervalMilliseconds;
        intervalSeconds = intervalMilliseconds / 1000;
        this.observedOrigin = observedOrigin;
        this.observedDestination = observedDestination;
        zero = new double[zoneCount];
        hatOrigin = [priorOrigin];
        hatDestination = [priorDestination];
        OriginFallbacks = originFallbacks;
        DestinationFallbacks = destinationFallbacks;
    }

    public double IntervalMilliseconds { get; }

    /// <summary>1 when the origin prior was empty and the uniform fallback was used.</summary>
    public int OriginFallbacks { get; }

    /// <summary>1 when the destination prior was empty and the uniform fallback was used.</summary>
    public int DestinationFallbacks { get; }

    public int ObservedIntervals => observedOrigin.Length;

    public static ZoneDemandRates Create(
        ShareabilityNetwork network,
        IReadOnlyList<double> arrivals,
        IReadOnlyList<int> originZones,
        IReadOnlyList<int> destinationZones,
        IReadOnlyList<double> priorOrigin,
        IReadOnlyList<double> priorDestination,
        double phi,
        double alpha,
        double sampled,
        double intervalMilliseconds = DefaultIntervalMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(arrivals);
        ArgumentNullException.ThrowIfNull(originZones);
        ArgumentNullException.ThrowIfNull(destinationZones);
        ArgumentNullException.ThrowIfNull(priorOrigin);
        ArgumentNullException.ThrowIfNull(priorDestination);

        if (!(alpha >= 0 && alpha <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), "alpha must be in [0, 1].");
        }

        if (arrivals.Count != originZones.Count || originZones.Count != destinationZones.Count)
        {
            throw new ArgumentException("One arrival, origin zone and destination zone per request.");
        }

        var zones = network.ZoneCount;
        if (priorOrigin.Count != zones || priorDestination.Count != zones)
        {
            throw new ArgumentException("One prior count per zone.");
        }

        var maximum = double.NegativeInfinity;
        foreach (var arrival in arrivals)
        {
            maximum = Math.Max(maximum, arrival);
        }

        var intervals = arrivals.Count > 0
            ? checked((int)Math.Floor(maximum / intervalMilliseconds) + 1)
            : 1;
        var observedOrigin = new double[intervals][];
        var observedDestination = new double[intervals][];
        for (var i = 0; i < intervals; i++)
        {
            observedOrigin[i] = new double[zones];
            observedDestination[i] = new double[zones];
        }

        for (var k = 0; k < arrivals.Count; k++)
        {
            var interval = Math.Floor(arrivals[k] / intervalMilliseconds);
            if (interval < 0)
            {
                throw new ArgumentException("Arrivals must not be negative.", nameof(arrivals));
            }

            observedOrigin[(int)interval][originZones[k]]++;
            observedDestination[(int)interval][destinationZones[k]]++;
        }

        var sizes = network.ZoneSizes.ToArray();
        var (originPrior, originFallback) = Prior(priorOrigin, phi, sampled, sizes, network.NodeCount);
        var (destinationPrior, destinationFallback) =
            Prior(priorDestination, phi, sampled, sizes, network.NodeCount);
        return new ZoneDemandRates(
            zones,
            sizes,
            alpha,
            intervalMilliseconds,
            observedOrigin,
            observedDestination,
            originPrior,
            destinationPrior,
            originFallback,
            destinationFallback);
    }

    /// <summary>Causal weights of interval i: lambda^i[z] / |N_z| (flat SES forecast).</summary>
    public ZoneWeights Causal(long interval)
    {
        if (interval < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Negative interval.");
        }

        if (!causal.TryGetValue(interval, out var weights))
        {
            weights = new ZoneWeights(
                PerZone(Hat(hatOrigin, observedOrigin, interval)),
                PerZone(Hat(hatDestination, observedDestination, interval)));
            causal[interval] = weights;
        }

        return weights;
    }

    /// <summary>Oracle (non-causal diagnostic) weights of interval i: obs^i[z] / (I/1000) / |N_z|.</summary>
    public ZoneWeights Oracle(long interval)
    {
        if (!oracle.TryGetValue(interval, out var weights))
        {
            var origin = new double[zoneCount];
            var destination = new double[zoneCount];
            var observedO = Observed(observedOrigin, interval);
            var observedD = Observed(observedDestination, interval);
            for (var z = 0; z < zoneCount; z++)
            {
                origin[z] = observedO[z] / intervalSeconds;
                destination[z] = observedD[z] / intervalSeconds;
            }

            weights = new ZoneWeights(PerZone(origin), PerZone(destination));
            oracle[interval] = weights;
        }

        return weights;
    }

    private static (double[] Prior, int Fallback) Prior(
        IReadOnlyList<double> counts,
        double phi,
        double sampled,
        int[] zoneSize,
        int nodeCount)
    {
        var output = new double[counts.Count];
        var total = 0.0;
        for (var z = 0; z < counts.Count; z++)
        {
            total += counts[z];
        }

        if (total > 0)
        {
            for (var z = 0; z < counts.Count; z++)
            {
                output[z] = phi * counts[z] / 3600;
            }

            return (output, 0);
        }

        for (var z = 0; z < counts.Count; z++)
        {
            output[z] = sampled / 7200 * zoneSize[z] / nodeCount;
        }

        return (output, 1);
    }

    private double[] Observed(double[][] observed, long interval) =>
        interval >= 0 && interval < observed.Length ? observed[interval] : zero;

    private double[] Hat(List<double[]> hat, double[][] observed, long interval)
    {
        while (hat.Count <= interval)
        {
            var j = hat.Count;
            var previous = hat[j - 1];
            var observation = Observed(observed, j - 1);
            var current = new double[zoneCount];
            for (var z = 0; z < zoneCount; z++)
            {
                current[z] = alpha * observation[z] / intervalSeconds + (1 - alpha) * previous[z];
            }

            hat.Add(current);
        }

        return hat[(int)interval];
    }

    private double[] PerZone(double[] lambda)
    {
        var weights = new double[zoneCount];
        for (var z = 0; z < zoneCount; z++)
        {
            weights[z] = lambda[z] / zoneSize[z];
        }

        return weights;
    }
}
