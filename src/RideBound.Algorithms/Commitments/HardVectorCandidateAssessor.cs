using System.Collections.Frozen;
using RideBound.Algorithms.Candidates;
using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Algorithms.Commitments;

/// <param name="IsForcedReference">
/// True for a candidate kept by forced-reference recovery although a commitment gate rejected
/// it: the safety no-op (ADR-075) or, under no-worse recovery, a changed route of the same
/// vehicle. Under kept-route recovery its utilization is reported as zero; under no-worse
/// recovery only the exempted riders are left unranked. The overrun is counted on the
/// forced-reference objective level and recorded as a typed breach.
/// </param>
public sealed record HardVectorCandidateAssessment(
    string CandidateId,
    long WorstHardUtilizationPartsPerMillion,
    CommitmentVector DecisionInducedRevision,
    bool HasApplicableHardLimit,
    CommitmentVector? WarningExcess = null,
    bool HasApplicableWarning = false,
    bool IsForcedReference = false);

public sealed record HardVectorCandidateAssessmentBatch(
    IReadOnlyList<VehicleCandidateSet> FeasibleCandidateSets,
    IReadOnlyDictionary<string, HardVectorCandidateAssessment> Assessments)
{
    /// <summary>
    /// Vehicles whose safety no-op was kept as a forced reference. Always empty
    /// unless forced-reference recovery was requested.
    /// </summary>
    public IReadOnlySet<VehicleId> ForcedReferenceVehicles { get; init; } =
        FrozenSet<VehicleId>.Empty;
}

public sealed record HardVectorCandidateAssessmentResult
{
    private HardVectorCandidateAssessmentResult(
        HardVectorCandidateAssessmentBatch? batch,
        CommitmentAssessmentWitness? witness)
    {
        Batch = batch;
        Witness = witness;
    }

    public bool IsSuccess => Batch is not null;

    public HardVectorCandidateAssessmentBatch? Batch { get; }

    public CommitmentAssessmentWitness? Witness { get; }

    public static HardVectorCandidateAssessmentResult Success(
        HardVectorCandidateAssessmentBatch batch) => new(batch, null);

    public static HardVectorCandidateAssessmentResult Failure(
        CommitmentAssessmentWitness witness) => new(null, witness);
}

/// <summary>
/// Performs the C1 hard gate and ranking assessment in the same validator pass.
/// Raw candidates are never added or mutated; only validator-invalid candidates
/// are removed. Normalization is exact ceiling PPM and never decides feasibility.
/// </summary>
public sealed class HardVectorCandidateAssessor
{
    public const long PartsPerMillion = 1_000_000;

    private readonly CommitmentDecisionValidator _validator;

    public HardVectorCandidateAssessor(
        CommitmentDecisionValidator? validator = null)
    {
        _validator = validator ?? new CommitmentDecisionValidator();
    }

    /// <param name="requireSafetyNoOp">
    /// Set by a caller whose selection model needs exactly one no-op per vehicle.
    /// Then a vehicle that loses its no-op fails here with the no-op's own
    /// rejection, and an emptied vehicle reports the no-op's rejection. Callers
    /// that do not need the no-op keep the behaviour they had before.
    /// </param>
    /// <param name="forcedReferenceRecovery">
    /// Exploratory recovery (off by default; requires <paramref name="requireSafetyNoOp"/>).
    /// A safety no-op rejected by the commitment validator is validated again with its
    /// vehicle named as forced-reference. If that validation accepts it, which happens
    /// only when every rejection was a deadline or budget gate, the no-op is kept
    /// as a forced option instead of failing the vehicle. Any other rejection keeps
    /// the fail-closed behaviour.
    /// </param>
    /// <param name="forcedNoWorse">
    /// Exploratory no-worse-than-reference recovery (off by default; requires
    /// <paramref name="forcedReferenceRecovery"/>). On a vehicle whose no-op was kept as forced,
    /// every other candidate the validator rejected is validated again under the no-worse rule;
    /// one that passes is kept as a forced option too, so the vehicle can still serve a new rider
    /// when that does not make any overrun of the kept route worse.
    /// </param>
    public HardVectorCandidateAssessmentResult AssessAndFilter(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> rawCandidateSets,
        ICommitmentWarningProfileProvider? warningProfiles = null,
        bool requireSafetyNoOp = false,
        bool forcedReferenceRecovery = false,
        bool forcedNoWorse = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rawCandidateSets);

        if (forcedReferenceRecovery && !requireSafetyNoOp)
        {
            throw new ArgumentException(
                "Forced-reference recovery keeps the safety no-op, so it requires it.",
                nameof(forcedReferenceRecovery));
        }

        if (forcedNoWorse && !forcedReferenceRecovery)
        {
            throw new ArgumentException(
                "No-worse recovery extends forced-reference recovery, so it requires it.",
                nameof(forcedNoWorse));
        }

