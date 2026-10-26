-- wf20/am2017 Step 1 facts (no simulation): weekday hourly profile of Manhattan-internal Yellow (RatecodeID 1, PU<>DO zone) and Via (HV0004)
-- trips, Feb-Dec 2019, from the wf6 derived parquet files (read-only). Weekday = Mon-Fri; US federal holidays 2019 removed.
SET threads=4; SET memory_limit='1GB';
CREATE TEMP TABLE hol AS SELECT CAST(x AS DATE) AS d FROM (VALUES ('2019-02-18'),('2019-05-27'),('2019-07-04'),('2019-09-02'),('2019-10-14'),('2019-11-11'),('2019-11-28'),('2019-12-25')) t(x);
CREATE TEMP TABLE y AS SELECT CAST(tpick AS DATE) AS day, hour(tpick) AS h, d, mi FROM read_parquet('E:/RideBoundData/company-intake/2026-10-23/sp/wf6/data/via-many-days/yellow_mn_r1_2019-*.parquet')
  WHERE tpick >= TIMESTAMP '2019-02-01' AND tpick < TIMESTAMP '2020-01-01';
CREATE TEMP TABLE v AS SELECT CAST(request_datetime AS DATE) AS day, hour(request_datetime) AS h, trip_time AS d, trip_miles AS mi FROM read_parquet('E:/RideBoundData/company-intake/2026-10-23/sp/wf6/data/via-many-days/via_mn_2019-*.parquet')
  WHERE request_datetime >= TIMESTAMP '2019-02-04' AND request_datetime < TIMESTAMP '2020-01-01';
CREATE TEMP TABLE yw AS SELECT * FROM y WHERE dayofweek(day) BETWEEN 1 AND 5 AND day NOT IN (SELECT d FROM hol);
CREATE TEMP TABLE vw AS SELECT * FROM v WHERE dayofweek(day) BETWEEN 1 AND 5 AND day NOT IN (SELECT d FROM hol);
.mode csv
.headers on
.once E:/RideBoundData/company-intake/2026-10-23/sp/wf20/am2017/facts/hourly_weekday_2019.csv
SELECT 'yellow' AS src, h, count(*) AS trips, count(DISTINCT day) AS days, count(*)::DOUBLE / count(DISTINCT day) AS per_day,
       median(d) FILTER (WHERE d BETWEEN 60 AND 10800) AS med_d_s, median(mi) FILTER (WHERE mi > 0) AS med_mi FROM yw GROUP BY h
UNION ALL
SELECT 'via' AS src, h, count(*), count(DISTINCT day), count(*)::DOUBLE / count(DISTINCT day), median(d) FILTER (WHERE d BETWEEN 60 AND 10800), median(mi) FILTER (WHERE mi > 0) FROM vw GROUP BY h
ORDER BY src, h;
.once E:/RideBoundData/company-intake/2026-10-23/sp/wf20/am2017/facts/window_weekday_2019.csv
SELECT 'yellow' AS src, w, count(*)::DOUBLE / (SELECT count(DISTINCT day) FROM yw) AS per_day_mean, median(d) FILTER (WHERE d BETWEEN 60 AND 10800) AS med_d_s, avg(d) FILTER (WHERE d BETWEEN 60 AND 10800) AS mean_d_s, median(mi) FILTER (WHERE mi > 0) AS med_mi
FROM (SELECT *, CASE WHEN h IN (8,9) THEN 'w08' WHEN h IN (13,14) THEN 'w13' WHEN h IN (17,18) THEN 'w17' ELSE 'all_other' END AS w FROM yw) GROUP BY w
UNION ALL
SELECT 'yellow', 'day', count(*)::DOUBLE / count(DISTINCT day), median(d) FILTER (WHERE d BETWEEN 60 AND 10800), avg(d) FILTER (WHERE d BETWEEN 60 AND 10800), median(mi) FILTER (WHERE mi > 0) FROM yw
UNION ALL
SELECT 'via' AS src, w, count(*)::DOUBLE / (SELECT count(DISTINCT day) FROM vw), median(d) FILTER (WHERE d BETWEEN 60 AND 10800), avg(d) FILTER (WHERE d BETWEEN 60 AND 10800), median(mi) FILTER (WHERE mi > 0)
FROM (SELECT *, CASE WHEN h IN (8,9) THEN 'w08' WHEN h IN (13,14) THEN 'w13' WHEN h IN (17,18) THEN 'w17' ELSE 'all_other' END AS w FROM vw) GROUP BY w
UNION ALL
SELECT 'via', 'day', count(*)::DOUBLE / count(DISTINCT day), median(d) FILTER (WHERE d BETWEEN 60 AND 10800), avg(d) FILTER (WHERE d BETWEEN 60 AND 10800), median(mi) FILTER (WHERE mi > 0) FROM vw
ORDER BY 1, 2;
.once E:/RideBoundData/company-intake/2026-10-23/sp/wf20/am2017/facts/daycount_weekday_2019.csv
SELECT 'yellow' AS src, count(DISTINCT day) AS days, count(*) AS trips FROM yw UNION ALL SELECT 'via', count(DISTINCT day), count(*) FROM vw;
