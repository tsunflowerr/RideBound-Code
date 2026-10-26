// wf21/n7 causal-index builder, step 1 (SQL generation). For each month R in 2019-08..2019-12 it copies the ORIGINAL generated
// wf7 run.sql (wf7/c-forecast/work/yellow-R/run.sql, i.e. tlc_factors.sql.tmpl with tpu = pickup bin, the exact SQL that built
// accuracy-index-2019.json) and changes ONLY the output directory. This is the "old rule" (one median polish per calendar month).
// For R in {2019-08, 2019-09, 2019-11} it APPENDS one causal block, run after the old outputs are written: every trip of month
// R+1 (same filters as the template would apply to month R+1) is scored against month R's final OD reference (tables o, odn:
// OD pairs with >= 20 trips in R and their median-polish row effects r_OD), e_med(day, bin) = median(log d - r_OD).
// Output bins_next.csv (day, bin, n, e_med). Nothing else of the polish is re-estimated, so these values use only month-R data
// plus the scored day's own trips.
// Usage: node build_sql.js   (writes cidx/work/yellow-R/run.sql)
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const ORIG = m => path.join(SP, 'wf7/c-forecast/work/yellow-' + m).split(path.sep).join('/');
const MINE = m => path.join(SP, 'wf21/n7/cidx/work/yellow-' + m).split(path.sep).join('/');
const PQ = {
  '2019-08': SP + '/wf7/data/tlc-yellow-2019/yellow_tripdata_2019-08.parquet',
  '2019-09': SP + '/wf7/data/tlc-yellow-2019/yellow_tripdata_2019-09.parquet',
  '2019-10': SP + '/wf7/data/tlc-yellow-2019/yellow_tripdata_2019-10.parquet',
  '2019-11': SP + '/wf5/data/tlc-yellow-2018-11-traffic/yellow_tripdata_2019-11.parquet',
  '2019-12': SP + '/wf7/data/tlc-yellow-2019/yellow_tripdata_2019-12.parquet',
};
const NEXT = { '2019-08': '2019-09', '2019-09': '2019-10', '2019-11': '2019-12' };   // reference month -> scored month
const first = m => m + '-01 00:00:00';
const nextMonth = m => { const [y, mo] = m.split('-').map(Number); return mo === 12 ? `${y + 1}-01` : `${y}-${String(mo + 1).padStart(2, '0')}`; };
const out = [];
for (const R of Object.keys(PQ)) {
  const src = fs.readFileSync(path.join(ORIG(R), 'run.sql'), 'utf8');
  if (!src.includes(ORIG(R))) throw new Error('original run.sql does not contain its out dir ' + R);
  if (!src.includes(PQ[R])) throw new Error('original run.sql does not read ' + PQ[R]);
  let sql = src.split(ORIG(R)).join(MINE(R));
  if (NEXT[R]) {
    const S = NEXT[R], T1 = first(S), T2 = first(nextMonth(S));
    sql += `
-- ===== wf21/n7 CAUSAL BLOCK (appended after the old outputs; does not change them) =====
-- month ${S} scored against the final OD reference of month ${R} (tables o and odn of the polish above)
CREATE TABLE raw2 AS
SELECT PULocationID::INT AS pu, DOLocationID::INT AS dz,
       epoch(tpep_dropoff_datetime) - epoch(tpep_pickup_datetime) AS d,
       trip_distance AS mi, tpep_pickup_datetime AS tpu,
       tpep_pickup_datetime + to_microseconds(((epoch(tpep_dropoff_datetime) - epoch(tpep_pickup_datetime)) / 2 * 1e6)::BIGINT) AS tmid
FROM read_parquet('${PQ[S]}')
WHERE tpep_pickup_datetime >= TIMESTAMP '${T1}' AND tpep_pickup_datetime < TIMESTAMP '${T2}'
  AND RatecodeID = 1;
CREATE TABLE t2 AS
SELECT (raw2.pu * 1000 + raw2.dz) AS od,
       strftime(tpu, '%Y-%m-%d') AS day,
       (hour(tpu) * 4 + minute(tpu) // 15)::INT AS bin,
       ln(d) AS logd
FROM raw2
JOIN z zp ON zp.id = raw2.pu AND zp.Borough = 'Manhattan'
JOIN z zd ON zd.id = raw2.dz AND zd.Borough = 'Manhattan'
WHERE raw2.pu <> raw2.dz AND raw2.pu NOT IN (103,104,105,264,265) AND raw2.dz NOT IN (103,104,105,264,265)
  AND d BETWEEN 60 AND 7200 AND mi BETWEEN 0.2 AND 30
  AND mi / (d / 3600.0) BETWEEN 0.5 AND 60
  AND tmid >= TIMESTAMP '${T1}' AND tmid < TIMESTAMP '${T2}';
DROP TABLE raw2;
CREATE TABLE res2 AS
SELECT t2.day, t2.bin, count(*) AS n, median(logd - r) AS e_med
FROM t2 JOIN odn USING (od) JOIN o USING (od) GROUP BY t2.day, t2.bin;
COPY (SELECT * FROM res2 ORDER BY day, bin) TO '${MINE(R)}/bins_next.csv' (HEADER, DELIMITER ',');
COPY (SELECT '${R}' AS ref_month, '${S}' AS scored_month, (SELECT count(*) FROM t2) AS n_filtered, (SELECT sum(n) FROM res2) AS n_scored, (SELECT count(*) FROM odn) AS n_od_ref) TO '${MINE(R)}/next_stats.csv' (HEADER, DELIMITER ',');
`;
  }
  fs.mkdirSync(path.join(MINE(R), 'tmp'), { recursive: true });
  fs.writeFileSync(path.join(MINE(R), 'run.sql'), sql);
  out.push(`${R}: run.sql written (${sql.length} bytes)${NEXT[R] ? ', causal block scores ' + NEXT[R] : ''}`);
}
console.log(out.join('\n'));
