"""Replay a recorded RideBound protocol transcript: the byte oracle for Runner and adapter changes.

runner   Lockstep replay through a Runner build (port of fastrun-2026-11-02/replay_runner_big.py): the recorded
         adapterToRunner frames, the original initializeRun manifest included, go to the Runner in transcript order,
         and every Runner line must equal the recorded runnerToAdapter frame byte for byte (decision hashes and the
         final checkpoint included). Stops at the first difference.
compare  Hash-free comparison of two runs (port of compare_runs_commit_free.py): two transcripts, two job folders,
         or two roots of job folders paired by summary.json. A differing line passes when direction, ordinal,
         schemaVersion and frameLengthBytes are equal and the decoded frames are equal without coreCommitSha and
         every key ending in Hash or Sha256, at any depth.
adapter  Runs a job script (world wrapper or launcher) against the adapter package next to this file, its Runner
         replaced through RideBoundProtocolSession(client_factory=...) by a process that plays back the recorded
         runnerToAdapter frames. Every frame the adapter writes must equal the recorded one; initializeRun may differ
         only in coreCommitSha and binarySha256.

  python -B replay.py runner  --transcript T --runner DIR --policy-config C --wp4-config W [--out result.json]
                              [--dotnet dotnet] [--max-line 1073741824] [--heap-hex 0x180000000] [--stall-seconds 900]
  python -B replay.py compare A B [--out report.json]
  python -B replay.py adapter --transcript T [--out result.json] -- <job.py> [job arguments ...]

Transcripts use the ProtocolTranscriptRecorder format (gzip when the name ends in .gz). Runner defaults are those of
the final runs: 1 GiB lines, 6 GiB heap cap (DOTNET_GCHeapHardLimit; --heap-hex "" for none). The Runner's peak memory
is read from the OS once it exits (no sampling, no psutil; null when unavailable). Exit code 0 means identical (and,
in adapter mode, a job that ended normally).
"""
from __future__ import annotations

import argparse, atexit, base64, gzip, hashlib, io, itertools, json  # noqa: E401
import os, pathlib, re, runpy, subprocess, sys, threading, time  # noqa: E401

HERE = pathlib.Path(__file__).resolve().parent
PYTHON = sys.executable  # interpreter of the playback process (adapter mode)
OUTER = ("direction", "ordinal", "schemaVersion", "frameLengthBytes")
MESSAGE_TYPE = re.compile(rb'"messageType":"([^"]*)"')


def open_transcript(path):
    return gzip.open(path, "rb") if str(path).endswith(".gz") else open(path, "rb")


def records(path, start: int = 0):
    with open_transcript(path) as handle:
        for line in itertools.islice(handle, start, None):
            yield json.loads(line)


def frame(record) -> bytes:
    return base64.b64decode(record["frameBase64"])


def same_frame(data: bytes, record) -> bool:
    return len(data) == record["frameLengthBytes"] and hashlib.sha256(data).hexdigest() == record["frameSha256"]


def message_type(data: bytes) -> str:
    match = MESSAGE_TYPE.search(data[:4096])  # the envelope's messageType comes before its payload
    return match.group(1).decode() if match else "?"


def without(value, keys, suffixes=()):
    """value without the object members named in keys or ending in one of suffixes, at any depth."""
    if isinstance(value, dict):
        return {k: without(v, keys, suffixes) for k, v in value.items() if k not in keys and not k.endswith(suffixes)}
    if isinstance(value, list):
        return [without(v, keys, suffixes) for v in value]
    return value


def difference(ordinal, recorded: bytes, replayed: bytes) -> dict:
    n, step = min(len(recorded), len(replayed)), 1 << 16
    block = next((i for i in range(0, n, step) if recorded[i:i + step] != replayed[i:i + step]), n)
    i = next((j for j in range(block, min(n, block + step)) if recorded[j] != replayed[j]), n)
    return {"ordinal": ordinal, "messageType": message_type(recorded), "byteIndex": i,
            "recorded": recorded[max(0, i - 80):i + 80].decode("utf-8", "replace"),
            "replayed": replayed[max(0, i - 80):i + 80].decode("utf-8", "replace")}


