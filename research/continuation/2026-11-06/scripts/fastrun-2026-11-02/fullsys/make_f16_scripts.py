"""Make the F16 copies of the three sealed F14 emulator scripts by exact, counted string replacements.
Writes new files next to the originals (never overwrites) and prints the sha256 of every source and output file."""
import hashlib
import pathlib
import sys

D = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02/fullsys")
F16_CELLS_JOIN = "path.join(SP, 'wf17/r1-identity-fence-pad/cells/f16/weekdays-P512')"
F16_CELLS_ABS = "'E:/RideBoundData/company-intake/2026-10-23/sp/wf17/r1-identity-fence-pad/cells/f16/weekdays-P512'"
NOTE = ("// F16 COPY (2026-11-05) of {src}, made by make_f16_scripts.py with counted exact replacements only: the cell\n"
        "// directory (wf17 F16 cells, 292/292 sha256 = expected/wf17-f16-cells.sha256), the panel (F16) and, in\n"
        "// emulate_fullsys, the lambda prior file (the F16 prior of wf20/fullsys/RUNBOOK.sh: work/prior_F16/prior_prevhour.csv).\n")

PLAN = {
    "emulate_fullsys.js": [
        ("const CELLS_DIR = path.join(SP, 'wf14/scenarios/weekdays-P512');", f"const CELLS_DIR = {F16_CELLS_JOIN};", 1),
        ("[...PANELS.F14]", "[...PANELS.F16]", 1),
        ("PRIOR = LIB.loadPrior(path.join(LIB.WF, 'work/prior_prevhour.csv'), NET.zoneIds);",
         "PRIOR = LIB.loadPrior(path.join(SP, 'wf20/fullsys/work/prior_F16/prior_prevhour.csv'), NET.zoneIds);", 1),
        ("f14Cells", "f16Cells", 4),
    ],
    "emulate_sequences.js": [
        ("require('./emulate_fullsys.js')", "require('./emulate_fullsys_f16.js')", 1),
        ("const CELLS_DIR = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf14/scenarios/weekdays-P512';",
         f"const CELLS_DIR = {F16_CELLS_ABS};", 1),
        ("FS.f14Cells()", "FS.f16Cells()", 1),
    ],
    "emulate_timed.js": [
        ("require('./emulate_fullsys.js')", "require('./emulate_fullsys_f16.js')", 1),
        ("const CELLS_DIR = path.join(SP, 'wf14/scenarios/weekdays-P512');", f"const CELLS_DIR = {F16_CELLS_JOIN};", 1),
        ("fullsys-f14-v2/predictions/fullsys-f14-predictions.tsv", "fullsys-f16-v2/predictions/fullsys-f16-predictions.tsv", 1),
        ("fullsys-f14-v2/predictions/fullsys-f14-drop-sequences.tsv", "fullsys-f16-v2/predictions/fullsys-f16-drop-sequences.tsv", 1),
        ("FS.f14Cells()", "FS.f16Cells()", 2),
    ],
    # 2026-11-05: the F14 timed seal (FULLSYS-F14-TIMED.sha256) lists emulate_timed2.js, not emulate_timed.js
    "emulate_timed2.js": [
        ("require('./emulate_fullsys.js')", "require('./emulate_fullsys_f16.js')", 1),
        ("const CELLS_DIR = path.join(SP, 'wf14/scenarios/weekdays-P512');", f"const CELLS_DIR = {F16_CELLS_JOIN};", 1),
        ("fullsys-f14-v2/predictions/fullsys-f14-predictions.tsv", "fullsys-f16-v2/predictions/fullsys-f16-predictions.tsv", 1),
        ("fullsys-f14-v2/predictions/fullsys-f14-drop-sequences.tsv", "fullsys-f16-v2/predictions/fullsys-f16-drop-sequences.tsv", 1),
        ("FS.f14Cells()", "FS.f16Cells()", 2),
    ],
}
ONLY = set(sys.argv[1:])
if ONLY:
    PLAN = {k: v for k, v in PLAN.items() if k in ONLY}

sha = lambda b: hashlib.sha256(b).hexdigest()
for name, reps in PLAN.items():
    src = D / name
    out = D / name.replace(".js", "_f16.js")
    if out.exists():
        sys.exit(f"refused: {out} exists")
    raw = src.read_bytes()
    text = raw.decode("utf-8")
    for old, new, count in reps:
        n = text.count(old)
        if n != count:
            sys.exit(f"{name}: '{old}' found {n} times, expected {count}")
        text = text.replace(old, new)
    code = "\n".join(l for l in text.splitlines() if not l.lstrip().startswith("//"))   # comments may still say F14
    if "wf14/scenarios" in code or "PANELS.F14" in code or "fullsys-f14" in code or "f14Cells" in code:
        sys.exit(f"{name}: an F14 reference is left in code")
    text = NOTE.format(src=name) + text
    out.write_bytes(text.encode("utf-8"))
    print(f"{name} {sha(raw)} -> {out.name} {sha(text.encode('utf-8'))}")
