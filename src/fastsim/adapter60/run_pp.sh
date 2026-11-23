#!/usr/bin/env bash
# A5: đợi run_all.sh xong, rồi chạy phạt đổi xe 300 s (120 ô, r0) và hiệu chỉnh đều (PAD). Không sửa script đang chạy.
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
cd "$HERE" || exit 1
while ! grep -q "ALL DONE" run_all.log 2>/dev/null; do sleep 60; done
date
bash run_shards_pp.sh pen300 cells-pen60.txt "G2:EOHZ35:N,G2:RCE35:N" 6 "--g2pen 300000"
date
PYTHONIOENCODING=utf-8 "${PY:-python}" calib_pad.py
date
echo "PP DONE"