def peak_rss_mib(process):
    """Peak resident memory of an exited child process, from the OS counters; None when unavailable."""
    try:
        if os.name != "nt":
            import resource
            return round(resource.getrusage(resource.RUSAGE_CHILDREN).ru_maxrss / 1024)  # KiB on Linux
        import ctypes
        from ctypes import wintypes

        class Counters(ctypes.Structure):  # PROCESS_MEMORY_COUNTERS
            _fields_ = [("cb", wintypes.DWORD), ("faults", wintypes.DWORD), ("peak", ctypes.c_size_t),
                        ("rest", ctypes.c_size_t * 7)]
        counters = Counters(cb=ctypes.sizeof(Counters))
        ok = ctypes.windll.psapi.GetProcessMemoryInfo(wintypes.HANDLE(int(process._handle)), ctypes.byref(counters),
                                                      counters.cb)
        return round(counters.peak / 2**20) if ok else None
    except Exception:
        return None


def replay_runner(command, transcript, *, env=None, stderr=None, max_line=2**30, stall_seconds=900.0) -> dict:
    started = time.time()
    # Raw pipes (bufsize=0) under one 1 MiB buffer each: a BufferedReader over an already-buffered pipe waits for a
    # full buffer and deadlocks the hello exchange (replay_runner.py, 2026-10-22).
    process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=stderr, env=env,
                               bufsize=0)
    reader, writer = io.BufferedReader(process.stdout, 1 << 20), io.BufferedWriter(process.stdin, 1 << 20)
    progress = {"t": time.time(), "stalled": False}

    def watchdog():  # no Runner frame for stall_seconds: kill it, and the pending readline returns EOF
        while process.poll() is None:
            if time.time() - progress["t"] > stall_seconds:
                progress["stalled"] = True
                process.kill()
            time.sleep(1)

    threading.Thread(target=watchdog, daemon=True).start()
    sent = compared = identical = 0
    first = None
    for record in records(transcript):
        if record["direction"] == "adapterToRunner":
            try:
                writer.write(frame(record) + b"\n")
                writer.flush()
            except OSError:
                first = {"ordinal": record["ordinal"], "reason": "the Runner closed its stdin"}
                break
            sent += 1
            continue
        got = reader.readline(max_line + 2)
        progress["t"] = time.time()
        compared += 1
        if not got.endswith(b"\n"):
            first = {"ordinal": record["ordinal"], "reason": "watchdog: Runner stalled" if progress["stalled"]
                     else f"no complete Runner line ({len(got)} bytes)"}
            break
        if not same_frame(got[:-1], record):
            first = difference(record["ordinal"], frame(record), got[:-1])
            break
        identical += 1
    if first is None:
        writer.close()  # EOF after the recorded shutdown frame; the Runner must exit without another line
        if reader.read(1):
            first = {"ordinal": None, "reason": "Runner output after the last recorded frame"}
    if first is not None and process.poll() is None:
        process.kill()
    code = process.wait()  # bounded by the watchdog
    for stream in (writer, reader):
        try:
            stream.close()
        except OSError:  # a frame left in the buffer of a Runner that already exited
            pass
    return {"framesSent": sent, "framesCompared": compared, "framesIdentical": identical,
            "identical": first is None and compared > 0, "firstDifference": first, "runnerExitCode": code,
            "wallSeconds": round(time.time() - started, 1), "runnerPeakRssMiB": peak_rss_mib(process)}


def pairs(a, b) -> dict:
    def transcript_of(folder):
        names = ("transcript-00.ndjson.gz", "transcript-00.ndjson")
        return next((folder / n for n in names if (folder / n).is_file()), None)
    a, b = pathlib.Path(a), pathlib.Path(b)
    if a.is_file() and b.is_file():
        return {b.name: (a, b)}
    if transcript_of(a) and transcript_of(b):
        return {b.name: (transcript_of(a), transcript_of(b))}
    names = sorted(p.name for p in b.iterdir() if all((r / p.name / "summary.json").exists() for r in (a, b)))
    return {name: (transcript_of(a / name), transcript_of(b / name)) for name in names}


