"""Write the three F16 seal files in the form of the F14 ones (PLAN-F16.md, 2026-11-05): a '#' header line with the UTC
time, then '<sha256> *<name>' lines, where <name> is relative to the data root (predictions/, drivers/, fixtures/, worlds/,
job-configs-v2/, jobs-bundle-f16/), to the kit (fullsys/..., convert_via_full_v2.py) or absolute (shared inputs). The
comparison tools look up 'predictions/<file name>' lines (compare_via_full.check_seal, compare_drop_sequences) or the digest
(eta_metrics, compare_shadow). Refuses to overwrite.
Usage: python -I -B seal_f16.py predictions|sequences|timed
"""
import datetime
import hashlib
import pathlib
import sys

DATA = pathlib.Path("E:/RideBoundData/research/fullsys-f16-v2")
KIT = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02")
SP = pathlib.Path("E:/RideBoundData/company-intake/2026-10-23/sp")
SHARED = [SP / "wf15/frozen/shadow-params.json", SP / "wf20/fullsys/work/prior_F16/prior_prevhour.csv",
          SP / "wf7/c-forecast/results/accuracy-index-2019.json", SP / "wf17/r1-identity-fence-pad/work/panels.json"]


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as source:
        for chunk in iter(lambda: source.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def entries(mode: str):
    if mode == "predictions":
        yield from ((DATA / "predictions" / n, "predictions/" + n)
                    for n in ("fullsys-f16-predictions.tsv", "fullsys-f16-predictions-counts.tsv"))
        for folder in ("drivers", "worlds"):
            yield from ((p, f"{folder}/{p.name}") for p in sorted((DATA / folder).glob("*.json")))
        for cell in sorted(p for p in (DATA / "fixtures").iterdir() if p.is_dir()):
            yield from ((p, f"fixtures/{cell.name}/{p.name}") for p in sorted(cell.iterdir()) if p.is_file())
        yield from ((p, f"predictions/shadow-inputs/{p.name}") for p in sorted((DATA / "predictions/shadow-inputs").glob("*.json")))
        for name in ("fullsys/emulate_fullsys_f16.js", "fullsys/engine_wf15_clint.js", "fullsys/plain-gate-F16.json",
                     "fullsys/make_f16_scripts.py", "fullsys/convert_f16.sh", "convert_via_full_v2.py", "make_run_drivers.py"):
            yield KIT / name, name
        yield from ((p, p.as_posix()) for p in SHARED)
    elif mode == "sequences":
        yield DATA / "predictions/fullsys-f16-drop-sequences.tsv", "predictions/fullsys-f16-drop-sequences.tsv"
        for name in ("fullsys/emulate_sequences_f16.js", "fullsys/compare_drop_sequences.py",
                     "fullsys/FULLSYS-F16-PREDICTIONS.sha256"):
            yield KIT / name, name
    elif mode == "timed":
        timed = DATA / "predictions-timed"
        for n in ("timed-adapter.tsv.gz", "attr-predictions.tsv", "attr-predictions-counts.tsv"):
            yield timed / n, "predictions/" + n
        for name in ("fullsys/emulate_timed2_f16.js", "fullsys/eta_metrics.py", "fullsys/compare_shadow.py",
                     "fullsys/finalize_job.py", "fullsys/evidence_reader.py", "fullsys/with_evidence.py",
                     "fullsys/make_job_configs.js", "fullsys/make_job_configs_v2.py", "fullsys/FULLSYS-F16-SEQUENCES.sha256",
                     "final/make_f16_jobs_linux.py", "final-linux/run_f16_linux.sh", "final-linux/final_report_linux.py"):
            yield KIT / name, name
        yield DATA / "job-configs-v2/CONFIGS-V2.json", "job-configs-v2/CONFIGS-V2.json"
        yield from ((p, f"jobs-bundle-f16/{p.name}") for p in sorted((DATA / "jobs-bundle-f16").glob("*.json")))
        yield from ((p, f"jobs-linux/{p.name}") for p in sorted((DATA / "jobs-linux").glob("*.json")))
    else:
        raise SystemExit("mode: predictions | sequences | timed")


HEADERS = {
    "predictions": "# PLAN-F16 final-comparison predictions (F16, 96 cells x FS-G2-SHRC71-CL, FS-G2-SHHZ71-N), adapter mode, "
                   "sealed {t} before any RideBound F16 job",
    "sequences": "# PLAN-F16 per-rider published drop-ETA sequences, rev60, tv5 for the 192 F16 cell-arms, sealed {t} before "
                 "any RideBound F16 job",
    "timed": "# PLAN-F16 timed predictions (96 F16 cells x the 2 main arms and the 2 attribution arms FS-G2-SHHZ71-CL, "
             "FS-G2-SHRC71-N; publication time and ETA per revision, t0, A), adapter mode, gzip; sealed {t} before any "
             "RideBound F16 job",
}


def main() -> int:
    mode = sys.argv[1]
    out = KIT / "fullsys" / f"FULLSYS-F16-{mode.upper()}.sha256"
    if out.exists():
        raise SystemExit(f"refused: {out} exists")
    t = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    lines = [HEADERS[mode].format(t=t)]
    n = 0
    for path, name in entries(mode):
        if not path.is_file():
            raise SystemExit(f"missing {path}")
        lines.append(f"{sha256(path)} *{name}")
        n += 1
    with open(out, "x", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")
    print(f"{out}: {n} files, {t}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