        var outputSets = new List<VehicleCandidateSet>(rawCandidateSets.Count);
        var assessments = new List<HardVectorCandidateAssessment>();
        var forcedVehicles = new List<VehicleId>();

        foreach (var set in rawCandidateSets.OrderBy(
                     value => value.VehicleId.Value,
                     StringComparer.Ordinal))
        {
            var retained = new List<InsertionCandidate>();
            var pruned = set.PrunedCandidates.ToList();
            var hardPruned = new List<CandidatePruneWitness>();
            var hardValidationWitnesses =
                new Dictionary<string, CommitmentValidationWitness>(
                    StringComparer.Ordinal);

            // G2 candidates are half of a move; they are assessed as pairs after this loop,
            // once the forced vehicles are known. Without G2 this is every candidate.
            foreach (var candidate in set.Candidates
                         .Where(value => value.RelocationRole is null)
                         .OrderBy(
                             value => value.CandidateId,
                             StringComparer.Ordinal))
            {
                var updated = CandidateStateApplicator.Apply(
                    context.ReducedState.Run,
                    candidate);

                if (!updated.IsSuccess)
                {
                    var prune = new CandidatePruneWitness(
                        candidate.CandidateId,
                        set.VehicleId,
                        candidate.NewRequestIds,
                        updated.Failure!.Code,
                        updated.Failure.Message);
                    pruned.Add(prune);
                    hardPruned.Add(prune);
                    continue;
                }

                var validation = _validator.Validate(
                    new CommitmentValidationContext(
                        context.BeforeEventState,
                        context.ReducedState,
                        context.ReducedState with { Run = updated.Value! },
                        context.Policies,
                        context.StopDistances,
                        context.PublicationScope,
                        context.SourceEventSequence,
                        RevisionReasonCode: "C1_HARD_VECTOR",
                        ScopedVehicleId: set.VehicleId,
                        InitialPromiseTrigger: context.InitialPromiseTrigger,
                        CollectAllCommitmentWitnesses:
                            context.CollectAllCommitmentWitnesses));

                if (!validation.IsValid)
                {
                    var witness = validation.Witnesses[0];
                    var prune = new CandidatePruneWitness(
                        candidate.CandidateId,
                        set.VehicleId,
                        candidate.NewRequestIds,
                        witness.Code,
                        witness.Message,
                        CommitmentWitnesses: validation.Witnesses);
                    pruned.Add(prune);
                    hardPruned.Add(prune);
                    hardValidationWitnesses.Add(
                        candidate.CandidateId,
                        witness);
                    continue;
                }

                var revision = AggregateDecisionRevision(validation.Publications);

                if (!revision.IsSuccess)
                {
                    return Failure(
                        revision.Witness!,
                        candidate,
                        set.VehicleId);
                }

                var utilization = CalculateWorstUtilization(
                    validation.ValidatedState!,
                    context.Policies,
                    set.VehicleId,
                    context.InitialPromiseTrigger);

                if (!utilization.IsSuccess)
                {
                    return Failure(
                        utilization.Witness!,
                        candidate,
                        set.VehicleId);
                }

                var warning = CalculateWarningExcess(
                    validation.ValidatedState!,
                    context.Policies,
                    warningProfiles,
                    set.VehicleId,
                    context.InitialPromiseTrigger);

                if (!warning.IsSuccess)
                {
                    return Failure(
                        warning.Witness!,
                        candidate,
                        set.VehicleId);
                }

                retained.Add(candidate);
                assessments.Add(
                    new HardVectorCandidateAssessment(
                        candidate.CandidateId,
                        utilization.PartsPerMillion,
                        revision.Value!,
                        utilization.HasApplicableHardLimit,
                        warning.Value,
                        warning.HasApplicableWarning));
            }

            // The safety no-op is the one candidate whose rejection says why a vehicle
            // lost its fallback. Identify it only when the set is well formed (exactly
            // one no-op); otherwise the selection model reports the malformed set.
            var noOps = requireSafetyNoOp
                ? set.Candidates.Where(value => value.IsNoOp).ToArray()
                : Array.Empty<InsertionCandidate>();
            var noOpPrune = noOps.Length == 1
                ? hardPruned.FirstOrDefault(
                    value => value.CandidateId == noOps[0].CandidateId)
                : null;

            // Only a validator rejection can be forced; a no-op that could not even be
            // applied to the reduced state is a physical failure and stays fatal.
            if (forcedReferenceRecovery
                && noOpPrune is not null
                && hardValidationWitnesses.ContainsKey(noOpPrune.CandidateId))
            {
                var forced = AssessForcedCandidate(
                    context,
                    noOps[0],
                    set.VehicleId,
                    warningProfiles,
                    noWorse: false,
                    rankNonExempt: forcedNoWorse);

                if (forced.Witness is not null)
                {
                    return Failure(forced.Witness, noOps[0], set.VehicleId);
                }

                if (forced.Assessment is not null)
                {
                    retained.Add(noOps[0]);
                    pruned.Remove(noOpPrune);
                    hardPruned.Remove(noOpPrune);
                    assessments.Add(forced.Assessment);
                    forcedVehicles.Add(set.VehicleId);
                    noOpPrune = null;

                    if (forcedNoWorse)
                    {
                        // Only validator rejections can be relaxed; a candidate that could not be
                        // applied, or failed physically, stays pruned. Ordinal order keeps it
                        // deterministic.
                        foreach (var prune in hardPruned
                                     .Where(value => hardValidationWitnesses.ContainsKey(value.CandidateId))
                                     .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                                     .ToArray())
                        {
                            var candidate = set.Candidates.Single(
                                value => value.CandidateId == prune.CandidateId);
                            var relaxed = AssessForcedCandidate(
                                context,
                                candidate,
                                set.VehicleId,
                                warningProfiles,
                                noWorse: true,
                                rankNonExempt: true);

                            if (relaxed.Witness is not null)
                            {
                                return Failure(relaxed.Witness, candidate, set.VehicleId);
                            }

                            if (relaxed.Assessment is null)
                            {
                                continue;
                            }

                            retained.Add(candidate);
                            pruned.Remove(prune);
                            hardPruned.Remove(prune);
                            hardValidationWitnesses.Remove(prune.CandidateId);
                            assessments.Add(relaxed.Assessment);
                        }
                    }

                    retained.Sort(
                        (left, right) => StringComparer.Ordinal.Compare(
                            left.CandidateId,
                            right.CandidateId));
                }
            }

            if (retained.Count == 0)
            {
                // Report the no-op's own rejection when there is one, so the cause of
                // an empty set is attributable; the ordinal first candidate is usually
                // not the no-op, because candidate ids are content hashes.
                var first = noOpPrune ?? hardPruned
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .FirstOrDefault();
                var citation = noOpPrune is not null
                    ? "No-op rejection"
                    : "First rejection";
                var firstValidation = first is not null
                    && hardValidationWitnesses.TryGetValue(
                        first.CandidateId,
                        out var value)
                        ? value
                        : null;
                return HardVectorCandidateAssessmentResult.Failure(
                    new CommitmentAssessmentWitness(
                        CommitmentFailureCodes.VehicleHasNoFeasibleCandidate,
                        first is null
                            ? "C1 reached a vehicle without any generated " +
                              "candidate; even the safety no-op was absent."
                            : "C1 rejected every generated candidate for this " +
                              $"vehicle. {citation}: {first.Message}",
                        first?.CandidateId,
                        set.VehicleId,
                        firstValidation?.RequestId,
                        firstValidation?.Dimension,
                        first?.Code,
                        firstValidation?.Before,
                        firstValidation?.After,
                        set.Candidates.Count,
                        hardPruned.Count));
            }

            if (noOpPrune is not null)
            {
                // A rule removed the safety no-op but kept another candidate. The
                // caller's selection model needs exactly one no-op per vehicle, so
                // this state used to die later as a generic model-mapping failure.
                // Failing here with the no-op's own rejection changes no successful
                // run of such a caller. The cited rejection may come from any stage.
                var noOpValidation = hardValidationWitnesses.TryGetValue(
                    noOpPrune.CandidateId,
                    out var noOpWitness)
                    ? noOpWitness
                    : null;
                return HardVectorCandidateAssessmentResult.Failure(
                    new CommitmentAssessmentWitness(
                        CommitmentFailureCodes.SafetyNoOpRejected,
                        "C1 rejected the safety no-op while other candidates survived. " +
                        $"No-op rejection: {noOpPrune.Message}",
                        noOpPrune.CandidateId,
                        set.VehicleId,
                        noOpValidation?.RequestId,
                        noOpValidation?.Dimension,
                        noOpPrune.Code,
                        noOpValidation?.Before,
                        noOpValidation?.After,
                        set.Candidates.Count,
                        hardPruned.Count));
            }

            outputSets.Add(
                new VehicleCandidateSet(
                    set.VehicleId,
                    retained.AsReadOnly(),
                    pruned
                        .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                        .ToArray(),
                    set.WasTruncated,
                    set.Loss));
        }

