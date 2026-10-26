# wf20/am2017 deviations and records (thăm dò sau kiểm toán)

Plan: `PLAN.md`, sha256 3645ffef3fb9608a2a6597883742034836d52af33bcd812a660cf3ebf7dec430, hashed in `PLAN-HASH.txt` at 2026-10-26T11:11:48+07:00,
before any gate or production run. Timestamps +07:00. Entries are written before the results they affect are read, unless stated.

## Setup
- **2026-10-26T11:12 (record).** Step 1 facts were computed before the plan (no simulation): `scripts/profile.sql` (DuckDB 1.5.5 on the wf6
  derived parquet files, read only) and `scripts/facts.js`. A first facts.js run parsed the DuckDB CSV with CRLF line ends, which left one
  field (kappaMedMiles) empty; fixed (strip \r) and re-run before the plan was written; no other number changed.
- **2026-10-26T11:12 (record).** Code copies verified byte-identical to `SP/wf17/r2-penalty-regime/code` and `analysis` (sha256 lists in
  PLAN-HASH.txt). Synthetic-row smoke test of `analysis/am_analyze.js` (`work/smoke/synth.js`, random numbers, not simulation output)
  ran before the hash; it exercised the exclusion log.
- **2026-10-26T11:13 (record). Pre-run gates PASS** (`work/gates/GATES.ndjson`, `logs/gates.log`): G1 F62 4 cells x 8 arm-configs vs wf14 main r0
  32/32; G2 GF56 / GF68 vs R5 V-F56 / V-F68 10/10 and 10/10 (incl. `sig`). These rows reproduce seen data.
- **2026-10-26T11:13 (record). Concurrency.** One other `node` process (PID 51564, started 11:10, not this branch) is running; free RAM 4.5 GB.
  This branch uses 4 worker threads as required; results are deterministic per task, so sharing the CPU changes only wall time.
- **2026-10-26T11:28 (record). Production finished**: 840 tasks, 3,360 rows, 0 errors; wall 14.7 min, CPU 4,548 s, peak RSS 709 MB, largest
  heartbeat gap 10.0 s (no suspend), 4 workers. No result had been read at this point.
- **2026-10-26T11:29 (record). Post-run gate G3 PASS**: all 480 F62 production rows equal wf14 main r0 (`logs/postgate.log`, GATES.ndjson).
- **2026-10-26T11:29 (record). Analysis** `analysis/am_analyze.js` ran unchanged (sha256 1d6ee6d28a4a7be36a57ffbdf332238f11d067dd65af254dfb17b80809b390bd, as in PLAN-HASH.txt); log empty (no missing
  cell, no excluded day). Outputs `results/am_tables.txt` (sha256 2800b19feadee76e58cd2863a1abb903579699f3942c9714cd2a310fff20d12b) and `.json`; rows file `results/am.ndjson` sha256 0e57dbc367f3fe2977f5b43af238f763de2fe0ded2d6d81b8e03c5d2743567eb.
- No other deviation: no arm, metric, day or fleet point dropped; no failed task; no re-run. All interpretation beyond the pre-stated labels
  and descriptive items (PLAN 4.3) is marked as interpretation in the report.
