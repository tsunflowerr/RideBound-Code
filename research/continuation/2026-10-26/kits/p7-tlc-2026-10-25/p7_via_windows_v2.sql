-- P7 v2 (amendment: rid rule = order by request_datetime, pu, dz, trip_miles, trip_time, found by p7_rid_rule.sql)
-- P7 (audit 23/10 §6-P7): re-derive the Via simulation input of two confirm months from TLC HVFHV files downloaded at home
-- (E:/RideBoundData/tlc-raw-2026-10-25, sha256 in DOWNLOAD-LOG.txt) and compare with the company's
-- sp/wf14/work/via_windows.csv by EXCEPT ALL in both directions, as wf16/finalize/via_windows_all_months.sql did against
-- the company's derived parquet. The row id (rid) is rebuilt with the exact rule of
-- nyc-via-bundle-20-10/nyc-via/scripts/extract_via.sql.tmpl: Via (HV0004), Manhattan-internal (Borough Manhattan without
-- zones 103, 104, 105, 264, 265), request inside the file's month, row_number over
-- (request_datetime, pickup_datetime, dropoff_datetime, pu, dz, trip_miles, trip_time, base_passenger_fare).
-- Expected: csv_minus_tlc = 0 and tlc_minus_csv = 0 for both months.
SET memory_limit='1500MB';
SET threads=4;
SET preserve_insertion_order=false;
SET temp_directory='E:/RideBoundData/tlc-raw-2026-10-25/tmp_duck';
CREATE TABLE z AS SELECT LocationID::INT AS id, Borough FROM read_csv_auto('E:/RideBoundData/tlc-raw-2026-10-25/taxi+_zone_lookup.csv', header=true);
CREATE TABLE mz AS SELECT id FROM z WHERE Borough = 'Manhattan' AND id NOT IN (103, 104, 105, 264, 265);
CREATE TABLE w AS SELECT * FROM read_csv_auto('E:/RideBoundData/company-intake/2026-10-23/sp/wf14/work/via_windows.csv', header=true)
  WHERE month IN ('2019-03', '2019-09');
CREATE TABLE via AS
  SELECT '2019-03' AS month, request_datetime, pickup_datetime, dropoff_datetime, PULocationID::INT AS pu, DOLocationID::INT AS dz,
         trip_miles, trip_time, base_passenger_fare
  FROM read_parquet('E:/RideBoundData/tlc-raw-2026-10-25/fhvhv_tripdata_2019-03.parquet')
  WHERE hvfhs_license_num = 'HV0004' AND request_datetime >= TIMESTAMP '2019-03-01' AND request_datetime < TIMESTAMP '2019-04-01'
  UNION ALL
  SELECT '2019-09' AS month, request_datetime, pickup_datetime, dropoff_datetime, PULocationID::INT AS pu, DOLocationID::INT AS dz,
         trip_miles, trip_time, base_passenger_fare
  FROM read_parquet('E:/RideBoundData/tlc-raw-2026-10-25/fhvhv_tripdata_2019-09.parquet')
  WHERE hvfhs_license_num = 'HV0004' AND request_datetime >= TIMESTAMP '2019-09-01' AND request_datetime < TIMESTAMP '2019-10-01';
CREATE TABLE mn AS
  SELECT month, row_number() OVER (PARTITION BY month ORDER BY request_datetime, pu, dz, trip_miles, trip_time) AS rid,
         request_datetime, pu, dz, trip_miles, trip_time
  FROM via WHERE pu IN (SELECT id FROM mz) AND dz IN (SELECT id FROM mz);
CREATE TABLE d AS
  SELECT month, strftime(request_datetime, '%Y-%m-%d') AS day,
         CASE WHEN hour(request_datetime) IN (8, 9) THEN 'w08' ELSE 'w17' END AS win,
         rid, epoch(request_datetime)::BIGINT AS req_s, pu, dz, trip_miles, trip_time
  FROM mn
  WHERE strftime(request_datetime, '%Y-%m-%d') IN (SELECT DISTINCT day::VARCHAR FROM w) AND hour(request_datetime) IN (8, 9, 17, 18);
SELECT month, count(*) AS via_all_rows FROM via GROUP BY month ORDER BY month;
SELECT month, count(*) AS via_manhattan_internal_rows FROM mn GROUP BY month ORDER BY month;
SELECT w.month, count(*) AS csv_rows, (SELECT count(*) FROM d WHERE d.month = w.month) AS tlc_rows FROM w GROUP BY w.month ORDER BY w.month;
SELECT 'csv_minus_tlc' AS k, count(*) AS v FROM (
    SELECT day::VARCHAR, win, rid, req_s, pu, dz, trip_miles, trip_time FROM w
    EXCEPT ALL SELECT day, win, rid, req_s, pu, dz, trip_miles, trip_time FROM d)
UNION ALL SELECT 'tlc_minus_csv', count(*) FROM (
    SELECT day, win, rid, req_s, pu, dz, trip_miles, trip_time FROM d
    EXCEPT ALL SELECT day::VARCHAR, win, rid, req_s, pu, dz, trip_miles, trip_time FROM w)
UNION ALL SELECT 'csv_days', count(DISTINCT day) FROM w;
