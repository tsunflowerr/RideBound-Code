#!/usr/bin/env bash
# Diagnostic, 2026-11-05: rerun the one F14 job whose riders differ from the sealed emulator rows
# (FS-G2-SHHZ71-N d20191015-vf-r1-w08, first difference at t0=418 s) with the same inputs, to see whether the
# real system reproduces its own result (deterministic) or not. Output only under /rb/runs-diag; /rb/runs is untouched.
set -Eeuo pipefail
export LANG=C.UTF-8 LC_ALL=C.UTF-8 TZ=ICT-7
unset PYTHONPATH PYTHONHOME PYTHONSTARTUP DOTNET_ROOT
PY=/rb/RideBoundData/final-v2-linux/python/runtime/bin/python3.10
"$PY" -I -c "
import json
j=json.load(open(\"/rb/RideBoundData/research/fullsys-f14-v2/jobs-linux/jobs-final.json\"))
x=[d for d in j if d[\"name\"]==\"FS-G2-SHHZ71-N-d20191015-vf-r1-w08\"]
assert len(x)==1
x[0][\"label\"]=x[0][\"label\"]+\"-diag1\"; x[0][\"transcript\"]=\"keep\"
open(\"/rb/runs-diag/jobs-diag.json\",\"x\").write(json.dumps(x,indent=2)+\"\n\")
"
"$PY" -B /rb/RideBoundData/final-v2-linux/tools/run_queue_linux.py --jobs /rb/runs-diag/jobs-diag.json --out /rb/runs-diag/final \
    --logs /rb/runs-diag/logs --stop /rb/runs-diag/STOP --workers 1 --ram-floor-gib 6.5 --disk-floor-gib 4
echo "=== diag done"
