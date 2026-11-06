"""GĐ2 gate (ADR-089): replay a recorded job through a Runner whose WP4 configuration adds `auditHash: light-v1`,
and check that every Runner frame equals the recorded one once every member named *Hash (at any depth) is removed.

The light configuration changes the WP4 content hash, so the initializeRun manifest's policyConfigurationHash is
rebound (Wp4PolicyConfigurationBinding.cs: SHA-256 of the domain, the commitment configuration hash and the WP4
configuration hash, each configuration hashed as canonical JSON). The tool first recomputes the recorded binding from
the original files and stops if it differs, so the rebinding is checked against the archive. Each decisionApplied
frame carries the light decision hash the Runner just returned. Everything else is sent byte for byte.

Usage: python -I -B replay_light.py --runner DIR --policy-config F --wp4-config F --transcript F --out DIR
                                   [--heap-hex 0x38000000] [--max-line 67108864] [--evidence keep|off]
Writes only into --out (a new directory): wp4-light.json, result.json, runner-stderr.txt.
"""
import argparse
import base64
import hashlib
import io
import json
import pathlib
import subprocess
import threading
import time

import psutil

DOTNET = r"C:\Program Files\dotnet\dotnet.exe"
BINDING_DOMAIN = b"RideBound.Wp4RunnerConfigurationBinding.v1\0"


def canonical(value) -> bytes:
    """Canonical JSON as CanonicalJson.cs writes it for these configuration files (ASCII keys, integers, strings)."""
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def content_hash(value) -> bytes:
    return hashlib.sha256(canonical(value)).digest()


def binding(commitment, wp4) -> str:
    return hashlib.sha256(BINDING_DOMAIN + content_hash(commitment) + content_hash(wp4)).hexdigest()


def strip_hashes(value):
    if isinstance(value, dict):
        return {k: strip_hashes(v) for k, v in value.items() if not k.endswith("Hash")}
    if isinstance(value, list):
        return [strip_hashes(v) for v in value]
    return value


def main():
    p = argparse.ArgumentParser()
    for name in ("--runner", "--policy-config", "--wp4-config", "--transcript", "--out"):
        p.add_argument(name, required=True)
    p.add_argument("--heap-hex", default="0x38000000")
    p.add_argument("--max-line", type=int, default=64 * 1024 * 1024)
    p.add_argument("--evidence", choices=("keep", "off"), default="keep")
    p.add_argument("--stop-before-checkpoint", action="store_true",
                   help="do not send the final checkpoint request (a Via checkpoint line is ~400 MB to compare)")
    p.add_argument("--env", action="append", default=[], help="extra KEY=VALUE for the Runner")
    a = p.parse_args()
    out = pathlib.Path(a.out)
    out.mkdir(parents=True, exist_ok=False)

    commitment = json.loads(pathlib.Path(a.policy_config).read_text(encoding="utf-8"))
    original = json.loads(pathlib.Path(a.wp4_config).read_text(encoding="utf-8"))
    light = dict(original, auditHash="light-v1")
    if a.evidence == "off":
        light["emitSolverExecutionEvidence"] = False
    light_path = out / "wp4-light.json"
    light_path.write_text(json.dumps(light, indent=2), encoding="utf-8")
    original_binding, light_binding = binding(commitment, original), binding(commitment, light)

    command = [DOTNET, str(pathlib.Path(a.runner) / "RideBound.Runner.dll"), "--mode", "commitment",
               "--policy-config", a.policy_config, "--wp4-config", str(light_path),
               "--solver-seed-source", "manifest-master-seed", "--maximum-line-bytes", str(a.max_line)]
    env = dict(__import__("os").environ, DOTNET_GCHeapHardLimit=a.heap_hex)
    env.update(kv.split("=", 1) for kv in a.env)
    (out / "runner-pid.txt").write_text("", encoding="utf-8")
    started = time.time()
    result = {"transcript": a.transcript, "evidence": a.evidence, "framesCompared": 0, "framesEqualWithoutHashes": 0,
              "decisions": 0, "firstDifference": None, "bindingChecked": False}
    with open(out / "runner-stderr.txt", "xb") as err:
        proc = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=err, env=env, bufsize=0)
        reader = io.BufferedReader(proc.stdout, buffer_size=1 << 20)
        writer = io.BufferedWriter(proc.stdin, buffer_size=1 << 20)
        ps = psutil.Process(proc.pid)
        (out / "runner-pid.txt").write_text(str(proc.pid), encoding="utf-8")
        peak = {"rss": 0}

        def watch():
            while proc.poll() is None:
                try:
                    peak["rss"] = max(peak["rss"], ps.memory_info().rss)
                except psutil.Error:
                    pass
                time.sleep(1)

        threading.Thread(target=watch, daemon=True).start()
        last_light_hash = None
        with open(a.transcript, encoding="utf-8") as fh:
            for line in fh:
                rec = json.loads(line)
                frame = base64.b64decode(rec["frameBase64"])
                if rec["direction"] == "adapterToRunner":
                    head = frame[:200]
                    if a.stop_before_checkpoint and b'"messageType":"checkpoint"' in head:
                        result["stoppedBeforeCheckpoint"] = True
                        break
                    if b'"messageType":"initializeRun"' in head or b'"decisionApplied"' in head:
                        message = json.loads(frame)
                        if message["messageType"] == "initializeRun":
                            manifest = message["payload"]["manifest"]
                            if manifest["policyConfigurationHash"] != original_binding:
                                result["firstDifference"] = {"reason": "recorded binding differs from the recomputed one",
                                                             "recorded": manifest["policyConfigurationHash"],
                                                             "recomputed": original_binding}
                                break
                            result["bindingChecked"] = True
                            manifest["policyConfigurationHash"] = light_binding
                        else:
                            message["payload"]["decisionHash"] = last_light_hash
                        frame = json.dumps(message, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
                    writer.write(frame + b"\n")
                    writer.flush()
                    continue
                got = reader.readline(a.max_line + 2)
                if not got:
                    result["firstDifference"] = {"ordinal": rec["ordinal"], "reason": "runner closed stdout"}
                    break
                got_message = json.loads(got)
                recorded = json.loads(frame)
                result["framesCompared"] += 1
                if got_message.get("messageType") == "decision":
                    result["decisions"] += 1
                    last_light_hash = got_message["payload"]["decisionHash"]
                if a.evidence == "off" and got_message.get("messageType") == "decision":
                    recorded["payload"]["solver"].pop("executionEvidence", None)
                if strip_hashes(got_message) == strip_hashes(recorded):
                    result["framesEqualWithoutHashes"] += 1
                else:
                    result["firstDifference"] = {"ordinal": rec["ordinal"], "messageType": recorded.get("messageType")}
                    break
        try:
            t = ps.cpu_times()
            result["runnerCpuSeconds"] = round(t.user + t.system, 1)
        except psutil.Error:
            pass
        try:
            writer.close()
        except OSError:
            pass
        try:
            proc.wait(timeout=120)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait()
    result["wallSeconds"] = round(time.time() - started, 1)
    result["runnerPeakRssMiB"] = round(peak["rss"] / 2**20)
    result["runnerExitCode"] = proc.returncode
    result["equal"] = (result["firstDifference"] is None and result["bindingChecked"]
                       and result["framesCompared"] == result["framesEqualWithoutHashes"] > 0)
    (out / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "transcript"}))


if __name__ == "__main__":
    main()
