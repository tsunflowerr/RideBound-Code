# RideBound

RideBound là hệ điều phối xe đi chung (ride-pooling) **giữ lời hứa giờ trả với từng khách**. Khi khách đặt xe, hệ hứa
một giờ trả. Mỗi lần điều phối lại, phần thay đổi giờ trả **do quyết định điều phối gây ra** được cộng dồn và không được
vượt một hạn mức cấp riêng cho từng khách (giới hạn cộng dồn). Hệ kết hợp cách cấp hạn mức theo khả năng đi chung
(Shadow) và dự báo thời gian đi theo khung giờ.

Repo có hai chương trình, dùng cho các thực nghiệm của báo cáo:

| Chương trình | Ở đâu | Dùng cho |
|---|---|---|
| **Chương trình chính**: RideBound (C#) chạy trong bộ mô phỏng giao thông FleetPy | `src/core`, `src/adapter` | Kết quả chính của cấu hình kết hợp (360 lượt chạy, tập ngày F14 và F16) |
| **Mô phỏng nhanh**: cùng luật điều phối viết lại bằng JavaScript, chạy trên đúng đầu vào của chương trình chính | `src/fastsim` | So sánh các cơ chế giới hạn, Shadow, dự báo, độ nhạy (60 ngày và 30 ngày) |

Dữ liệu đầu vào được sinh từ dữ liệu công khai (chuyến xe Via của TLC New York 2019, mạng đường FleetPy Manhattan)
bằng `src/datagen`. Repo không chứa kết quả; mọi kết quả sinh ra khi chạy.

```
README.md
references/        bài báo và tài liệu được trích dẫn (README.md liệt kê nguồn và giấy phép)
src/
  core/            C# .NET 10: Runner (máy chủ giao thức NDJSON) và bộ điều phối: sinh phương án, cổng cam kết,
                   chọn phương án bằng CP-SAT, Shadow, dự báo; configs/final: cấu hình các lượt chạy cuối; tests/
  adapter/         Python 3.10 + FleetPy 1.0.2: chạy một lượt hoặc cả đợt, chuyển ô thành fixture, đọc bảng từng khách
  fastsim/         Node 20 (+ Python 3 để dựng bảng): mô phỏng nhanh; data/ = tham số đã đóng băng;
                   sample/ = hai ô mẫu và lệnh kiểm nhanh (src/fastsim/README.md)
  datagen/         Node 20 + DuckDB: sinh các ô từ dữ liệu TLC, tải mạng đường, xuất mạng cho Shadow; sha256 để kiểm
```

---

## 1. Cài đặt môi trường

| Công cụ | Phiên bản | Cần cho |
|---|---|---|
| Node.js | 20 (đã dùng 20.20.2). Chỉ dùng module có sẵn, không cần `npm install` | `src/fastsim`, `src/datagen` |
| Python | 3.10 (đã dùng 3.10.20), có numpy | `src/adapter`; bộ chuyển fixture; script dựng bảng (thư viện chuẩn) |
| FleetPy | 1.0.2, commit `053aa9d4fcfde91c5d303435d5748f9206c071b0` (https://github.com/TUM-VT/FleetPy, MIT) | chạy chương trình chính |
| .NET SDK | 10.0.301 trở lên trong dòng 10.0 (`src/core/global.json`) | `src/core` |
| DuckDB CLI | 1.5.5 | chỉ khi sinh dữ liệu đầy đủ từ TLC (mục 3.1) |
| bash, `sha256sum`, `gzip` | Linux, macOS hoặc Git Bash trên Windows | các lệnh trong README |

```bash
# Python và FleetPy (môi trường conda đã dùng cho các lượt chạy cuối, ghim từng phiên bản)
conda env create -f src/adapter/environment.lock.yml        # tạo môi trường tên fleetpy-1.0.2
conda activate fleetpy-1.0.2
git clone https://github.com/TUM-VT/FleetPy
git -C FleetPy checkout 053aa9d4fcfde91c5d303435d5748f9206c071b0

# C#: OR-Tools và các gói khác được tải tự động
cd src/core && dotnet restore RideBound.slnx && cd ../..
```

- Mọi lệnh trong README chạy từ thư mục gốc của repo, trừ khi lệnh tự `cd`.
- `environment.lock.yml` ghi bản build của Windows 64-bit; trên hệ khác dùng cùng số phiên bản và bỏ phần build.
- Trên Windows (Git Bash), Node không hiểu đường dẫn dạng `/c/...` trong biến môi trường. Khi cần đường dẫn tuyệt đối,
  dùng `$(pwd -W)`; các lệnh dưới viết `$(pwd -W 2>/dev/null || pwd)` để chạy được cả trên Linux và macOS.
- Các lệnh viết `python`. Nếu Python của môi trường `fleetpy-1.0.2` không nằm trên PATH (chưa `conda activate`), thay
  `python` bằng đường dẫn đầy đủ tới nó, và đặt `export PY=<đường dẫn đó>` cho các script `.sh`.

---

## 2. Demo

### 2.1 Mô phỏng nhanh trên hai ô mẫu (5–10 phút)

`src/fastsim/sample/wf14/` chứa đầu vào của hai ô: ngày 19/9/2019, khung 8:00–10:00 và 17:00–19:00, mẫu nhu cầu r0
(512 yêu cầu, 62 xe). Mỗi ô gồm file yêu cầu, ma trận thời gian đi và provenance; cả sáu file trùng sha256 với ô đã dùng
cho báo cáo (`src/datagen/expected/wf14-60day-cells.sha256`). Lệnh dưới dựng một data root nhỏ (tải mạng đường từ Zenodo một lần), chuyển hai ô thành fixture
bằng bộ chuyển của chương trình chính, chạy mô phỏng nhanh với các cấu hình của Chương 5 rồi so từng dòng kết quả với
`expected.tsv`.

```bash
bash src/fastsim/sample/kiem_nhanh.sh demo          # demo/ là thư mục mới
```

Mỗi dòng in `equal <ô> <cấu hình> served <số khách phục vụ>, reversals <số khách có giờ trả đổi qua lại>`.
Dòng cuối phải là `sample check: 36 rows equal, 0 differ, 0 without an expected row`. "Equal" nghĩa là chữ ký của
lượt chạy (SHA-256 của mọi khách: phục vụ hay không, xe, giờ báo đầu, giờ đến, các lần đổi giờ) giống hệt lượt chạy đã
dùng cho báo cáo.

### 2.2 Một lượt RideBound C# và so từng khách với mô phỏng nhanh (khoảng 30 phút)

Lượt chạy dùng ô mẫu buổi sáng, cấu hình đối chứng `FS-G2-SHHZ71-N` (Shadow, hạn điều chỉnh, không dự báo). Cần đã chạy
mục 2.1 (dùng lại `demo/`), có FleetPy và .NET. Một lượt cần 15–25 phút và tới 6 GB RAM.

```bash
export RB_DATA=$(cd demo && (pwd -W 2>/dev/null || pwd))/data
P=demo/fixtures; C=d20190919-vf-r0-w08; A=FS-G2-SHHZ71-N; F=src/core/configs/final
DOTNET=$(command -v dotnet)       # đường dẫn đầy đủ tới file chạy dotnet (Linux, macOS)
# Windows (Git Bash): DOTNET="C:/Program Files/dotnet/dotnet.exe"   (run_job.py kiểm file này có tồn tại)

# 1. Driver của lượt chạy (từ driver mà bộ chuyển đã ghi)
python -B src/adapter/make_run_drivers.py write $P/drivers/$C.driver.json $P/drivers-run/$C.driver.json complete-graph-direct-v1 gzip-v1

# 2. Dự đoán của mô phỏng nhanh cho ô này, kèm đầu vào Shadow của Runner (predictions/shadow-inputs/)
mkdir -p $P/predictions
FS_ONLY=2019-09-19-w08-r0 node src/fastsim/fullsys/emulate_fullsys.js adapter $P $P/predictions/fullsys-f14-predictions.tsv

# 3. Mạng đường cho bộ chấm điểm Shadow của C# (D.u16, network.json)
node src/datagen/export_network.js manhattan demo/network $RB_DATA

# 4. Cấu hình lượt chạy từ các mẫu của lượt chạy cuối
python -B src/adapter/make_job_configs.py --data $P --network $(cd demo && (pwd -W 2>/dev/null || pwd))/network \
  --g2-rce35 $F/templates/G2-RCE35.json --g2-eohz35 $F/templates/G2-EOHZ35.json --wp4-template $F/wp4-via-g2k8-a1-fast.json

# 5. Runner (không sinh file .exe)
(cd src/core && dotnet publish RideBound.Runner/RideBound.Runner.csproj -c Release -o ../../runner -p:UseAppHost=false)

# 6. Lượt chạy
DOTNET_GCHeapHardLimit=0x180000000 python -B src/adapter/run_job.py --label $A-$C-fin2 --role $A \
  --fleetpy-root FleetPy --runner-root runner --dotnet "$DOTNET" \
  --commitment-config $P/job-configs-v2/commitment/SHHZ71.json --wp4-config $P/job-configs-v2/wp4/$A/$C.json \
  --scenario $P/fixtures/$C/scenario-content.json --derivative-manifest $P/fixtures/$C/derivative-manifest.json \
  --normalization-report $P/fixtures/$C/normalization-report.json --selection-frame $P/fixtures/$C/selection-frame.json \
  --driver $P/drivers-run/$C.driver.json --world $P/worlds/$C.world.json --master-seed 7 --transcript keep \
  --output demo/runs/$A-$C

# 7. Bảng từng khách của lượt chạy, rồi so với dự đoán của mô phỏng nhanh
python -B src/adapter/riders.py --root demo/runs --out demo/real.tsv --arms $A
node src/fastsim/sample/compare_riders.js demo/real.tsv $P/predictions/fullsys-f14-predictions.tsv
```

Kết quả mong đợi: dòng `FS-G2-SHHZ71-N|d20190919-vf-r0-w08` kết thúc bằng `IDENTICAL`, tức mọi khách giống nhau trên
chín trường (giờ đặt, giờ báo đầu, giờ đến, phục vụ hay không, tổng và số lần đổi do điều phối, thay đổi ròng, thay đổi
lớn nhất, có đi chung không). Lần kiểm trên Windows: 335/335 khách giống nhau, 22 phút, RAM đỉnh khoảng 5 GB. Trên
toàn bộ 360 lượt của báo cáo, phép so này cho 125.871/125.964 khách giống nhau và 353/360 lượt giống hoàn toàn.

### 2.3 Test

```bash
cd src/core && dotnet test RideBound.slnx -c Release && cd ../..          # 7 dự án, 884 test
(cd src/adapter/tests && python -B -m unittest test_replay test_run_job test_run_campaign test_make_job_configs test_riders)
```

Test của adapter có dùng FleetPy chạy khi đặt `RIDEBOUND_FLEETPY_ROOT=<thư mục FleetPy>`
(`python -B -m unittest discover -s src/adapter/tests -t src/adapter/tests -p "test_*.py"`).

---

## 3. Chạy lại toàn bộ thực nghiệm

### 3.1 Sinh dữ liệu đầu vào (khoảng 20 GB, vài giờ)

```bash
export RB_DATA=$(pwd -W 2>/dev/null || pwd)/rb-data         # thư mục mới

# 1. Mạng đường FleetPy Manhattan (Zenodo 15187906, CC BY 4.0) và bảng thời gian đi ngắn nhất
node src/datagen/fetch_network.js $RB_DATA
(cd $RB_DATA && sha256sum -c $OLDPWD/src/datagen/expected/network.sha256)

# 2. Chuyến Via (HV0004) từ dữ liệu TLC công khai, mỗi tháng 2019-02..2019-12
mkdir -p tlc
for m in 02 03 04 05 06 07 08 09 10 11 12; do
  duckdb -bail -cmd "SET VARIABLE month = '2019-$m';
    SET VARIABLE source = 'https://d37ci6vzurychx.cloudfront.net/trip-data/fhvhv_tripdata_2019-$m.parquet';
    SET VARIABLE zones = 'https://d37ci6vzurychx.cloudfront.net/misc/taxi_zone_lookup.csv';
    SET VARIABLE out_dir = 'tlc';" -f src/datagen/via_month.sql
done
for s in 60 F16; do
  duckdb -bail -cmd "SET VARIABLE via_dir = 'tlc'; SET VARIABLE days = 'src/datagen/data/days_$s.csv';
    SET VARIABLE out = 'tlc/via_windows_$s.csv';" -f src/datagen/via_windows.sql
done

# 3. Các ô: 60 ngày (F14 nằm trong đó) và F16; mỗi ô 512 yêu cầu rút bằng hạt giống cố định, ba mẫu r0, r1, r2
for k in 0 1 2; do
  node src/datagen/gen17.js --days src/datagen/data/days_60.csv --windows tlc/via_windows_60.csv \
    --factors src/datagen/data/factors.json.gz --data $RB_DATA --out $RB_DATA/wf14/scenarios --fmax 512:96 --resample $k
  node src/datagen/gen17.js --days src/datagen/data/days_F16.csv --windows tlc/via_windows_F16.csv \
    --factors src/datagen/data/factors.json.gz --data $RB_DATA --out $RB_DATA/wf17/r1-identity-fence-pad/cells/f16 \
    --seedtag wf17-fresh --fmax 512:96 --resample $k
done
(cd $RB_DATA/wf14/scenarios && sha256sum -c --quiet $OLDPWD/src/datagen/expected/wf14-60day-cells.sha256) && echo "60-day cells OK"
(cd $RB_DATA/wf17/r1-identity-fence-pad/cells/f16 && sha256sum -c --quiet $OLDPWD/src/datagen/expected/wf17-f16-cells.sha256) && echo "F16 cells OK"

# 4. Tham số đã đóng băng (ngưỡng Shadow, prior, chỉ số dự báo)
cp -r src/fastsim/data/. $RB_DATA/

# 5. Fixture: đầu vào chung của hai chương trình (khoảng 15 GB). Ba thư mục: F14 (84 ô), F16 (96 ô), 276 ô còn lại của
#    tập 60 ngày; thêm ô 81 xe cho khảo sát quy mô đội xe.
L=src/fastsim/adapter60
tr -d '\r' < $L/cells-f14.txt > f14.txt; tr -d '\r' < $L/cells-30.txt | grep -v -x -F -f f14.txt > f16.txt
tr -d '\r' < $L/cells-60-convert.txt > rest60.txt
conv() {   # <danh sách ô> <thư mục đích>
  for c in $(cat $1); do
    d=${c:0:10}; vf="d${d//-/}-vf-${c:15:2}-${c:11:3}"
    src=$RB_DATA/wf14/scenarios/weekdays-P512/$c.json; [ -f $src ] || src=$RB_DATA/wf17/r1-identity-fence-pad/cells/f16/weekdays-P512/$c.json
    [ -d $2/fixtures/$vf ] || python -B src/adapter/convert_via_full_v2.py $src $2 $vf > /dev/null
  done; }
conv f14.txt $RB_DATA/../panel-f14; conv f16.txt $RB_DATA/../panel-f16; conv rest60.txt $RB_DATA/../fixtures60
(cd $L && RB_FIXTURE_OUT=$RB_DATA/../fixtures81 bash convert_fleet81.sh)
```

Các ô sinh ra phải trùng sha256 với ô đã dùng cho báo cáo (bước 3). Ngưỡng Shadow θ và x\* (72 % với INS, 71 % với G2)
được hiệu chuẩn trên 55 ngày riêng trước khi đánh giá và đóng băng trong `src/fastsim/data/wf15/frozen/shadow-params.json`.

### 3.2 Mô phỏng nhanh: Bảng 5.4–5.11, Hình 5.2–5.3 (khoảng 3 giờ với 6 tiến trình)

```bash
B="$(cd $RB_DATA/.. && (pwd -W 2>/dev/null || pwd))"
export RB_ROOTS="$B/panel-f14;$B/panel-f16;$B/fixtures60" RB_FLEET81_ROOT="$B/fixtures81"
export RB_RESULTS=$(pwd -W 2>/dev/null || pwd)/results         # thư mục mới
cd src/fastsim/adapter60
bash run_shards.sh main60 cells-60.txt "INS:U:N,INS:EO35:N,INS:EOHZ35:N,INS:RCE35:N,G2:U:N,G2:EO35:N,G2:EOHZ35:N,G2:RCE35:N,G2:SHHZ71:N,G2:SHRC71:N" 6
bash run_shards.sh nce-oce60 cells-60.txt "G2:RCE35:N,G2:EOHZ35:N,G2:NCE35:N,G2:OCE35:N" 6
bash run_shards.sh fc30 cells-30.txt "G2:SHRC71:N,G2:SHRC71:CL,G2:SHRC71:CLc" 6
A=""; for a in 0 10 20 50 75 100; do A="$A,G2:EOHZ$a:N,G2:RCE$a:N"; done
bash run_shards.sh alpha10 cells-alpha10.txt "${A#,}" 6
for n in 20 29 43 52 62 67 81; do ROOTS_OVERRIDE="$RB_FLEET81_ROOT" bash run_shards.sh fleet-$n cells-fleet30.txt "G2:EOHZ35:N,G2:RCE35:N" 6 "--nveh $n"; done
bash run_shards_pp.sh pen300 cells-pen60.txt "G2:EOHZ35:N,G2:RCE35:N" 6 "--g2pen 300000"
python calib_pad.py        # hiệu chỉnh đều: tối đa 6 vòng (padcal-0..5); ghi lại flat_iter1..5.json và calib-result.json
bash run_shards_eta.sh eta30 cells-30.txt "G2:SHRC71:N,G2:SHRC71:CL,G2:SHRC71:CLc,G2:SHRC71:PAD,G2:SHHZ71:N,G2:SHHZ71:CL" 6 "--flat $(pwd -W 2>/dev/null || pwd)/flat_iter5.json"
```

Dựng bảng từ thư mục kết quả vừa chạy (`RB_RESULTS` vẫn đặt như trên):

| Bảng trong báo cáo | Lệnh | Ghi chú |
|---|---|---|
| 5.4, 5.5, 5.6, 5.11; Hình 5.3; quy mô đội xe | `python aggregate.py` rồi `python gen_tables.py`; `python shadow_scope.py` | Tên khối trong `gen_tables.py` là số bảng cũ: "Bảng 5.5" = 5.4, "Bảng 5.6" = 5.5 và 5.6 (cột G2+Shadow), "Ngân sách" = Hình 5.3, "Bảng 5.12" = quy mô đội xe, "Bảng 5.13" = 5.11 |
| Hình 5.2 | `python make_figs.py` (cần matplotlib) | ghi vào `RB_FIGURES` hoặc `src/fastsim/results/figures` |
| 5.7 | `node ../sync-rerun/table57.js $RB_RESULTS/nce-oce60` | |
| 5.8, 5.9, 5.10 | `PYTHONHASHSEED=0 python eta_analysis.py` rồi `python gen_eta_tables.py` | `eta_analysis.py` đọc `$RB_RESULTS/eta30`, in các mức và ghi `eta_analysis.json`; `gen_eta_tables.py` in các hiệu kèm khoảng tin cậy 95 %. Tên khối là số bảng cũ: "5.4: bang muc" = 5.8, "hieu du bao - hieu chinh deu" = 5.9, "5.6: phan ra" = 5.10. Khối "5.5" (= Bảng 5.3, chương trình chính) chỉ in khi có `full_common.json` |

`PYTHONHASHSEED=0` cố định thứ tự cộng của `eta_analysis.py` (nó cộng số thực theo thứ tự duyệt một `set`). Thiếu biến này,
`eta_analysis.json` giữa hai lần chạy lệch nhau ở chữ số thứ 13; bảng in ra không đổi.

`calib_pad.py` chọn hệ số hiệu chỉnh đều sao cho thời lượng báo đầu trung bình lệch dự báo không quá 0,5 giây, tối đa
sáu vòng. Hệ số cuối là `flat_iter5.json`; các file nó ghi lại phải giống bản trong repo (`git status` không thấy thay đổi).

### 3.3 Chương trình chính: 360 lượt chạy (90–150 giờ máy)

Mỗi lượt là một ô × một cấu hình: `FS-G2-SHRC71-CL` (cấu hình kết hợp: Shadow, giới hạn cộng dồn, dự báo) hoặc
`FS-G2-SHHZ71-N` (đối chứng). F14: 84 ô, 168 lượt; F16: 96 ô, 192 lượt. Một lượt cần 15–25 phút và tới 6 GB RAM.
Mỗi tập ngày dùng thư mục fixture riêng của mục 3.1 (`panel-f14`, `panel-f16`).

```bash
F=src/core/configs/final; NET=$(pwd -W 2>/dev/null || pwd)/network
P14=$RB_DATA/../panel-f14; P16=$RB_DATA/../panel-f16
# 1. Driver của từng lượt chạy
for P in $P14 $P16; do
  for d in $P/drivers/*.driver.json; do
    python -B src/adapter/make_run_drivers.py write $d $P/drivers-run/$(basename $d) complete-graph-direct-v1 gzip-v1
  done
  mkdir -p $P/predictions
done
# 2. Dự đoán của mô phỏng nhanh và đầu vào Shadow của Runner; hồ sơ cổng dự báo (F14); mạng cho Shadow
node src/fastsim/fullsys/emulate_fullsys.js adapter $P14 $P14/predictions/fullsys-f14-predictions.tsv
node src/fastsim/fullsys/emulate_fullsys_f16.js adapter $P16 $P16/predictions/fullsys-f16-predictions.tsv
mkdir -p $P14/predictions-forecast-gate && node src/fastsim/fullsys/emulate_forecast_gate.js $P14 $P14/predictions-forecast-gate/forecast-gate.tsv
node src/datagen/export_network.js manhattan $NET $RB_DATA
# 3. Cấu hình lượt chạy
M="--network $NET --g2-rce35 $F/templates/G2-RCE35.json --g2-eohz35 $F/templates/G2-EOHZ35.json --wp4-template $F/wp4-via-g2k8-a1-fast.json"
python -B src/adapter/make_job_configs.py --data $P14 $M --forecast-gate
python -B src/adapter/make_job_configs.py --data $P16 $M
# 4. Chạy cả đợt (Runner dựng như mục 2.2, bước 5; DOTNET đặt như mục 2.2)
python -B src/adapter/run_campaign.py --panel f14 --data $P14 --out runs --logs logs --fleetpy-root FleetPy --runner-root runner --dotnet "$DOTNET" --workers 2
python -B src/adapter/run_campaign.py --panel f16 --data $P16 --out runs --logs logs --fleetpy-root FleetPy --runner-root runner --dotnet "$DOTNET" --workers 2
# 5. Bảng từng khách và phép so của mục 5.2
python -B src/adapter/riders.py --root runs --out riders.tsv --sequences sequences.tsv
cat $P14/predictions/fullsys-f14-predictions.tsv <(tail -n +2 $P16/predictions/fullsys-f16-predictions.tsv) > predictions.tsv
node src/fastsim/sample/compare_riders.js riders.tsv predictions.tsv
```

- `--workers` = min(số nhân CPU, (RAM tính bằng GB − 4) // 7). Chạy lại thì bỏ qua các lượt đã xong.
- `riders.py` ghi bảng từng khách (giờ đặt, giờ báo đầu, giờ đến, phục vụ, tổng thay đổi do điều phối, …) và chuỗi
  giờ trả đã công bố; Bảng 5.2–5.3 tính từ hai bảng này theo quy tắc ở mục 5.1 của báo cáo (gộp theo ngày, hiệu ghép cặp,
  khoảng tin cậy 95 % theo phân phối t). `compare_riders.js` cho phép so của mục 5.2.
- Dự đoán của mô phỏng nhanh được khóa bằng SHA-256 trước lượt chạy chương trình chính đầu tiên.

---

## 4. Đọc mã

| Muốn hiểu | Đọc |
|---|---|
| Một quyết định điều phối, từ lô sự kiện đến quyết định đã xác nhận | `src/core/RideBound.Runner/Protocol/RunnerSession.Messages.cs`, `src/core/RideBound.Algorithms/Dispatch/DispatchStep.cs`, `src/core/RideBound.Algorithms/Policies/SolverBackedRidePoolingPolicy.cs` |
| Sinh phương án (giữ tuyến, chèn, rút gọn 100, chuyển khách G2) | `src/core/RideBound.Algorithms/Candidates/InsertionCandidateGenerator.cs`, `CandidatePortfolioRetainer.cs`, `WaitingRiderRelocationGenerator.cs` |
| Hạn mức và cổng cam kết (x, c, z; δ, B; giới hạn cộng dồn; hạn điều chỉnh) | `src/core/RideBound.Application/Commitments/CommitmentDecisionValidator.cs`, `src/core/RideBound.Domain/Commitments/CommitmentPolicy.cs`, `CommitmentLockEvaluator.cs` |
| Chọn phương án cho cả đội xe (CP-SAT, thứ tự mục tiêu) | `src/core/RideBound.Algorithms/Policies/SolverBackedFleetSelection.cs`, `src/core/RideBound.Solvers.OrTools/OrToolsCandidateSelectionSolver.cs` |
| Shadow (điểm đi chung, cường độ nhu cầu) | `src/core/RideBound.Algorithms/Shareability/`, `src/fastsim/wf20/runner/sscore.js`, `lambda.js` |
| Dự báo thời gian đi | `src/core/RideBound.Domain/Validation/TravelTimeForecast.cs`, `src/fastsim/wf20/scripts/forecast.js` |
| Cùng các luật trong mô phỏng nhanh | `src/fastsim/engines/engine_wf15_clint.js` (`admits` và `charge`: các cách giới hạn; `decide`: một quyết định; `selectPlan`: chọn cho cả đội xe) |
| Bộ điều khiển sự kiện trong FleetPy | `src/adapter/run_job.py`, `src/adapter/ridebound_fleetpy/fleet_control.py` |

---

## 5. Giới hạn

- Chạy lại 360 lượt của chương trình chính cần máy mạnh và nhiều ngày; mục 2.2 chạy một lượt và so với mô phỏng nhanh.
- Repo không có script tính sẵn Bảng 5.2–5.3 từ bảng từng khách của 360 lượt; quy tắc tính nằm ở mục 5.1 của báo cáo.
  `src/fastsim/adapter60/full_common.py` tính các số của Bảng 5.3 từ tệp bằng chứng của các lượt chạy cuối
  (`publication-projection-f14/f16.jsonl.gz`, biến `RB_EVIDENCE`); các tệp này không đi kèm repo.
- Ngưỡng Shadow được dùng ở dạng đã đóng băng; phép hiệu chuẩn 55 ngày không nằm trong repo.
- Bước DuckDB của mục 3.1 đọc dữ liệu TLC trực tiếp từ trang của TLC; nếu TLC đổi đường dẫn, cần sửa biến `source`.
