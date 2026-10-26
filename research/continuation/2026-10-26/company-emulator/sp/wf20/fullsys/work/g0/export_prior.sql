-- wf15 (SPEC appendix B): eligible Via requests (pu and dz both in the 63 FleetPy node zones) of the hour BEFORE each window,
-- [07:00, 08:00) -> w08 and [16:00, 17:00) -> w17, naive NYC local time as in wf6 export_windows.sql.tmpl, counted per side
-- (O = pickup zone, D = drop-off zone). Placeholders: E:/RideBoundData/company-intake/2026-10-23/sp/wf6/data/via-many-days (dir with via_mn_YYYY-MM.parquet), E:/RideBoundData/company-intake/2026-10-23/sp/wf15/work/days_all.csv (csv with column
-- day), E:/RideBoundData/company-intake/2026-10-23/sp/wf15/work/zones.csv (csv with column zone), E:/RideBoundData/company-intake/2026-10-23/sp/wf20/fullsys/work/g0 (output dir).
SET memory_limit='400MB';
SET threads=3;
CREATE TABLE days  AS SELECT day::VARCHAR AS day FROM read_csv_auto('E:/RideBoundData/company-intake/2026-10-23/sp/wf15/work/days_all.csv', header=true, all_varchar=true);
CREATE TABLE zones AS SELECT zone::INTEGER AS zone FROM read_csv_auto('E:/RideBoundData/company-intake/2026-10-23/sp/wf15/work/zones.csv', header=true);
CREATE TABLE prev AS
  SELECT strftime(request_datetime, '%Y-%m-%d') AS day,
         CASE WHEN hour(request_datetime) = 7 THEN 'w08' ELSE 'w17' END AS win, pu, dz
  FROM read_parquet('E:/RideBoundData/company-intake/2026-10-23/sp/wf6/data/via-many-days/via_mn_*.parquet')
  WHERE strftime(request_datetime, '%Y-%m-%d') IN (SELECT day FROM days)
    AND hour(request_datetime) IN (7, 16)
    AND pu IN (SELECT zone FROM zones) AND dz IN (SELECT zone FROM zones);
COPY (
  SELECT day, win, 'O' AS side, pu AS zone, count(*) AS n FROM prev GROUP BY ALL
  UNION ALL
  SELECT day, win, 'D' AS side, dz AS zone, count(*) AS n FROM prev GROUP BY ALL
  ORDER BY day, win, side, zone
) TO 'E:/RideBoundData/company-intake/2026-10-23/sp/wf20/fullsys/work/g0/prior_prevhour.csv' (HEADER, DELIMITER ',');
