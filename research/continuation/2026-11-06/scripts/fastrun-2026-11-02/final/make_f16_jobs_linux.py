"""F16 job lists and their Linux copies (2026-11-05, PLAN-F16.md step 4). Same job form as make_final_jobs.py (F14) and
the same Linux rewrite as make_linux_overlay.py steps 1-2; only the data root (fullsys-f16-v2), the cell count (96) and the
gate cells (the first F16 day, r0, both windows) differ. Every input must exist; nothing is overwritten.

  jobs-bundle-f16/jobs-gate-f16.json     4 jobs  d20190920-vf-r0-w08/w17 x {FS-G2-SHRC71-CL, FS-G2-SHHZ71-N}, transcripts kept
  jobs-bundle-f16/jobs-final-f16.json  192 jobs  96 F16 cells x the 2 main arms (gate cells keep their transcripts)
  job-configs-linux/                   commitment copies; WP4 configs whose only change is shadowAllocation.networkDirectory
  jobs-linux/                          the two lists with /rb paths and job-configs-linux
Usage: python -I -B make_f16_jobs_linux.py
"""
import json
import pathlib
import re
import shutil

DATA = pathlib.Path(r"E:/RideBoundData/research/fullsys-f16-v2")
ARMS = {"FS-G2-SHRC71-CL": ("SHRC71", "FS-G2-SHRC71-CL"), "FS-G2-SHHZ71-N": ("SHHZ71", "FS-G2-SHHZ71-N")}
GATE_CELLS = ("d20190920-vf-r0-w08", "d20190920-vf-r0-w17")
NETWORK_WIN = "E:/RideBoundData/research/via-full-v1/shadow-gate/manhattan-network"
NETWORK_LINUX = "/rb/RideBoundData/research/via-full-v1/shadow-gate/manhattan-network"
WINDOWS_PATH = re.compile(r"\b[A-Za-z]:[/\\]")


def job(role: str, cell: str, transcript: str) -> dict:
    commitment, wp4 = ARMS[role]
    value = {"name": f"{role}-{cell}", "label": f"{role}-{cell}-f16", "role": role,
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


def write_new(path: pathlib.Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    print(f"{path}")


def main() -> int:
    cells = sorted(p.name[:-len(".driver.json")] for p in (DATA / "drivers-run").glob("*.driver.json"))
    if len(cells) != 96 or len({p.name for p in (DATA / "fixtures").iterdir()} & set(cells)) != 96:
        raise SystemExit(f"expected 96 cells with fixtures, found {len(cells)}")
    if not set(GATE_CELLS) <= set(cells):
        raise SystemExit("gate cells missing")
    lists = {
        "jobs-gate-f16.json": [job(r, c, "keep") for c in GATE_CELLS for r in ARMS],
        "jobs-final-f16.json": [job(r, c, "keep" if c in GATE_CELLS else "drop") for c in cells for r in ARMS],
    }
    for name, jobs in lists.items():
        write_new(DATA / "jobs-bundle-f16" / name, json.dumps(jobs, indent=2) + "\n")
    # Linux configurations (make_linux_overlay.py step 1)
    out = DATA / "job-configs-linux"
    if out.exists():
        raise SystemExit(f"refused: {out} exists")
    for path in sorted((DATA / "job-configs-v2/commitment").glob("*.json")):
        target = out / "commitment" / path.name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target)
    n = 0
    for arm in ARMS:
        for path in sorted((DATA / "job-configs-v2/wp4" / arm).glob("*.json")):
            text = path.read_text(encoding="utf-8")
            old = f'"networkDirectory": "{NETWORK_WIN}"'
            if text.count(old) != 1:
                raise SystemExit(f"{path}: expected exactly one networkDirectory")
            text = text.replace(old, f'"networkDirectory": "{NETWORK_LINUX}"')
            before, after = json.loads(path.read_text(encoding="utf-8")), json.loads(text)
            before["shadowAllocation"]["networkDirectory"] = NETWORK_LINUX
            if before != after:
                raise SystemExit(f"{path}: more than networkDirectory changed")
            target = out / "wp4" / arm / path.name
            target.parent.mkdir(parents=True, exist_ok=True)
            with open(target, "x", encoding="utf-8", newline="\n") as handle:
                handle.write(text)
            n += 1
    print(f"{out}: {n} WP4 configs")
    # Linux job lists (make_linux_overlay.py step 2)
    for name in lists:
        text = (DATA / "jobs-bundle-f16" / name).read_text(encoding="utf-8")
        text = text.replace("E:/RideBoundData/research/fullsys-f16-v2/job-configs-v2/",
                            "/rb/RideBoundData/research/fullsys-f16-v2/job-configs-linux/")
        text = text.replace('"E:/', '"/rb/')
        jobs = json.loads(text)
        if any(WINDOWS_PATH.search(str(v)) for j in jobs for v in j.values()):
            raise SystemExit(f"{name}: a Windows path is left")
        write_new(DATA / "jobs-linux" / name, text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
