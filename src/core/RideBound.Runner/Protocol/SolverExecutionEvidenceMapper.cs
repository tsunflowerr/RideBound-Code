using System.Buffers;
using System.Text.Json;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.Optimization;
using RideBound.Domain.Routes;

namespace RideBound.Runner.Protocol;

internal static class SolverExecutionEvidenceMapper
{
    private const string CandidatePortfolioSchemaId =
        "https://ridebound.local/schemas/wp13/v1/" +
        "runner-retained-candidate-portfolio-evidence.schema.json";

    public static JsonElement? Map(RollingCostDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.GenerationDiagnostics is null
            || decision.SelectionExecution is null)
        {
            return null;
        }

        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            // 1.3.0 = G2 on (relocationLosses and the candidates' relocation fields may appear);
            // without G2 the version and every byte are as before.
            writer.WriteString(
                "evidenceVersion",
                decision.GenerationDiagnostics.RelocationLosses is not null
                    ? "1.3.0"
                    : decision.CandidatePortfolioEvidence is null
                        ? "1.1.0"
                        : "1.2.0");
            writer.WritePropertyName("generation");
            WriteGeneration(writer, decision.GenerationDiagnostics);

            if (decision.CandidatePortfolioEvidence is { } portfolio)
            {
                writer.WritePropertyName("candidatePortfolio");
                WriteCandidatePortfolio(writer, portfolio);
            }

            writer.WritePropertyName("prunedCandidates");
            writer.WriteStartArray();

