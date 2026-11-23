# Kiểm repo sau khi gom bộ mô phỏng nhanh (23/11/2026)

File này chỉ nằm trên nhánh `kiem-o-nha-2311`. Code cần kiểm là nhánh `dev` tại `cde8287`.
Các lệnh dùng biến cho mọi thư mục dữ liệu; bước 0 đặt các biến đó theo đường dẫn trên máy đang chạy.

## Bối cảnh

Nhánh `dev` có 4 commit mới sau `68cd8ff`, đều đứng tên `tsunflowerr`:

| Commit | Nội dung |
|---|---|
| `1ecb941` | Thêm `src/fastsim`: bộ mô phỏng nhanh khớp FleetPy đã sinh Bảng 5.4–5.11. Chép nguyên văn từ gói ngày 18/11 (`goi-may-nha-20261118`), chỉ đổi đường dẫn |
| `abb992d` | Bỏ chế độ plain (`src/emulator`), bỏ `src/analysis` và mọi dòng kết quả; các script sinh dữ liệu chuyển sang `src/datagen` |
| `6dfcbd1` | Thêm `src/fastsim/fullsys`: bộ sinh đầu vào của chương trình chính (`emulate_fullsys*.js`, `emulate_forecast_gate.js`, `emulate_sequences*.js`), chép nguyên văn, chỉ đổi đường dẫn |
| `cde8287` | README mới (cài đặt, demo, chạy lại toàn bộ); thêm `src/fastsim/sample/`: `kiem_nhanh.sh`, `expected.tsv`, `check_sample.js`, `compare_riders.js` |

Đã kiểm trước, không cần dữ liệu ổ E: và không cần Python:
- sha256 của mọi file chép;
- cú pháp mọi file JS và shell;
- các bộ chạy nạp đủ module và tham số;
- từ dòng kết quả gốc, các bảng 5.4–5.11 khớp 289/289 số với file `.tex` ngày 22/11;
- `compare_riders.js` chạy trên dữ liệu cũ cho đúng 125.871/125.964 khách và 353/360 lượt như mục 5.2.

Phần cần kiểm ở đây là phần **chạy thật** trên dữ liệu ổ E:.

## Luật

- Chỉ đọc dữ liệu cũ. Mọi đầu ra ghi vào một thư mục **mới** (`$OUT`).
- Không commit, không push, không sinh file .exe. Script ghi đè file nào trong repo thì `git checkout -- <file>` sau khi ghi lại kết quả.
- Báo lại theo mẫu ở cuối, bằng tiếng Việt.

## 0. Lấy code và đặt biến

```bash
cd <repo RideBound đang dùng>
git fetch origin && git checkout dev && git merge --ff-only origin/dev
git log --oneline -5                     # đầu danh sách: cde8287 [9.04] ...
R=$(pwd)/src/fastsim
# Đặt theo đường dẫn của máy này (các thư mục mà gói 18/11 ghi trong FILES-ORIGINAL-PATHS.tsv):
RBD=<thư mục RideBoundData>
export RB_DATA=$RBD/<thư mục sp của bộ dữ liệu 23/10, chứa wf14 wf15 wf20 wf5 wf6 wf7 wf17 wf21>
export RB_ROOTS="$RBD/research/fullsys-f14-v2;$RBD/research/fullsys-f16-v2;$RBD/research/adapter60-v1;$RBD/research/via-full-v1"
export RB_FLEET81_ROOT=$RBD/research/adapter60-fleet81
export RB_STORE=$RBD/research/adapter60-v1/results
export PY=<python của môi trường fleetpy-1.0.2>
A60=<thư mục adapter60-2026-11-08 (bộ chạy Chương 5 ngày 8/11)>
FS=<thư mục fastrun-2026-11-02/fullsys>
EMU15=<thư mục emulator-wf15 bản 2026-10-23>
SYNC=<thư mục sync-rerun của bản kiểm 16/11 (kiem-lai-20261116)>
DAYS=<nhom-ngay.json trong chung-cu/via-20261030 của repo khóa luận>
OUT=$RBD/research/fastsim-check-2311
[ -e "$OUT" ] && echo "DỪNG: $OUT đã có" || mkdir -p "$OUT"
```

## 1. Code trong repo bằng bản gốc, trừ các dòng đường dẫn

```bash
ck() { local sub=$1 od=$2; shift 2; for f in "$@"; do n=$(diff "$od/$f" "$R/$sub/$f" | grep -c '^>'); echo "$n $sub/$f"; done; }
{
ck adapter60 $A60 $(cd $R/adapter60 && ls *.js *.py *.sh *.txt flat_iter*.json calib-result.json)
ck engines $FS engine_wf15_clint.js
ck engines $A60 engine_wf15_clint_pp.js
ck fullsys $FS emulate_fullsys.js emulate_fullsys_f16.js emulate_forecast_gate.js emulate_sequences.js emulate_sequences_f16.js
ck wf20/runner $RB_DATA/wf20/fullsys/code/runner engine17.js identity.js lambda.js lib.js netfull.js normalizer_emul.js scenario.js sscore.js
ck wf20/scripts $RB_DATA/wf20/fullsys/code/scripts shcore.js shlib.js forecast.js metrics_sh.js
ck wf5 $RB_DATA/wf5/ds-nyc-via/scripts network.js nyclib.js
ck wf15emu $EMU15 lib.js identity.js normalizer_emul.js engine.js
ck sync-rerun $SYNC table57.js compare_sig.js repro_spotcheck.sh
n=$(diff "$DAYS" "$R/adapter60/nhom-ngay.json" | grep -c '^>'); echo "$n adapter60/nhom-ngay.json"
} | tee $OUT/1-code.txt | awk '$1!=0'
```

