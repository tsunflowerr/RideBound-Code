#!/bin/sh
# wf21/n7: run the generated SQL month by month, sequentially (one DuckDB process, threads=3 as in the original SQL)
DB="E:/RideBoundData/company-intake/2026-10-23/sp/tools/duckdb-v1.5.5-sd/duckdb.exe"
for m in "$@"; do
  W="E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7/cidx/work/yellow-$m"
  t0=$(date +%s)
  "$DB" -bail < "$W/run.sql" > "$W/duck.log" 2>&1; rc=$?
  echo "$m rc=$rc $(( $(date +%s) - t0 )) s"
done
