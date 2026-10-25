"""Lockstep replay of a recorded RideBound job through a (possibly different) Runner build.

Cross-replay gate for A0 (bundle docs/wf9/review/REVIEW.md B1): the recorded adapter->Runner frames, including the
initializeRun manifest with the ORIGINAL build's binarySha256, are written to the Runner's stdin in transcript order;
every Runner->adapter line is read and compared BYTE FOR BYTE with the recorded frame. The Runner never checks
binarySha256 against itself, so an exact build must reproduce every frame, including state hashes and checkpoint.
Stops at the first differing frame.

Usage: python -B replay_runner.py --runner <runner dir> --policy-config <arm.json> --wp4-config <wp4.json>
                                  --transcript <transcript-00.ndjson> --out <result.json> [--selfcheck]
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import io
import json
import os
import pathlib
import subprocess
import sys
import threading
import time

import psutil

DOTNET = r"C:\Program Files\dotnet\dotnet.exe"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runner", required=True)
    parser.add_argument("--policy-config", required=True)
    parser.add_argument("--wp4-config", required=True)
    parser.add_argument("--transcript", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--selfcheck", action="store_true", help="set RIDEBOUND_CANONICAL_SELFCHECK=1 (hash patch guard)")
    args = parser.parse_args()
    out = pathlib.Path(args.out)
    if out.exists():
        raise SystemExit(f"refused: {out} exists")

    dll = pathlib.Path(args.runner) / "RideBound.Runner.dll"
    command = [DOTNET, str(dll), "--mode", "commitment", "--policy-config", args.policy_config,
               "--wp4-config", args.wp4_config, "--solver-seed-source", "manifest-master-seed",
               "--maximum-line-bytes", str(64 * 1024 * 1024)]
    env = dict(os.environ, DOTNET_GCHeapHardLimit="0x38000000")
    if args.selfcheck:
        env["RIDEBOUND_CANONICAL_SELFCHECK"] = "1"
    stderr_path = out.with_suffix(".stderr.txt")
    started = time.time()
    with open(stderr_path, "xb") as stderr_file:
        # bufsize=0: raw pipes. A BufferedReader over an already-buffered stdout deadlocks, because the inner
        # buffer's readinto waits for the full 1 MiB (first fix of 2026-10-22: the hello exchange hung).
        process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=stderr_file, env=env,
                                   bufsize=0)
        reader = io.BufferedReader(process.stdout, buffer_size=1 << 20)
        writer = io.BufferedWriter(process.stdin, buffer_size=1 << 20)
        ps = psutil.Process(process.pid)
        peak = {"rss": 0}

        def watch():
            while process.poll() is None:
                try:
                    peak["rss"] = max(peak["rss"], ps.memory_info().rss)
                except psutil.Error:
                    pass
                time.sleep(1)

        threading.Thread(target=watch, daemon=True).start()
        progress = {"t": time.time(), "stalled": False}

        def watchdog():      # no Runner frame for 900 s -> kill it; the pending readline then returns EOF
            while process.poll() is None:
                if time.time() - progress["t"] > 900:
                    progress["stalled"] = True
                    process.kill()
                    return
                time.sleep(5)

        threading.Thread(target=watchdog, daemon=True).start()
        sent = compared = identical = 0
        first_difference = None
        cpu = None
        with open(args.transcript, encoding="utf-8") as handle:
            for line in handle:
                record = json.loads(line)
                frame = base64.b64decode(record["frameBase64"])
                if record["direction"] == "adapterToRunner":
                    writer.write(frame + b"\n")
                    writer.flush()
                    sent += 1
                    continue
                got = reader.readline(64 * 1024 * 1024 + 2)
                progress["t"] = time.time()
                compared += 1
                if not got:
                    reason = "watchdog: no Runner frame for 900 s" if progress["stalled"] else "runner closed stdout"
                    first_difference = {"ordinal": record["ordinal"], "reason": reason}
                    break
                got = got.rstrip(b"\n")
                if got == frame:
                    identical += 1
                else:
                    try:
                        mtype = json.loads(frame).get("messageType")
                    except ValueError:
                        mtype = "?"
                    index = next((i for i, (a, b) in enumerate(zip(got, frame)) if a != b), min(len(got), len(frame)))
                    first_difference = {"ordinal": record["ordinal"], "messageType": mtype, "byteIndex": index,
                                        "recorded": frame[max(0, index - 80):index + 80].decode("utf-8", "replace"),
                                        "replayed": got[max(0, index - 80):index + 80].decode("utf-8", "replace")}
                    break
        try:
            times = ps.cpu_times()
            cpu = round(times.user + times.system, 2)
        except psutil.Error:
            pass
        try:
            writer.close()
        except OSError:
            pass
        try:
            process.wait(timeout=60)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
    result = {
        "runner": str(dll), "runnerDllSha256": hashlib.sha256(dll.read_bytes()).hexdigest(),
        "transcript": args.transcript, "selfcheck": args.selfcheck,
        "framesSent": sent, "framesCompared": compared, "framesIdentical": identical,
        "identical": first_difference is None and compared == identical and compared > 0,
        "firstDifference": first_difference, "runnerExitCode": process.returncode,
        "runnerCpuSeconds": cpu, "wallSeconds": round(time.time() - started, 1),
        "runnerPeakRssMiB": round(peak["rss"] / 2**20),
    }
    with open(out, "x", encoding="utf-8", newline="\n") as handle:
        json.dump(result, handle, indent=2)
        handle.write("\n")
    print(json.dumps({k: result[k] for k in ("framesCompared", "framesIdentical", "identical", "runnerCpuSeconds",
                                              "wallSeconds", "runnerPeakRssMiB", "firstDifference")}))
    return 0 if result["identical"] else 1


if __name__ == "__main__":
    sys.exit(main())
