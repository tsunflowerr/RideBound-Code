"""Job lists of the final-run bundle (ADR-089 GĐ5/GĐ6, 2026-11-04). Every input must exist; lists are never overwritten.

  jobs-gate-forecast.json   2 jobs  FS-G2-RCE35-CL on d20190919 r0 w08/w17 (forecast arithmetic; sealed rider rows)
  jobs-gate-shadow.json     8 jobs  d20190919 r0 w08/w17 x {SHRC71-CL, SHHZ71-N, SHHZ71-CL, SHRC71-N}
  jobs-final.json         168 jobs  84 F14 cells x {FS-G2-SHRC71-CL (ours), FS-G2-SHHZ71-N (Elmi)}
Gate jobs keep their transcripts; final jobs drop them after finalize_job.py has written and re-read the evidence, except
the cells in KEEP (the gate cells, so the rented machine's transcripts of those can be compared with this machine's).
WP4 files: the CL arms use wp4/FS-G2-SHRC71-CL/<cell>.json (Shadow + forecast), the N arms wp4/FS-G2-SHHZ71-N/<cell>.json
(Shadow only); the commitment file decides rule C (SHRC71) or the relaxed fence (SHHZ71).

Usage: python -I -B make_final_jobs.py <out dir>
"""
import json
import pathlib
import sys

DATA = pathlib.Path(r"E:/RideBoundData/research/fullsys-f14-v2")
ARMS = {  # role: (commitment, wp4 folder)
    "FS-G2-SHRC71-CL": ("SHRC71", "FS-G2-SHRC71-CL"),
    "FS-G2-SHHZ71-N": ("SHHZ71", "FS-G2-SHHZ71-N"),
    "FS-G2-SHHZ71-CL": ("SHHZ71", "FS-G2-SHRC71-CL"),
    "FS-G2-SHRC71-N": ("SHRC71", "FS-G2-SHHZ71-N"),
    "FS-G2-RCE35-CL": ("RCE35", "FS-G2-RCE35-CL"),
}
GATE_CELLS = ("d20190919-vf-r0-w08", "d20190919-vf-r0-w17")
KEEP = set(GATE_CELLS)


def job(role: str, cell: str, transcript: str) -> dict:
    commitment, wp4 = ARMS[role]
    value = {"name": f"{role}-{cell}", "label": f"{role}-{cell}-fin2", "role": role,
             "config": f"{DATA.as_posix()}/job-configs-v2/commitment/{commitment}.json",
             "wp4": f"{DATA.as_posix()}/job-configs-v2/wp4/{wp4}/{cell}.json",
             "fixture": f"{DATA.as_posix()}/fixtures/{cell}",
             "driver": f"{DATA.as_posix()}/drivers-run/{cell}.driver.json",
             "world": f"{DATA.as_posix()}/worlds/{cell}.world.json",
             "transcript": transcript}
    for key in ("config", "wp4", "driver", "world"):
        if not pathlib.Path(value[key]).is_file():
            raise SystemExit(f"missing {value[key]}")
    for name in ("scenario-content", "derivative-manifest", "normalization-report", "selection-frame"):
        if not (pathlib.Path(value["fixture"]) / f"{name}.json").is_file():
            raise SystemExit(f"missing {name} in {value['fixture']}")
    return value


def write(path: pathlib.Path, jobs: list) -> None:
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        json.dump(jobs, handle, indent=2)
        handle.write("\n")
    print(f"{path}: {len(jobs)} jobs")


def main() -> int:
    out = pathlib.Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    cells = sorted(p.name[:-len(".driver.json")] for p in (DATA / "drivers-run").glob("*.driver.json"))
    if len(cells) != 84 or len({p.name for p in (DATA / "fixtures").iterdir()} & set(cells)) != 84:
        raise SystemExit(f"expected 84 cells with fixtures, found {len(cells)}")
    write(out / "jobs-gate-forecast.json", [job("FS-G2-RCE35-CL", c, "keep") for c in GATE_CELLS])
    write(out / "jobs-gate-shadow.json", [job(r, c, "keep") for r in ("FS-G2-SHRC71-CL", "FS-G2-SHHZ71-N",
                                                                       "FS-G2-SHHZ71-CL", "FS-G2-SHRC71-N")
                                          for c in GATE_CELLS])
    write(out / "jobs-final.json", [job(r, c, "keep" if c in KEEP else "drop")
                                    for c in cells for r in ("FS-G2-SHRC71-CL", "FS-G2-SHHZ71-N")])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
