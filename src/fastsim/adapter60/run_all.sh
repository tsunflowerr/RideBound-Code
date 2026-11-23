#!/usr/bin/env bash
# Chạy chính A -> B -> C (PLAN.md, bổ sung A1-A4), lần lượt, 6 tiến trình mỗi bước. Chỉ chạy khi cổng đã đạt.
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
cd "$HERE" || exit 1
grep -q "CỔNG ĐẠT" gate-check.txt || { echo "gate not passed; stop"; exit 1; }
date
# A: câu hỏi 1, 60 ngày, 10 nhánh không dự báo
bash run_shards.sh main60 cells-60.txt "INS:U:N,INS:EO35:N,INS:EOHZ35:N,INS:RCE35:N,G2:U:N,G2:EO35:N,G2:EOHZ35:N,G2:RCE35:N,G2:SHHZ71:N,G2:SHRC71:N" 6
date
# B: câu hỏi 2, 30 ngày, cộng dồn + Shadow + cho đổi xe; không dự báo, dự báo cả tháng, dự báo tháng trước
bash run_shards.sh fc30 cells-30.txt "G2:SHRC71:N,G2:SHRC71:CL,G2:SHRC71:CLc" 6
date
# C1: ngân sách, 10 ngày, cho đổi xe, hạn điều chỉnh và cộng dồn (35% đã có ở A)
A=""
for a in 0 10 20 50 75 100; do A="$A,G2:EOHZ$a:N,G2:RCE$a:N"; done
bash run_shards.sh alpha10 cells-alpha10.txt "${A#,}" 6
date
# C2: số xe, 30 ngày, r0, cho đổi xe, hạn điều chỉnh và cộng dồn 35%, ô 81 xe (A4)
for n in 20 29 43 52 62 67 81; do
  ROOTS_OVERRIDE="${RB_FLEET81_ROOT:?set RB_FLEET81_ROOT to the 81-vehicle fixture root}" bash run_shards.sh fleet-$n cells-fleet30.txt "G2:EOHZ35:N,G2:RCE35:N" 6 "--nveh $n"
done
date
echo "ALL DONE"
