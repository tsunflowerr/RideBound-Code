-- wf7 copy of wf5/survey-traffic/scripts/tlc_factors.sql.tmpl; tpu = tpu (pickup-bin, matches FleetPy; PRIMARY here) or tmid (midpoint)
-- Derive per-15-min travel-time factors from NYC TLC trip records (Yellow schema).
-- Method (pre-specified): robust two-way median polish on log trip duration,
--   log d_i = r_OD(i) + e_bin(i) + eps_i ,  OD = ordered (PU zone, DO zone) pair, bin = 15-min bin of trip MIDPOINT.
-- Placeholders: E:/RideBoundData/company-intake/2026-10-23/sp/wf5/data/tlc-yellow-2018-11-traffic/yellow_tripdata_2019-11.parquet, E:/RideBoundData/company-intake/2026-10-23/sp/wf5/data/tlc-yellow-2018-11-traffic/taxi_zone_lookup.csv, E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7/cidx/work/yellow-2019-11, 2019-11-01 00:00:00, 2019-12-01 00:00:00, __PUCOL__, __DOCOL__, __DIST__ (miles col), Manhattan
SET memory_limit='1GB';
SET threads=3;
SET temp_directory='E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7/cidx/work/yellow-2019-11/tmp';
SET preserve_insertion_order=false;
CREATE TABLE z AS SELECT LocationID::INT AS id, Borough, Zone, service_zone FROM read_csv_auto('E:/RideBoundData/company-intake/2026-10-23/sp/wf5/data/tlc-yellow-2018-11-traffic/taxi_zone_lookup.csv', header=true);
CREATE TABLE raw AS
SELECT PULocationID::INT AS pu, DOLocationID::INT AS dz,
       epoch(tpep_dropoff_datetime) - epoch(tpep_pickup_datetime) AS d,
       trip_distance AS mi, tpep_pickup_datetime AS tpu,
       tpep_pickup_datetime + to_microseconds(((epoch(tpep_dropoff_datetime) - epoch(tpep_pickup_datetime)) / 2 * 1e6)::BIGINT) AS tmid,
       hash(VendorID, tpep_pickup_datetime, tpep_dropoff_datetime, PULocationID, DOLocationID, trip_distance, total_amount) % 2 AS half
FROM read_parquet('E:/RideBoundData/company-intake/2026-10-23/sp/wf5/data/tlc-yellow-2018-11-traffic/yellow_tripdata_2019-11.parquet')
WHERE tpep_pickup_datetime >= TIMESTAMP '2019-11-01 00:00:00' AND tpep_pickup_datetime < TIMESTAMP '2019-12-01 00:00:00'
  AND RatecodeID = 1;
