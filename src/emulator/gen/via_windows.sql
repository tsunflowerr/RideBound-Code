-- The Via requests of the chosen days in the windows [08:00, 10:00) ("w08") and [17:00, 19:00) ("w17"), naive New York
-- local time, read from the via_mn_<month>.parquet files of via_month.sql -> via_windows.csv, the --windows input
-- of gen17.js. One row per request, sorted by day, window and rid; month comes from the file name, req_s is the
-- request time in epoch seconds of the naive local time; sreq and smatch are the shared-request and shared-match
-- flags.
--
-- Usage (DuckDB CLI 1.5.5):
--   duckdb -bail -cmd "SET VARIABLE via_dir = '<dir of the via_mn_*.parquet files>'; SET VARIABLE days = '<days.csv>';
--                      SET VARIABLE out = '<via_windows.csv>';" -f via_windows.sql
--   days   a CSV with a column "day": data/days_60.csv (60-day and F14 cells) or data/days_F16.csv (F16 cells)
SET memory_limit = '400MB';
SET threads = 3;
CREATE TABLE days AS
  SELECT day::VARCHAR AS day FROM read_csv_auto(getvariable('days'), header = true, all_varchar = true);
COPY (
  SELECT strftime(request_datetime, '%Y-%m-%d') AS day,
         CASE WHEN hour(request_datetime) IN (8, 9) THEN 'w08' ELSE 'w17' END AS win,
         regexp_extract(filename, 'via_mn_([0-9]{4}-[0-9]{2})', 1) AS month, rid,
         epoch(request_datetime)::BIGINT AS req_s, pu, dz, trip_miles, trip_time,
         shared_request_flag AS sreq, shared_match_flag AS smatch
  FROM read_parquet(getvariable('via_dir') || '/via_mn_*.parquet', filename = true)
  WHERE strftime(request_datetime, '%Y-%m-%d') IN (SELECT day FROM days) AND hour(request_datetime) IN (8, 9, 17, 18)
  ORDER BY day, win, rid
) TO (getvariable('out')) (HEADER, DELIMITER ',');
