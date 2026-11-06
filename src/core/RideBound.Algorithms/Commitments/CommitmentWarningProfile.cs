using System.Collections.Frozen;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;

namespace RideBound.Algorithms.Commitments;

public sealed record CommitmentWarningLimit
{
    public CommitmentWarningLimit(
        CommitmentDimension dimension,
        long? warningLimit)
    {
        if (!Enum.IsDefined(dimension)
            || warningLimit is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(warningLimit));
        }

        Dimension = dimension;
        WarningLimit = warningLimit;
    }

    public CommitmentDimension Dimension { get; }

    public long? WarningLimit { get; }
}

public sealed class CommitmentWarningProfile
{
    private readonly FrozenDictionary<
        CommitmentDimension,
        CommitmentWarningLimit> _limits;

    public CommitmentWarningProfile(
        string policyId,
        IEnumerable<CommitmentWarningLimit> limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (string.IsNullOrEmpty(policyId))
        {
            throw new ArgumentException(
                "Warning profile policy ID cannot be empty.",
                nameof(policyId));
        }

        PolicyId = policyId;
        var materialized = limits.ToArray();

        if (materialized.Length != CommitmentDimensionVocabulary.Ordered.Count
            || materialized.Select(value => value.Dimension).Distinct().Count()
                != CommitmentDimensionVocabulary.Ordered.Count)
        {
            throw new ArgumentException(
                "A warning profile must explicitly define every dimension once.",
                nameof(limits));
        }

        _limits = materialized.ToFrozenDictionary(value => value.Dimension);
    }

    public string PolicyId { get; }

    public IReadOnlyDictionary<
        CommitmentDimension,
        CommitmentWarningLimit> Limits => _limits;
}

public interface ICommitmentWarningProfileProvider
{
    bool TryGetProfile(string policyId, out CommitmentWarningProfile profile);
}

public sealed class CommitmentWarningProfileCatalog
    : ICommitmentWarningProfileProvider
{
    private readonly FrozenDictionary<string, CommitmentWarningProfile> _profiles;

    public CommitmentWarningProfileCatalog(
        IEnumerable<CommitmentWarningProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var materialized = profiles.ToArray();

        if (materialized.Select(value => value.PolicyId)
            .Distinct(StringComparer.Ordinal).Count() != materialized.Length)
        {
            throw new ArgumentException(
                "Warning profile identifiers must be unique.",
                nameof(profiles));
        }

        _profiles = materialized.ToFrozenDictionary(
            value => value.PolicyId,
            StringComparer.Ordinal);
    }

    public bool TryGetProfile(
        string policyId,
        out CommitmentWarningProfile profile) =>
        _profiles.TryGetValue(policyId, out profile!);
}
