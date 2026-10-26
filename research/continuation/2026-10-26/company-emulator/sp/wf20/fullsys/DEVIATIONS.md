# wf20/fullsys DEVIATIONS (label wf20-fullsys-exploratory-post-audit)

Timestamped, in order. Plan: PLAN.md (hash in PLAN-HASH.txt).

- D1 (2026-10-26T04:11:06Z), analysis addition, before any production result was read: `analysis/labels.js` applies the PLAN §5
  label rules mechanically to `results/tables.json` (no new hypothesis, no change of bands or flip criteria). sha256
  7d8fa970a6d1275cad0ecc89780ba527ef2902b173ea2a4496d2d027fc8806d8. Written while the production job was running; nothing from `results/rows.ndjson` had been opened.
  Notes on how PLAN wording is encoded: "days" = non-tied days; P10/P17/P6 "outside with sign p < 0.05" = any significant sign test
  when the mean lies outside the band; P8 flip uses rel ≤ −20 % and ≥ 75 % of non-tied days negative; P18 "REVERSED" means only
  |Δ| > 5 pp (= calibration caveat, PLAN §5).
- D2 (2026-10-26T04:11:52Z), presentation addition, before any production result was read: `analysis/keys.js` (compact extract of
  `results/tables.json`, no new statistic), sha256 ffc84a8ee3607d3b1a8f9eacb7b2f99b97d7a2d079abda8efdc88f9d59f68bd8. Dry-run only on `work/evidence/evidence.json`.
  - D3 correction (2026-10-26T04:38:56Z): the first fix (sed) left a literal newline inside a quoted string (syntax error; the sha256 recorded above is of that broken file, and no v2 was written then). Fixed by Edit; final keys.js sha256 62ffe018127ac79b944710ca84fa8a563113fceb5dc6d133fc3393232b10e7e2; `results/key_numbers.v2.txt` written from it.
- D3 (2026-10-26T04:39:07Z), presentation bug found after production (entry written late; the "D3 correction" line above
  belongs to it and its phrase "sha256 recorded above" refers to nothing because this entry had not been appended when the broken
  sed fix failed): `analysis/keys.js` called P('', header) with two arguments, so the 34 section headers were dropped from
  `results/key_numbers.txt` (numbers correct, headers missing; file kept unchanged). Fixed (header passed as one string, by Edit),
  final sha256 62ffe018127ac79b944710ca84fa8a563113fceb5dc6d133fc3393232b10e7e2; output `results/key_numbers.v2.txt`. No statistic changed.
