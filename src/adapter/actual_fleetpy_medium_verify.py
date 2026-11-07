#!/usr/bin/env python3
"""Independently verify a WP7 actual-FleetPy medium evidence bundle."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import pathlib
import re


_HASH = re.compile(r"^[0-9a-f]{64}$")
_ZERO_HASH = "0" * 64
_GENERATED_ACTION_FIELDS = {
    "candidateId",
    "publicationId",
}
_RECORD_FIELDS = {
    "schemaVersion",
    "ordinal",
    "direction",
    "frameLengthBytes",
    "frameSha256",
    "frameBase64",
}


def _strict_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON field {key!r}")
        result[key] = value
    return result


def _reject_number(text):
    raise ValueError(f"non-integer JSON number {text!r}")


def _loads(data):
    return json.loads(
        data,
        object_pairs_hook=_strict_object,
        parse_float=_reject_number,
        parse_constant=_reject_number,
    )


def _canonical(value):
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def _framed_hash(domain, tag, value):
    tag_bytes = tag.encode("utf-8")
    if len(tag_bytes) > 65535:
        raise ValueError("hash frame tag is too long")
    digest = hashlib.sha256()
    digest.update(domain)
    digest.update(len(tag_bytes).to_bytes(2, "big"))
    digest.update(tag_bytes)
    digest.update(len(value).to_bytes(8, "big"))
    digest.update(value)
    return digest.hexdigest()


def _manifest_hash(manifest):
    if not isinstance(manifest, dict):
        raise RuntimeError("manifest is not an object")
    conversions = manifest.get("sourceUnitConversions")
    selection = manifest.get("capabilitySelection")
    if (
        not isinstance(conversions, list)
        or any(not isinstance(item, dict) for item in conversions)
        or not isinstance(selection, dict)
        or not isinstance(selection.get("capabilities"), list)
        or any(
            not isinstance(capability, str)
            for capability in selection["capabilities"]
        )
    ):
        raise RuntimeError("manifest normalized fields are invalid")
    normalized = dict(manifest)
    normalized["sourceUnitConversions"] = sorted(
        conversions,
        key=lambda item: item.get("quantity", ""),
    )
    normalized_selection = dict(selection)
    normalized_selection["capabilities"] = sorted(
        set(selection["capabilities"])
    )
    normalized["capabilitySelection"] = normalized_selection
    return _framed_hash(
        b"RideBound.ManifestHash.v1\0",
        "canonicalManifest",
        _canonical(normalized),
    )


def _checkpoint_hash(content):
    return _framed_hash(
        b"RideBound.CheckpointHash.v1\0",
        "canonicalCheckpointContent",
        _canonical(content),
    )


def _semantic_hash(semantic):
    return hashlib.sha256(
        b"RideBound.Wp7ActualFleetPyMedium.v1\0" + _canonical(semantic)
    ).hexdigest()


def _without_generated_action_fields(value):
    if isinstance(value, dict):
        return {
            key: _without_generated_action_fields(child)
            for key, child in value.items()
            if key not in _GENERATED_ACTION_FIELDS
        }
    if isinstance(value, list):
        return [_without_generated_action_fields(child) for child in value]
    return value


def _behavioral_projection_hash(decisions):
    return _framed_hash(
        b"RideBound.FleetPyBehavioralProjection.v1\0",
        "canonicalBehavioralProjection",
        _canonical(decisions),
    )


def _require_unique_strings(values, field):
    if (
        not isinstance(values, list)
        or any(not isinstance(value, str) or not value for value in values)
        or len(values) != len(set(values))
    ):
        raise RuntimeError(f"{field} must contain unique non-empty strings")
    return set(values)


def _require_fields(value, field, required, optional=()):
    if not isinstance(value, dict):
        raise RuntimeError(f"{field} must be an object")
    actual = set(value)
    required = set(required)
    allowed = required | set(optional)
    if not required <= actual or not actual <= allowed:
        raise RuntimeError(
            f"{field} fields differ: missing={sorted(required-actual)!r}; "
            f"extra={sorted(actual-allowed)!r}"
        )
    return value


def _nonnegative_integer(value, field):
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise RuntimeError(f"{field} must be a non-negative integer")
    return value


def _optional_hash(value, field):
    if value is not None and (
        not isinstance(value, str) or _HASH.fullmatch(value) is None
    ):
        raise RuntimeError(f"{field} must be a lowercase SHA-256")


def _verify_audited_solver_evidence(solver, epoch):
    field = f"epoch {epoch} solver evidence"
    if solver.get("status") != "completed":
        raise RuntimeError(f"{field}: solver shell is not completed")
    raw_evidence = solver.get("executionEvidence")
    if not isinstance(raw_evidence, dict):
        raise RuntimeError(f"{field}: executionEvidence must be an object")
    evidence_version = raw_evidence.get("evidenceVersion")
    required_evidence_fields = {
        "evidenceVersion",
        "generation",
        "prunedCandidates",
        "selection",
    }
    if evidence_version == "1.2.0":
        required_evidence_fields.add("candidatePortfolio")
    # 1.3.0 (G2): 1.2.0 plus generation.relocationLosses and the candidates' relocation
    # fields; the portfolio is optional and verified when present.
    evidence = _require_fields(
        raw_evidence,
        field,
        required_evidence_fields,
        {"candidatePortfolio"} if evidence_version == "1.3.0" else (),
    )
    if evidence_version not in {"1.0.0", "1.1.0", "1.2.0", "1.3.0"}:
        raise RuntimeError(f"{field}: unknown evidence version")
    generation_fields = {
        "totalPendingRequestCount",
        "consideredRequestCount",
        "omittedRequestCount",
        "vehicleLosses",
        "omissions",
    }
    if evidence_version in {"1.1.0", "1.2.0", "1.3.0"}:
        generation_fields.add("exogenousServiceQualityBreaches")
    if evidence_version == "1.3.0":
        generation_fields.add("relocationLosses")
    generation = _require_fields(
        evidence["generation"],
        f"{field}.generation",
        generation_fields,
    )
    for name in (
        "totalPendingRequestCount",
        "consideredRequestCount",
        "omittedRequestCount",
    ):
        _nonnegative_integer(generation[name], f"{field}.generation.{name}")
    if (
        generation["consideredRequestCount"]
        + generation["omittedRequestCount"]
        != generation["totalPendingRequestCount"]
    ):
        raise RuntimeError(f"{field}: request generation accounting differs")
    losses = generation["vehicleLosses"]
    omissions = generation["omissions"]
    if not isinstance(losses, list) or not isinstance(omissions, list):
        raise RuntimeError(f"{field}: generation evidence arrays are invalid")
    loss_fields = {
        "vehicleId",
        "explorationWorkUnits",
        "evaluatedCandidatePathCount",
        "uniqueFeasibleCandidateCountBeforeCap",
        "retainedCandidateCount",
        "physicallyOrSchedulePrunedCount",
        "omittedUnexpandedCandidatePathCount",
        "omittedFeasibleCandidateCountByCap",
        "workBudgetExhausted",
        "candidateCapApplied",
        "omissionCountWasSaturated",
        "eligibleRepairRequestCount",
        "consideredRepairRequestCount",
        "omittedRepairRequestCount",
    }
    vehicle_ids = set()
    generation_work = 0
    for index, loss in enumerate(losses):
        loss_field = f"{field}.generation.vehicleLosses[{index}]"
        _require_fields(loss, loss_field, loss_fields)
        vehicle_id = loss["vehicleId"]
        if (
            not isinstance(vehicle_id, str)
            or not vehicle_id
            or vehicle_id in vehicle_ids
        ):
            raise RuntimeError(f"{loss_field}.vehicleId is invalid/duplicate")
        vehicle_ids.add(vehicle_id)
        for name in loss_fields - {
            "vehicleId",
            "workBudgetExhausted",
            "candidateCapApplied",
            "omissionCountWasSaturated",
        }:
            _nonnegative_integer(loss[name], f"{loss_field}.{name}")
        for name in (
            "workBudgetExhausted",
            "candidateCapApplied",
            "omissionCountWasSaturated",
        ):
            if not isinstance(loss[name], bool):
                raise RuntimeError(f"{loss_field}.{name} must be boolean")
        if loss["omissionCountWasSaturated"]:
            raise RuntimeError(f"{loss_field}: saturated evidence is not auditable")
        generation_work += loss["explorationWorkUnits"]
    omitted_candidate_count = 0
    omission_fields = {"code", "count", "stableDigest", "countWasSaturated"}
    for index, omission in enumerate(omissions):
        omission_field = f"{field}.generation.omissions[{index}]"
        _require_fields(
            omission,
            omission_field,
            omission_fields,
            {"vehicleId", "requestIds"},
        )
        if not isinstance(omission["code"], str) or not omission["code"]:
            raise RuntimeError(f"{omission_field}.code is invalid")
        _nonnegative_integer(omission["count"], f"{omission_field}.count")
        _optional_hash(omission["stableDigest"], f"{omission_field}.stableDigest")
        if not isinstance(omission["countWasSaturated"], bool):
            raise RuntimeError(f"{omission_field}.countWasSaturated must be boolean")
        if omission["countWasSaturated"]:
            raise RuntimeError(f"{omission_field}: saturated evidence is not auditable")
        if "vehicleId" in omission and (
            not isinstance(omission["vehicleId"], str)
            or not omission["vehicleId"]
        ):
            raise RuntimeError(f"{omission_field}.vehicleId is invalid")
        if "requestIds" in omission:
            _require_unique_strings(
                omission["requestIds"],
                f"{omission_field}.requestIds",
            )
        omitted_candidate_count += omission["count"]

    if evidence_version == "1.3.0":
        relocation_losses = generation["relocationLosses"]
        if not isinstance(relocation_losses, list):
            raise RuntimeError(f"{field}.generation.relocationLosses must be an array")
        for index, loss in enumerate(relocation_losses):
            loss_field = f"{field}.generation.relocationLosses[{index}]"
            _require_fields(
                loss,
                loss_field,
                {
                    "donorVehicleId",
                    "eligibleRiderCount",
                    "consideredRiderCount",
                    "omittedRiderCount",
                    "workUnits",
                    "evaluatedReceiverRouteCount",
                    "feasibleReceiverCount",
                    "evaluatedDonorPathCount",
                    "feasibleDonorCount",
                    "omittedUnexpandedDonorPathCount",
                    "omissionCountWasSaturated",
                    "ridersWithoutFeasibleReceiver",
                    "ridersWithInfeasibleDonorRoute",
                },
            )
            if loss["omissionCountWasSaturated"] is not False:
                raise RuntimeError(f"{loss_field}: saturated evidence is not auditable")
            for name, value in loss.items():
                if name not in {"donorVehicleId", "omissionCountWasSaturated"}:
                    _nonnegative_integer(value, f"{loss_field}.{name}")
            if loss["consideredRiderCount"] + loss["omittedRiderCount"] != loss["eligibleRiderCount"]:
                raise RuntimeError(f"{loss_field}: rider accounting differs")
            generation_work += loss["workUnits"]

    if evidence_version in {"1.1.0", "1.2.0", "1.3.0"}:
        breaches = generation["exogenousServiceQualityBreaches"]
        if not isinstance(breaches, list):
            raise RuntimeError(
                f"{field}.generation.exogenousServiceQualityBreaches must be an array"
            )
        previous_key = None
        breach_fields = {
            "vehicleId",
            "requestId",
            "code",
            "dimension",
            "contractualMilliseconds",
            "exogenousMilliseconds",
        }
        for index, breach in enumerate(breaches):
            breach_field = (
                f"{field}.generation.exogenousServiceQualityBreaches[{index}]"
            )
            _require_fields(breach, breach_field, breach_fields)
            for name in ("vehicleId", "requestId", "code", "dimension"):
                if not isinstance(breach[name], str) or not breach[name]:
                    raise RuntimeError(f"{breach_field}.{name} is invalid")
            for name in ("contractualMilliseconds", "exogenousMilliseconds"):
                _nonnegative_integer(breach[name], f"{breach_field}.{name}")
            if breach["exogenousMilliseconds"] <= breach["contractualMilliseconds"]:
                raise RuntimeError(
                    f"{breach_field}: exogenous value does not breach the contract"
                )
            key = (
                breach["vehicleId"],
                breach["requestId"],
                breach["code"],
                breach["dimension"],
            )
            if previous_key is not None and key <= previous_key:
                raise RuntimeError(
                    f"{breach_field}: breaches are duplicate or non-canonical"
                )
            previous_key = key

    pruned = evidence["prunedCandidates"]
    if not isinstance(pruned, list):
        raise RuntimeError(f"{field}.prunedCandidates must be an array")
    for index, witness in enumerate(pruned):
        witness_field = f"{field}.prunedCandidates[{index}]"
        _require_fields(
            witness,
            witness_field,
            {
                "candidateId",
                "vehicleId",
                "newRequestIds",
                "code",
                "commitmentWitnesses",
            },
            {"physicalWitness"},
        )
        for name in ("candidateId", "vehicleId", "code"):
            if not isinstance(witness[name], str) or not witness[name]:
                raise RuntimeError(f"{witness_field}.{name} is invalid")
        _require_unique_strings(
            witness["newRequestIds"],
            f"{witness_field}.newRequestIds",
        )
        if not isinstance(witness["commitmentWitnesses"], list):
            raise RuntimeError(f"{witness_field}.commitmentWitnesses is invalid")
        if "physicalWitness" in witness:
            _require_fields(
                witness["physicalWitness"],
                f"{witness_field}.physicalWitness",
                {"code", "vehicleId"},
                {"requestId", "stopId", "dimension", "expected", "actual"},
            )
        for commitment_index, commitment in enumerate(
            witness["commitmentWitnesses"]
        ):
            commitment_field = (
                f"{witness_field}.commitmentWitnesses[{commitment_index}]"
            )
            _require_fields(
                commitment,
                commitment_field,
                {"stage", "code"},
                {
                    "vehicleId",
                    "requestId",
                    "dimension",
                    "rule",
                    "limit",
                    "before",
                    "delta",
                    "after",
                },
            )

    selection = _require_fields(
        evidence["selection"],
        f"{field}.selection",
        {
            "consumedGenerationWorkUnits",
            "consumedValidationWorkUnits",
            "omittedCandidateCount",
            "omissionCountWasSaturated",
            "primarySolveStatus",
            "primarySolverDiagnostics",
            "finalSolveStatus",
            "finalSolverDiagnostics",
            "executionPath",
            "fallbackValidationAttempts",
            "primaryIncumbentRejected",
            "validationWitnesses",
        },
        {"omissionDigest"},
    )
    for name in (
        "consumedGenerationWorkUnits",
        "consumedValidationWorkUnits",
        "omittedCandidateCount",
        "fallbackValidationAttempts",
    ):
        _nonnegative_integer(selection[name], f"{field}.selection.{name}")
    if (
        selection["consumedGenerationWorkUnits"] != generation_work
        or selection["omittedCandidateCount"] != omitted_candidate_count
        or selection["omissionCountWasSaturated"] is not False
        or selection["primarySolveStatus"] != "optimal"
        or selection["finalSolveStatus"] != "optimal"
        or selection["executionPath"] != "validatedIncumbent"
        or selection["fallbackValidationAttempts"] != 0
        or selection["primaryIncumbentRejected"] is not False
        or selection["validationWitnesses"] != []
    ):
        raise RuntimeError(f"{field}: solver execution is not audited-optimal")
    _optional_hash(selection.get("omissionDigest"), f"{field}.selection.omissionDigest")
    if (selection["omittedCandidateCount"] == 0) != (
        "omissionDigest" not in selection
    ):
        raise RuntimeError(f"{field}: omission digest/count pairing differs")
    primary = _verify_audited_solver_diagnostics(
        selection["primarySolverDiagnostics"],
        f"{field}.selection.primarySolverDiagnostics",
    )
    final = _verify_audited_solver_diagnostics(
        selection["finalSolverDiagnostics"],
        f"{field}.selection.finalSolverDiagnostics",
    )
    if primary != final:
        raise RuntimeError(f"{field}: primary/final optimal diagnostics differ")
    if evidence_version == "1.2.0" or (
        evidence_version == "1.3.0" and "candidatePortfolio" in evidence
    ):
        _verify_retained_candidate_portfolio(
            evidence["candidatePortfolio"],
            f"{field}.candidatePortfolio",
        )
    return evidence_version


def _verify_retained_candidate_portfolio(portfolio, field):
    _require_fields(
        portfolio,
        field,
        {
            "portfolioVersion",
            "schemaId",
            "objectiveProfile",
            "generatedCandidateCount",
            "policyEligibleCandidateCount",
            "selectedCandidateIds",
            "selectionProblem",
            "candidates",
        },
    )
    if (
        portfolio["portfolioVersion"] != "1.0.0"
        or portfolio["schemaId"]
        != (
            "https://ridebound.local/schemas/wp13/v1/"
            "runner-retained-candidate-portfolio-evidence.schema.json"
        )
        or portfolio["objectiveProfile"]
        not in {"rollingCost", "revisionPenalty", "hardVector", "softHardHybrid"}
    ):
        raise RuntimeError(f"{field}: portfolio identity/profile differs")
    generated_count = _nonnegative_integer(
        portfolio["generatedCandidateCount"],
        f"{field}.generatedCandidateCount",
    )
    eligible_count = _nonnegative_integer(
        portfolio["policyEligibleCandidateCount"],
        f"{field}.policyEligibleCandidateCount",
    )
    if generated_count < 1 or eligible_count < 1:
        raise RuntimeError(f"{field}: portfolio counts must be positive")
    selected_ids = portfolio["selectedCandidateIds"]
    _require_unique_strings(selected_ids, f"{field}.selectedCandidateIds")

    problem = _require_fields(
        portfolio["selectionProblem"],
        f"{field}.selectionProblem",
        {"vehicleIds", "requestIds", "objectiveLevels"},
    )
    vehicle_ids = problem["vehicleIds"]
    request_ids = problem["requestIds"]
    _require_unique_strings(vehicle_ids, f"{field}.selectionProblem.vehicleIds")
    _require_unique_strings(request_ids, f"{field}.selectionProblem.requestIds")
    if (
        not vehicle_ids
        or vehicle_ids != sorted(vehicle_ids)
        or request_ids != sorted(request_ids)
    ):
        raise RuntimeError(f"{field}: problem IDs are empty/non-canonical")
    objectives = problem["objectiveLevels"]
    if not isinstance(objectives, list) or not objectives:
        raise RuntimeError(f"{field}.selectionProblem.objectiveLevels is invalid")
    objective_names = set()
    for index, objective in enumerate(objectives):
        objective_field = f"{field}.selectionProblem.objectiveLevels[{index}]"
        _require_fields(
            objective,
            objective_field,
            {"levelIndex", "name", "sense", "aggregation"},
        )
        if (
            objective["levelIndex"] != index
            or not isinstance(objective["name"], str)
            or not objective["name"]
            or objective["name"] in objective_names
            or objective["sense"] not in {"minimize", "maximize"}
            or objective["aggregation"] not in {"sum", "maximum"}
        ):
            raise RuntimeError(f"{objective_field}: objective contract differs")
        objective_names.add(objective["name"])

    candidates = portfolio["candidates"]
    if not isinstance(candidates, list) or len(candidates) != generated_count:
        raise RuntimeError(f"{field}: candidate count differs")
    candidate_by_id = {}
    previous_key = None
    eligible_seen = 0
    no_op_by_vehicle = {vehicle_id: [0, 0] for vehicle_id in vehicle_ids}
    for index, candidate in enumerate(candidates):
        candidate_field = f"{field}.candidates[{index}]"
        _require_fields(
            candidate,
            candidate_field,
            {
                "candidateId",
                "vehicleId",
                "newRequestIds",
                "isNoOp",
                "scheduleStrategy",
                "relocatedWaitMs",
                "route",
                "schedule",
                "policyEligibility",
            },
            {
                "certifiedForwardSlackMs",
                "repairedIncumbentRequestId",
                "objectiveContributions",
                "relocatedRequestId",
                "relocationRole",
            },
        )
        if ("relocatedRequestId" in candidate) != ("relocationRole" in candidate) or candidate.get(
            "relocationRole", "donor"
        ) not in {"donor", "receiver"}:
            raise RuntimeError(f"{candidate_field}: relocation fields are inconsistent")
        candidate_id = candidate["candidateId"]
        vehicle_id = candidate["vehicleId"]
        if (
            not isinstance(candidate_id, str)
            or not candidate_id
            or candidate_id in candidate_by_id
            or vehicle_id not in no_op_by_vehicle
        ):
            raise RuntimeError(f"{candidate_field}: candidate identity differs")
        key = (vehicle_id, candidate_id)
        if previous_key is not None and key <= previous_key:
            raise RuntimeError(f"{candidate_field}: candidate order differs")
        previous_key = key
        request_values = candidate["newRequestIds"]
        _require_unique_strings(request_values, f"{candidate_field}.newRequestIds")
        if request_values != sorted(request_values):
            raise RuntimeError(f"{candidate_field}: request order differs")
        if not isinstance(candidate["isNoOp"], bool) or (
            candidate["isNoOp"] and request_values
        ):
            raise RuntimeError(f"{candidate_field}: no-op contract differs")
        if candidate["scheduleStrategy"] not in {
            "earliestFeasible",
            "originHoldRelocatedWait",
        }:
            raise RuntimeError(f"{candidate_field}: schedule strategy differs")
        _nonnegative_integer(
            candidate["relocatedWaitMs"],
            f"{candidate_field}.relocatedWaitMs",
        )
        if "certifiedForwardSlackMs" in candidate:
            _nonnegative_integer(
                candidate["certifiedForwardSlackMs"],
                f"{candidate_field}.certifiedForwardSlackMs",
            )
        eligibility = candidate["policyEligibility"]
        has_objectives = "objectiveContributions" in candidate
        if eligibility not in {"eligible", "pruned"} or (
            (eligibility == "eligible") != has_objectives
        ):
            raise RuntimeError(f"{candidate_field}: eligibility/objective shape differs")
        if eligibility == "eligible":
            eligible_seen += 1
            if any(request_id not in request_ids for request_id in request_values):
                raise RuntimeError(f"{candidate_field}: eligible request is undeclared")
            contributions = candidate["objectiveContributions"]
            if (
                not isinstance(contributions, list)
                or len(contributions) != len(objectives)
            ):
                raise RuntimeError(f"{candidate_field}: objective count differs")
            for objective_index, value in enumerate(contributions):
                _nonnegative_integer(
                    value,
                    f"{candidate_field}.objectiveContributions[{objective_index}]",
                )
        if candidate["isNoOp"]:
            no_op_by_vehicle[vehicle_id][0] += 1
            if eligibility == "eligible":
                no_op_by_vehicle[vehicle_id][1] += 1
        _verify_candidate_route_schedule(candidate, candidate_field)
        candidate_by_id[candidate_id] = candidate
    if eligible_seen != eligible_count or any(
        counts != [1, 1] for counts in no_op_by_vehicle.values()
    ):
        raise RuntimeError(f"{field}: eligible/no-op counts differ")
    if len(selected_ids) != len(vehicle_ids):
        raise RuntimeError(f"{field}: selected count differs")
    selected_requests = set()
    for index, candidate_id in enumerate(selected_ids):
        candidate = candidate_by_id.get(candidate_id)
        if (
            candidate is None
            or candidate["policyEligibility"] != "eligible"
            or candidate["vehicleId"] != vehicle_ids[index]
            or any(
                request_id in selected_requests
                for request_id in candidate["newRequestIds"]
            )
        ):
            raise RuntimeError(f"{field}.selectedCandidateIds[{index}] differs")
        selected_requests.update(candidate["newRequestIds"])


def _verify_candidate_route_schedule(candidate, field):
    route = _require_fields(
        candidate["route"],
        f"{field}.route",
        {"planVersion", "executedStopCount", "frozenPrefix", "mutableSuffix"},
    )
    _nonnegative_integer(route["planVersion"], f"{field}.route.planVersion")
    executed = _nonnegative_integer(
        route["executedStopCount"],
        f"{field}.route.executedStopCount",
    )
    all_stop_ids = set()
    route_ids = {}
    for name in ("frozenPrefix", "mutableSuffix"):
        stops = route[name]
        if not isinstance(stops, list):
            raise RuntimeError(f"{field}.route.{name} must be an array")
        ids = []
        for index, stop in enumerate(stops):
            stop_field = f"{field}.route.{name}[{index}]"
            _require_fields(
                stop,
                stop_field,
                {"stopId", "nodeId", "kind", "serviceDurationMs"},
                {"requestId"},
            )
            stop_id = stop["stopId"]
            kind = stop["kind"]
            if (
                not isinstance(stop_id, str)
                or not stop_id
                or stop_id in all_stop_ids
                or not isinstance(stop["nodeId"], str)
                or not stop["nodeId"]
                or kind not in {"waypoint", "pickup", "dropOff"}
                or (kind == "waypoint") == ("requestId" in stop)
                or "requestId" in stop
                and (
                    not isinstance(stop["requestId"], str)
                    or not stop["requestId"]
                )
            ):
                raise RuntimeError(f"{stop_field}: route stop contract differs")
            _nonnegative_integer(
                stop["serviceDurationMs"],
                f"{stop_field}.serviceDurationMs",
            )
            all_stop_ids.add(stop_id)
            ids.append(stop_id)
        route_ids[name] = ids
    if executed > len(route_ids["frozenPrefix"]):
        raise RuntimeError(f"{field}.route.executedStopCount is outside prefix")
    expected_schedule_ids = (
        route_ids["frozenPrefix"][executed:] + route_ids["mutableSuffix"]
    )
    schedule = _require_fields(
        candidate["schedule"],
        f"{field}.schedule",
        {"operationalCost", "stops"},
    )
    _nonnegative_integer(
        schedule["operationalCost"],
        f"{field}.schedule.operationalCost",
    )
    if not isinstance(schedule["stops"], list):
        raise RuntimeError(f"{field}.schedule.stops must be an array")
    actual_schedule_ids = []
    previous_departure = None
    for index, stop in enumerate(schedule["stops"]):
        stop_field = f"{field}.schedule.stops[{index}]"
        _require_fields(
            stop,
            stop_field,
            {
                "stopId",
                "arrivalTimeMs",
                "serviceStartTimeMs",
                "departureTimeMs",
            },
        )
        arrival = _nonnegative_integer(
            stop["arrivalTimeMs"],
            f"{stop_field}.arrivalTimeMs",
        )
        service = _nonnegative_integer(
            stop["serviceStartTimeMs"],
            f"{stop_field}.serviceStartTimeMs",
        )
        departure = _nonnegative_integer(
            stop["departureTimeMs"],
            f"{stop_field}.departureTimeMs",
        )
        if (
            arrival > service
            or service > departure
            or previous_departure is not None
            and previous_departure > arrival
        ):
            raise RuntimeError(f"{stop_field}: schedule times differ")
        actual_schedule_ids.append(stop["stopId"])
        previous_departure = departure
    if actual_schedule_ids != expected_schedule_ids:
        raise RuntimeError(f"{field}: schedule/remaining-route IDs differ")


def _verify_audited_solver_diagnostics(diagnostics, field):
    _require_fields(
        diagnostics,
        field,
        {
            "consumedWorkUnits",
            "consumedDeterministicTimeMicros",
            "objectiveBounds",
        },
        {"detailCode"},
    )
    _nonnegative_integer(diagnostics["consumedWorkUnits"], f"{field}.consumedWorkUnits")
    _nonnegative_integer(
        diagnostics["consumedDeterministicTimeMicros"],
        f"{field}.consumedDeterministicTimeMicros",
    )
    bounds = diagnostics["objectiveBounds"]
    if not isinstance(bounds, list) or not bounds:
        raise RuntimeError(f"{field}.objectiveBounds must be non-empty")
    for index, bound in enumerate(bounds):
        bound_field = f"{field}.objectiveBounds[{index}]"
        _require_fields(
            bound,
            bound_field,
            {
                "levelIndex",
                "objectiveName",
                "incumbentValue",
                "bestBound",
                "gapNumerator",
                "gapDenominator",
                "isProvenOptimal",
            },
        )
        for name in (
            "levelIndex",
            "incumbentValue",
            "bestBound",
            "gapNumerator",
            "gapDenominator",
        ):
            _nonnegative_integer(bound[name], f"{bound_field}.{name}")
        if (
            bound["levelIndex"] != index
            or not isinstance(bound["objectiveName"], str)
            or not bound["objectiveName"]
            or bound["incumbentValue"] != bound["bestBound"]
            or bound["gapNumerator"] != 0
            or bound["gapDenominator"] < 1
            or bound["isProvenOptimal"] is not True
        ):
            raise RuntimeError(f"{bound_field}: objective is not exactly optimal")
    return diagnostics


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _read_json(path):
    with path.open("rb") as source:
        value = _loads(source.read())
    if not isinstance(value, dict):
        raise RuntimeError(f"{path.name}: root is not an object")
    return value


def decode_transcript(path):
    decoded = []
    with path.open("rb") as source:
        for expected_ordinal, raw_line in enumerate(source, 1):
            if not raw_line.endswith(b"\n") or raw_line.endswith(b"\r\n"):
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: non-canonical LF framing"
                )
            line = raw_line[:-1]
            record = _loads(line)
            if not isinstance(record, dict) or set(record) != _RECORD_FIELDS:
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: transcript fields differ"
                )
            if _canonical(record) != line:
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: record is not canonical JSON"
                )
            if (
                record["schemaVersion"] != "1.0.0"
                or record["ordinal"] != expected_ordinal
                or record["direction"]
                not in {"adapterToRunner", "runnerToAdapter"}
                or isinstance(record["frameLengthBytes"], bool)
                or not isinstance(record["frameLengthBytes"], int)
                or record["frameLengthBytes"] < 1
                or not isinstance(record["frameSha256"], str)
                or _HASH.fullmatch(record["frameSha256"]) is None
                or not isinstance(record["frameBase64"], str)
            ):
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: invalid transcript metadata"
                )
            try:
                frame = base64.b64decode(
                    record["frameBase64"],
                    validate=True,
                )
            except (ValueError, TypeError) as exc:
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: invalid frame base64"
                ) from exc
            if (
                len(frame) != record["frameLengthBytes"]
                or hashlib.sha256(frame).hexdigest() != record["frameSha256"]
            ):
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: frame length/hash mismatch"
                )
            envelope = _loads(frame)
            if not isinstance(envelope, dict) or _canonical(envelope) != frame:
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: frame is not canonical JSON"
                )
            decoded.append((record["direction"], envelope))
    if not decoded:
        raise RuntimeError(f"{path.name}: transcript is empty")
    return decoded


def _expect(frames, index, direction, message_type):
    try:
        actual_direction, envelope = frames[index]
    except IndexError as exc:
        raise RuntimeError(
            f"transcript ended before {direction}/{message_type}"
        ) from exc
    if actual_direction != direction or envelope.get("messageType") != message_type:
        raise RuntimeError(
            f"frame {index + 1}: expected {direction}/{message_type}; "
            f"actual={actual_direction}/{envelope.get('messageType')!r}"
        )
    return envelope


def verify_transcript(
    path,
    report,
    include_behavioral_hash=False,
    require_audited_solver_evidence=False,
    require_retained_candidate_portfolio=False,
):
    frames = decode_transcript(path)
    hello = _expect(frames, 0, "adapterToRunner", "hello")
    hello_ack = _expect(frames, 1, "runnerToAdapter", "helloAck")
    initialize = _expect(frames, 2, "adapterToRunner", "initializeRun")
    initialized = _expect(frames, 3, "runnerToAdapter", "initialized")
    if hello.get("schemaVersion") != "1.0.0" or hello_ack.get(
        "schemaVersion"
    ) != "1.0.0":
        raise RuntimeError("hello schema negotiation drifted")
    run_id = initialize.get("runId")
    scenario_id = initialize.get("scenarioId")
    if (
        not isinstance(run_id, str)
        or not run_id
        or not isinstance(scenario_id, str)
        or not scenario_id
        or initialized.get("runId") != run_id
        or initialized.get("scenarioId") != scenario_id
    ):
        raise RuntimeError("initialize identity mismatch")
    manifest = initialize.get("payload", {}).get("manifest")
    if not isinstance(manifest, dict):
        raise RuntimeError("initialize manifest is missing")
    manifest_hash = initialized.get("payload", {}).get("manifestHash")
    if manifest_hash != _manifest_hash(manifest):
        raise RuntimeError("initialized manifest hash is not independently valid")
    if manifest_hash != report["semantic"]["manifestHash"]:
        raise RuntimeError("initialized/report manifest hash mismatch")

    initial_state = initialized.get("payload", {}).get("initialStateIdentity")
    if (
        not isinstance(initial_state, dict)
        or initial_state.get("epochId") != 0
        or initial_state.get("nextEventSeq") != 1
        or initial_state.get("simTimeMs") != 0
        or not isinstance(initial_state.get("stateHash"), str)
        or _HASH.fullmatch(initial_state["stateHash"]) is None
    ):
        raise RuntimeError("initialized state identity is invalid")

    next_epoch = 1
    next_event_sequence = 1
    prior_sim_time = 0
    prior_decision_hash = None
    prior_state_after = None
    lifecycle = {
        "requestArrived": [],
        "bookingConfirmed": [],
        "passengerBoarded": [],
        "passengerAlighted": [],
        "requestCancelledBeforeAcceptance": [],
        "requestCancelledAfterAcceptance": [],
    }
    accepted = []
    rejected = []
    publications = []
    behavioral_decisions = []
    checkpoint = None
    retained_portfolio_evidence_count = 0
    index = 4
    while index < len(frames):
        direction, envelope = frames[index]
        message_type = envelope.get("messageType")
        if direction != "adapterToRunner":
            raise RuntimeError(f"frame {index + 1}: unexpected unpaired Runner output")
        if message_type == "shutdown":
            if index != len(frames) - 1:
                raise RuntimeError("shutdown is not the final transcript frame")
            index += 1
            break
        if message_type == "checkpoint":
            checkpoint = _expect(
                frames,
                index + 1,
                "runnerToAdapter",
                "checkpoint",
            )
            index += 2
            continue
        if message_type != "eventBatch":
            raise RuntimeError(
                f"frame {index + 1}: unexpected adapter message {message_type!r}"
            )
        batch = envelope
        if (
            batch.get("runId") != run_id
            or batch.get("scenarioId") != scenario_id
            or batch.get("epochId") != next_epoch
            or isinstance(batch.get("simTimeMs"), bool)
            or not isinstance(batch.get("simTimeMs"), int)
            or batch["simTimeMs"] < prior_sim_time
        ):
            raise RuntimeError(f"epoch {next_epoch}: context/time is not gapless")
        events = batch.get("payload", {}).get("events")
        if not isinstance(events, list) or not events:
            raise RuntimeError(f"epoch {next_epoch}: event list is empty/invalid")
        for event in events:
            if event.get("eventSeq") != next_event_sequence:
                raise RuntimeError(
                    f"event sequence gap: expected={next_event_sequence}; "
                    f"actual={event.get('eventSeq')!r}"
                )
            event_type = event.get("eventType")
            payload = event.get("payload")
            if not isinstance(payload, dict):
                raise RuntimeError(f"event {next_event_sequence}: invalid payload")
            if event_type in lifecycle:
                request_id = (
                    payload.get("request", {}).get("requestId")
                    if event_type == "requestArrived"
                    else payload.get("requestId")
                )
                if not isinstance(request_id, str) or not request_id:
                    raise RuntimeError(
                        f"event {next_event_sequence}: lifecycle identity missing"
                    )
                lifecycle[event_type].append(request_id)
            next_event_sequence += 1

        decision = _expect(
            frames,
            index + 1,
            "runnerToAdapter",
            "decision",
        )
        if any(
            decision.get(field) != batch.get(field)
            for field in ("runId", "scenarioId", "epochId", "simTimeMs")
        ):
            raise RuntimeError(f"epoch {next_epoch}: decision context mismatch")
        payload = decision.get("payload", {})
        if not isinstance(payload, dict):
            raise RuntimeError(f"epoch {next_epoch}: decision payload is invalid")
        decision_hash = payload.get("decisionHash")
        if not isinstance(decision_hash, str) or _HASH.fullmatch(decision_hash) is None:
            raise RuntimeError(f"epoch {next_epoch}: invalid decision hash")
        expected_previous_decision = (
            _ZERO_HASH if prior_decision_hash is None else prior_decision_hash
        )
        expected_state_before = (
            initial_state["stateHash"]
            if prior_state_after is None
            else prior_state_after
        )
        if payload.get("previousDecisionHash") != expected_previous_decision:
            raise RuntimeError(f"epoch {next_epoch}: decision hash chain broke")
        if payload.get("stateBeforeHash") != expected_state_before:
            raise RuntimeError(f"epoch {next_epoch}: state hash chain broke")
        state_after = payload.get("stateAfterHash")
        if not isinstance(state_after, str) or _HASH.fullmatch(state_after) is None:
            raise RuntimeError(f"epoch {next_epoch}: invalid stateAfterHash")
        actions = payload.get("actions")
        if not isinstance(actions, list):
            raise RuntimeError(f"epoch {next_epoch}: actions are invalid")
        solver = payload.get("solver")
        if (
            not isinstance(solver, dict)
            or not isinstance(solver.get("status"), str)
            or not solver["status"]
        ):
            raise RuntimeError(f"epoch {next_epoch}: solver status is invalid")
        if require_audited_solver_evidence:
            evidence_version = _verify_audited_solver_evidence(solver, next_epoch)
            has_portfolio = evidence_version == "1.2.0" or (
                evidence_version == "1.3.0"
                and "candidatePortfolio" in solver["executionEvidence"]
            )
            if require_retained_candidate_portfolio and not has_portfolio:
                raise RuntimeError(
                    f"epoch {next_epoch}: retained portfolio evidence is required"
                )
            if has_portfolio:
                retained_portfolio_evidence_count += 1
        behavioral_decisions.append(
            {
                "epochId": next_epoch,
                "simTimeMs": batch["simTimeMs"],
                "solverStatus": solver["status"],
                "actions": _without_generated_action_fields(actions),
            }
        )
        for action in actions:
            action_type = action.get("decisionType")
            action_payload = action.get("payload", {})
            if action_type == "requestAccepted":
                accepted.append(action_payload.get("requestId"))
            elif action_type == "requestRejected":
                rejected.append(action_payload.get("requestId"))
            elif action_type == "promisePublished":
                promise = action_payload.get("promise", {})
                publications.append(
                    {
                        "requestId": promise.get("requestId"),
                        "publicationId": action_payload.get("publicationId"),
                        "promiseVersion": action_payload.get("promiseVersion"),
                        "reasonCode": action_payload.get("reasonCode"),
                        "sourceEventSeq": action_payload.get("sourceEventSeq"),
                    }
                )
        acknowledgement = _expect(
            frames,
            index + 2,
            "adapterToRunner",
            "decisionApplied",
        )
        if (
            any(
                acknowledgement.get(field) != decision.get(field)
                for field in ("runId", "scenarioId", "epochId", "simTimeMs")
            )
            or acknowledgement.get("payload") != {"decisionHash": decision_hash}
        ):
            raise RuntimeError(f"epoch {next_epoch}: ACK is not exact")
        prior_decision_hash = decision_hash
        prior_state_after = state_after
        prior_sim_time = batch["simTimeMs"]
        next_epoch += 1
        index += 3

    if index != len(frames) or checkpoint is None:
        raise RuntimeError("transcript lacks final checkpoint/shutdown closure")
    checkpoint_payload = checkpoint.get("payload", {})
    content = checkpoint_payload.get("content")
    if not isinstance(content, dict):
        raise RuntimeError("final checkpoint content is missing")
    if (
        checkpoint.get("schemaVersion") != "1.0.0"
        or checkpoint.get("runId") != run_id
        or checkpoint.get("scenarioId") != scenario_id
        or checkpoint_payload.get("checkpointVersion") != "1.0.0"
        or checkpoint_payload.get("checkpointHash") != _checkpoint_hash(content)
        or content.get("manifestHash") != manifest_hash
        or content.get("previousDecisionHash") != prior_decision_hash
        or content.get("stateHash") != prior_state_after
        or content.get("appliedEpoch") != next_epoch - 1
        or content.get("nextEventSeq") != next_event_sequence
        or content.get("simTimeMs") != prior_sim_time
    ):
        raise RuntimeError("final checkpoint identity/state boundary mismatch")

    semantic = report.get("semantic")
    if (
        not isinstance(semantic, dict)
        or report.get("semanticHash") != _semantic_hash(semantic)
        or isinstance(semantic.get("requestCount"), bool)
        or not isinstance(semantic.get("requestCount"), int)
        or semantic["requestCount"] < 0
        or semantic["nextEpoch"] != next_epoch
        or semantic["nextEventSeq"] != next_event_sequence
        or semantic["finalSimulationTimeMs"] != prior_sim_time
        or semantic["travelSnapshotVersion"] != 1
        or not isinstance(semantic.get("finalVehiclePositions"), list)
        or not isinstance(semantic.get("checkpointBindingHash"), str)
        or _HASH.fullmatch(semantic["checkpointBindingHash"]) is None
    ):
        raise RuntimeError("report/transcript terminal counters differ")
    arrived = _require_unique_strings(
        lifecycle["requestArrived"],
        "requestArrived",
    )
    booked = _require_unique_strings(
        lifecycle["bookingConfirmed"],
        "bookingConfirmed",
    )
    boarded = _require_unique_strings(
        lifecycle["passengerBoarded"],
        "passengerBoarded",
    )
    alighted = _require_unique_strings(
        lifecycle["passengerAlighted"],
        "passengerAlighted",
    )
    accepted_set = _require_unique_strings(accepted, "requestAccepted")
    rejected_set = _require_unique_strings(rejected, "requestRejected")
    semantic_accepted = _require_unique_strings(
        semantic.get("acceptedRequestIds"),
        "semantic.acceptedRequestIds",
    )
    semantic_rejected = _require_unique_strings(
        semantic.get("rejectedRequestIds"),
        "semantic.rejectedRequestIds",
    )
    if not (
        len(arrived) == semantic["requestCount"]
        and not (accepted_set & rejected_set)
        and accepted_set | rejected_set == arrived
        and booked == accepted_set
        and boarded == booked
        and alighted == boarded
        and not lifecycle["requestCancelledBeforeAcceptance"]
        and not lifecycle["requestCancelledAfterAcceptance"]
        and not (semantic_accepted & semantic_rejected)
        and len(semantic_accepted) == len(accepted_set)
        and len(semantic_rejected) == len(rejected_set)
        and len(semantic_accepted | semantic_rejected)
        == semantic["requestCount"]
    ):
        raise RuntimeError("request lifecycle/outcome conservation failed")
    if len(publications) != semantic["publicationCount"]:
        raise RuntimeError("publication count differs from raw decisions")
    publication_digest = hashlib.sha256(_canonical(publications)).hexdigest()
    if publication_digest != semantic["publicationDigest"]:
        raise RuntimeError("publication digest differs from raw decisions")
    online_state = content.get("onlineState")
    if not isinstance(online_state, dict):
        raise RuntimeError("terminal checkpoint did not drain requests/fleet")
    checkpoint_requests = online_state.get("requests")
    checkpoint_vehicles = online_state.get("vehicles")
    if not isinstance(checkpoint_requests, list) or not isinstance(
        checkpoint_vehicles,
        list,
    ):
        raise RuntimeError("terminal checkpoint did not drain requests/fleet")
    checkpoint_outcomes = {}
    for request in checkpoint_requests:
        if not isinstance(request, dict):
            raise RuntimeError("terminal checkpoint request is invalid")
        request_id = request.get("requestId")
        lifecycle_state = request.get("lifecycle")
        if (
            not isinstance(request_id, str)
            or not request_id
            or request_id in checkpoint_outcomes
            or lifecycle_state not in {"completed", "rejected"}
        ):
            raise RuntimeError("terminal checkpoint request is invalid")
        checkpoint_outcomes[request_id] = lifecycle_state
    completed_ids = {
        request_id
        for request_id, lifecycle_state in checkpoint_outcomes.items()
        if lifecycle_state == "completed"
    }
    rejected_ids = {
        request_id
        for request_id, lifecycle_state in checkpoint_outcomes.items()
        if lifecycle_state == "rejected"
    }
    if (
        len(checkpoint_outcomes) != semantic["requestCount"]
        or completed_ids != accepted_set
        or rejected_ids != rejected_set
        or len(checkpoint_vehicles) != len(semantic["finalVehiclePositions"])
    ):
        raise RuntimeError("terminal checkpoint did not drain requests/fleet")
    result = {
        "frameCount": len(frames),
        "epochCount": next_epoch - 1,
        "eventCount": next_event_sequence - 1,
        "requestCount": len(arrived),
        "publicationCount": len(publications),
        "checkpointHash": checkpoint_payload.get("checkpointHash"),
    }
    if include_behavioral_hash:
        result["behavioralProjectionHash"] = _behavioral_projection_hash(
            behavioral_decisions
        )
    if require_retained_candidate_portfolio:
        result["retainedPortfolioEvidenceCount"] = (
            retained_portfolio_evidence_count
        )
    return result


def verify_bundle(
    directory,
    include_behavioral_hash=False,
    require_audited_solver_evidence=False,
    require_retained_candidate_portfolio=False,
):
    manifest_path = directory / "bundle-manifest.json"
    manifest = _read_json(manifest_path)
    if set(manifest) != {"schemaVersion", "bundleType", "files"} or (
        manifest["schemaVersion"] != "1.0.0"
        or manifest["bundleType"] != "ridebound-wp7-actual-fleetpy-medium-v1"
        or not isinstance(manifest["files"], list)
    ):
        raise RuntimeError("bundle manifest contract differs")
    declared = {}
    for item in manifest["files"]:
        if set(item) != {"path", "lengthBytes", "sha256"}:
            raise RuntimeError("bundle manifest file record differs")
        relative = item["path"]
        if (
            not isinstance(relative, str)
            or not relative
            or pathlib.PurePosixPath(relative).is_absolute()
            or ".." in pathlib.PurePosixPath(relative).parts
            or relative in declared
            or isinstance(item.get("lengthBytes"), bool)
            or not isinstance(item.get("lengthBytes"), int)
            or item["lengthBytes"] < 0
            or not isinstance(item.get("sha256"), str)
            or _HASH.fullmatch(item["sha256"]) is None
        ):
            raise RuntimeError("bundle manifest path is invalid/duplicate")
        declared[relative] = item
    actual = {
        path.relative_to(directory).as_posix()
        for path in directory.rglob("*")
        if path.is_file() and path != manifest_path
    }
    if actual != set(declared):
        raise RuntimeError(
            f"bundle inventory differs: missing={sorted(set(declared)-actual)!r}; "
            f"extra={sorted(actual-set(declared))!r}"
        )
    for relative, item in declared.items():
        path = directory / pathlib.PurePosixPath(relative)
        if (
            path.stat().st_size != item["lengthBytes"]
            or _sha256(path) != item["sha256"]
        ):
            raise RuntimeError(f"bundle artifact drift: {relative}")
    summary = _read_json(directory / "summary.json")
    if (
        summary.get("status") != "pass"
        or isinstance(summary.get("repeatCount"), bool)
        or not isinstance(summary.get("repeatCount"), int)
        or summary["repeatCount"] < 1
        or not isinstance(summary.get("semanticHash"), str)
        or _HASH.fullmatch(summary["semanticHash"]) is None
    ):
        raise RuntimeError("medium summary is not a pass")
    runs = []
    for repeat in range(summary["repeatCount"]):
        report = _read_json(directory / f"run-{repeat:02d}.json")
        if (
            report.get("status") != "succeeded"
            or report.get("repeat") != repeat
            or report.get("semanticHash") != summary.get("semanticHash")
            or report.get("artifactReceiptsEqual") is not True
        ):
            raise RuntimeError(f"run-{repeat:02d}: report/summary mismatch")
        runs.append(
            verify_transcript(
                directory / f"transcript-{repeat:02d}.ndjson",
                report,
                include_behavioral_hash,
                require_audited_solver_evidence,
                require_retained_candidate_portfolio,
            )
        )
    result = {
        "schemaVersion": "1.0.0",
        "status": "pass",
        "repeatCount": len(runs),
        "semanticHash": summary["semanticHash"],
        "runs": runs,
    }
    if include_behavioral_hash:
        behavioral_hashes = {
            run["behavioralProjectionHash"]
            for run in runs
        }
        if len(behavioral_hashes) != 1:
            raise RuntimeError("behavioral projection repeats diverged")
        result["behavioralProjectionHash"] = next(iter(behavioral_hashes))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle", required=True, type=pathlib.Path)
    parser.add_argument("--include-behavioral-hash", action="store_true")
    parser.add_argument(
        "--require-audited-solver-evidence",
        action="store_true",
    )
    parser.add_argument(
        "--require-retained-candidate-portfolio",
        action="store_true",
    )
    arguments = parser.parse_args()
    if (
        arguments.require_retained_candidate_portfolio
        and not arguments.require_audited_solver_evidence
    ):
        parser.error(
            "--require-retained-candidate-portfolio requires audited evidence"
        )
    result = verify_bundle(
        arguments.bundle.resolve(),
        arguments.include_behavioral_hash,
        arguments.require_audited_solver_evidence,
        arguments.require_retained_candidate_portfolio,
    )
    print(json.dumps(result, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
