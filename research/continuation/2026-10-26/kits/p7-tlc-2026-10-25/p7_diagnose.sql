-- P7 diagnosis: which column makes the 33,888-row EXCEPT ALL difference? Same inputs as p7_via_windows.sql.
SET memory_limit='1500MB'; SET threads=4; SET preserve_insertion_order=false;
SET temp_directory='E:/RideBoundData/tlc-raw-2026-10-25/tmp_duck';
CREATE TABLE z AS SELECT LocationID::INT AS id, Borough FROM read_csv_auto('E:/RideBoundData/tlc-raw-2026-10-25/taxi+_zone_lookup.csv', header=true);
CREATE TABLE mz AS SELECT id FROM z WHERE Borough = 'Manhattan' AND id NOT IN (103, 104, 105, 264, 265);
CREATE TABLE w AS SELECT * FROM read_csv_auto('E:/RideBoundData/company-intake/2026-10-23/sp/wf14/work/via_windows.csv', header=true)
  WHERE month IN ('2019-03', '2019-09');
CREATE TABLE via AS
  SELECT '2019-03' AS month, * FROM read_parquet('E:/RideBoundData/tlc-raw-2026-10-25/fhvhv_tripdata_2019-03.parquet')
  WHERE hvfhs_license_num = 'HV0004' AND request_datetime >= TIMESTAMP '2019-03-01' AND request_datetime < TIMESTAMP '2019-04-01'
  UNION ALL
  SELECT '2019-09' AS month, * FROM read_parquet('E:/RideBoundData/tlc-raw-2026-10-25/fhvhv_tripdata_2019-09.parquet')
  WHERE hvfhs_license_num = 'HV0004' AND request_datetime >= TIMESTAMP '2019-09-01' AND request_datetime < TIMESTAMP '2019-10-01';
CREATE TABLE mn AS
  SELECT month, row_number() OVER (PARTITION BY month ORDER BY request_datetime, pickup_datetime, dropoff_datetime,
         PULocationID, DOLocationID, trip_miles, trip_time, base_passenger_fare) AS rid,
         request_datetime, PULocationID::INT AS pu, DOLocationID::INT AS dz, trip_miles, trip_time
  FROM via WHERE PULocationID IN (SELECT id FROM mz) AND DOLocationID IN (SELECT id FROM mz);
CREATE TABLE d AS
  SELECT month, strftime(request_datetime, '%Y-%m-%d') AS day,
         CASE WHEN hour(request_datetime) IN (8, 9) THEN 'w08' ELSE 'w17' END AS win,
         rid, epoch(request_datetime)::BIGINT AS req_s, pu, dz, trip_miles, trip_time
  FROM mn
  WHERE strftime(request_datetime, '%Y-%m-%d') IN (SELECT DISTINCT day::VARCHAR FROM w) AND hour(request_datetime) IN (8, 9, 17, 18);
SELECT 'without rid' AS k, count(*) AS v FROM (
    SELECT day::VARCHAR, win, req_s, pu, dz, trip_miles, trip_time FROM w
    EXCEPT ALL SELECT day, win, req_s, pu, dz, trip_miles, trip_time FROM d)
UNION ALL SELECT 'without trip_miles', count(*) FROM (
    SELECT day::VARCHAR, win, rid, req_s, pu, dz, trip_time FROM w
    EXCEPT ALL SELECT day, win, rid, req_s, pu, dz, trip_time FROM d)
UNION ALL SELECT 'rid only + day', count(*) FROM (
    SELECT day::VARCHAR, rid FROM w EXCEPT ALL SELECT day, rid FROM d);
-- how do the two rid series relate? join on the full content key and look at rid differences
CREATE TABLE j AS SELECT w.month, w.day, w.rid AS rid_csv, d.rid AS rid_tlc
  FROM w JOIN d ON w.day::VARCHAR = d.day AND w.win = d.win AND w.req_s = d.req_s AND w.pu = d.pu AND w.dz = d.dz
              AND w.trip_miles = d.trip_miles AND w.trip_time = d.trip_time;
SELECT month, count(*) AS joined, count(*) FILTER (WHERE rid_csv = rid_tlc) AS same_rid,
       min(rid_csv - rid_tlc) AS min_diff, max(rid_csv - rid_tlc) AS max_diff
FROM j GROUP BY month ORDER BY month;
SELECT month, day, count(*) AS n, min(rid_csv - rid_tlc) AS min_diff, max(rid_csv - rid_tlc) AS max_diff
FROM j WHERE rid_csv <> rid_tlc GROUP BY month, day ORDER BY month, day LIMIT 30;
