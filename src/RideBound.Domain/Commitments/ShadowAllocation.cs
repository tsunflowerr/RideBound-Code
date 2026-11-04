namespace RideBound.Domain.Commitments;

/// <summary>
/// Shadow Control class of a rider (Elmi, Moylan, Alonso-Mora and Fielbaum 2026), frozen at the
/// rider's first published promise: a guaranteed rider keeps a zero drop allowance, an allowed
/// rider gets its whole residual slack.
/// </summary>
public enum ShadowAllocationClass
{
    Guaranteed = 0,
    Allowed = 1,
}

/// <summary>
/// The class of one rider and the shareability score it was decided from. The score is kept as
/// the shortest round-trip decimal string of the IEEE double, because the canonical state holds
/// integers and strings only.
/// </summary>
public sealed record ShadowAllocation(ShadowAllocationClass Class, string Score)
{
    public ShadowAllocationClass Class { get; } = Enum.IsDefined(Class)
        ? Class
        : throw new ArgumentOutOfRangeException(nameof(Class));

    public string Score { get; } = !string.IsNullOrWhiteSpace(Score)
        ? Score
        : throw new ArgumentException("A shadow allocation records its score.", nameof(Score));

    public static ShadowAllocation From(double score, double threshold)
    {
        if (!double.IsFinite(score))
        {
            throw new ArgumentOutOfRangeException(nameof(score), "A shareability score is finite.");
        }

        // classOf of the comparison emulator: sS <= theta -> guaranteed.
        return new ShadowAllocation(
            score <= threshold ? ShadowAllocationClass.Guaranteed : ShadowAllocationClass.Allowed,
            score.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }
}