CREATE TABLE f0 AS SELECT count(*) AS n_raw FROM raw;
-- filters
CREATE TABLE t AS
SELECT (raw.pu * 1000 + raw.dz) AS od, raw.pu, zp.service_zone AS region,
       strftime(tpu, '%Y-%m-%d') AS day,
       (hour(tpu) * 4 + minute(tpu) // 15)::INT AS bin,
       ln(d) AS logd, half
FROM raw
JOIN z zp ON zp.id = raw.pu AND zp.Borough = 'Manhattan'
JOIN z zd ON zd.id = raw.dz AND zd.Borough = 'Manhattan'
WHERE raw.pu <> raw.dz AND raw.pu NOT IN (103,104,105,264,265) AND raw.dz NOT IN (103,104,105,264,265)
  AND d BETWEEN 60 AND 7200 AND mi BETWEEN 0.2 AND 30
  AND mi / (d / 3600.0) BETWEEN 0.5 AND 60
  AND tmid >= TIMESTAMP '2019-11-01 00:00:00' AND tmid < TIMESTAMP '2019-12-01 00:00:00';
-- keep OD pairs with >= 20 trips in the month
CREATE TABLE odn AS SELECT od, count(*) AS n FROM t GROUP BY od HAVING count(*) >= 20;
CREATE TABLE tt AS SELECT t.* FROM t JOIN odn USING (od);
DROP TABLE raw; DROP TABLE t;
CREATE TABLE stats AS SELECT (SELECT n_raw FROM f0) AS n_raw, count(*) AS n_used, count(DISTINCT od) AS n_od FROM tt;
-- median polish, 4 alternations
CREATE TABLE b AS SELECT day, bin, 0.0::DOUBLE AS e FROM tt GROUP BY day, bin;
CREATE OR REPLACE TABLE o AS SELECT od, median(logd - e) AS r FROM tt JOIN b USING (day, bin) GROUP BY od;
CREATE OR REPLACE TABLE b AS SELECT day, bin, median(logd - r) AS e FROM tt JOIN o USING (od) GROUP BY day, bin;
CREATE OR REPLACE TABLE o AS SELECT od, median(logd - e) AS r FROM tt JOIN b USING (day, bin) GROUP BY od;
CREATE OR REPLACE TABLE b AS SELECT day, bin, median(logd - r) AS e FROM tt JOIN o USING (od) GROUP BY day, bin;
CREATE OR REPLACE TABLE o AS SELECT od, median(logd - e) AS r FROM tt JOIN b USING (day, bin) GROUP BY od;
CREATE OR REPLACE TABLE b AS SELECT day, bin, median(logd - r) AS e FROM tt JOIN o USING (od) GROUP BY day, bin;
CREATE OR REPLACE TABLE o AS SELECT od, median(logd - e) AS r FROM tt JOIN b USING (day, bin) GROUP BY od;
-- final bin effects: median (primary), 10%-trimmed mean (sensitivity), split halves, regions (PU service_zone)
CREATE TABLE res AS
SELECT tt.day, tt.bin, count(*) AS n,
       median(logd - r) AS e_med,
       avg(CASE WHEN true THEN logd - r END) AS e_mean,
       quantile_cont(logd - r, 0.25) AS q25, quantile_cont(logd - r, 0.75) AS q75,
       median(CASE WHEN half = 0 THEN logd - r END) AS e_h0,
       median(CASE WHEN half = 1 THEN logd - r END) AS e_h1,
       median(CASE WHEN region = 'Yellow Zone' THEN logd - r END) AS e_yellow,
       count(CASE WHEN region = 'Yellow Zone' THEN 1 END) AS n_yellow,
       median(CASE WHEN region <> 'Yellow Zone' THEN logd - r END) AS e_boro,
       count(CASE WHEN region <> 'Yellow Zone' THEN 1 END) AS n_boro
FROM tt JOIN o USING (od) GROUP BY tt.day, tt.bin;
-- trimmed mean (drop residuals outside 10-90% within bin)
CREATE TABLE trim AS
WITH x AS (SELECT tt.day, tt.bin, logd - r AS u FROM tt JOIN o USING (od)),
q AS (SELECT day, bin, quantile_cont(u, 0.1) AS lo, quantile_cont(u, 0.9) AS hi FROM x GROUP BY day, bin)
SELECT x.day, x.bin, avg(u) AS e_trim FROM x JOIN q USING (day, bin) WHERE u BETWEEN lo AND hi GROUP BY x.day, x.bin;
COPY (SELECT res.*, trim.e_trim FROM res JOIN trim USING (day, bin) ORDER BY day, bin) TO 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7/cidx/work/yellow-2019-11/bins.csv' (HEADER, DELIMITER ',');
COPY (SELECT * FROM stats) TO 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7/cidx/work/yellow-2019-11/stats.csv' (HEADER, DELIMITER ',');
COPY (SELECT od, r, n FROM o JOIN odn USING (od) ORDER BY n DESC) TO 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7/cidx/work/yellow-2019-11/od_ref.csv' (HEADER, DELIMITER ',');

-- ===== wf21/n7 CAUSAL BLOCK (appended after the old outputs; does not change them) =====
-- month 2019-12 scored against the final OD reference of month 2019-11 (tables o and odn of the polish above)
CREATE TABLE raw2 AS
SELECT PULocationID::INT AS pu, DOLocationID::INT AS dz,
       epoch(tpep_dropoff_datetime) - epoch(tpep_pickup_datetime) AS d,
       trip_distance AS mi, tpep_pickup_datetime AS tpu,
       tpep_pickup_datetime + to_microseconds(((epoch(tpep_dropoff_datetime) - epoch(tpep_pickup_datetime)) / 2 * 1e6)::BIGINT) AS tmid
FROM read_parquet('E:/RideBoundData/company-intake/2026-10-23/sp/wf7/data/tlc-yellow-2019/yellow_tripdata_2019-12.parquet')
WHERE tpep_pickup_datetime >= TIMESTAMP '2019-12-01 00:00:00' AND tpep_pickup_datetime < TIMESTAMP '2020-01-01 00:00:00'
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
  AND tmid >= TIMESTAMP '2019-12-01 00:00:00' AND tmid < TIMESTAMP '2020-01-01 00:00:00';
DROP TABLE raw2;
CREATE TABLE res2 AS
SELECT t2.day, t2.bin, count(*) AS n, median(logd - r) AS e_med
FROM t2 JOIN odn USING (od) JOIN o USING (od) GROUP BY t2.day, t2.bin;
COPY (SELECT * FROM res2 ORDER BY day, bin) TO 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7/cidx/work/yellow-2019-11/bins_next.csv' (HEADER, DELIMITER ',');
COPY (SELECT '2019-11' AS ref_month, '2019-12' AS scored_month, (SELECT count(*) FROM t2) AS n_filtered, (SELECT sum(n) FROM res2) AS n_scored, (SELECT count(*) FROM odn) AS n_od_ref) TO 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7/cidx/work/yellow-2019-11/next_stats.csv' (HEADER, DELIMITER ',');