Kết quả đúng là danh sách sau; mọi file khác bằng 0. Mỗi file khác 0 thì xem `diff`: chỉ được thấy dòng đường dẫn hoặc `import os`.

```
4 adapter60/emulate_adapter.js      4 adapter60/emulate_adapter_eta.js   4 adapter60/emulate_adapter_pp.js
2 adapter60/aggregate.py            2 adapter60/calib_pad.py             3 adapter60/check_eta_gates.py
2 adapter60/check_fleet62.py        4 adapter60/check_gates.py           5 adapter60/convert60.sh
5 adapter60/convert_fleet81.sh      2 adapter60/eta_analysis.py          2 adapter60/full_common.py
2 adapter60/gen_tables.py           3 adapter60/make_cells.py            2 adapter60/make_figs.py
2 adapter60/run_all.sh              3 adapter60/run_eta.sh               2 adapter60/run_pp.sh
3 adapter60/run_shards.sh           3 adapter60/run_shards_eta.sh        3 adapter60/run_shards_pp.sh
2 adapter60/shadow_scope.py         2 engines/engine_wf15_clint.js       2 engines/engine_wf15_clint_pp.js
5 fullsys/emulate_fullsys.js        5 fullsys/emulate_fullsys_f16.js     3 fullsys/emulate_forecast_gate.js
3 fullsys/emulate_sequences.js      3 fullsys/emulate_sequences_f16.js   4 wf15emu/lib.js
1 wf20/runner/lib.js                3 wf20/runner/netfull.js             1 wf20/scripts/shlib.js
5 sync-rerun/repro_spotcheck.sh
```

## 2. Tham số trong repo bằng bản trên ổ E:

```bash
(cd $R/data && find . -type f | while read f; do cmp -s "$f" "$RB_DATA/$f" && echo "OK $f" || echo "KHAC $f"; done) | tee $OUT/2-data.txt
```

Kết quả đúng: 14 dòng `OK`, không có `KHAC`.

## 3. Chạy lại bằng code của repo, so chữ ký từng dòng với kết quả gốc

Mỗi họ chạy lại một ô (10–20 phút):

```bash
bash $R/sync-rerun/repro_spotcheck.sh $OUT/spot 2>&1 | tee $OUT/3-spot.txt
head -1 $R/adapter60/cells-60.txt > $OUT/cell-nce.txt
(cd $R/adapter60 && node emulate_adapter.js --cells $OUT/cell-nce.txt --arms G2:NCE35:N,G2:OCE35:N --roots "$RB_ROOTS" --out $OUT/nce)
node $R/sync-rerun/compare_sig.js $OUT/nce $SYNC/rows/nce-oce60 nce-oce | tee -a $OUT/3-spot.txt
grep -E 'sig equal|SIG DIFFERS|not in stored' $OUT/3-spot.txt
```

Kết quả đúng: 7 dòng tóm tắt (main60, fc30, alpha10, fleet20, pen300, eta30, nce-oce). Mỗi dòng có `differ 0; not found in stored 0`.

**Tùy chọn, khoảng 3 giờ:** chạy lại toàn bộ bằng các lệnh ở README mục 3.2, với `RB_RESULTS=$OUT/full`. Sau đó so từng họ với kết quả gốc:

```bash
for f in main60 fc30 alpha10 fleet-20 fleet-29 fleet-43 fleet-52 fleet-62 fleet-67 fleet-81 pen300 eta30; do
  node $R/sync-rerun/compare_sig.js $OUT/full/$f $RB_STORE/$f $f | tail -1; done | tee $OUT/3-full.txt
```

## 4. Dựng bảng bằng script của repo, trên kết quả gốc

```bash
cd $R/adapter60
export RB_RESULTS=$RB_STORE
"$PY" -X utf8 aggregate.py > $OUT/4-aggregate.txt; cmp aggregate.json $A60/aggregate.json && echo "aggregate.json giống bản gốc"
"$PY" -X utf8 gen_tables.py > $OUT/4-gen_tables.txt
"$PY" -X utf8 shadow_scope.py > $OUT/4-shadow_scope.txt
"$PY" -X utf8 eta_analysis.py > $OUT/4-eta_analysis.txt; cmp eta_analysis.json $A60/eta_analysis.json && echo "eta_analysis.json giống bản gốc"
node ../sync-rerun/table57.js $SYNC/rows/nce-oce60 $SYNC/rows/nce-oce60b $SYNC/rows/nce-oce60c > $OUT/4-table57.txt
rm -f aggregate.json eta_analysis.json; unset RB_RESULTS; cd -
```

