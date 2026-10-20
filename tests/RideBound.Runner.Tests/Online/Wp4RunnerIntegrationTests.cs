using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Algorithms.Policies;
using RideBound.Application.Optimization;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Online;

public sealed class Wp4RunnerIntegrationTests
{
    [Fact]
    public async Task Cli_explicit_line_budget_accepts_a_bounded_frame_above_legacy_default()
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(
            Path.Combine(AppContext.BaseDirectory, "RideBound.Runner.dll"));
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("online");
        startInfo.ArgumentList.Add("--maximum-line-bytes");
        startInfo.ArgumentList.Add((2 * 1024 * 1024).ToString(
            System.Globalization.CultureInfo.InvariantCulture));

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var oversizedForLegacy = "{\"padding\":\""
            + new string('x', 1_100_000)
            + "\"}\n";
        await process.StandardInput.WriteAsync(oversizedForLegacy);
        process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("SCHEMA_VALIDATION_FAILED", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("MESSAGE_TOO_LARGE", stderr, StringComparison.Ordinal);
        using var response = JsonDocument.Parse(stdout.Trim());
        Assert.Equal(
            "error",
            response.RootElement.GetProperty("messageType").GetString());
        Assert.Equal(
            "SCHEMA_VALIDATION_FAILED",
            response.RootElement.GetProperty("payload")
                .GetProperty("code")
                .GetString());
    }

