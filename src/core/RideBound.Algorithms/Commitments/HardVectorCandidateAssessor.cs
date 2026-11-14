using System.Collections.Frozen;
using RideBound.Algorithms.Candidates;
using RideBound.Application.Commitments;
using RideBound.Domain.Commitments;

namespace RideBound.Algorithms.Commitments;

public sealed record HardVectorCandidateAssessment(string CandidateId);

public sealed record HardVectorCandidateAssessmentBatch(
    IReadOnlyList<VehicleCandidateSet> FeasibleCandidateSets,
    IReadOnlyDictionary<string, HardVectorCandidateAssessment> Assessments);

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
/// Performs the C1 hard gate with the commitment validator.
/// Raw candidates are never added or mutated; only validator-invalid candidates
/// are removed.
/// </summary>
public sealed class HardVectorCandidateAssessor
{
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
    public HardVectorCandidateAssessmentResult AssessAndFilter(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> rawCandidateSets,
        bool requireSafetyNoOp = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rawCandidateSets);

        var outputSets = new List<VehicleCandidateSet>(rawCandidateSets.Count);
        var assessments = new List<HardVectorCandidateAssessment>();

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

            // G2 candidates are half of a move; they are assessed as pairs after this loop.
            // Without G2 this is every candidate.
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
                        ScopedVehicleId: set.VehicleId));

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

                retained.Add(candidate);
                assessments.Add(new HardVectorCandidateAssessment(candidate.CandidateId));
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
            var relocated = AssessRelocations(context, rawCandidateSets);

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
                    StringComparer.Ordinal)));
    }

    public const string RelocationDonorOnlyRejected = "RELOCATION_DONOR_ONLY_REJECTED";
    public const string RelocationNoReceiver = "RELOCATION_NO_RECEIVER";
    public const string RelocationMalformed = "RELOCATION_MALFORMED";

    /// <summary>
    /// G2 pair assessment (DESIGN 3.3), per moved rider w in ordinal order. The validator is
    /// scoped to one vehicle, so a candidate is checked in a pair state with a canonical partner,
    /// and the result does not depend on which partner (only the scoped vehicle's riders and
    /// route are checked):
    /// <list type="number">
    /// <item>the donor-only route D0, paired with the first receiver, must pass, otherwise every
    /// candidate of w is dropped (the emulator's admissible-donor rule);</item>
    /// <item>each receiver is checked paired with D0;</item>
    /// <item>with no surviving receiver every donor is dropped; otherwise each donor is checked
    /// paired with the first surviving receiver.</item>
    /// </list>
    /// </summary>
    private RelocationAssessment AssessRelocations(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> rawCandidateSets)
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

            var d0 = donorOnly[0];
            var d0Assessment = AssessPair(context, d0, d0, receivers[0]);

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
                var assessed = AssessPair(context, receiver, d0, receiver);

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
                var assessed = AssessPair(context, donor, donor, survivingReceivers[0]);

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
        InsertionCandidate receiver)
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
                ScopedVehicleId: candidate.VehicleId));

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

        return PairResult.Kept(new HardVectorCandidateAssessment(candidate.CandidateId));
    }

    private sealed record PairResult(
        HardVectorCandidateAssessment? Assessment,
        CandidatePruneWitness? Prune)
    {
        public static PairResult Kept(HardVectorCandidateAssessment assessment) => new(assessment, null);

        public static PairResult Pruned(CandidatePruneWitness prune) => new(null, prune);
    }

    private sealed class RelocationAssessment
    {
        public List<InsertionCandidate> Retained { get; } = [];

        public List<CandidatePruneWitness> Pruned { get; } = [];

        public List<HardVectorCandidateAssessment> Assessments { get; } = [];

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
}