def compare_transcripts(a, b) -> dict:
    lines = same_bytes = same_after_strip = 0
    stripped, first = {}, None
    with open_transcript(a) as ta, open_transcript(b) as tb:
        for la, lb in itertools.zip_longest(ta, tb):
            lines += 1
            if la == lb:
                same_bytes += 1
                continue
            ok = la is not None and lb is not None
            if ok:
                oa, ob = json.loads(la), json.loads(lb)
                ok = all(oa.get(k) == ob.get(k) for k in OUTER)
                if ok:
                    fa, fb = (without(json.loads(frame(o)), {"coreCommitSha"}, ("Hash", "Sha256")) for o in (oa, ob))
                    ok, kind = fa == fb, fa.get("messageType", "?")
                    stripped[kind] = stripped.get(kind, 0) + 1
            if ok:
                same_after_strip += 1
            elif first is None:
                first = lines
    return {"lines": lines, "sameBytes": same_bytes, "sameAfterStrip": same_after_strip,
            "differing": lines - same_bytes - same_after_strip, "firstDifferingLine": first,
            "strippedByMessageType": stripped}


class ReplayDifference(Exception):
    """The adapter wrote a frame that differs from the transcript; it ends the job."""


class AdapterReplay:
    """Checks every frame through the client's transcript_writer and gives each client its own playback process."""

    def __init__(self, transcript):
        self.transcript, self.expected = str(transcript), records(transcript)
        self.position = 0  # frames seen so far; a client made after a Runner restart plays back from here
        self.first, self.rebound = None, False  # rebound: initializeRun differed only in its build identity

    def factory(self, command, artifact_paths, **options):
        from ridebound_fleetpy.runner_client import RunnerClient
        inner = options.get("transcript_writer")

        def writer(direction, data):
            self.check(direction, data)
            if inner is not None:
                inner(direction, data)

        options["transcript_writer"] = writer
        playback = [PYTHON, "-I", "-B", str(HERE / "replay.py"), "serve", self.transcript, str(self.position)]
        return RunnerClient(playback, artifact_paths, **options)

    def check(self, direction, data):
        if self.first is not None:
            return  # the job is closing down after the first difference
        record = next(self.expected, None)
        self.position += 1
        if record is None or record["direction"] != direction:
            has = record["direction"] if record else "nothing more"
            self.first = {"ordinal": self.position, "reason": f"{direction} frame where the transcript has {has}"}
        elif not same_frame(data, record):
            recorded, build = frame(record), {"coreCommitSha", "binarySha256"}
            if message_type(recorded) == "initializeRun" and \
                    without(json.loads(recorded), build) == without(json.loads(data), build):
                self.rebound = True
            else:
                self.first = difference(record["ordinal"], recorded, data)
        if self.first is not None:
            raise ReplayDifference(json.dumps(self.first)[:400])


def run_adapter(transcript, job) -> dict:
    """Runs job() with every RideBoundProtocolSession talking to the recorded Runner frames."""
    if str(HERE) not in sys.path:
        sys.path.insert(0, str(HERE))
    from ridebound_fleetpy import session

    replay, original = AdapterReplay(transcript), session.RideBoundProtocolSession.__init__

    def init(self, settings, client_factory=None):
        original(self, settings, client_factory=replay.factory)

    session.RideBoundProtocolSession.__init__ = init
    try:
        job()
        outcome = "returned"
    except SystemExit as exc:
        outcome = f"exit {exc.code}"
    except Exception as exc:  # a ReplayDifference ends the job here
        outcome = f"{type(exc).__name__}: {exc}"[:500]
    finally:
        session.RideBoundProtocolSession.__init__ = original
    if replay.first is None and next(replay.expected, None) is not None:
        replay.first = {"ordinal": replay.position + 1, "reason": "the job ended before the transcript"}
    replay.expected.close()
    return {"transcript": replay.transcript, "adapter": str(pathlib.Path(session.__file__).parent),
            "framesChecked": replay.position, "initializeRunRebound": replay.rebound,
            "identical": replay.first is None and replay.position > 0, "firstDifference": replay.first,
            "job": outcome, "jobOk": outcome in ("returned", "exit 0", "exit None")}


