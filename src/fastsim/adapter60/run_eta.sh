#!/usr/bin/env bash
# A6: 180 cells of the 30-day set x 6 arms (SHRC71 N/CL/CLc/PAD, SHHZ71 N/CL), per-rider accuracy output. Do not edit while running.
cd "$(dirname "$0")" || exit 1
bash run_shards_eta.sh eta30 cells-30.txt "G2:SHRC71:N,G2:SHRC71:CL,G2:SHRC71:CLc,G2:SHRC71:PAD,G2:SHHZ71:N,G2:SHHZ71:CL" 6 "--flat $(pwd)/flat_iter5.json"
PYTHONIOENCODING=utf-8 "${PY:-python}" -I -B -X utf8 check_eta_gates.py eta30
echo "ETA DONE"