    [Fact]
    public async Task Wp4_cli_runs_real_child_process_with_bound_configuration()
    {
        var commitment = CommitmentConfiguration();
        var wp4 = PublishedWp4Configuration(commitment);
        var lines = File.ReadAllLines(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "scenarios",
            "wp3-commitment-tiny",
            "commitment-demo.input.ndjson"));
        var initialize = JsonNode.Parse(lines[1])!;
        var manifest = initialize["payload"]!["manifest"]!;
        manifest["policyId"] = wp4.PolicyId;
        manifest["policyVersion"] = wp4.PolicyVersion;
        manifest["policyConfigurationHash"] =
            wp4.BindToCommitmentConfiguration(commitment.ContentHash).Value;
        lines[1] = initialize.ToJsonString();
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(
            Path.Combine(AppContext.BaseDirectory, "RideBound.Runner.dll"));
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("commitment");
        startInfo.ArgumentList.Add("--policy-config");
        startInfo.ArgumentList.Add(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp3-boundary-test-v1.json"));
        startInfo.ArgumentList.Add("--wp4-config");
        startInfo.ArgumentList.Add(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp4-rolling-cost-boundary-v1.json"));
        startInfo.ArgumentList.Add("--solver-seed-source");
        startInfo.ArgumentList.Add("manifest-master-seed");

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        await process.StandardInput.WriteAsync(
            string.Join('\n', lines.Take(3)) + "\n");
        process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, stderr);
        var responses = stdout.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, responses.Length);
        using var decision = JsonDocument.Parse(responses[2]);
        Assert.Equal(
            "completed",
            decision.RootElement.GetProperty("payload")
                .GetProperty("solver")
                .GetProperty("status")
                .GetString());
    }

    [Fact]
    public void OrTools_policy_is_manifest_bound_hashed_retried_and_ack_committed()
    {
        var setup = CreateSession(PublishedWp4Configuration());
        var batch = ReadFixture("wp2/valid-bootstrap-event-batch.json");

        var firstResponse = setup.Session.Process(batch).Response!;
        var retryResponse = setup.Session.Process(batch).Response!;
        var first = DecisionPayloadCodec.Decode(firstResponse.Payload);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(SolverStatus.Completed, first.Value!.Solver.Status);
        Assert.Null(first.Value.Solver.ExecutionEvidence);
        Assert.Equal(CertificateStatus.Produced, first.Value.Certificate.Status);
        Assert.Contains(
            first.Value.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "requestAccepted");
        Assert.Equal(
            CanonicalJson.Serialize(firstResponse),
            CanonicalJson.Serialize(retryResponse));
        Assert.Equal(0, setup.Session.AppliedEpoch);

        var wrong = setup.Session.Process(
            DecisionApplied(2, 1_000, first.Value.DecisionHash.Value));
        Assert.Equal("error", wrong.Response?.MessageType.Value);
        Assert.Equal(0, setup.Session.AppliedEpoch);

        var applied = setup.Session.Process(
            DecisionApplied(1, 1_000, first.Value.DecisionHash.Value));
        Assert.Null(applied.Response);
        Assert.Equal(1, setup.Session.AppliedEpoch);
        Assert.Single(setup.Session.CommittedOnlineState!.Commitments.Histories);
    }

    [Fact]
    public void Audited_solver_evidence_is_hash_bound_complete_and_excludes_wall_time()
    {
        var commitment = CommitmentConfiguration();
        var wp4 = PublishedWp4Configuration(
            commitment,
            emitSolverExecutionEvidence: true);
        var setup = CreateSession(wp4, commitmentConfiguration: commitment);
        var response = setup.Session.Process(
            ReadFixture("wp2/valid-bootstrap-event-batch.json")).Response!;
        var decision = DecisionPayloadCodec.Decode(response.Payload);

        Assert.True(decision.IsSuccess, decision.Error?.Message);
        var evidence = decision.Value!.Solver.ExecutionEvidence;
        Assert.NotNull(evidence);
        Assert.Equal(
            "1.1.0",
            evidence.Value.GetProperty("evidenceVersion").GetString());
        var generation = evidence.Value.GetProperty("generation");
        Assert.True(generation.GetProperty("totalPendingRequestCount").GetInt64() > 0);
        Assert.Equal(
            JsonValueKind.Array,
            generation.GetProperty("exogenousServiceQualityBreaches").ValueKind);
        Assert.All(
            generation.GetProperty("vehicleLosses").EnumerateArray(),
            loss => Assert.False(string.IsNullOrEmpty(
                loss.GetProperty("vehicleId").GetString())));
        var selection = evidence.Value.GetProperty("selection");
        Assert.Equal("optimal", selection.GetProperty("primarySolveStatus").GetString());
        Assert.Equal("optimal", selection.GetProperty("finalSolveStatus").GetString());
        Assert.Equal(
            "validatedIncumbent",
            selection.GetProperty("executionPath").GetString());
        Assert.NotEmpty(
            selection.GetProperty("finalSolverDiagnostics")
                .GetProperty("objectiveBounds")
                .EnumerateArray());
        Assert.DoesNotContain(
            "wallTime",
            evidence.Value.GetRawText(),
            StringComparison.OrdinalIgnoreCase);

        var encoded = DecisionPayloadCodec.Encode(decision.Value);
        using var encodedDocument = JsonDocument.Parse(encoded);
        var roundTrip = DecisionPayloadCodec.Decode(encodedDocument.RootElement);
        Assert.True(roundTrip.IsSuccess, roundTrip.Error?.Message);
        Assert.Equal(
            evidence.Value.GetRawText(),
            roundTrip.Value!.Solver.ExecutionEvidence!.Value.GetRawText());
    }

    [Fact]
    public void Retained_portfolio_profile_is_complete_and_operationally_neutral()
    {
        var commitment = CommitmentConfiguration();
        var legacySetup = CreateSession(
            PublishedWp4Configuration(
                commitment,
                emitSolverExecutionEvidence: true),
            commitmentConfiguration: commitment);
        var portfolioSetup = CreateSession(
            PublishedWp4Configuration(
                commitment,
                emitSolverExecutionEvidence: true,
                retainCandidatePortfolio: true),
            commitmentConfiguration: commitment);
        var batch = ReadFixture("wp2/valid-bootstrap-event-batch.json");
        var legacy = DecisionPayloadCodec.Decode(
            legacySetup.Session.Process(batch).Response!.Payload);
        var retained = DecisionPayloadCodec.Decode(
            portfolioSetup.Session.Process(batch).Response!.Payload);

        Assert.True(legacy.IsSuccess, legacy.Error?.Message);
        Assert.True(retained.IsSuccess, retained.Error?.Message);
        Assert.Equal(legacy.Value!.Status, retained.Value!.Status);
        Assert.Equal(legacy.Value.ReasonCode, retained.Value.ReasonCode);
        Assert.Equal(legacy.Value.StateBeforeHash, retained.Value.StateBeforeHash);
        Assert.Equal(legacy.Value.StateAfterHash, retained.Value.StateAfterHash);
        Assert.Equal(legacy.Value.Solver.Status, retained.Value.Solver.Status);
        Assert.Equal(
            legacy.Value.Actions.Select(value => value.GetRawText()),
            retained.Value.Actions.Select(value => value.GetRawText()));

        var legacyEvidence = legacy.Value.Solver.ExecutionEvidence!.Value;
        var retainedEvidence = retained.Value.Solver.ExecutionEvidence!.Value;
        Assert.Equal(
            "1.1.0",
            legacyEvidence.GetProperty("evidenceVersion").GetString());
        Assert.False(legacyEvidence.TryGetProperty("candidatePortfolio", out _));
        Assert.Equal(
            "1.2.0",
            retainedEvidence.GetProperty("evidenceVersion").GetString());
        var portfolio = retainedEvidence.GetProperty("candidatePortfolio");
        Assert.Equal(
            "1.0.0",
            portfolio.GetProperty("portfolioVersion").GetString());
        Assert.Equal(
            "https://ridebound.local/schemas/wp13/v1/" +
            "runner-retained-candidate-portfolio-evidence.schema.json",
            portfolio.GetProperty("schemaId").GetString());
        Assert.Equal(
            "rollingCost",
            portfolio.GetProperty("objectiveProfile").GetString());

        var problem = portfolio.GetProperty("selectionProblem");
        var objectiveCount = problem.GetProperty("objectiveLevels")
            .GetArrayLength();
        var requestIds = problem.GetProperty("requestIds")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var vehicleIds = problem.GetProperty("vehicleIds")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var candidates = portfolio.GetProperty("candidates")
            .EnumerateArray()
            .ToArray();
        Assert.NotEmpty(candidates);
        Assert.Equal(
            (long)candidates.Length,
            portfolio.GetProperty("generatedCandidateCount").GetInt64());
        Assert.Equal(
            (long)candidates.Count(
                value => value.GetProperty("policyEligibility").GetString()
                    == "eligible"),
            portfolio.GetProperty("policyEligibleCandidateCount").GetInt64());
        Assert.Equal(
            candidates.Length,
            candidates.Select(
                    value => value.GetProperty("candidateId").GetString())
                .Distinct(StringComparer.Ordinal)
                .Count());
        var eligibleIds = candidates
            .Where(
                value => value.GetProperty("policyEligibility").GetString()
                    == "eligible")
            .Select(value => value.GetProperty("candidateId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var selectedIds = portfolio.GetProperty("selectedCandidateIds")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        Assert.Equal(vehicleIds.Count, selectedIds.Length);
        Assert.All(selectedIds, value => Assert.Contains(value, eligibleIds));

        foreach (var candidate in candidates)
        {
            Assert.Contains(
                candidate.GetProperty("vehicleId").GetString()!,
                vehicleIds);
            var eligible = candidate.GetProperty("policyEligibility")
                .GetString() == "eligible";

            if (eligible)
            {
                Assert.All(
                    candidate.GetProperty("newRequestIds").EnumerateArray(),
                    value => Assert.Contains(value.GetString()!, requestIds));
            }

            Assert.Equal(
                eligible,
                candidate.TryGetProperty(
                    "objectiveContributions",
                    out var contributions));

            if (eligible)
            {
                Assert.Equal(objectiveCount, contributions.GetArrayLength());
            }

            var route = candidate.GetProperty("route");
            var remainingStopIds = route.GetProperty("frozenPrefix")
                .EnumerateArray()
                .Skip(route.GetProperty("executedStopCount").GetInt32())
                .Concat(route.GetProperty("mutableSuffix").EnumerateArray())
                .Select(value => value.GetProperty("stopId").GetString());
            var scheduledStopIds = candidate.GetProperty("schedule")
                .GetProperty("stops")
                .EnumerateArray()
                .Select(value => value.GetProperty("stopId").GetString());
            Assert.Equal(remainingStopIds, scheduledStopIds);
        }

        Assert.DoesNotContain(
            "wallTime",
            retainedEvidence.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Booking_confirmation_C1_ranks_each_vehicle_scope_before_fleet_validation()
    {
        var commitment = CommitmentConfiguration();
        var wp4 = Wp4RunnerConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp7-fleetpy-ridebound-hard-vector-v1.json")),
            commitment);
        var setup = CreateSession(
            wp4,
            commitmentConfiguration: commitment);
        var bootstrap = JsonNode.Parse(
            FixtureLoader.ReadUtf8("wp2/valid-bootstrap-event-batch.json"))!;
        var events = bootstrap["payload"]!["events"]!.AsArray();
        var secondVehicle = events[2]!.DeepClone();
        secondVehicle["eventSeq"] = 4;
        secondVehicle["payload"]!["vehicle"]!["vehicleId"] = "v-2";
        secondVehicle["payload"]!["vehicle"]!["route"]!["mutableSuffix"] =
            new JsonArray();
        events.Add(secondVehicle);

        var offerResponse = setup.Session.Process(
            Encoding.UTF8.GetBytes(bootstrap.ToJsonString())).Response!;
        var offer = DecisionPayloadCodec.Decode(offerResponse.Payload);
        Assert.True(offer.IsSuccess, offer.Error?.Message);
        Assert.Contains(
            offer.Value!.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "requestAccepted");
        var accepted = offer.Value.Actions.Single(
            action => action.GetProperty("decisionType").GetString()
                == "requestAccepted");
        var selectedVehicle = accepted.GetProperty("payload")
            .GetProperty("vehicleId")
            .GetString()!;
        var selectedRoute = offer.Value.Actions.Single(
            action => action.GetProperty("decisionType").GetString()
                == "vehiclePlanUpdated"
                && action.GetProperty("payload")
                    .GetProperty("vehicleId")
                    .GetString() == selectedVehicle)
            .GetProperty("payload")
            .GetProperty("route")
            .GetRawText();
        _ = setup.Session.Process(
            DecisionApplied(1, 1_000, offer.Value.DecisionHash.Value));

        var bookingResponse = setup.Session.Process(
            Encoding.UTF8.GetBytes(
                """
                {
                  "schemaVersion":"1.0.0",
                  "messageType":"eventBatch",
                  "runId":"wp2-run-001",
                  "scenarioId":"wp2-two-epoch-small",
                  "epochId":2,
                  "simTimeMs":1000,
                  "payload":{"events":[
                    {
                      "eventSeq":5,
                      "eventType":"bookingConfirmed",
                      "payload":{"requestId":"r-1"}
                    }
                  ]}
                }
                """)).Response!;
        var booking = DecisionPayloadCodec.Decode(bookingResponse.Payload);

        Assert.Equal("decision", bookingResponse.MessageType.Value);
        Assert.True(booking.IsSuccess, booking.Error?.Message);
        Assert.Equal(SolverStatus.Completed, booking.Value!.Solver.Status);
        Assert.Single(
            booking.Value.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "promisePublished");
        _ = setup.Session.Process(
            DecisionApplied(2, 1_000, booking.Value.DecisionHash.Value));

        // Half-edge progress is deliberately quantized one permille below the
        // exact elapsed fraction: the old plan moves from pickup ETA 1100 to
        // 1101. This is exogenous observation drift, not a decision changing a
        // final-confirmation lock, so C1 must retain its no-op candidate.
        var progressBatch = JsonNode.Parse(
                """
                {
                  "schemaVersion":"1.0.0",
                  "messageType":"eventBatch",
                  "runId":"wp2-run-001",
                  "scenarioId":"wp2-two-epoch-small",
                  "epochId":3,
                  "simTimeMs":1050,
                  "payload":{"events":[
                    {
                      "eventSeq":6,
                      "eventType":"vehicleAdvanced",
                      "payload":{"vehicle":{
                        "vehicleId":"placeholder",
                        "capacity":4,
                        "occupiedSeats":0,
                        "position":{
                          "kind":"edgeProgress",
                          "edgeId":"edge-n0-n1",
                          "fromNodeId":"n-0",
                          "toNodeId":"n-1",
                          "progressPermille":499
                        },
                        "onboardRequestIds":[],
                        "acceptedRequestIds":["r-1"],
                        "route":{}
                      }}
                    }
                  ]}
                }
                """)!;
        var observedVehicle = progressBatch["payload"]!["events"]![0]![
            "payload"]!["vehicle"]!;
        observedVehicle["vehicleId"] = selectedVehicle;
        observedVehicle["route"] = JsonNode.Parse(selectedRoute);
        var progressResponse = setup.Session.Process(
            Encoding.UTF8.GetBytes(progressBatch.ToJsonString())).Response!;
        var progress = DecisionPayloadCodec.Decode(progressResponse.Payload);

        Assert.Equal("decision", progressResponse.MessageType.Value);
        Assert.True(progress.IsSuccess, progress.Error?.Message);
        var revised = Assert.Single(
            progress.Value!.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "promisePublished");
        var publication = revised.GetProperty("payload");
        Assert.Equal(
            1,
            publication.GetProperty("exogenousDelta")
                .GetProperty("pickupEtaTotalMs")
                .GetInt64());
        Assert.Equal(
            0,
            publication.GetProperty("decisionDelta")
                .GetProperty("pickupEtaTotalMs")
                .GetInt64());
    }

    [Fact]
    public void Forced_reference_recovery_keeps_a_late_route_as_a_certified_breach()
    {
        // ADR-075. A one-millisecond drop deadline and a vehicle that has barely left
        // its node: exogenous drift alone pushes the booked rider past the deadline.
        // Fail-closed C1 has no candidate left and the session fails; with recovery the
        // route is kept, one typed breach is recorded, and the certificate says so.
        var commitment = DeadlineCommitmentConfiguration(slackMs: 1);
        var failClosed = CreateSession(
            HardVectorConfiguration(commitment, recovery: null),
            commitmentConfiguration: commitment);
        var legacy = DriftBookedRider(failClosed, progressPermille: 1);
        Assert.Equal("error", legacy.MessageType.Value);
        Assert.Equal("INTERNAL_ERROR", legacy.Payload.GetProperty("code").GetString());
        var legacyMessage = legacy.Payload.GetProperty("message").GetString()!;
        Assert.StartsWith(
            "C1 rejected every generated candidate for this vehicle. No-op rejection: ",
            legacyMessage,
            StringComparison.Ordinal);
        Assert.Contains("deadline", legacyMessage, StringComparison.Ordinal);
        Assert.Equal(RunnerSessionStatus.Failed, failClosed.Session.Status);

        var setup = CreateSession(
            HardVectorConfiguration(commitment, recovery: "forced-reference-v1"),
            commitmentConfiguration: commitment);
        var response = DriftBookedRider(setup, progressPermille: 1);
        var decision = DecisionPayloadCodec.Decode(response.Payload);

        Assert.Equal("decision", response.MessageType.Value);
        Assert.True(decision.IsSuccess, decision.Error?.Message);
        var body = decision.Value!.Certificate.Body!;
        Assert.False(body.NormalOperation);
        var witness = Assert.Single(body.Witnesses);
        Assert.Equal("forcedReference", witness.Stage);
        Assert.Equal("COMMITMENT_DEADLINE_EXCEEDED", witness.Code);
        Assert.Equal("r-1", witness.RequestId);
        Assert.Single(
            decision.Value.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "promisePublished");

        _ = setup.Session.Process(
            DecisionApplied(3, 1_050, decision.Value.DecisionHash.Value));
        var breach = Assert.Single(
            setup.Session.CommittedOnlineState!.Incidents.Breaches);
        Assert.Equal(
            RideBound.Domain.Incidents.CommitmentBreachKind.ForcedReference,
            breach.Kind);
        Assert.Equal(["COMMITMENT_DEADLINE_EXCEEDED"], breach.WitnessCodes);

        var checkpoint = setup.Session.Process(CheckpointRequest()).Response!;
        var restored = CreateSession(
            HardVectorConfiguration(commitment, recovery: "forced-reference-v1"),
            commitmentConfiguration: commitment);
        Assert.Equal(
            "restore",
            restored.Session.Process(RestoreRequest(checkpoint.Payload))
                .Response?.MessageType.Value);
        Assert.Equal(
            breach.BreachId,
            Assert.Single(restored.Session.CommittedOnlineState!.Incidents.Breaches)
                .BreachId);
    }

    [Fact]
    public void Forced_reference_recovery_repeats_the_exemption_but_records_a_breach_per_revision()
    {
        // After the first forced decision the rider stays past the deadline. A timer tick at
        // the same time changes nothing: the certificate is still non-normal, but no second
        // breach is written. A tick 30 ms later moves the kept route again, which revises the
        // promise and records a second breach.
        var commitment = DeadlineCommitmentConfiguration(slackMs: 1);
        var setup = CreateSession(
            HardVectorConfiguration(commitment, recovery: "forced-reference-v1"),
            commitmentConfiguration: commitment);
        var first = DecisionPayloadCodec.Decode(
            DriftBookedRider(setup, progressPermille: 1).Payload).Value!;
        _ = setup.Session.Process(DecisionApplied(3, 1_050, first.DecisionHash.Value));
        Assert.Single(setup.Session.CommittedOnlineState!.Incidents.Breaches);

        var same = DecisionPayloadCodec.Decode(
            setup.Session.Process(TimerTick(epoch: 4, sequence: 7, simTime: 1_050))
                .Response!.Payload);
        Assert.True(same.IsSuccess, same.Error?.Message);
        Assert.False(same.Value!.Certificate.Body!.NormalOperation);
        Assert.Equal(
            "COMMITMENT_DEADLINE_EXCEEDED",
            Assert.Single(same.Value.Certificate.Body.Witnesses).Code);
        Assert.DoesNotContain(
            same.Value.Actions,
            action => action.GetProperty("decisionType").GetString() == "promisePublished");
        _ = setup.Session.Process(DecisionApplied(4, 1_050, same.Value.DecisionHash.Value));
        Assert.Single(setup.Session.CommittedOnlineState!.Incidents.Breaches);

        var later = DecisionPayloadCodec.Decode(
            setup.Session.Process(TimerTick(epoch: 5, sequence: 8, simTime: 1_080))
                .Response!.Payload);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.False(later.Value!.Certificate.Body!.NormalOperation);
        Assert.Single(
            later.Value.Actions,
            action => action.GetProperty("decisionType").GetString() == "promisePublished");
        _ = setup.Session.Process(DecisionApplied(5, 1_080, later.Value.DecisionHash.Value));
        Assert.Equal(2, setup.Session.CommittedOnlineState!.Incidents.Breaches.Count);
    }

    [Fact]
    public void An_explicit_fail_closed_recovery_behaves_like_the_default()
    {
        var commitment = DeadlineCommitmentConfiguration(slackMs: 1);
        var explicitSetup = CreateSession(
            HardVectorConfiguration(commitment, recovery: "fail-closed"),
            commitmentConfiguration: commitment);

        var response = DriftBookedRider(explicitSetup, progressPermille: 1);

        Assert.False(explicitSetup.Wp4.SolverPolicyOptions!.ForcedReferenceRecovery);
        Assert.Equal("error", response.MessageType.Value);
        Assert.Equal("INTERNAL_ERROR", response.Payload.GetProperty("code").GetString());
        Assert.StartsWith(
            "C1 rejected every generated candidate for this vehicle. No-op rejection: ",
            response.Payload.GetProperty("message").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_late_boarding_is_rejected_by_default_and_recorded_with_record_v1()
    {
        // ADR-076. r-1 may be picked up in [1000, 2000] ms and boards at 2500 ms. By default
        // the Runner rejects the observation, which is the Tier 2 "window-wall" death. With
        // `lateBoarding: record-v1` the rider is onboard, the window is unchanged, the 500 ms
        // gap is one record, and the record survives checkpoint/restore.
        var commitment = CommitmentConfiguration();
        var byDefault = CreateSession(
            HardVectorConfiguration(commitment, recovery: null),
            commitmentConfiguration: commitment);
        var rejected = BoardBookedRider(byDefault, at: 2_500);
        Assert.Equal("error", rejected.MessageType.Value);
        Assert.Equal(
            "SCHEMA_VALIDATION_FAILED",
            rejected.Payload.GetProperty("code").GetString());
        Assert.Equal(
            "Actual pickup time must remain inside the accepted pickup window.",
            rejected.Payload.GetProperty("message").GetString());

        var configuration = HardVectorConfiguration(
            commitment,
            recovery: null,
            lateBoarding: "record-v1");
        Assert.True(configuration.RecordsLateBoarding);
        var setup = CreateSession(configuration, commitmentConfiguration: commitment);
        var response = BoardBookedRider(setup, at: 2_500);
        var decision = DecisionPayloadCodec.Decode(response.Payload);

        Assert.Equal("decision", response.MessageType.Value);
        Assert.True(decision.IsSuccess, decision.Error?.Message);
        _ = setup.Session.Process(
            DecisionApplied(3, 2_500, decision.Value!.DecisionHash.Value));
        var committed = setup.Session.CommittedOnlineState!;
        var late = Assert.Single(committed.Incidents.LatePickups);
        Assert.Equal(500, late.LatenessMilliseconds);
        var rider = committed.Run.Requests[new RideBound.Domain.Common.RequestId("r-1")];
        Assert.Equal(RideBound.Domain.Requests.RequestLifecycle.Onboard, rider.Lifecycle);
        Assert.Equal(2_500, rider.ActualPickupTime!.Value.Milliseconds);
        Assert.Equal(2_000, rider.LatestPickup.Milliseconds);

        var checkpoint = setup.Session.Process(CheckpointRequest()).Response!;
        var restored = CreateSession(configuration, commitmentConfiguration: commitment);
        Assert.Equal(
            "restore",
            restored.Session.Process(RestoreRequest(checkpoint.Payload))
                .Response?.MessageType.Value);
        Assert.Equal(
            late,
            Assert.Single(restored.Session.CommittedOnlineState!.Incidents.LatePickups));
    }

    [Fact]
    public void The_late_boarding_setting_accepts_only_its_two_values()
    {
        var commitment = CommitmentConfiguration();

        Assert.False(
            HardVectorConfiguration(commitment, recovery: null, lateBoarding: "reject")
                .RecordsLateBoarding);
        Assert.False(HardVectorConfiguration(commitment, recovery: null).RecordsLateBoarding);
        Assert.Throws<InvalidDataException>(
            () => HardVectorConfiguration(commitment, recovery: null, lateBoarding: "record-v2"));
    }

    [Fact]
    public void Forced_reference_recovery_leaves_an_untriggered_decision_unchanged()
    {
        // Without a gate-rejected no-op the recovery option changes no action and no
        // certificate content. The configuration hash differs by construction and is
        // bound into the state hashes, so those are not compared.
        var commitment = DeadlineCommitmentConfiguration(slackMs: 3_600_000);
        var failClosed = CreateSession(
            HardVectorConfiguration(commitment, recovery: null),
            commitmentConfiguration: commitment);
        var recovering = CreateSession(
            HardVectorConfiguration(commitment, recovery: "forced-reference-v1"),
            commitmentConfiguration: commitment);

        var expected = DecisionPayloadCodec.Decode(
            DriftBookedRider(failClosed, progressPermille: 1).Payload).Value!;
        var actual = DecisionPayloadCodec.Decode(
            DriftBookedRider(recovering, progressPermille: 1).Payload).Value!;

        Assert.True(actual.Certificate.Body!.NormalOperation);
        Assert.Empty(actual.Certificate.Body.Witnesses);
        Assert.Equal(
            expected.Certificate.Body!.PublicationIds,
            actual.Certificate.Body.PublicationIds);
        Assert.Equal(expected.ReasonCode, actual.ReasonCode);
        Assert.Equal(expected.Solver.Status, actual.Solver.Status);
        Assert.Equal(
            expected.Actions.Select(value => value.GetRawText()),
            actual.Actions.Select(value => value.GetRawText()));
    }

    [Fact]
    public void No_worse_recovery_extends_forced_reference_and_keeps_a_lone_kept_route()
    {
        // With one booked rider and no other candidate, no-worse recovery has nothing to relax:
        // the decision is the kept route, exactly as under forced-reference-v1, and it restores.
        var commitment = DeadlineCommitmentConfiguration(slackMs: 1);
        var configuration = HardVectorConfiguration(
            commitment,
            recovery: "no-worse-than-reference-v1");
        Assert.True(configuration.SolverPolicyOptions!.ForcedReferenceRecovery);
        Assert.True(configuration.SolverPolicyOptions.ForcedNoWorseRecovery);
        Assert.False(
            HardVectorConfiguration(commitment, recovery: "forced-reference-v1")
                .SolverPolicyOptions!.ForcedNoWorseRecovery);

        var setup = CreateSession(configuration, commitmentConfiguration: commitment);
        var decision = DecisionPayloadCodec.Decode(
            DriftBookedRider(setup, progressPermille: 1).Payload);

        Assert.True(decision.IsSuccess, decision.Error?.Message);
        var body = decision.Value!.Certificate.Body!;
        Assert.False(body.NormalOperation);
        Assert.Equal("forcedReference", Assert.Single(body.Witnesses).Stage);
        _ = setup.Session.Process(
            DecisionApplied(3, 1_050, decision.Value.DecisionHash.Value));
        Assert.Equal(
            RideBound.Domain.Incidents.CommitmentBreachKind.ForcedReference,
            Assert.Single(setup.Session.CommittedOnlineState!.Incidents.Breaches).Kind);

        var checkpoint = setup.Session.Process(CheckpointRequest()).Response!;
        var restored = CreateSession(configuration, commitmentConfiguration: commitment);
        Assert.Equal(
            "restore",
            restored.Session.Process(RestoreRequest(checkpoint.Payload))
                .Response?.MessageType.Value);
    }

    [Fact]
    public void A_relaxed_deadline_keeps_the_late_route_as_normal_operation_without_recovery()
    {
        // The scenario above: drift alone carries the booked rider past a one-millisecond
        // deadline. The relaxed deadline is the later of that deadline and the kept route, so the
        // kept route passes under every recovery setting, including fail-closed: the certificate is
        // normal, no breach is recorded, and the fleet actions are those the strict deadline
        // reaches only through a forced vehicle.
        var strict = DeadlineCommitmentConfiguration(slackMs: 1);
        var relaxed = DeadlineCommitmentConfiguration(slackMs: 1, relaxed: true);
        Assert.True(relaxed.TryGetPolicy("uniform-v1", out var relaxedPolicy));
        Assert.True(relaxedPolicy.DropEtaDeadlineRelaxed);
        var forcedSetup = CreateSession(
            HardVectorConfiguration(strict, recovery: "forced-reference-v1"),
            commitmentConfiguration: strict);
        var forced = DecisionPayloadCodec.Decode(
            DriftBookedRider(forcedSetup, progressPermille: 1).Payload).Value!;
        Assert.False(forced.Certificate.Body!.NormalOperation);

        foreach (var recovery in new string?[]
                 {
                     null,
                     "forced-reference-v1",
                     "no-worse-than-reference-v1",
                 })
        {
            var setup = CreateSession(
                HardVectorConfiguration(relaxed, recovery),
                commitmentConfiguration: relaxed);
            var response = DriftBookedRider(setup, progressPermille: 1);

            Assert.Equal("decision", response.MessageType.Value);
            var decision = DecisionPayloadCodec.Decode(response.Payload);
            Assert.True(decision.IsSuccess, decision.Error?.Message);
            var body = decision.Value!.Certificate.Body!;
            Assert.True(body.NormalOperation);
            Assert.Empty(body.Witnesses);
            Assert.Equal(forced.Certificate.Body.PublicationIds, body.PublicationIds);
            Assert.Equal(
                forced.Actions.Select(value => value.GetRawText()),
                decision.Value.Actions.Select(value => value.GetRawText()));
            _ = setup.Session.Process(
                DecisionApplied(3, 1_050, decision.Value.DecisionHash.Value));
            Assert.Empty(setup.Session.CommittedOnlineState!.Incidents.Breaches);
        }
    }

    [Fact]
    public void A_revision_cap_lets_drift_through_and_never_forces_the_kept_route()
    {
        // The same drift under a one-millisecond per-decision cap and no deadline. The decision
        // only republishes the kept route, whose own change is zero, so under every recovery
        // setting it is the uncapped decision: normal operation and no breach.
        var plain = CommitmentConfiguration();
        var capped = RevisionLimitCommitmentConfiguration(limitMs: 1);
        Assert.True(capped.TryGetPolicy("uniform-v1", out var cappedPolicy));
        Assert.Equal(1, cappedPolicy.DropEtaRevisionLimit?.Milliseconds);
        var expected = DecisionPayloadCodec.Decode(
            DriftBookedRider(
                CreateSession(
                    HardVectorConfiguration(plain, recovery: null),
                    commitmentConfiguration: plain),
                progressPermille: 1).Payload).Value!;
        Assert.Single(
            expected.Actions,
            action => action.GetProperty("decisionType").GetString() == "promisePublished");

        foreach (var recovery in new string?[]
                 {
                     null,
                     "forced-reference-v1",
                     "no-worse-than-reference-v1",
                 })
        {
            var setup = CreateSession(
                HardVectorConfiguration(capped, recovery),
                commitmentConfiguration: capped);
            var response = DriftBookedRider(setup, progressPermille: 1);

            Assert.Equal("decision", response.MessageType.Value);
            var decision = DecisionPayloadCodec.Decode(response.Payload);
            Assert.True(decision.IsSuccess, decision.Error?.Message);
            Assert.True(decision.Value!.Certificate.Body!.NormalOperation);
            Assert.Empty(decision.Value.Certificate.Body.Witnesses);
            Assert.Equal(
                expected.Actions.Select(value => value.GetRawText()),
                decision.Value.Actions.Select(value => value.GetRawText()));
            _ = setup.Session.Process(
                DecisionApplied(3, 1_050, decision.Value.DecisionHash.Value));
            Assert.Empty(setup.Session.CommittedOnlineState!.Incidents.Breaches);
        }
    }

    [Fact]
    public void No_worse_recovery_is_rejected_outside_the_solver_backed_C1_and_C2()
    {
        var commitment = CommitmentConfiguration();
        var json = File.ReadAllText(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp4-rolling-cost-boundary-v1.json"))
            .Replace(
                "\"policyVersion\": \"wp4-boundary-v1\"",
                "\"policyVersion\": \"wp4-boundary-v1\",\n  "
                + "\"commitmentRecovery\": \"no-worse-than-reference-v1\"",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(json), commitment));
    }

    [Fact]
    public void Forced_reference_recovery_is_rejected_outside_the_solver_backed_C1_and_C2()
    {
        var commitment = CommitmentConfiguration();
        var json = File.ReadAllText(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp4-rolling-cost-boundary-v1.json"))
            .Replace(
                "\"policyVersion\": \"wp4-boundary-v1\"",
                "\"policyVersion\": \"wp4-boundary-v1\",\n  "
                + "\"commitmentRecovery\": \"forced-reference-v1\"",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(json), commitment));
        Assert.Throws<InvalidDataException>(
            () => HardVectorConfiguration(commitment, recovery: "forced-reference-v2"));
    }

    /// <summary>
    /// Replays the booking-confirmation scenario of the C1 ranking test up to the
    /// progress observation and returns the response to that observation.
    /// </summary>
    private static ProtocolEnvelope DriftBookedRider(
        SessionSetup setup,
        int progressPermille)
    {
        var (selectedVehicle, selectedRoute) = BookRider(setup);

        var progressBatch = JsonNode.Parse(
                """
                {
                  "schemaVersion":"1.0.0",
                  "messageType":"eventBatch",
                  "runId":"wp2-run-001",
                  "scenarioId":"wp2-two-epoch-small",
                  "epochId":3,
                  "simTimeMs":1050,
                  "payload":{"events":[
                    {
                      "eventSeq":6,
                      "eventType":"vehicleAdvanced",
                      "payload":{"vehicle":{
                        "vehicleId":"placeholder",
                        "capacity":4,
                        "occupiedSeats":0,
                        "position":{
                          "kind":"edgeProgress",
                          "edgeId":"edge-n0-n1",
                          "fromNodeId":"n-0",
                          "toNodeId":"n-1",
                          "progressPermille":499
                        },
                        "onboardRequestIds":[],
                        "acceptedRequestIds":["r-1"],
                        "route":{}
                      }}
                    }
                  ]}
                }
                """)!;
        var observedVehicle = progressBatch["payload"]!["events"]![0]![
            "payload"]!["vehicle"]!;
        observedVehicle["vehicleId"] = selectedVehicle;
        observedVehicle["position"]!["progressPermille"] = progressPermille;
        observedVehicle["route"] = JsonNode.Parse(selectedRoute);
        return setup.Session.Process(
            Encoding.UTF8.GetBytes(progressBatch.ToJsonString())).Response!;
    }

    /// <summary>
    /// Offers r-1 to the two-vehicle fleet and confirms its booking, applying both decisions
    /// (epochs 1 and 2, event sequences 1..5). Returns the selected vehicle and its route.
    /// </summary>
    private static (string Vehicle, string Route) BookRider(SessionSetup setup)
    {
        var bootstrap = JsonNode.Parse(
            FixtureLoader.ReadUtf8("wp2/valid-bootstrap-event-batch.json"))!;
        var events = bootstrap["payload"]!["events"]!.AsArray();
        var secondVehicle = events[2]!.DeepClone();
        secondVehicle["eventSeq"] = 4;
        secondVehicle["payload"]!["vehicle"]!["vehicleId"] = "v-2";
        secondVehicle["payload"]!["vehicle"]!["route"]!["mutableSuffix"] =
            new JsonArray();
        events.Add(secondVehicle);
        var offer = DecisionPayloadCodec.Decode(
            setup.Session.Process(
                Encoding.UTF8.GetBytes(bootstrap.ToJsonString())).Response!.Payload);
        Assert.True(offer.IsSuccess, offer.Error?.Message);
        var accepted = offer.Value!.Actions.Single(
            action => action.GetProperty("decisionType").GetString()
                == "requestAccepted");
        var selectedVehicle = accepted.GetProperty("payload")
            .GetProperty("vehicleId")
            .GetString()!;
        var selectedRoute = offer.Value.Actions.Single(
            action => action.GetProperty("decisionType").GetString()
                == "vehiclePlanUpdated"
                && action.GetProperty("payload")
                    .GetProperty("vehicleId")
                    .GetString() == selectedVehicle)
            .GetProperty("payload")
            .GetProperty("route")
            .GetRawText();
        _ = setup.Session.Process(
            DecisionApplied(1, 1_000, offer.Value.DecisionHash.Value));
        var booking = DecisionPayloadCodec.Decode(
            setup.Session.Process(
                Encoding.UTF8.GetBytes(
                    """
                    {
                      "schemaVersion":"1.0.0",
                      "messageType":"eventBatch",
                      "runId":"wp2-run-001",
                      "scenarioId":"wp2-two-epoch-small",
                      "epochId":2,
                      "simTimeMs":1000,
                      "payload":{"events":[
                        {
                          "eventSeq":5,
                          "eventType":"bookingConfirmed",
                          "payload":{"requestId":"r-1"}
                        }
                      ]}
                    }
                    """)).Response!.Payload);
        Assert.True(booking.IsSuccess, booking.Error?.Message);
        _ = setup.Session.Process(
            DecisionApplied(2, 1_000, booking.Value!.DecisionHash.Value));
        return (selectedVehicle, selectedRoute);
    }

    /// <summary>
    /// After <see cref="BookRider"/>, one batch at <paramref name="at"/> that reaches every
    /// stop up to r-1's pickup and boards r-1 there.
    /// </summary>
    private static ProtocolEnvelope BoardBookedRider(SessionSetup setup, long at)
    {
        var (vehicleId, routeJson) = BookRider(setup);
        var route = JsonNode.Parse(routeJson)!;
        var planVersion = route["planVersion"]!.GetValue<long>();
        var executed = route["executedStopCount"]!.GetValue<int>();
        var remaining = route["frozenPrefix"]!.AsArray()
            .Skip(executed)
            .Concat(route["mutableSuffix"]!.AsArray())
            .ToArray();
        var events = new JsonArray();
        var sequence = 6L;

        foreach (var stop in remaining)
        {
            events.Add(
                new JsonObject
                {
                    ["eventSeq"] = sequence++,
                    ["eventType"] = "vehicleReachedStop",
                    ["payload"] = new JsonObject
                    {
                        ["vehicleId"] = vehicleId,
                        ["stopId"] = stop!["stopId"]!.GetValue<string>(),
                        ["planVersion"] = planVersion,
                        ["position"] = new JsonObject
                        {
                            ["kind"] = "node",
                            ["nodeId"] = stop["nodeId"]!.GetValue<string>(),
                        },
                    },
                });

            if (stop["kind"]!.GetValue<string>() == "pickup"
                && stop["requestId"]?.GetValue<string>() == "r-1")
            {
                break;
            }
        }

        events.Add(
            new JsonObject
            {
                ["eventSeq"] = sequence,
                ["eventType"] = "passengerBoarded",
                ["payload"] = new JsonObject
                {
                    ["vehicleId"] = vehicleId,
                    ["requestId"] = "r-1",
                    ["planVersion"] = planVersion,
                },
            });
        var batch = new JsonObject
        {
            ["schemaVersion"] = "1.0.0",
            ["messageType"] = "eventBatch",
            ["runId"] = "wp2-run-001",
            ["scenarioId"] = "wp2-two-epoch-small",
            ["epochId"] = 3,
            ["simTimeMs"] = at,
            ["payload"] = new JsonObject { ["events"] = events },
        };
        return setup.Session.Process(
            Encoding.UTF8.GetBytes(batch.ToJsonString())).Response!;
    }

    private static byte[] TimerTick(long epoch, long sequence, long simTime) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"eventBatch",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "epochId":{{epoch}},
              "simTimeMs":{{simTime}},
              "payload":{"events":[
                {
                  "eventSeq":{{sequence}},
                  "eventType":"timerTick",
                  "payload":{ }
                }
              ]}
            }
            """);

    private static CommitmentPolicyConfiguration DeadlineCommitmentConfiguration(
        long slackMs,
        bool relaxed = false)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp3-boundary-test-v1.json")))!;
        json["policies"]![0]!["dropEtaDeadlineSlackMs"] = slackMs;

        if (relaxed)
        {
            json["policies"]![0]!["dropEtaDeadlineRelaxed"] = true;
        }

        return CommitmentPolicyConfiguration.Decode(
            Encoding.UTF8.GetBytes(json.ToJsonString()));
    }

    private static CommitmentPolicyConfiguration RevisionLimitCommitmentConfiguration(
        long limitMs)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp3-boundary-test-v1.json")))!;
        json["policies"]![0]!["dropEtaRevisionLimitMs"] = limitMs;
        return CommitmentPolicyConfiguration.Decode(
            Encoding.UTF8.GetBytes(json.ToJsonString()));
    }

    private static Wp4RunnerConfiguration HardVectorConfiguration(
        CommitmentPolicyConfiguration commitment,
        string? recovery,
        string? lateBoarding = null)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp7-fleetpy-ridebound-hard-vector-v1.json")))!;

        if (recovery is not null)
        {
            json["commitmentRecovery"] = recovery;
        }

        if (lateBoarding is not null)
        {
            json["lateBoarding"] = lateBoarding;
        }

        return Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(json.ToJsonString()),
            commitment);
    }

    [Fact]
    public void Unknown_solver_uses_full_validator_no_op_and_reports_safe_fallback()
    {
        var setup = CreateSession(
            PublishedWp4Configuration(),
            new UnknownSolver());

        var response = setup.Session.Process(
            ReadFixture("wp2/valid-bootstrap-event-batch.json")).Response!;
        var decision = DecisionPayloadCodec.Decode(response.Payload);

        Assert.True(decision.IsSuccess, decision.Error?.Message);
        Assert.Equal(SolverStatus.SafeFallback, decision.Value!.Solver.Status);
        Assert.Equal(CertificateStatus.Produced, decision.Value.Certificate.Status);
        Assert.Contains(
            decision.Value.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "requestDeferred");
        Assert.DoesNotContain(
            decision.Value.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "requestAccepted");
        Assert.Empty(decision.Value.Certificate.Body!.PublicationIds);
    }

    [Fact]
    public void Explicit_manifest_seed_mode_passes_the_manifest_seed_to_solver()
    {
        var solver = new CapturingUnknownSolver();
        var setup = CreateSession(
            PublishedWp4Configuration(),
            solver,
            useManifestSolverSeed: true);

        var response = setup.Session.Process(
            ReadFixture("wp2/valid-bootstrap-event-batch.json")).Response!;
        var decision = DecisionPayloadCodec.Decode(response.Payload);

        Assert.True(decision.IsSuccess, decision.Error?.Message);
        Assert.Equal(20260729, solver.ObservedSeed);
        Assert.Equal(SolverStatus.SafeFallback, decision.Value!.Solver.Status);
    }

    [Fact]
    public void Manifest_policy_identity_mismatch_fails_before_online_state_changes()
    {
        var commitment = CommitmentConfiguration();
        var wp4 = PublishedWp4Configuration(commitment);
        var session = NewSession(commitment, wp4);
        Assert.Equal(
            "helloAck",
            session.Process(ReadFixture("hello/valid-hello.json"))
                .Response?.MessageType.Value);
        var initialize = InitializePayload(wp4, commitment);
        initialize["payload"]!["manifest"]!["policyId"] =
            "ridebound-hard-vector";

        var rejected = session.Process(
            Encoding.UTF8.GetBytes(initialize.ToJsonString()));

        Assert.Equal("error", rejected.Response?.MessageType.Value);
        Assert.Equal(
            "HASH_MISMATCH",
            rejected.Response?.Payload.GetProperty("code").GetString());
        Assert.Equal(RunnerSessionStatus.Failed, session.Status);
        Assert.Null(session.CommittedOnlineState);
    }

    [Fact]
    public void B5_plan_pool_commits_only_on_ack_and_survives_checkpoint_restore()
    {
        var commitment = CommitmentConfiguration();
        var b5 = B5Configuration(commitment);
        var setup = CreateSession(b5, commitmentConfiguration: commitment);
        var decision = DecisionPayloadCodec.Decode(
            setup.Session.Process(
                ReadFixture("wp2/valid-bootstrap-event-batch.json"))
                .Response!.Payload).Value!;

        Assert.Equal(SolverStatus.Completed, decision.Solver.Status);
        Assert.Empty(setup.Session.CommittedOnlineState!.PlanPool.Plans);
        _ = setup.Session.Process(
            DecisionApplied(1, 1_000, decision.DecisionHash.Value));
        Assert.NotEmpty(setup.Session.CommittedOnlineState!.PlanPool.Plans);

        var checkpointResponse = setup.Session.Process(CheckpointRequest()).Response!;
        var restoredSetup = CreateSession(
            b5,
            commitmentConfiguration: commitment);
        var restored = restoredSetup.Session.Process(
            RestoreRequest(checkpointResponse.Payload));

        Assert.Equal("restore", restored.Response?.MessageType.Value);
        Assert.Equal(
            setup.Session.CommittedOnlineState.PlanPool.Version,
            restoredSetup.Session.CommittedOnlineState!.PlanPool.Version);
        Assert.Equal(
            setup.Session.CommittedOnlineState.PlanPool.DistinguishedPlanId,
            restoredSetup.Session.CommittedOnlineState.PlanPool.DistinguishedPlanId);
    }

    private static SessionSetup CreateSession(
        Wp4RunnerConfiguration wp4,
        ICandidateSelectionSolver? solver = null,
        CommitmentPolicyConfiguration? commitmentConfiguration = null,
        bool useManifestSolverSeed = false)
    {
        var commitment = commitmentConfiguration ?? CommitmentConfiguration();
        var session = NewSession(
            commitment,
            wp4,
            solver,
            useManifestSolverSeed);
        Assert.Equal(
            "helloAck",
            session.Process(ReadFixture("hello/valid-hello.json"))
                .Response?.MessageType.Value);
        var initialized = session.Process(
            Encoding.UTF8.GetBytes(
                InitializePayload(wp4, commitment).ToJsonString()));
        Assert.Equal(
            "initialized",
            initialized.Response?.MessageType.Value);
        return new SessionSetup(session, commitment, wp4);
    }

    private static RunnerSession NewSession(
        CommitmentPolicyConfiguration commitment,
        Wp4RunnerConfiguration wp4,
        ICandidateSelectionSolver? solver = null,
        bool useManifestSolverSeed = false) =>
        new(
            RunnerDefaults.CapabilityRequirements,
            RunnerExecutionMode.OnlineCommitment,
            commitmentPolicies: commitment,
            stopDistances: commitment,
            commitmentPolicyConfigurationHash:
                wp4.BindToCommitmentConfiguration(commitment.ContentHash),
            wp4Configuration: wp4,
            solverBackedPolicy: solver is null
                ? null
                : new SolverBackedRidePoolingPolicy(solver),
            useManifestSolverSeed: useManifestSolverSeed);

    private static JsonNode InitializePayload(
        Wp4RunnerConfiguration wp4,
        CommitmentPolicyConfiguration commitment)
    {
        var initialize = JsonNode.Parse(
            FixtureLoader.ReadUtf8("initialize/valid-initialize-run.json"))!;
        initialize["runId"] = "wp2-run-001";
        initialize["scenarioId"] = "wp2-two-epoch-small";
        var manifest = initialize["payload"]!["manifest"]!;
        manifest["travelTimeSnapshotHash"] = new string('a', 64);
        manifest["policyId"] = wp4.PolicyId;
        manifest["policyVersion"] = wp4.PolicyVersion;
        manifest["policyConfigurationHash"] =
            wp4.BindToCommitmentConfiguration(commitment.ContentHash).Value;
        return initialize;
    }

    private static Wp4RunnerConfiguration PublishedWp4Configuration(
        CommitmentPolicyConfiguration? commitmentConfiguration = null,
        bool emitSolverExecutionEvidence = false,
        bool retainCandidatePortfolio = false)
    {
        var commitment = commitmentConfiguration ?? CommitmentConfiguration();
        var json = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp4-rolling-cost-boundary-v1.json"));

        if (emitSolverExecutionEvidence || retainCandidatePortfolio)
        {
            var profile = retainCandidatePortfolio
                ? ",\n  \"solverExecutionEvidenceProfile\": " +
                    "\"retained-portfolio-v1\""
                : string.Empty;
            json = json.Replace(
                "\"policyVersion\": \"wp4-boundary-v1\"",
                "\"policyVersion\": \"wp4-boundary-v1\",\n  " +
                "\"emitSolverExecutionEvidence\": true" + profile,
                StringComparison.Ordinal);
        }

        return Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(json),
            commitment);
    }

    private static Wp4RunnerConfiguration B5Configuration(
        CommitmentPolicyConfiguration commitment) =>
        Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(
                """
                {
                  "configurationVersion":"1.0.0",
                  "policyId":"least-commitment-consensus",
                  "policyVersion":"wp4-b5-test-v1",
                  "candidateGeneration":{
                    "maximumCandidatesPerVehicle":100,
                    "maximumNewRequestsPerVehicle":2,
                    "exactSmallMode":false,
                    "scheduleStrategy":"earliest-feasible",
                    "maximumExplorationWorkUnits":10000
                  },
                  "multiplePlan":{
                    "maximumPlanCount":4,
                    "maximumCombinationWorkUnits":10000,
                    "requireCompleteEnumeration":true
                  }
                }
                """),
            commitment);

    private static CommitmentPolicyConfiguration CommitmentConfiguration() =>
        CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp3-boundary-test-v1.json")));

    private static byte[] ReadFixture(string path) =>
        Encoding.UTF8.GetBytes(FixtureLoader.ReadUtf8(path));

    private static byte[] DecisionApplied(
        long epoch,
        long simTime,
        string hash) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"decisionApplied",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "epochId":{{epoch}},
              "simTimeMs":{{simTime}},
              "payload":{"decisionHash":"{{hash}}"}
            }
            """);

    private static byte[] CheckpointRequest() =>
        Encoding.UTF8.GetBytes(
            """
            {
              "schemaVersion":"1.0.0",
              "messageType":"checkpoint",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "payload":{}
            }
            """);

    private static byte[] RestoreRequest(JsonElement payload) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"restore",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "payload":{{payload.GetRawText()}}
            }
            """);

    private static string RepositoryRoot() =>
        typeof(Wp4RunnerIntegrationTests)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(value => value.Key == "RideBoundRepositoryRoot")
            .Value!;

    private sealed record SessionSetup(
        RunnerSession Session,
        CommitmentPolicyConfiguration Commitment,
        Wp4RunnerConfiguration Wp4);

    private sealed class UnknownSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            var diagnostics = CandidateSelectionSolverDiagnostics.Create(
                problem,
                budget,
                0,
                0,
                0,
                []).Value!;
            return CandidateSelectionSolveResult.Unknown(
                diagnostics,
                "TEST_UNKNOWN",
                "The injected test solver did not produce an incumbent.");
        }
    }

    private sealed class CapturingUnknownSolver : ICandidateSelectionSolver
    {
        public long? ObservedSeed { get; private set; }

        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            ObservedSeed = budget.RandomSeed;
            var diagnostics = CandidateSelectionSolverDiagnostics.Create(
                problem,
                budget,
                0,
                0,
                0,
                []).Value!;
            return CandidateSelectionSolveResult.Unknown(
                diagnostics,
                "TEST_UNKNOWN",
                "The injected test solver did not produce an incumbent.");
        }
    }
}
