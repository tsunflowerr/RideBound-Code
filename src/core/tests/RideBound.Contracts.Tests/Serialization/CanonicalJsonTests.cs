using System.Globalization;
using System.Text;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Serialization;

public sealed class CanonicalJsonTests
{
    [Fact]
    public void Golden_vector_matches_exact_utf8_bytes()
    {
        var input = FixtureLoader.ReadUtf8("canonical/envelope-unordered.input.json");
        var expectedHex = ReadExpectedHex();

        var actual = CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expectedHex, Convert.ToHexStringLower(actual));
        Assert.DoesNotContain((byte)'\n', actual);
    }

    [Fact]
    public void Envelope_overload_uses_the_same_canonical_projection()
    {
        var input = FixtureLoader.ReadUtf8("canonical/envelope-unordered.input.json");
        var decoded = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(input));

        Assert.True(decoded.IsSuccess);

        var canonical = CanonicalJson.Serialize(decoded.Envelope!);

        Assert.Equal(ReadExpectedHex(), Convert.ToHexStringLower(canonical));
    }

    [Fact]
    public void Canonical_bytes_do_not_depend_on_current_culture()
    {
        const string json = """{"z":1234,"i":"İ","a":-5}""";
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var french = CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(json));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            var turkish = CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(json));

            Assert.Equal(french, turkish);
            Assert.Equal("""{"a":-5,"i":"İ","z":1234}""", Encoding.UTF8.GetString(french));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Ordered_arrays_are_never_sorted()
    {
        var first = CanonicalJson.Canonicalize(
            """{"route":["stop-b","stop-a"]}"""u8);
        var second = CanonicalJson.Canonicalize(
            """{"route":["stop-a","stop-b"]}"""u8);

        Assert.NotEqual(first, second);
        Assert.Equal(
            """{"route":["stop-b","stop-a"]}""",
            Encoding.UTF8.GetString(first));
    }

    [Theory]
    [InlineData("""{"value":1.0}""", CanonicalJsonErrorCode.NonIntegerNumber)]
    [InlineData("""{"value":1e3}""", CanonicalJsonErrorCode.NonIntegerNumber)]
    [InlineData("""{"value":1E3}""", CanonicalJsonErrorCode.NonIntegerNumber)]
    [InlineData("""{"value":-0}""", CanonicalJsonErrorCode.NonIntegerNumber)]
    [InlineData(
        """{"value":9007199254740992}""",
        CanonicalJsonErrorCode.IntegerOutOfRange)]
    [InlineData("""{"value":null}""", CanonicalJsonErrorCode.NullNotAllowed)]
    [InlineData(
        """{"value":1,"value":2}""",
        CanonicalJsonErrorCode.DuplicateProperty)]
    public void Rejects_noncanonical_json(
        string json,
        CanonicalJsonErrorCode expectedCode)
    {
        var exception = Assert.Throws<CanonicalJsonException>(
            () => CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(json)));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public void Safe_integer_boundaries_are_preserved()
    {
        const string json =
            """{"min":-9007199254740991,"max":9007199254740991}""";

        var canonical = CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(json));

        Assert.Equal(
            """{"max":9007199254740991,"min":-9007199254740991}""",
            Encoding.UTF8.GetString(canonical));
    }

    [Theory]
    [InlineData("""{"value":"\uD800"}""")]
    [InlineData("""{"value":"\uDC00"}""")]
    [InlineData("""{"value":"\uD800x"}""")]
    public void Rejects_unpaired_escaped_surrogates(string json)
    {
        var exception = Assert.Throws<CanonicalJsonException>(
            () => CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(json)));

        Assert.Equal(CanonicalJsonErrorCode.InvalidUnicode, exception.Code);
    }

    [Fact]
    public void Accepts_paired_escaped_surrogates_as_utf8()
    {
        var canonical = CanonicalJson.Canonicalize(
            """{"value":"\uD83D\uDE80"}"""u8);

        Assert.Equal("""{"value":"🚀"}""", Encoding.UTF8.GetString(canonical));
    }

    private static string ReadExpectedHex()
    {
        var value = FixtureLoader.ReadUtf8("canonical/envelope-unordered.expected.hex");

        Assert.EndsWith("\n", value, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", value, StringComparison.Ordinal);

        return value[..^1];
    }
}
