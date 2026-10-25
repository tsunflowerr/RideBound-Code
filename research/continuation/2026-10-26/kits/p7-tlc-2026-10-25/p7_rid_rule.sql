-- P7 follow-up: which ordering reproduces the company's rid (the trip contents already match exactly)?
-- For each candidate ordering, count csv rows whose rid equals the candidate rid after a content join.
SET memory_limit='1500MB'; SET threads=4; SET preserve_insertion_order=true;
SET temp_directory='E:/RideBoundData/tlc-raw-2026-10-25/tmp_duck';
CREATE TABLE z AS SELECT LocationID::INT AS id, Borough FROM read_csv_auto('E:/RideBoundData/tlc-raw-2026-10-25/taxi+_zone_lookup.csv', header=true);
CREATE TABLE mz AS SELECT id FROM z WHERE Borough = 'Manhattan' AND id NOT IN (103, 104, 105, 264, 265);
CREATE TABLE w AS SELECT * FROM read_csv_auto('E:/RideBoundData/company-intake/2026-10-23/sp/wf14/work/via_windows.csv', header=true)
  WHERE month = '2019-03';
CREATE TABLE m AS
  SELECT * FROM read_parquet('E:/RideBoundData/tlc-raw-2026-10-25/fhvhv_tripdata_2019-03.parquet', file_row_number = true)
  WHERE hvfhs_license_num = 'HV0004' AND request_datetime >= TIMESTAMP '2019-03-01' AND request_datetime < TIMESTAMP '2019-04-01'
    AND PULocationID IN (SELECT id FROM mz) AND DOLocationID IN (SELECT id FROM mz);
CREATE TABLE c AS SELECT
  epoch(request_datetime)::BIGINT AS req_s, PULocationID::INT AS pu, DOLocationID::INT AS dz, trip_miles, trip_time,
  row_number() OVER (ORDER BY request_datetime, pickup_datetime, dropoff_datetime, PULocationID, DOLocationID, trip_miles, trip_time, base_passenger_fare) AS r_template,
  row_number() OVER (ORDER BY request_datetime, file_row_number) AS r_file,
  row_number() OVER (ORDER BY request_datetime, on_scene_datetime, pickup_datetime, dropoff_datetime, PULocationID, DOLocationID, trip_miles, trip_time) AS r_onscene,
  row_number() OVER (ORDER BY file_row_number) AS r_fileonly,
  row_number() OVER (ORDER BY request_datetime, PULocationID, DOLocationID, trip_miles, trip_time) AS r_req_od,
  row_number() OVER (ORDER BY request_datetime, pickup_datetime, PULocationID, DOLocationID) AS r_req_pick_od
FROM m;
SELECT count(*) AS csv_rows,
  count(*) FILTER (WHERE w.rid = c.r_template) AS template,
  count(*) FILTER (WHERE w.rid = c.r_file) AS req_then_file_order,
  count(*) FILTER (WHERE w.rid = c.r_onscene) AS with_on_scene,
  count(*) FILTER (WHERE w.rid = c.r_fileonly) AS file_order_only,
  count(*) FILTER (WHERE w.rid = c.r_req_od) AS req_od,
  count(*) FILTER (WHERE w.rid = c.r_req_pick_od) AS req_pick_od
FROM w JOIN c ON w.req_s = c.req_s AND w.pu = c.pu AND w.dz = c.dz AND w.trip_miles = c.trip_miles AND w.trip_time = c.trip_time;