        if (rawCandidateSets.Any(set => set.Candidates.Any(value => value.RelocationRole is not null)))
        {
            var relocated = AssessRelocations(
                context,
                rawCandidateSets,
                forcedVehicles.ToHashSet(),
                warningProfiles);

            if (relocated.Witness is not null)
            {
                return HardVectorCandidateAssessmentResult.Failure(relocated.Witness);
            }

            for (var index = 0; index < outputSets.Count; index++)
            {
                var set = outputSets[index];
                var retained = relocated.Retained
                    .Where(value => value.VehicleId == set.VehicleId)
                    .ToArray();
                var pruned = relocated.Pruned
                    .Where(value => value.VehicleId == set.VehicleId)
                    .ToArray();

                if (retained.Length != 0 || pruned.Length != 0)
                {
                    outputSets[index] = set with
                    {
                        Candidates = set.Candidates
                            .Concat(retained)
                            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                            .ToArray(),
                        PrunedCandidates = set.PrunedCandidates
                            .Concat(pruned)
                            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                            .ToArray(),
                    };
                }
            }

            assessments.AddRange(relocated.Assessments);
        }

        return HardVectorCandidateAssessmentResult.Success(
            new HardVectorCandidateAssessmentBatch(
                outputSets.AsReadOnly(),
                assessments.ToFrozenDictionary(
                    value => value.CandidateId,
                    StringComparer.Ordinal))
            {
                ForcedReferenceVehicles = forcedVehicles.ToFrozenSet(),
            });
    }

    public const string RelocationForcedVehicle = "RELOCATION_FORCED_VEHICLE";
    public const string RelocationDonorOnlyRejected = "RELOCATION_DONOR_ONLY_REJECTED";
    public const string RelocationNoReceiver = "RELOCATION_NO_RECEIVER";
    public const string RelocationMalformed = "RELOCATION_MALFORMED";

    /// <summary>
    /// G2 pair assessment (DESIGN 3.3), per moved rider w in ordinal order. The validator is
    /// scoped to one vehicle, so a candidate is checked in a pair state with a canonical partner,
    /// and the result does not depend on which partner (only the scoped vehicle's riders and
    /// route are checked):
    /// <list type="number">
    /// <item>a forced-reference donor drops every candidate of w; a forced receiver drops its own
    /// (the emulator excludes forced vehicles on both sides);</item>
    /// <item>the donor-only route D0, paired with the first receiver, must pass, otherwise every
    /// candidate of w is dropped (the emulator's admissible-donor rule);</item>
    /// <item>each receiver is checked paired with D0;</item>
    /// <item>with no surviving receiver every donor is dropped; otherwise each donor is checked
    /// paired with the first surviving receiver.</item>
    /// </list>
    /// </summary>
    private RelocationAssessment AssessRelocations(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> rawCandidateSets,
        IReadOnlySet<VehicleId> forcedVehicles,
        ICommitmentWarningProfileProvider? warningProfiles)
    {
        var result = new RelocationAssessment();
        var byRider = rawCandidateSets
            .SelectMany(set => set.Candidates)
            .Where(value => value.RelocationRole is not null)
            .GroupBy(value => value.RelocatedRequestId!.Value)
            .OrderBy(group => group.Key.Value, StringComparer.Ordinal);

        foreach (var group in byRider)
        {
            var donors = group
                .Where(value => value.RelocationRole == RelocationRole.Donor)
                .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                .ToArray();
            var receivers = group
                .Where(value => value.RelocationRole == RelocationRole.Receiver)
                .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
                .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
                .ToArray();
            var donorOnly = donors.Where(value => value.NewRequestIds.Count == 0).ToArray();
            var donorVehicle = context.ReducedState.Run.Requests.TryGetValue(group.Key, out var rider)
                ? rider.AssignedVehicleId
                : null;

            if (donorOnly.Length != 1
                || receivers.Length == 0
                || donorVehicle is null
                || donors.Any(value => value.VehicleId != donorVehicle)
                || receivers.Any(value => value.VehicleId == donorVehicle))
            {
                result.PruneAll(group, RelocationMalformed, "G2 candidates of this rider do not form a donor-only route and receivers on other vehicles.");
                continue;
            }

            if (forcedVehicles.Contains(donorVehicle.Value))
            {
                result.PruneAll(group, RelocationForcedVehicle, "A forced-reference vehicle cannot give up a waiting rider.");
                continue;
            }

            foreach (var forcedReceiver in receivers.Where(value => forcedVehicles.Contains(value.VehicleId)))
            {
                result.Prune(forcedReceiver, RelocationForcedVehicle, "A forced-reference vehicle cannot take a waiting rider.");
            }

            receivers = receivers.Where(value => !forcedVehicles.Contains(value.VehicleId)).ToArray();

            if (receivers.Length == 0)
            {
                result.PruneAll(donors, RelocationNoReceiver, "No receiver of this waiting rider is on a vehicle free to take it.");
                continue;
            }

            var d0 = donorOnly[0];
            var d0Assessment = AssessPair(context, d0, d0, receivers[0], warningProfiles);

            if (d0Assessment.Witness is not null)
            {
                return RelocationAssessment.Failure(d0Assessment.Witness);
            }

            if (d0Assessment.Prune is not null)
            {
                result.Pruned.Add(d0Assessment.Prune);
                result.PruneAll(
                    donors.Where(value => value.CandidateId != d0.CandidateId).Concat(receivers),
                    RelocationDonorOnlyRejected,
                    "The donor route without this waiting rider is not admissible, so it cannot move.");
                continue;
            }

            var survivingReceivers = new List<InsertionCandidate>();

            foreach (var receiver in receivers)
            {
                var assessed = AssessPair(context, receiver, d0, receiver, warningProfiles);

                if (assessed.Witness is not null)
                {
                    return RelocationAssessment.Failure(assessed.Witness);
                }

                if (assessed.Prune is not null)
                {
                    result.Pruned.Add(assessed.Prune);
                    continue;
                }

                survivingReceivers.Add(receiver);
                result.Retained.Add(receiver);
                result.Assessments.Add(assessed.Assessment!);
            }

            if (survivingReceivers.Count == 0)
            {
                result.PruneAll(donors, RelocationNoReceiver, "Every receiver of this waiting rider was rejected.");
                continue;
            }

            result.Retained.Add(d0);
            result.Assessments.Add(d0Assessment.Assessment!);

            foreach (var donor in donors.Where(value => value.CandidateId != d0.CandidateId))
            {
                var assessed = AssessPair(context, donor, donor, survivingReceivers[0], warningProfiles);

                if (assessed.Witness is not null)
                {
                    return RelocationAssessment.Failure(assessed.Witness);
                }

                if (assessed.Prune is not null)
                {
                    result.Pruned.Add(assessed.Prune);
                    continue;
                }

                result.Retained.Add(donor);
                result.Assessments.Add(assessed.Assessment!);
            }
        }

        return result;
    }

    /// <summary>
    /// Validates <paramref name="candidate"/> (one side of the pair) in the pair state, scoped to
    /// its own vehicle, exactly as the main loop validates an ordinary candidate.
    /// </summary>
    private PairResult AssessPair(
        CommitmentMechanismContext context,
        InsertionCandidate candidate,
        InsertionCandidate donor,
        InsertionCandidate receiver,
        ICommitmentWarningProfileProvider? warningProfiles)
    {
        var updated = CandidateStateApplicator.ApplyRelocationPair(
            context.ReducedState.Run,
            donor,
            receiver);

        if (!updated.IsSuccess)
        {
            return PairResult.Pruned(
                new CandidatePruneWitness(
                    candidate.CandidateId,
                    candidate.VehicleId,
                    candidate.NewRequestIds,
                    updated.Failure!.Code,
                    updated.Failure.Message));
        }

        var validation = _validator.Validate(
            new CommitmentValidationContext(
                context.BeforeEventState,
                context.ReducedState,
                context.ReducedState with { Run = updated.Value! },
                context.Policies,
                context.StopDistances,
                context.PublicationScope,
                context.SourceEventSequence,
                RevisionReasonCode: "C1_HARD_VECTOR",
                ScopedVehicleId: candidate.VehicleId,
                InitialPromiseTrigger: context.InitialPromiseTrigger,
                CollectAllCommitmentWitnesses: context.CollectAllCommitmentWitnesses));

        if (!validation.IsValid)
        {
            var witness = validation.Witnesses[0];
            return PairResult.Pruned(
                new CandidatePruneWitness(
                    candidate.CandidateId,
                    candidate.VehicleId,
                    candidate.NewRequestIds,
                    witness.Code,
                    witness.Message,
                    CommitmentWitnesses: validation.Witnesses));
        }

        var revision = AggregateDecisionRevision(validation.Publications);

        if (!revision.IsSuccess)
        {
            return PairResult.Failed(revision.Witness!, candidate);
        }

        var utilization = CalculateWorstUtilization(
            validation.ValidatedState!,
            context.Policies,
            candidate.VehicleId,
            context.InitialPromiseTrigger);

        if (!utilization.IsSuccess)
        {
            return PairResult.Failed(utilization.Witness!, candidate);
        }

        var warning = CalculateWarningExcess(
            validation.ValidatedState!,
            context.Policies,
            warningProfiles,
            candidate.VehicleId,
            context.InitialPromiseTrigger);

        if (!warning.IsSuccess)
        {
            return PairResult.Failed(warning.Witness!, candidate);
        }

        return PairResult.Kept(
            new HardVectorCandidateAssessment(
                candidate.CandidateId,
                utilization.PartsPerMillion,
                revision.Value!,
                utilization.HasApplicableHardLimit,
                warning.Value,
                warning.HasApplicableWarning));
    }

    private sealed record PairResult(
        HardVectorCandidateAssessment? Assessment,
        CandidatePruneWitness? Prune,
        CommitmentAssessmentWitness? Witness)
    {
        public static PairResult Kept(HardVectorCandidateAssessment assessment) => new(assessment, null, null);

        public static PairResult Pruned(CandidatePruneWitness prune) => new(null, prune, null);

        public static PairResult Failed(CommitmentAssessmentWitness witness, InsertionCandidate candidate) =>
            new(null, null, witness with { CandidateId = candidate.CandidateId, VehicleId = candidate.VehicleId });
    }

    private sealed class RelocationAssessment
    {
        public List<InsertionCandidate> Retained { get; } = [];

        public List<CandidatePruneWitness> Pruned { get; } = [];

        public List<HardVectorCandidateAssessment> Assessments { get; } = [];

        public CommitmentAssessmentWitness? Witness { get; private init; }

        public static RelocationAssessment Failure(CommitmentAssessmentWitness witness) =>
            new() { Witness = witness };

        public void Prune(InsertionCandidate candidate, string code, string message) =>
            Pruned.Add(
                new CandidatePruneWitness(
                    candidate.CandidateId,
                    candidate.VehicleId,
                    candidate.NewRequestIds,
                    code,
                    message));

        public void PruneAll(IEnumerable<InsertionCandidate> candidates, string code, string message)
        {
            foreach (var candidate in candidates.OrderBy(value => value.CandidateId, StringComparer.Ordinal))
            {
                Prune(candidate, code, message);
            }
        }
    }

    /// <summary>
    /// Validates a rejected candidate again with its vehicle named as forced-reference: the
    /// no-op (kept route, <paramref name="noWorse"/> false) or, under no-worse recovery, a
    /// changed route of the same vehicle. Returns no assessment when the validator still rejects
    /// it, and a witness only for a ranking failure.
    /// </summary>
    /// <param name="rankNonExempt">
    /// True under no-worse recovery: riders the validator did not exempt keep their normal
    /// utilization ranking. False under kept-route recovery, which ranks the vehicle at zero.
    /// </param>
    private ForcedNoOpResult AssessForcedCandidate(
        CommitmentMechanismContext context,
        InsertionCandidate candidate,
        VehicleId vehicleId,
        ICommitmentWarningProfileProvider? warningProfiles,
        bool noWorse,
        bool rankNonExempt)
    {
        var updated = CandidateStateApplicator.Apply(
            context.ReducedState.Run,
            candidate);

        if (!updated.IsSuccess)
        {
            return ForcedNoOpResult.NotForcible;
        }

        var validation = _validator.Validate(
            new CommitmentValidationContext(
                context.BeforeEventState,
                context.ReducedState,
                context.ReducedState with { Run = updated.Value! },
                context.Policies,
                context.StopDistances,
                context.PublicationScope,
                context.SourceEventSequence,
                RevisionReasonCode: "C1_HARD_VECTOR",
                ScopedVehicleId: vehicleId,
                InitialPromiseTrigger: context.InitialPromiseTrigger,
                CollectAllCommitmentWitnesses:
                    context.CollectAllCommitmentWitnesses,
                ForcedReferenceVehicles: new[] { vehicleId }.ToFrozenSet(),
                ForcedNoWorse: noWorse));

        if (!validation.IsValid || validation.ForcedExemptions.Count == 0)
        {
            return ForcedNoOpResult.NotForcible;
        }

        var revision = AggregateDecisionRevision(validation.Publications);

        if (!revision.IsSuccess)
        {
            return ForcedNoOpResult.Failure(revision.Witness!);
        }

        // Kept-route recovery leaves the whole forced vehicle unranked (ADR-075). Under no-worse
        // recovery only an exempted rider's dimensions that are over their limit are unranked;
        // everything else on the vehicle is ranked as on any vehicle, so a forced vehicle does not
        // look emptier than it is on the utilization level.
        var utilization = CalculateWorstUtilization(
            validation.ValidatedState!,
            context.Policies,
            vehicleId,
            context.InitialPromiseTrigger,
            forcedReference: !rankNonExempt,
            unrankedRiders: rankNonExempt
                ? validation.ForcedExemptions.Select(value => value.RequestId).ToFrozenSet()
                : null);

        if (!utilization.IsSuccess)
        {
            return ForcedNoOpResult.Failure(utilization.Witness!);
        }

        var warning = CalculateWarningExcess(
            validation.ValidatedState!,
            context.Policies,
            warningProfiles,
            vehicleId,
            context.InitialPromiseTrigger);

        if (!warning.IsSuccess)
        {
            return ForcedNoOpResult.Failure(warning.Witness!);
        }

        return ForcedNoOpResult.Forced(
            new HardVectorCandidateAssessment(
                candidate.CandidateId,
                utilization.PartsPerMillion,
                revision.Value!,
                utilization.HasApplicableHardLimit,
                warning.Value,
                warning.HasApplicableWarning,
                IsForcedReference: true));
    }

    private static VectorResult AggregateDecisionRevision(
        IReadOnlyList<PromisePublication> publications)
    {
        var aggregate = CommitmentVector.Zero;

        foreach (var publication in publications)
        {
            var added = aggregate.Add(
                publication.Entry.Deltas.DecisionInduced);

            if (!added.IsSuccess)
            {
                return VectorResult.Failure(
                    new CommitmentAssessmentWitness(
                        added.Failure!.Code,
                        added.Failure.Message,
                        Dimension: added.Failure.Dimension));
            }

            aggregate = added.Value!;
        }

        return VectorResult.Success(aggregate);
    }

    private static UtilizationResult CalculateWorstUtilization(
        OnlineState state,
        ICommitmentPolicyProvider policies,
        VehicleId scopedVehicleId,
        InitialPromiseTrigger initialPromiseTrigger,
        bool forcedReference = false,
        IReadOnlySet<RequestId>? unrankedRiders = null)
    {
        long worst = 0;
        var hasLimit = false;

        foreach (var request in state.Run.Requests.Values
                     .Where(
                         value => value.IsAcceptedActive
                             && value.AssignedVehicleId == scopedVehicleId)
                     .OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            if (initialPromiseTrigger == InitialPromiseTrigger.BookingConfirmation
                && request.Lifecycle == RequestLifecycle.Accepted
                && !state.Commitments.Histories.ContainsKey(request.Id))
            {
                continue;
            }

            // An exempted rider of a no-worse candidate may be over a limit; only the dimensions it
            // is over are left unranked, and its applicable hard limits still count as present.
            var unranked = unrankedRiders is not null && unrankedRiders.Contains(request.Id);

            if (!policies.TryGetPolicy(request.CommitmentPolicyId, out var policy)
                || !StringComparer.Ordinal.Equals(
                    request.CommitmentPolicyId,
                    policy.PolicyId)
                || !state.Commitments.Histories.TryGetValue(
                    request.Id,
                    out var history))
            {
                return UtilizationResult.Failure(
                    new CommitmentAssessmentWitness(
                        "COMMITMENT_POLICY_OR_LEDGER_NOT_FOUND",
                        "An active request requires an exact policy and ledger " +
                        "history before hard utilization can be ranked.",
                        RequestId: request.Id,
                        Dimension: "commitmentPolicyId"));
            }

            var phase = ToPhase(request.Lifecycle);

            foreach (var dimension in CommitmentDimensionVocabulary.Ordered)
            {
                var configured = policy.Limits[dimension];

                if ((configured.ApplicablePhases & phase) == 0
                    || configured.HardLimit is not long hardLimit)
                {
                    continue;
                }

                hasLimit = true;

                if (forcedReference)
                {
                    // A kept-route forced vehicle may be charged past its limit. Its utilization
                    // is not ranked, so one forced vehicle cannot flatten the fleet maximum.
                    continue;
                }

                var value = history.Current.BudgetAfter.Get(dimension);

                if (unranked && value > hardLimit)
                {
                    // An exempted rider over this limit cannot be ranked on it; its dimensions
                    // within their limits are ranked as on any vehicle.
                    continue;
                }

                if (hardLimit == 0)
                {
                    if (value != 0)
                    {
                        return UtilizationResult.Failure(
                            new CommitmentAssessmentWitness(
                                CommitmentFailureCodes.BudgetExceeded,
                                "A zero hard limit has non-zero validated usage.",
                                RequestId: request.Id,
                                Dimension: CommitmentDimensionVocabulary
                                    .ToProtocolValue(dimension)));
                    }

                    worst = PartsPerMillion;
                    continue;
                }

                worst = Math.Max(
                    worst,
                    CeilingPartsPerMillion(value, hardLimit));
            }
        }

        return UtilizationResult.Success(worst, hasLimit);
    }

    public static long CeilingPartsPerMillion(long value, long hardLimit)
    {
        if (value < 0
            || hardLimit < 0
            || value > hardLimit
            || value > DomainLimits.MaxCanonicalInteger
            || hardLimit > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (hardLimit == 0)
        {
            return PartsPerMillion;
        }

        var scaled = (UInt128)(ulong)value
            * (UInt128)(ulong)PartsPerMillion;
        var quotient = scaled / (UInt128)(ulong)hardLimit;
        var remainder = scaled % (UInt128)(ulong)hardLimit;
        var ceiling = quotient + (remainder == 0 ? 0u : 1u);
        return checked((long)ceiling);
    }

    private static WarningResult CalculateWarningExcess(
        OnlineState state,
        ICommitmentPolicyProvider policies,
        ICommitmentWarningProfileProvider? warningProfiles,
        VehicleId scopedVehicleId,
        InitialPromiseTrigger initialPromiseTrigger)
    {
        if (warningProfiles is null)
        {
            return WarningResult.Success(CommitmentVector.Zero, false);
        }

        var values = new long[CommitmentDimensionVocabulary.Ordered.Count];
        var hasWarning = false;

        foreach (var request in state.Run.Requests.Values
                     .Where(
                         value => value.IsAcceptedActive
                             && value.AssignedVehicleId == scopedVehicleId)
                     .OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            if (initialPromiseTrigger == InitialPromiseTrigger.BookingConfirmation
                && request.Lifecycle == RequestLifecycle.Accepted
                && !state.Commitments.Histories.ContainsKey(request.Id))
            {
                continue;
            }

            if (!policies.TryGetPolicy(request.CommitmentPolicyId, out var policy)
                || !warningProfiles.TryGetProfile(
                    request.CommitmentPolicyId,
                    out var warningProfile)
                || !StringComparer.Ordinal.Equals(
                    warningProfile.PolicyId,
                    policy.PolicyId)
                || !state.Commitments.Histories.TryGetValue(
                    request.Id,
                    out var history))
            {
                return WarningResult.Failure(
                    new CommitmentAssessmentWitness(
                        "COMMITMENT_WARNING_PROFILE_NOT_FOUND",
                        "C2 requires an exact warning profile, policy and ledger " +
                        "history for every active request.",
                        RequestId: request.Id,
                        Dimension: "commitmentPolicyId"));
            }

            var phase = ToPhase(request.Lifecycle);

            for (var index = 0;
                 index < CommitmentDimensionVocabulary.Ordered.Count;
                 index++)
            {
                var dimension = CommitmentDimensionVocabulary.Ordered[index];
                var warning = warningProfile.Limits[dimension].WarningLimit;

                if (warning is not long warningLimit)
                {
                    continue;
                }

                var hard = policy.Limits[dimension];

                if (hard.HardLimit is not long hardLimit
                    || warningLimit > hardLimit)
                {
                    return WarningResult.Failure(
                        new CommitmentAssessmentWitness(
                            "INVALID_COMMITMENT_WARNING_LIMIT",
                            "An enabled warning requires a finite hard limit and " +
                            "cannot exceed it.",
                            RequestId: request.Id,
                            Dimension: CommitmentDimensionVocabulary
                                .ToProtocolValue(dimension)));
                }

                if ((hard.ApplicablePhases & phase) == 0)
                {
                    continue;
                }

                hasWarning = true;
                var usage = history.Current.BudgetAfter.Get(dimension);
                var excess = usage > warningLimit ? usage - warningLimit : 0;

                if (values[index] > DomainLimits.MaxCanonicalInteger - excess)
                {
                    return WarningResult.Failure(
                        new CommitmentAssessmentWitness(
                            CommitmentFailureCodes.VectorOverflow,
                            "Aggregate warning excess exceeds the canonical range.",
                            RequestId: request.Id,
                            Dimension: CommitmentDimensionVocabulary
                                .ToProtocolValue(dimension)));
                }

                values[index] += excess;
            }
        }

        return WarningResult.Success(
            new CommitmentVector(
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5],
                values[6],
                values[7],
                values[8],
                values[9]),
            hasWarning);
    }

    private static CommitmentPhase ToPhase(RequestLifecycle lifecycle) =>
        lifecycle switch
        {
            RequestLifecycle.Accepted => CommitmentPhase.Accepted,
            RequestLifecycle.WaitingPickup => CommitmentPhase.WaitingPickup,
            RequestLifecycle.Onboard => CommitmentPhase.Onboard,
            _ => CommitmentPhase.None,
        };

    private static HardVectorCandidateAssessmentResult Failure(
        CommitmentAssessmentWitness witness,
        InsertionCandidate candidate,
        VehicleId vehicleId) =>
        HardVectorCandidateAssessmentResult.Failure(
            witness with
            {
                CandidateId = candidate.CandidateId,
                VehicleId = vehicleId,
            });

    private sealed record ForcedNoOpResult(
        HardVectorCandidateAssessment? Assessment,
        CommitmentAssessmentWitness? Witness)
    {
        public static ForcedNoOpResult NotForcible { get; } = new(null, null);

        public static ForcedNoOpResult Forced(
            HardVectorCandidateAssessment assessment) => new(assessment, null);

        public static ForcedNoOpResult Failure(
            CommitmentAssessmentWitness witness) => new(null, witness);
    }

    private sealed record VectorResult(
        CommitmentVector? Value,
        CommitmentAssessmentWitness? Witness)
    {
        public bool IsSuccess => Value is not null;

        public static VectorResult Success(CommitmentVector value) =>
            new(value, null);

        public static VectorResult Failure(CommitmentAssessmentWitness witness) =>
            new(null, witness);
    }

    private sealed record UtilizationResult(
        long PartsPerMillion,
        bool HasApplicableHardLimit,
        CommitmentAssessmentWitness? Witness)
    {
        public bool IsSuccess => Witness is null;

        public static UtilizationResult Success(
            long partsPerMillion,
            bool hasApplicableHardLimit) =>
            new(partsPerMillion, hasApplicableHardLimit, null);

        public static UtilizationResult Failure(
            CommitmentAssessmentWitness witness) =>
            new(0, false, witness);
    }

    private sealed record WarningResult(
        CommitmentVector? Value,
        bool HasApplicableWarning,
        CommitmentAssessmentWitness? Witness)
    {
        public bool IsSuccess => Value is not null;

        public static WarningResult Success(
            CommitmentVector value,
            bool hasApplicableWarning) =>
            new(value, hasApplicableWarning, null);

        public static WarningResult Failure(
            CommitmentAssessmentWitness witness) =>
            new(null, false, witness);
    }
}
