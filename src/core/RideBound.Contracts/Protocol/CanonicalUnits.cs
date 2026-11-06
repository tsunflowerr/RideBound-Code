namespace RideBound.Contracts.Protocol;

public readonly record struct DistanceMillimeters
{
    private DistanceMillimeters(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public static bool TryCreate(long value, out DistanceMillimeters distance)
    {
        if (value is < 0 or > ProtocolLimits.MaxCanonicalInteger)
        {
            distance = default;
            return false;
        }

        distance = new DistanceMillimeters(value);
        return true;
    }
}

public readonly record struct LatitudeE7
{
    public const long Minimum = -900_000_000;
    public const long Maximum = 900_000_000;

    private LatitudeE7(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public static bool TryCreate(long value, out LatitudeE7 latitude)
    {
        if (value is < Minimum or > Maximum)
        {
            latitude = default;
            return false;
        }

        latitude = new LatitudeE7(value);
        return true;
    }
}

public readonly record struct LongitudeE7
{
    public const long Minimum = -1_800_000_000;
    public const long Maximum = 1_800_000_000;

    private LongitudeE7(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public static bool TryCreate(long value, out LongitudeE7 longitude)
    {
        if (value is < Minimum or > Maximum)
        {
            longitude = default;
            return false;
        }

        longitude = new LongitudeE7(value);
        return true;
    }
}

public readonly record struct EdgeProgressPermille
{
    private EdgeProgressPermille(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static bool TryCreate(int value, out EdgeProgressPermille progress)
    {
        if (value is < 1 or > 999)
        {
            progress = default;
            return false;
        }

        progress = new EdgeProgressPermille(value);
        return true;
    }
}

public readonly record struct CostMicros
{
    private CostMicros(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public static bool TryCreate(long value, out CostMicros cost)
    {
        if (value is < ProtocolLimits.MinCanonicalInteger
            or > ProtocolLimits.MaxCanonicalInteger)
        {
            cost = default;
            return false;
        }

        cost = new CostMicros(value);
        return true;
    }
}

public static class CanonicalUnitConversions
{
    public static bool TrySecondsToMilliseconds(
        decimal seconds,
        out SimulationTimeMilliseconds milliseconds)
    {
        if (seconds < 0
            || !TryScale(
                seconds,
                1_000m,
                minimum: 0,
                ProtocolLimits.MaxCanonicalInteger,
                out var value))
        {
            milliseconds = default;
            return false;
        }

        return SimulationTimeMilliseconds.TryCreate(value, out milliseconds);
    }

    public static bool TryMetersToMillimeters(
        decimal meters,
        out DistanceMillimeters millimeters)
    {
        if (meters < 0
            || !TryScale(
                meters,
                1_000m,
                minimum: 0,
                ProtocolLimits.MaxCanonicalInteger,
                out var value))
        {
            millimeters = default;
            return false;
        }

        return DistanceMillimeters.TryCreate(value, out millimeters);
    }

    public static bool TryLatitudeDegrees(decimal degrees, out LatitudeE7 latitude)
    {
        if (!TryScale(
                degrees,
                10_000_000m,
                LatitudeE7.Minimum,
                LatitudeE7.Maximum,
                out var value))
        {
            latitude = default;
            return false;
        }

        return LatitudeE7.TryCreate(value, out latitude);
    }

    public static bool TryLongitudeDegrees(decimal degrees, out LongitudeE7 longitude)
    {
        if (!TryScale(
                degrees,
                10_000_000m,
                LongitudeE7.Minimum,
                LongitudeE7.Maximum,
                out var value))
        {
            longitude = default;
            return false;
        }

        return LongitudeE7.TryCreate(value, out longitude);
    }

    public static bool TryCostUnits(decimal costUnits, out CostMicros cost)
    {
        if (!TryScale(
                costUnits,
                1_000_000m,
                ProtocolLimits.MinCanonicalInteger,
                ProtocolLimits.MaxCanonicalInteger,
                out var value))
        {
            cost = default;
            return false;
        }

        return CostMicros.TryCreate(value, out cost);
    }

    private static bool TryScale(
        decimal source,
        decimal scale,
        long minimum,
        long maximum,
        out long value)
    {
        decimal scaled;

        try
        {
            scaled = checked(source * scale);
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }

        var rounded = decimal.Round(scaled, decimals: 0, MidpointRounding.ToEven);

        if (rounded < minimum || rounded > maximum)
        {
            value = default;
            return false;
        }

        value = decimal.ToInt64(rounded);
        return true;
    }
}
