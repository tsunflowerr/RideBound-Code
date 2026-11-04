using RideBound.Domain.Common;

namespace RideBound.Domain.Validation;

/// <summary>
/// Time-of-day forecast of travel times, used only where the dispatcher projects a plan
/// (closed-loop forecast of the comparison emulator, engine17.js legF :151-167, :454).
/// A leg that takes w ms under the current snapshot is integrated over fixed bins: weight 1
/// in the bin of the evaluation time b0, weight profile[b] / profile[b0] in any other bin
/// (the profile is held after its last value). The arrival is rounded to whole milliseconds,
/// ties up (the integer convention of the adapter-mode emulator, engine_wf15_clint.js).
/// The arithmetic is the emulator's, in IEEE double, step for step. Physical motion is not
/// affected: the simulator moves vehicles on its own travel times.
/// </summary>
public sealed class TravelTimeForecast
{
    // A step crosses one bin, so a leg converges when its forecast duration (the leg scaled by the
    // profile weights) spans fewer than MaximumBinSteps bins: with bins of at least a minute, about
    // 69 days of forecast time. The recorded runs use 15-minute bins and weights near 1.
    public const long MinimumBinMilliseconds = 60_000;
    private const int MaximumBinSteps = 100_000;
    private readonly double[] _profile;

    public TravelTimeForecast(IReadOnlyList<double> profile, long binMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Count == 0
            || profile.Any(value => !double.IsFinite(value) || value <= 0))
        {
            throw new ArgumentException(
                "A forecast profile needs at least one finite positive factor.",
                nameof(profile));
        }

        if (binMilliseconds < MinimumBinMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(binMilliseconds),
                $"A forecast bin is at least {MinimumBinMilliseconds} ms.");
        }

        _profile = profile.ToArray();
        BinMilliseconds = binMilliseconds;
    }

    public IReadOnlyList<double> Profile => _profile;

    public long BinMilliseconds { get; }

    public SimTime Arrive(SimTime evaluationTime, SimTime departure, Duration travelTime)
    {
        if (travelTime.Milliseconds <= 0)
        {
            return departure + travelTime;
        }

        double bin = BinMilliseconds;
        double last = _profile.Length - 1;
        var currentBin = Math.Floor(evaluationTime.Milliseconds / bin);
        var currentFactor = _profile[(int)Math.Min(currentBin, last)];
        double time = departure.Milliseconds;
        double remaining = travelTime.Milliseconds;

        for (var step = 0; step < MaximumBinSteps; step++)
        {
            var binIndex = Math.Floor(time / bin);
            var weight = binIndex == currentBin
                ? 1.0
                : _profile[(int)Math.Min(binIndex, last)] / currentFactor;
            var covered = ((binIndex + 1) * bin - time) / weight;

            if (covered >= remaining)
            {
                return new SimTime(
                    checked((long)Math.Round(
                        time + remaining * weight,
                        MidpointRounding.AwayFromZero)));
            }

            remaining -= covered;
            time = (binIndex + 1) * bin;
        }

        throw new InvalidOperationException("Forecast leg integration did not converge.");
    }
}
