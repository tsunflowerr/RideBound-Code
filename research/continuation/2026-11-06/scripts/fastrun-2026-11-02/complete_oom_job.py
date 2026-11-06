"""Complete a Via job whose Runner ran out of memory on the final checkpoint (VIA-LOG V4), with the fast-runner build.

1. Replays the recorded adapter->Runner frames through the new Runner in the default (full v1) audit mode and checks every
   recorded Runner frame byte for byte, up to the final checkpoint request.
2. The recorded answer to that request is the INTERNAL_ERROR frame; the new Runner answers with the checkpoint instead.
3. Writes <out>/<job>/transcript-00.ndjson: the recorded transcript with only that last frame replaced (same ordinal,
   length and SHA-256 recomputed), plus summary.json (status pass, provenance) for compare_via.py. The recorded job folder
   is never modified.
Because every earlier frame is byte-identical, the checkpoint is the one the recorded Runner would have written.

Usage: python -I -B complete_oom_job.py --runner DIR --policy-config F --wp4-config F --job-dir DIR --out-root DIR
"""
import argparse
import base64
import hashlib
import io
import json
import os
import pathlib
import subprocess
import threading
import time

import psutil

DOTNET = r"C:\Program Files\dotnet\dotnet.exe"
MAX_LINE = 1 << 30


def record_for(frame: bytes, ordinal: int, schema: str) -> str:
    return json.dumps({"direction": "runnerToAdapter", "frameBase64": base64.b64encode(frame).decode("ascii"),
                       "frameLengthBytes": len(frame), "frameSha256": hashlib.sha256(frame).hexdigest(),
                       "ordinal": ordinal, "schemaVersion": schema}, separators=(",", ":"))


def main() -> int:
    p = argparse.ArgumentParser()
    for name in ("--runner", "--policy-config", "--wp4-config", "--job-dir", "--out-root"):
        p.add_argument(name, required=True)
    p.add_argument("--heap-hex", default="0x180000000")
    a = p.parse_args()
    job = pathlib.Path(a.job_dir)
    out = pathlib.Path(a.out_root) / job.name
    out.mkdir(parents=True, exist_ok=False)
    transcript = job / "transcript-00.ndjson"
    command = [DOTNET, str(pathlib.Path(a.runner) / "RideBound.Runner.dll"), "--mode", "commitment",
               "--policy-config", a.policy_config, "--wp4-config", a.wp4_config,
               "--solver-seed-source", "manifest-master-seed", "--maximum-line-bytes", str(MAX_LINE)]
    env = dict(os.environ, DOTNET_GCHeapHardLimit=a.heap_hex)
    started = time.time()
    result = {"job": job.name, "sourceTranscript": str(transcript), "framesCompared": 0, "framesIdentical": 0,
              "firstDifference": None, "checkpointReplaced": False}
    checkpoint_frame = None
    last_record = None
    with open(out / "runner-stderr.txt", "xb") as err:
        proc = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=err, env=env, bufsize=0)
        reader = io.BufferedReader(proc.stdout, buffer_size=1 << 20)
        writer = io.BufferedWriter(proc.stdin, buffer_size=1 << 20)
        ps = psutil.Process(proc.pid)
        peak = {"rss": 0}

        def watch():
            while proc.poll() is None:
                try:
                    peak["rss"] = max(peak["rss"], ps.memory_info().rss)
                except psutil.Error:
                    pass
                time.sleep(1)

        threading.Thread(target=watch, daemon=True).start()
        checkpoint_sent = False
        with open(transcript, encoding="utf-8") as fh:
            for line in fh:
                rec = json.loads(line)
                frame = base64.b64decode(rec["frameBase64"])
                if rec["direction"] == "adapterToRunner":
                    writer.write(frame + b"\n")
                    writer.flush()
                    checkpoint_sent = b'"messageType":"checkpoint"' in frame[:200]
                    continue
                got = reader.readline(MAX_LINE + 2)
                if not got:
                    result["firstDifference"] = {"ordinal": rec["ordinal"], "reason": "runner closed stdout"}
                    break
                got = got.rstrip(b"\n")
                if checkpoint_sent:
                    recorded = json.loads(frame)
                    if recorded.get("messageType") != "error" or not got.startswith(b'{"messageType":"checkpoint"'):
                        result["firstDifference"] = {"ordinal": rec["ordinal"], "reason": "unexpected final frames"}
                        break
                    checkpoint_frame = got
                    last_record = rec
                    continue
                result["framesCompared"] += 1
                if got == frame:
                    result["framesIdentical"] += 1
                else:
                    result["firstDifference"] = {"ordinal": rec["ordinal"], "reason": "frame differs"}
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

    ok = (result["firstDifference"] is None and checkpoint_frame is not None
          and result["framesCompared"] == result["framesIdentical"] > 0)
    if ok:
        with open(transcript, encoding="utf-8") as src, open(out / "transcript-00.ndjson", "x", encoding="utf-8",
                                                              newline="\n") as dst:
            for line in src:
                rec = json.loads(line)
                if rec["ordinal"] == last_record["ordinal"]:
                    dst.write(record_for(checkpoint_frame, rec["ordinal"], rec["schemaVersion"]) + "\n")
                    result["checkpointReplaced"] = True
                else:
                    dst.write(line if line.endswith("\n") else line + "\n")
        summary = {"status": "pass", "label": job.name,
                   "note": "ADR-089: recorded run failed with OutOfMemoryException on the final checkpoint; every earlier "
                           "Runner frame reproduced byte for byte by the fast-runner build, whose checkpoint replaces "
                           "the recorded INTERNAL_ERROR frame",
                   "runner": a.runner,
                   "runnerDllSha256": hashlib.sha256((pathlib.Path(a.runner) / "RideBound.Runner.dll").read_bytes()).hexdigest()}
        (out / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    result["checkpointBytes"] = len(checkpoint_frame) if checkpoint_frame else 0
    result["wallSeconds"] = round(time.time() - started, 1)
    result["runnerPeakRssMiB"] = round(peak["rss"] / 2**20)
    result["completed"] = ok and result["checkpointReplaced"]
    (out / "complete-result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))
    return 0 if result["completed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
