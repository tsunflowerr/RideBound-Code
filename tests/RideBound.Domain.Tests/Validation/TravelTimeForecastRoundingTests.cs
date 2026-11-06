using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Domain.Tests.Validation;

// A forecast arrival that is exactly half a millisecond rounds up, like the emulator's Math.round.
// The recorded profiles (1.25/1.5/1.1) and the random golden profiles never produce an exact .5, so this is the
// only test that tells MidpointRounding.AwayFromZero apart from MidpointRounding.ToEven.
public sealed class TravelTimeForecastRoundingTests
{
    [Fact]
    public void A_half_millisecond_arrival_rounds_up_like_the_emulator()
    {
        // 900 s left in bin 0 at weight 1, then 1 ms in bin 1 at weight 1 / 2 (profile 2 -> 1): 900,000.5 ms.
        var forecast = new TravelTimeForecast([2.0, 1.0], 900_000);

        Assert.Equal(
            900_001,
            forecast.Arrive(new SimTime(0), new SimTime(0), new Duration(900_001)).Milliseconds);
    }
}