def serve(transcript, start: int) -> int:
    """Playback process of adapter mode: answers like the recorded Runner, from record number start on."""
    for record in records(transcript, start):
        if record["direction"] == "runnerToAdapter":
            sys.stdout.buffer.write(frame(record) + b"\n")
            sys.stdout.buffer.flush()
            continue
        line = sys.stdin.buffer.readline().rstrip(b"\n")
        if line != frame(record) and message_type(line) != "initializeRun":
            return 0  # EOF, or the adapter diverged (the job reports it): hang up like an exiting Runner
    return 0


def main(argv=None) -> int:
    argv = list(sys.argv[1:] if argv is None else argv)
    if argv[:1] == ["serve"]:  # internal: the playback process of adapter mode
        return serve(argv[1], int(argv[2]))
    job = argv[argv.index("--") + 1:] if "--" in argv else []
    argv = argv[:argv.index("--")] if "--" in argv else argv
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    modes = parser.add_subparsers(dest="mode", required=True)
    runner, compare, adapter = (modes.add_parser(name) for name in ("runner", "compare", "adapter"))
    for name in ("--transcript", "--runner", "--policy-config", "--wp4-config"):
        runner.add_argument(name, required=True)
    for name, default in (("--dotnet", "dotnet"), ("--max-line", 2**30), ("--heap-hex", "0x180000000"),
                          ("--stall-seconds", 900.0)):
        runner.add_argument(name, type=type(default), default=default)
    compare.add_argument("a")
    compare.add_argument("b")
    adapter.add_argument("--transcript", required=True)
    for sub in (runner, compare, adapter):
        sub.add_argument("--out")
    args = parser.parse_args(argv)
    if args.out and pathlib.Path(args.out).exists():
        raise SystemExit(f"refused: {args.out} exists")
    if args.mode == "runner":
        command = [args.dotnet, str(pathlib.Path(args.runner) / "RideBound.Runner.dll"), "--mode", "commitment",
                   "--policy-config", args.policy_config, "--wp4-config", args.wp4_config,
                   "--solver-seed-source", "manifest-master-seed", "--maximum-line-bytes", str(args.max_line)]
        env = dict(os.environ, **({"DOTNET_GCHeapHardLimit": args.heap_hex} if args.heap_hex else {}))
        stderr = open(pathlib.Path(args.out).with_suffix(".stderr.txt"), "xb") if args.out else None
        result = {"command": command, "transcript": args.transcript,
                  "runnerDllSha256": hashlib.sha256(pathlib.Path(command[1]).read_bytes()).hexdigest(),
                  **replay_runner(command, args.transcript, env=env, stderr=stderr, max_line=args.max_line,
                                  stall_seconds=args.stall_seconds)}
        if stderr is not None:
            stderr.close()
        ok = result["identical"]
        print(json.dumps({k: v for k, v in result.items() if k != "command"}))
    elif args.mode == "compare":
        result = {}
        for name, (a, b) in pairs(args.a, args.b).items():
            result[name] = compare_transcripts(a, b)
            print(name, json.dumps(result[name]), flush=True)
        ok = bool(result) and all(r["differing"] == 0 for r in result.values())
        print(f"commit-free identical {sum(r['differing'] == 0 for r in result.values())}/{len(result)}")
    else:
        if not job:
            parser.error("adapter: give the job script and its arguments after --")

        def run_job():
            sys.argv = list(job)
            try:
                runpy.run_path(job[0], run_name="__main__")
            finally:
                atexit._run_exitfuncs()  # the fleet control closes its session (the shutdown frame) at exit

        result = run_adapter(args.transcript, run_job)
        ok = result["identical"] and result["jobOk"]
        print(json.dumps(result))
    if args.out:
        with open(args.out, "x", encoding="utf-8", newline="\n") as handle:
            handle.write(json.dumps(result, indent=2) + "\n")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