Kết quả đúng: hai dòng "giống bản gốc". Đối chiếu số in ra với file `.tex` ngày 22/11 theo bảng ở README mục 3.2.

## 5. Gói đầu vào mẫu cho `src/fastsim/sample/`

README mục 2.1 dùng hai ô mẫu `2019-09-19-w08-r0` và `2019-09-19-w17-r0`. Gói chỉ gồm **file ô và ma trận thời gian đi** của hai ô, đặt đúng chỗ `kiem_nhanh.sh` đọc. Fixture sẽ được sinh lại từ chúng.

```bash
S=$R/sample/wf14/scenarios; mkdir -p $S/weekdays-P512 $S/matrices
for c in 2019-09-19-w08-r0 2019-09-19-w17-r0; do
  cp $RB_DATA/wf14/scenarios/weekdays-P512/$c.json $S/weekdays-P512/
  m=$(node -e "console.log(require('$RB_DATA/wf14/scenarios/weekdays-P512/$c.json').travel.base.matrixFile)")
  cp $RB_DATA/wf14/scenarios/matrices/$m $S/matrices/
done
du -sh $R/sample/wf14; (cd $R/sample && find wf14 -type f | sort | xargs sha256sum) | tee $OUT/5-sample.sha256
```

Kiểm gói bằng chính lệnh demo của README. Lệnh này tự tải mạng đường từ Zenodo, sinh fixture, chạy 36 dòng và so với `expected.tsv`:

```bash
bash $R/sample/kiem_nhanh.sh $OUT/demo 2>&1 | tee $OUT/5-demo.txt | tail -3
for c in w08 w17; do diff -r $OUT/demo/fixtures/fixtures/d20190919-vf-r0-$c $RBD/research/fullsys-f14-v2/fixtures/d20190919-vf-r0-$c > /dev/null && echo "fixture $c giống bản đã dùng" || echo "fixture $c KHÁC"; done
```

Kết quả đúng:
- dòng `sample check: 36 rows equal, 0 differ, 0 without an expected row`;
- hai dòng "fixture … giống bản đã dùng".

Gửi về: thư mục `src/fastsim/sample/wf14` (nén thành `mau-dau-vao.tar.gz`) cùng `5-sample.sha256`. **Không commit.**

```bash
tar -czf $OUT/mau-dau-vao.tar.gz -C $R/sample wf14 && ls -la $OUT/mau-dau-vao.tar.gz && sha256sum $OUT/mau-dau-vao.tar.gz
```

## 6. Một lượt RideBound C# trên ô mẫu (khoảng 30 phút)

Chạy đúng các lệnh ở README mục 2.2, từ thư mục gốc của repo, với thư mục demo là `$OUT/demo` thay cho `demo`. Sau đó kiểm thêm hai điều:
- đầu vào Shadow vừa sinh giống bản đã niêm phong;
- dòng dự đoán của ô giống bản đã niêm phong.

```bash
cmp $OUT/demo/fixtures/predictions/shadow-inputs/d20190919-vf-r0-w08.json $RBD/research/fullsys-f14-v2/predictions/shadow-inputs/d20190919-vf-r0-w08.json && echo "shadow-inputs giống bản niêm phong"
diff <(grep d20190919-vf-r0-w08 $OUT/demo/fixtures/predictions/fullsys-f14-predictions.tsv) <(grep d20190919-vf-r0-w08 $RBD/research/fullsys-f14-v2/predictions/fullsys-f14-predictions.tsv) > /dev/null && echo "dự đoán giống bản niêm phong"
```

Kết quả đúng:
- hai dòng "giống bản niêm phong";
- `compare_riders.js` in `FS-G2-SHHZ71-N|d20190919-vf-r0-w08: ... -> IDENTICAL`.

Nếu một lệnh của README chạy không được, ghi lại nguyên văn lỗi. Đó chính là chỗ README cần sửa.

## Mẫu báo lại

```
KIỂM REPO cde8287 (ngày ...)
1. code: các file khác 0 = <"đúng như mong đợi" hoặc danh sách>; diff chỉ có dòng đường dẫn: có/không (nếu không: dán diff)
2. data: OK <n>/14, KHAC <...>
3. spot: <7 dòng tóm tắt>;  full (nếu chạy): <12 dòng>
4. aggregate.json giống: có/không; eta_analysis.json giống: có/không; Bảng 5.4..5.11, Hình 5.3, đội xe: khớp/lệch <chi tiết>
5. gói mẫu: dung lượng <...>, tar.gz <... MB>, sha256 <...>; kiem_nhanh.sh: <dòng sample check>; fixture w08/w17 giống: có/không
6. một lượt C#: shadow-inputs giống: có/không; dự đoán giống: có/không; compare_riders: <dòng kết quả>; thời gian chạy, RAM
Lỗi khi làm theo README (nếu có): <lệnh, lỗi nguyên văn>
```