            foreach (var witness in decision.PrunedCandidates
                         .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
                         .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
                         .ThenBy(value => value.Code, StringComparer.Ordinal))
            {
                WritePruneWitness(writer, witness);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("selection");
            WriteSelection(writer, decision.SelectionExecution);
            writer.WriteEndObject();
            writer.Flush();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void WriteCandidatePortfolio(
        Utf8JsonWriter writer,
        CandidatePortfolioEvidenceSnapshot snapshot)
    {
        var generated = snapshot.GeneratedPhysicalCandidateSets
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .SelectMany(
                value => value.Candidates.OrderBy(
                    candidate => candidate.CandidateId,
                    StringComparer.Ordinal))
            .ToArray();
        var eligibleIds = snapshot.PolicyEligibleCandidateSets
            .SelectMany(value => value.Candidates)
            .Select(value => value.CandidateId)
            .ToHashSet(StringComparer.Ordinal);
        var options = snapshot.SelectionProblem.Options.ToDictionary(
            value => value.OptionId,
            StringComparer.Ordinal);

        writer.WriteStartObject();
        writer.WriteString("portfolioVersion", "1.0.0");
        writer.WriteString("schemaId", CandidatePortfolioSchemaId);
        writer.WriteString(
            "objectiveProfile",
            ObjectiveProfile(snapshot.ObjectiveProfile));
        writer.WriteNumber("generatedCandidateCount", generated.Length);
        writer.WriteNumber("policyEligibleCandidateCount", eligibleIds.Count);
        writer.WritePropertyName("selectedCandidateIds");
        writer.WriteStartArray();

        foreach (var candidateId in snapshot.SelectedCandidateIds)
        {
            writer.WriteStringValue(candidateId);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("selectionProblem");
        WriteSelectionProblem(writer, snapshot.SelectionProblem);
        writer.WritePropertyName("candidates");
        writer.WriteStartArray();

        foreach (var candidate in generated)
        {
            WriteCandidate(
                writer,
                candidate,
                eligibleIds.Contains(candidate.CandidateId)
                    ? options[candidate.CandidateId]
                    : null);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSelectionProblem(
        Utf8JsonWriter writer,
        CandidateSelectionProblem problem)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("vehicleIds");
        writer.WriteStartArray();

        foreach (var vehicleId in problem.VehicleIds)
        {
            writer.WriteStringValue(vehicleId.Value);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("requestIds");
        writer.WriteStartArray();

        foreach (var requestId in problem.RequestIds)
        {
            writer.WriteStringValue(requestId.Value);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("objectiveLevels");
        writer.WriteStartArray();

        for (var index = 0; index < problem.ObjectiveLevels.Count; index++)
        {
            var objective = problem.ObjectiveLevels[index];
            writer.WriteStartObject();
            writer.WriteNumber("levelIndex", index);
            writer.WriteString("name", objective.Name);
            writer.WriteString("sense", ObjectiveSense(objective.Sense));
            writer.WriteString(
                "aggregation",
                ObjectiveAggregation(objective.Aggregation));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteCandidate(
        Utf8JsonWriter writer,
        InsertionCandidate candidate,
        CandidateSelectionOption? option)
    {
        writer.WriteStartObject();
        writer.WriteString("candidateId", candidate.CandidateId);
        writer.WriteString("vehicleId", candidate.VehicleId.Value);
        writer.WritePropertyName("newRequestIds");
        writer.WriteStartArray();

        foreach (var requestId in candidate.NewRequestIds.OrderBy(
                     value => value.Value,
                     StringComparer.Ordinal))
        {
            writer.WriteStringValue(requestId.Value);
        }

        writer.WriteEndArray();
        writer.WriteBoolean("isNoOp", candidate.IsNoOp);
        writer.WriteString(
            "scheduleStrategy",
            ScheduleStrategy(candidate.ScheduleStrategy));
        writer.WriteNumber(
            "relocatedWaitMs",
            candidate.RelocatedWaitMilliseconds);

        if (candidate.CertifiedForwardSlackMilliseconds is { } slack)
        {
            writer.WriteNumber("certifiedForwardSlackMs", slack);
        }

        if (candidate.RepairedIncumbentRequestId is { } repaired)
        {
            writer.WriteString("repairedIncumbentRequestId", repaired.Value);
        }

        // G2 fields are written only for relocation candidates, so evidence without G2 is unchanged.
        if (candidate.RelocatedRequestId is { } relocated)
        {
            writer.WriteString("relocatedRequestId", relocated.Value);
            writer.WriteString(
                "relocationRole",
                candidate.RelocationRole == RelocationRole.Donor ? "donor" : "receiver");
        }

        writer.WritePropertyName("route");
        WriteRoute(writer, candidate.Route);
        writer.WritePropertyName("schedule");
        WriteSchedule(writer, candidate.Schedule);
        writer.WriteString(
            "policyEligibility",
            option is null ? "pruned" : "eligible");

        if (option is not null)
        {
            writer.WritePropertyName("objectiveContributions");
            writer.WriteStartArray();

            foreach (var contribution in option.ObjectiveContributions)
            {
                writer.WriteNumberValue(contribution);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteRoute(Utf8JsonWriter writer, RoutePlan route)
    {
        writer.WriteStartObject();
        writer.WriteNumber("planVersion", route.Version.Value);
        writer.WriteNumber("executedStopCount", route.ExecutedStopCount);
        writer.WritePropertyName("frozenPrefix");
        WriteRouteStops(writer, route.FrozenPrefix);
        writer.WritePropertyName("mutableSuffix");
        WriteRouteStops(writer, route.MutableSuffix);
        writer.WriteEndObject();
    }

    private static void WriteRouteStops(
        Utf8JsonWriter writer,
        IReadOnlyList<RouteStop> stops)
    {
        writer.WriteStartArray();

        foreach (var stop in stops)
        {
            writer.WriteStartObject();
            writer.WriteString("stopId", stop.StopId.Value);
            writer.WriteString("nodeId", stop.NodeId.Value);
            writer.WriteString("kind", RouteStopKindValue(stop.Kind));

            if (stop.RequestId is { } requestId)
            {
                writer.WriteString("requestId", requestId.Value);
            }

            writer.WriteNumber(
                "serviceDurationMs",
                stop.ServiceDuration.Milliseconds);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteSchedule(
        Utf8JsonWriter writer,
        CandidateSchedule schedule)
    {
        writer.WriteStartObject();
        writer.WriteNumber("operationalCost", schedule.OperationalCost);
        writer.WritePropertyName("stops");
        writer.WriteStartArray();

        foreach (var stop in schedule.Stops)
        {
            writer.WriteStartObject();
            writer.WriteString("stopId", stop.StopId.Value);
            writer.WriteNumber("arrivalTimeMs", stop.ArrivalTime.Milliseconds);
            writer.WriteNumber(
                "serviceStartTimeMs",
                stop.ServiceStartTime.Milliseconds);
            writer.WriteNumber("departureTimeMs", stop.DepartureTime.Milliseconds);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static string ObjectiveProfile(SolverBackedObjectiveProfile profile) =>
        profile switch
        {
            SolverBackedObjectiveProfile.RollingCost => "rollingCost",
            SolverBackedObjectiveProfile.RevisionPenalty => "revisionPenalty",
            SolverBackedObjectiveProfile.HardVector => "hardVector",
            SolverBackedObjectiveProfile.SoftHardHybrid => "softHardHybrid",
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };

    private static string ObjectiveSense(CandidateSelectionObjectiveSense sense) =>
        sense switch
        {
            CandidateSelectionObjectiveSense.Minimize => "minimize",
            CandidateSelectionObjectiveSense.Maximize => "maximize",
            _ => throw new ArgumentOutOfRangeException(nameof(sense)),
        };

    private static string ObjectiveAggregation(
        CandidateSelectionObjectiveAggregation aggregation) =>
        aggregation switch
        {
            CandidateSelectionObjectiveAggregation.Sum => "sum",
            CandidateSelectionObjectiveAggregation.Maximum => "maximum",
            _ => throw new ArgumentOutOfRangeException(nameof(aggregation)),
        };

    private static string ScheduleStrategy(CandidateScheduleStrategy strategy) =>
        strategy switch
        {
            CandidateScheduleStrategy.EarliestFeasible => "earliestFeasible",
            CandidateScheduleStrategy.OriginHoldRelocatedWait =>
                "originHoldRelocatedWait",
            _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
        };

    private static string RouteStopKindValue(RouteStopKind kind) =>
        kind switch
        {
            RouteStopKind.Waypoint => "waypoint",
            RouteStopKind.Pickup => "pickup",
            RouteStopKind.DropOff => "dropOff",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static void WriteGeneration(
        Utf8JsonWriter writer,
        CandidateGenerationDiagnostics diagnostics)
    {
        writer.WriteStartObject();
        writer.WriteNumber(
            "totalPendingRequestCount",
            diagnostics.TotalPendingRequestCount);
        writer.WriteNumber("consideredRequestCount", diagnostics.ConsideredRequestCount);
        writer.WriteNumber("omittedRequestCount", diagnostics.OmittedRequestCount);
        writer.WritePropertyName("vehicleLosses");
        writer.WriteStartArray();

        foreach (var loss in diagnostics.VehicleLosses
                     .OrderBy(
                         value => value.VehicleId?.Value ?? string.Empty,
                         StringComparer.Ordinal))
        {
            writer.WriteStartObject();

            if (loss.VehicleId is not null)
            {
                writer.WriteString("vehicleId", loss.VehicleId.Value.Value);
            }

            writer.WriteNumber("explorationWorkUnits", loss.ExplorationWorkUnits);
            writer.WriteNumber(
                "evaluatedCandidatePathCount",
                loss.EvaluatedCandidatePathCount);
            writer.WriteNumber(
                "uniqueFeasibleCandidateCountBeforeCap",
                loss.UniqueFeasibleCandidateCountBeforeCap);
            writer.WriteNumber("retainedCandidateCount", loss.RetainedCandidateCount);
            writer.WriteNumber(
                "physicallyOrSchedulePrunedCount",
                loss.PhysicallyOrSchedulePrunedCount);
            writer.WriteNumber(
                "omittedUnexpandedCandidatePathCount",
                loss.OmittedUnexpandedCandidatePathCount);
            writer.WriteNumber(
                "omittedFeasibleCandidateCountByCap",
                loss.OmittedFeasibleCandidateCountByCap);
            writer.WriteBoolean("workBudgetExhausted", loss.WorkBudgetExhausted);
            writer.WriteBoolean("candidateCapApplied", loss.CandidateCapApplied);
            writer.WriteBoolean(
                "omissionCountWasSaturated",
                loss.OmissionCountWasSaturated);
            writer.WriteNumber(
                "eligibleRepairRequestCount",
                loss.EligibleRepairRequestCount);
            writer.WriteNumber(
                "consideredRepairRequestCount",
                loss.ConsideredRepairRequestCount);
            writer.WriteNumber(
                "omittedRepairRequestCount",
                loss.OmittedRepairRequestCount);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("omissions");
        writer.WriteStartArray();

        foreach (var omission in diagnostics.Omissions
                     .OrderBy(value => value.Code, StringComparer.Ordinal)
                     .ThenBy(value => value.StableDigest, StringComparer.Ordinal)
                     .ThenBy(
                         value => value.VehicleId?.Value ?? string.Empty,
                         StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("code", omission.Code);
            writer.WriteNumber("count", omission.Count);
            writer.WriteString("stableDigest", omission.StableDigest);
            writer.WriteBoolean("countWasSaturated", omission.CountWasSaturated);

            if (omission.VehicleId is not null)
            {
                writer.WriteString("vehicleId", omission.VehicleId.Value.Value);
            }

            if (omission.RequestIds is not null)
            {
                writer.WritePropertyName("requestIds");
                writer.WriteStartArray();

                foreach (var requestId in omission.RequestIds
                             .OrderBy(value => value.Value, StringComparer.Ordinal))
                {
                    writer.WriteStringValue(requestId.Value);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("exogenousServiceQualityBreaches");
        writer.WriteStartArray();

        foreach (var breach in diagnostics.ExogenousServiceQualityBreaches
                     .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
                     .ThenBy(value => value.RequestId.Value, StringComparer.Ordinal)
                     .ThenBy(value => value.Code, StringComparer.Ordinal)
                     .ThenBy(value => value.Dimension, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("vehicleId", breach.VehicleId.Value);
            writer.WriteString("requestId", breach.RequestId.Value);
            writer.WriteString("code", breach.Code);
            writer.WriteString("dimension", breach.Dimension);
            writer.WriteNumber(
                "contractualMilliseconds",
                breach.ContractualMilliseconds);
            writer.WriteNumber(
                "exogenousMilliseconds",
                breach.ExogenousMilliseconds);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        // G2 work is part of consumedGenerationWorkUnits, so its accounting is published too.
        if (diagnostics.RelocationLosses is { } relocationLosses)
        {
            writer.WritePropertyName("relocationLosses");
            writer.WriteStartArray();

            foreach (var loss in relocationLosses.OrderBy(
                         value => value.DonorVehicleId.Value,
                         StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("donorVehicleId", loss.DonorVehicleId.Value);
                writer.WriteNumber("eligibleRiderCount", loss.EligibleRiderCount);
                writer.WriteNumber("consideredRiderCount", loss.ConsideredRiderCount);
                writer.WriteNumber("omittedRiderCount", loss.OmittedRiderCount);
                writer.WriteNumber("workUnits", loss.WorkUnits);
                writer.WriteNumber("evaluatedReceiverRouteCount", loss.EvaluatedReceiverRouteCount);
                writer.WriteNumber("feasibleReceiverCount", loss.FeasibleReceiverCount);
                writer.WriteNumber("evaluatedDonorPathCount", loss.EvaluatedDonorPathCount);
                writer.WriteNumber("feasibleDonorCount", loss.FeasibleDonorCount);
                writer.WriteNumber("omittedUnexpandedDonorPathCount", loss.OmittedUnexpandedDonorPathCount);
                writer.WriteBoolean("omissionCountWasSaturated", loss.OmissionCountWasSaturated);
                writer.WriteNumber("ridersWithoutFeasibleReceiver", loss.RidersWithoutFeasibleReceiver);
                writer.WriteNumber("ridersWithInfeasibleDonorRoute", loss.RidersWithInfeasibleDonorRoute);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WritePruneWitness(
        Utf8JsonWriter writer,
        CandidatePruneWitness witness)
    {
        writer.WriteStartObject();
        writer.WriteString("candidateId", witness.CandidateId);
        writer.WriteString("vehicleId", witness.VehicleId.Value);
        writer.WritePropertyName("newRequestIds");
        writer.WriteStartArray();

        foreach (var requestId in witness.NewRequestIds
                     .OrderBy(value => value.Value, StringComparer.Ordinal))
        {
            writer.WriteStringValue(requestId.Value);
        }

        writer.WriteEndArray();
        writer.WriteString("code", witness.Code);

        if (witness.PhysicalWitness is not null)
        {
            var physical = witness.PhysicalWitness;
            writer.WritePropertyName("physicalWitness");
            writer.WriteStartObject();
            writer.WriteString("code", physical.Code);
            writer.WriteString("vehicleId", physical.VehicleId.Value);
            WriteOptional(writer, "requestId", physical.RequestId?.Value);
            WriteOptional(writer, "stopId", physical.StopId?.Value);
            WriteOptional(writer, "dimension", physical.Dimension);
            WriteOptional(writer, "expected", physical.Expected);
            WriteOptional(writer, "actual", physical.Actual);
            writer.WriteEndObject();
        }

        writer.WritePropertyName("commitmentWitnesses");
        writer.WriteStartArray();

        foreach (var commitment in witness.CommitmentWitnesses
                     ?? Array.Empty<CommitmentValidationWitness>())
        {
            writer.WriteStartObject();
            writer.WriteString("stage", Stage(commitment.Stage));
            writer.WriteString("code", commitment.Code);
            WriteOptional(writer, "vehicleId", commitment.VehicleId?.Value);
            WriteOptional(writer, "requestId", commitment.RequestId?.Value);
            WriteOptional(writer, "dimension", commitment.Dimension);
            WriteOptional(writer, "rule", commitment.Rule);
            WriteOptional(writer, "limit", commitment.Limit);
            WriteOptional(writer, "before", commitment.Before);
            WriteOptional(writer, "delta", commitment.Delta);
            WriteOptional(writer, "after", commitment.After);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSelection(
        Utf8JsonWriter writer,
        CandidateSelectionExecutionResult execution)
    {
        var diagnostics = execution.Diagnostics;
        writer.WriteStartObject();
        writer.WriteNumber(
            "consumedGenerationWorkUnits",
            diagnostics.ConsumedGenerationWorkUnits);
        writer.WriteNumber(
            "consumedValidationWorkUnits",
            diagnostics.ConsumedValidationWorkUnits);
        writer.WriteNumber("omittedCandidateCount", diagnostics.OmittedCandidateCount);
        WriteOptional(writer, "omissionDigest", diagnostics.OmissionDigest);
        writer.WriteBoolean(
            "omissionCountWasSaturated",
            diagnostics.OmissionCountWasSaturated);
        writer.WriteString("primarySolveStatus", Status(diagnostics.PrimarySolveStatus));
        writer.WritePropertyName("primarySolverDiagnostics");
        WriteSolverDiagnostics(writer, diagnostics.PrimarySolverDiagnostics);
        writer.WriteString("finalSolveStatus", Status(execution.SolveResult.Status));
        writer.WritePropertyName("finalSolverDiagnostics");
        WriteSolverDiagnostics(writer, execution.SolveResult.Diagnostics);
        writer.WriteString("executionPath", Path(diagnostics.ExecutionPath));
        writer.WriteNumber(
            "fallbackValidationAttempts",
            diagnostics.FallbackValidationAttempts);
        writer.WriteBoolean(
            "primaryIncumbentRejected",
            diagnostics.PrimaryIncumbentRejected);
        writer.WritePropertyName("validationWitnesses");
        writer.WriteStartArray();

        foreach (var witness in diagnostics.ValidationWitnesses)
        {
            writer.WriteStartObject();
            writer.WriteString("attemptedPath", Path(witness.AttemptedPath));
            writer.WritePropertyName("selectedOptionIds");
            writer.WriteStartArray();

            foreach (var optionId in witness.SelectedOptionIds.Order(StringComparer.Ordinal))
            {
                writer.WriteStringValue(optionId);
            }

            writer.WriteEndArray();
            writer.WriteString("reasonCode", witness.ReasonCode);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSolverDiagnostics(
        Utf8JsonWriter writer,
        CandidateSelectionSolverDiagnostics diagnostics)
    {
        writer.WriteStartObject();
        writer.WriteNumber("consumedWorkUnits", diagnostics.ConsumedWorkUnits);
        writer.WriteNumber(
            "consumedDeterministicTimeMicros",
            diagnostics.ConsumedDeterministicTimeMicros);
        writer.WritePropertyName("objectiveBounds");
        writer.WriteStartArray();

        foreach (var bound in diagnostics.ObjectiveBounds)
        {
            writer.WriteStartObject();
            writer.WriteNumber("levelIndex", bound.LevelIndex);
            writer.WriteString("objectiveName", bound.ObjectiveName);
            writer.WriteNumber("incumbentValue", bound.IncumbentValue);
            writer.WriteNumber("bestBound", bound.BestBound);
            writer.WriteNumber("gapNumerator", bound.GapNumerator);
            writer.WriteNumber("gapDenominator", bound.GapDenominator);
            writer.WriteBoolean("isProvenOptimal", bound.IsProvenOptimal);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteOptional(writer, "detailCode", diagnostics.DetailCode);
        writer.WriteEndObject();
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, long? value)
    {
        if (value is not null)
        {
            writer.WriteNumber(name, value.Value);
        }
    }

    private static string Status(CandidateSelectionSolveStatus status) =>
        status switch
        {
            CandidateSelectionSolveStatus.Optimal => "optimal",
            CandidateSelectionSolveStatus.Feasible => "feasible",
            CandidateSelectionSolveStatus.Infeasible => "infeasible",
            CandidateSelectionSolveStatus.Unknown => "unknown",
            CandidateSelectionSolveStatus.ModelInvalid => "modelInvalid",
            CandidateSelectionSolveStatus.SafeFallback => "safeFallback",
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    private static string Path(CandidateSelectionExecutionPath path) =>
        path switch
        {
            CandidateSelectionExecutionPath.None => "none",
            CandidateSelectionExecutionPath.ValidatedIncumbent =>
                "validatedIncumbent",
            CandidateSelectionExecutionPath.SafeNoOp => "safeNoOp",
            CandidateSelectionExecutionPath.GreedySingleRequest =>
                "greedySingleRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(path)),
        };

    private static string Stage(CommitmentValidationStage stage) =>
        stage switch
        {
            CommitmentValidationStage.State => "state",
            CommitmentValidationStage.Physical => "physical",
            CommitmentValidationStage.Projection => "projection",
            CommitmentValidationStage.Lock => "lock",
            CommitmentValidationStage.Budget => "budget",
            CommitmentValidationStage.Ledger => "ledger",
            _ => throw new ArgumentOutOfRangeException(nameof(stage)),
        };
}
