namespace RideBound.Contracts.Protocol;

public sealed record Sha256Hex
{
    private Sha256Hex(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? value, out Sha256Hex? hash)
    {
        if (!IsLowerHex(value, expectedLength: 64))
        {
            hash = null;
            return false;
        }

        hash = new Sha256Hex(value!);
        return true;
    }

    public override string ToString() => Value;

    internal static bool IsLowerHex(string? value, int expectedLength)
    {
        if (value is null || value.Length != expectedLength)
        {
            return false;
        }

        return value.All(
            character => character is >= '0' and <= '9'
                or >= 'a' and <= 'f');
    }
}

public sealed record SourceCommitSha
{
    private SourceCommitSha(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? value, out SourceCommitSha? commit)
    {
        if (!Sha256Hex.IsLowerHex(value, expectedLength: 40)
            && !Sha256Hex.IsLowerHex(value, expectedLength: 64))
        {
            commit = null;
            return false;
        }

        commit = new SourceCommitSha(value!);
        return true;
    }

    public override string ToString() => Value;
}
