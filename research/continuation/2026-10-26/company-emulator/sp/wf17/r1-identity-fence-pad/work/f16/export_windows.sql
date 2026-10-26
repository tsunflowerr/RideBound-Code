-- via-many-days: export the Via requests of the selected days' windows [08,10) and [17,19) (naive NYC local time) from the
-- per-month derived parquets. Placeholders: E:/RideBoundData/company-intake/2026-10-23/sp/wf6/data/via-many-days (dir with via_mn_YYYY-MM.parquet), E:/RideBoundData/company-intake/2026-10-23/sp/wf17/r1-identity-fence-pad/work/f16/days.csv (csv with column day), E:/RideBoundData/company-intake/2026-10-23/sp/wf17/r1-identity-fence-pad/work/f16
SET memory_limit='400MB';
SET threads=3;
CREATE TABLE days AS SELECT day::VARCHAR AS day FROM read_csv_auto('E:/RideBoundData/company-intake/2026-10-23/sp/wf17/r1-identity-fence-pad/work/f16/days.csv', header=true, all_varchar=true);
COPY (
  SELECT strftime(request_datetime, '%Y-%m-%d') AS day, CASE WHEN hour(request_datetime) IN (8, 9) THEN 'w08' ELSE 'w17' END AS win,
         regexp_extract(filename, 'via_mn_([0-9]{4}-[0-9]{2})', 1) AS month, rid,
         epoch(request_datetime)::BIGINT AS req_s, pu, dz, trip_miles, trip_time, shared_request_flag AS sreq, shared_match_flag AS smatch
  FROM read_parquet('E:/RideBoundData/company-intake/2026-10-23/sp/wf6/data/via-many-days/via_mn_*.parquet', filename=true)
  WHERE strftime(request_datetime, '%Y-%m-%d') IN (SELECT day FROM days) AND hour(request_datetime) IN (8, 9, 17, 18)
  ORDER BY day, win, rid
) TO 'E:/RideBoundData/company-intake/2026-10-23/sp/wf17/r1-identity-fence-pad/work/f16/via_windows.csv' (HEADER, DELIMITER ',');
COPY (
  SELECT strftime(request_datetime, '%Y-%m-%d') AS day, hour(request_datetime) AS h, count(*) AS n,
         avg(CASE WHEN shared_match_flag = 'Y' THEN 1.0 ELSE 0.0 END) AS match_share, median(trip_time) AS med_trip_time_s
  FROM read_parquet('E:/RideBoundData/company-intake/2026-10-23/sp/wf6/data/via-many-days/via_mn_*.parquet')
  WHERE strftime(request_datetime, '%Y-%m-%d') IN (SELECT day FROM days)
  GROUP BY ALL ORDER BY day, h
) TO 'E:/RideBoundData/company-intake/2026-10-23/sp/wf17/r1-identity-fence-pad/work/f16/via_selected_day_hour.csv' (HEADER, DELIMITER ',');
