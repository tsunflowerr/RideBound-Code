-- One month of the NYC TLC High Volume FHV trip records -> the Via trips that via_windows.sql and gen17.js use:
-- hvfhs_license_num HV0004 (Via), pickup and drop-off both in a Manhattan taxi zone (zones 103-105, the islands, and
-- 264-265, unknown, left out), requested inside the month. rid numbers these trips in the order of all kept columns.
-- Writes <out_dir>/via_mn_<month>.parquet. Only the needed columns of the source are read, so a remote source is
-- fetched in part. The 60-day and F16 cells use the months 2019-02 to 2019-12, one run per month.
--
-- Usage (DuckDB CLI 1.5.5):
--   duckdb -bail -cmd "SET VARIABLE month = '2019-02'; SET VARIABLE source = '<file or URL>';
--                      SET VARIABLE zones = '<file or URL>'; SET VARIABLE out_dir = '<dir>';" -f via_month.sql
--   source   https://d37ci6vzurychx.cloudfront.net/trip-data/fhvhv_tripdata_<month>.parquet, or a local copy of it
--   zones    https://d37ci6vzurychx.cloudfront.net/misc/taxi_zone_lookup.csv, or a local copy of it
-- (DuckDB loads its httpfs extension by itself for https sources.)
SET memory_limit = '550MB';
SET threads = 4;
SET preserve_insertion_order = false;
CREATE TABLE mz AS
  SELECT LocationID::INT AS id FROM read_csv_auto(getvariable('zones'), header = true)
  WHERE Borough = 'Manhattan' AND LocationID::INT NOT IN (103, 104, 105, 264, 265);
COPY (
  SELECT row_number() OVER (ORDER BY request_datetime, pu, dz, trip_miles, trip_time, shared_request_flag,
                                     shared_match_flag) AS rid,
         request_datetime, pu, dz, trip_miles, trip_time, shared_request_flag, shared_match_flag
  FROM (SELECT request_datetime, PULocationID::INT AS pu, DOLocationID::INT AS dz, trip_miles, trip_time,
               shared_request_flag, shared_match_flag
        FROM read_parquet(getvariable('source'))
        WHERE hvfhs_license_num = 'HV0004')
  WHERE pu IN (SELECT id FROM mz) AND dz IN (SELECT id FROM mz)
    AND request_datetime >= (getvariable('month') || '-01')::TIMESTAMP
    AND request_datetime < (getvariable('month') || '-01')::TIMESTAMP + INTERVAL 1 MONTH
  ORDER BY rid
) TO (getvariable('out_dir') || '/via_mn_' || getvariable('month') || '.parquet') (FORMAT PARQUET, COMPRESSION ZSTD);
