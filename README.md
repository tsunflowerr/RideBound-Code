# RideBound

RideBound là một hệ điều phối xe đi chung (ride-pooling) giữ lời hứa với khách.
Khi khách đặt xe, hệ hứa một giờ trả khách. Sau đó, mỗi lần điều phối lại, hệ chỉ được dời giờ đó trong một ngân sách cho từng khách.
Repo này chứa mã và dữ liệu để tái lập các bảng của báo cáo hệ thống (§6, §7, §9) và báo cáo cuối.

Repo có bốn phần:

| Phần | Ngôn ngữ | Vai trò |
|---|---|---|
| `src/core` | C# (.NET 10) | **Runner**: tiến trình điều phối thật. Nhận sự kiện qua giao thức NDJSON v1, trả quyết định. |
| `src/adapter` | Python 3.10 + FleetPy 1.0.2 | Nối Runner vào bộ mô phỏng giao thông FleetPy để chạy "thật": một job, cả đợt F14/F16, sinh cấu hình job, đọc bảng từng khách (mục 5.6). |
| `src/emulator` | Node 20 | **Bộ mô phỏng so sánh**: cùng luật điều phối, viết lại gọn bằng JavaScript, chạy nhanh trên nhiều ô. Sinh số của §6 và §7. |
| `src/analysis` | Node 20 | Dựng mọi bảng báo cáo từ dữ liệu đi kèm repo, không cần chạy lại mô phỏng. |

Quan hệ giữa chúng:
- Con số chính của báo cáo đến từ bộ mô phỏng (nhanh, chạy được 60 ngày × nhiều luật).
- Runner C# + FleetPy chạy trên ít ô hơn. Kết quả của nó dùng để kiểm bộ mô phỏng **từng khách một** (§9 và phép so 360 job F14/F16).

Bài báo, dữ liệu và phần mềm được trích dẫn: `references/README.md`. Ba bài truy cập mở đi kèm dưới dạng PDF; các nguồn khác chỉ ghi DOI hoặc link.

Mọi lệnh trong README chạy từ thư mục gốc của repo, trừ khi lệnh tự `cd`.
Lệnh có ghi chú `# máy nhà`, và các lệnh trong bảng của mục 6, cần Python 3.10, FleetPy hoặc dữ liệu của các lần chạy
thật; chúng chưa được chạy trên máy kiểm của dự án. Các lệnh còn lại đã chạy một lần trên máy kiểm và cho kết quả ghi bên
cạnh, với hai ngoại lệ:
- lệnh `duckdb`: máy kiểm không có DuckDB, nên dùng file `via_windows.csv` đã lưu thay cho đầu ra của chúng (mục 3.2);
- các lần chạy lại đầy đủ của mục 5.2 và 5.3: lần kiểm cuối chạy chúng trên một ô (cùng lệnh, thêm `--cells <ô>`).
  Bản đầy đủ đã chạy trước đó với cùng mã bộ mô phỏng (từ đó `engine.js` chỉ đổi chú thích) và khớp digest:
  5.040/5.040 dòng 60 ngày, 3.600/3.600 dòng fullsys, 3.600/3.600 dòng n7.

---

## 1. Cây thư mục

```
README.md
references/                      3 bài báo truy cập mở (PDF) + README: 12 nguồn, DOI/link, giấy phép từng file
src/
  .gitattributes .gitignore .editorconfig
  core/                          C# .NET 10: Runner (máy chủ giao thức) + bộ điều phối
    RideBound.slnx  Directory.Build.props  global.json
    RideBound.Contracts/         thông điệp giao thức v1, JSON chuẩn hóa, các hàm hash
    RideBound.Domain/            lời hứa, luật cam kết, dự báo thời gian đi
    RideBound.Application/       giảm sự kiện thành trạng thái, kiểm quyết định, sổ lời hứa
    RideBound.Algorithms/        sinh ứng viên, cổng cam kết, chọn phương án cho đội xe, Shadow
      Dispatch/                  một bước điều phối (DispatchStep): quyết định, kiểm lại, ghi vi phạm do giao thông, Shadow
    RideBound.Solvers.OrTools/   mô hình CP-SAT (OR-Tools 9.15.6755)
    RideBound.Runner/            tiến trình Runner: Program.cs (dòng lệnh) và 4 thư mục
      Protocol/                  phiên giao thức (RunnerSession), đọc/ghi NDJSON, kiểm initializeRun và thứ tự lô, hash quyết định
      Mapping/                   sự kiện giao thức -> sự kiện miền, quyết định -> hành động giao thức
      StateAudit/                JSON chuẩn hóa và hash của trạng thái, đọc/ghi checkpoint
      Configuration/             đọc file commitment và file WP4 (kể cả khối shadowAllocation)
    protocol/schemas/            57 file JSON của giao thức v1 (v1/) + README
    protocol/fixtures/           thông điệp giao thức mẫu cho test C# (41 file) + README
    configs/final/               cấu hình của các lần chạy cuối, cộng các mẫu G2 và INS (mục 7)
    configs/test/                19 cấu hình mà test đọc (test C# và test_mapping.py của adapter)
    tests/                       7 dự án test (879 test) + TestPaths.cs (tìm thư mục chứa RideBound.slnx)
  adapter/                       Python 3.10 + FleetPy 1.0.2
    ridebound_fleetpy/           gói adapter: phiên giao thức, client của Runner, điều khiển đội xe trong FleetPy
    run_job.py                   một job: FleetPy + Runner -> transcript + evidence.json.gz + summary.json
    run_campaign.py              danh sách job F14/F16, nhiều worker, chạy tiếp được
    riders.py                    transcript hoặc evidence.json.gz của các job -> bảng TSV từng khách (+ chuỗi ETA trả)
    make_job_configs.py          mẫu cấu hình + đầu vào Shadow/forecast từng ô + thư mục mạng -> cấu hình job
    convert_via_full_v2.py  make_run_drivers.py   ô -> fixture, driver, world (giữ nguyên từng byte)
    replay.py                    kiểm: phát lại transcript qua Runner hoặc adapter, so hai lần chạy
    dev/                         chỗ FleetPy nạp lớp điều khiển đội xe của RideBound (run_job.py cần)
    assets/wp7_tiny.csv          loại xe của FleetPy (4 chỗ)
    environment.lock.yml         môi trường conda của các lần chạy thật (Python 3.10.20, numpy, pandas, ...)
    assets/wp7-fleetpy-medium/  actual_fleetpy_medium_verify.py  capability-matrix.json
                                 cùng environment.lock.yml: các file mà mỗi job băm làm biên nhận (mục 5.6)
    tests/
  emulator/                      Node 20: bộ mô phỏng so sánh (một engine, một driver)
    engine.js                    luật điều phối, một lần quyết định
    identity.js scenario.js sscore.js lambda.js forecast.js metrics.js network.js
    run.js worker.js pool.js shcore.js shlib.js     driver: chạy nhiều ô song song, chạy tiếp được
    confirm_digest.js            so dòng kết quả với digest đã niêm phong
    fetch_network.js             tải mạng đường FleetPy Manhattan từ Zenodo
    export_network.js            xuất mạng và golden cho bộ chấm điểm Shadow của C#
    gen/                         gen17.js via_month.sql via_windows.sql: sinh ô từ dữ liệu TLC công khai
    checks/                      kiểm §9 + dữ liệu nhỏ của chúng (checks/data/)
    data/                        danh sách ngày, prior F16, tỉ lệ pad, hệ số giao thông
    expected/                    digest đã niêm phong và danh sách sha256
    tests/
  analysis/                      Node 20: mọi bảng báo cáo từ dữ liệu đi kèm
    report_tables.js             báo cáo cuối §3–§6: hệ thật vs dự đoán, hệ của ta vs Elmi
    sixty_day_tables.js          báo cáo hệ thống §6: hai bảng 60 ngày INS và G2, 7 nhánh
    policy_curves.js             §6: câu "đảo hứa +20–91 % ở cùng mức phục vụ"
    forecast_tables.js           báo cáo hệ thống §7: dự báo vs đệm, phân rã
    ours_vs_theirs.js            hệ của ta vs đối thủ, bộ mô phỏng plain (luận văn §4.7)
    forecast_accuracy.js         §7: sai số dự báo 2,6–3,1 % (giữ giao thông hiện tại: 5–9 %)
    data/                        bảng khách của các lần chạy thật, dòng kết quả mô phỏng đã ghi, dự đoán, dữ liệu TLC đã gộp; MANIFEST.tsv
    expected/                    kết quả đã lưu của các script gốc
    tests/
```

Mọi phiên bản cũ (kể cả các tính năng đã bỏ, các script nghiên cứu và tài liệu cũ) nằm ở commit `ebca912`,
nhánh `research/final-real-runs-2026-11-06`. Lấy một file cũ bằng `git show ebca912:<đường dẫn>`.

Từ `ebca912` đến cây này, mỗi bước thay đổi là một commit có tiền tố mã bước, ví dụ `[2.14g]` (trừ 4 commit đầu, thêm
test và bộ kiểm, không có tiền tố). Nhiều mục bên dưới nhắc các mã bước này. Xem một bước bằng:

```bash
git log --format='%h %s' --grep='^\[2\.14g\]'
```

---

## 2. Cài đặt

| Công cụ | Phiên bản | Cần cho |
|---|---|---|
| .NET SDK | 10.0.301 trở lên, trong dòng 10.0 (`src/core/global.json`, `rollForward: latestFeature` không nhận SDK 11). Đã thử với 10.0.400. | `src/core` |
| Node.js | 20 (đã thử 20.20.2). Chỉ dùng module có sẵn, không cần `npm install`. | `src/emulator`, `src/analysis` |
| git | bản clone **đầy đủ** (không dùng `--depth`), vì vài lệnh đọc commit `ebca912` | §9, chỉ số dự báo nhân quả |
| bash, `sha256sum`, `gzip`, `awk` | Linux, macOS, hoặc Git Bash trên Windows | các lệnh trong README |
| DuckDB CLI | 1.5.5 | chỉ khi tự sinh ô từ dữ liệu TLC (mục 3.2) |
| Python | 3.10 (đúng bản 3.10.20 khi chạy thật) | chỉ `src/adapter` |
| FleetPy | 1.0.2, commit `053aa9d4fcfde91c5d303435d5748f9206c071b0` (https://github.com/TUM-VT/FleetPy, MIT) | chỉ `src/adapter` |

**C#.** OR-Tools 9.15.6755 và các gói khác do `dotnet restore` tự tải. Mọi lệnh `dotnet` chạy từ thư mục `src/core`, vì SDK đọc `global.json` từ thư mục hiện hành.

**Python (chỉ cho adapter).** `src/adapter/environment.lock.yml` là môi trường conda đã dùng cho các lần chạy thật,
ghim đúng từng bản: Python 3.10.20, numpy 2.2.6, scipy 1.15.2, pandas 2.2.3, geopandas 1.0.1, shapely 2.0.6, ...
Các bản build trong file là của Windows 64-bit. Trên hệ khác, dùng cùng số phiên bản và bỏ phần build.
`riders.py`, `make_job_configs.py`, `replay.py compare` và `run_campaign.py --dry-run` không cần FleetPy.

```bash
conda env create -f src/adapter/environment.lock.yml                    # máy nhà; tạo môi trường tên fleetpy-1.0.2
conda activate fleetpy-1.0.2                                            # máy nhà
git clone https://github.com/TUM-VT/FleetPy                             # máy nhà
git -C FleetPy checkout 053aa9d4fcfde91c5d303435d5748f9206c071b0        # máy nhà
```

**Đường dẫn trên Windows.** Trong Git Bash, `$PWD` có dạng `/c/...`; Node có thể không hiểu dạng này trong biến môi trường.
Khi cần đường dẫn tuyệt đối, dùng `$(pwd -W)`: lệnh này cho đường dẫn kiểu Windows, có tên ổ đĩa và dấu `/`.
Các lệnh bên dưới viết `$(pwd -W 2>/dev/null || pwd)`, nên chạy được cả trên Linux và macOS. Chúng giả sử đường dẫn không có dấu cách.

---

## 3. Dữ liệu

### 3.1 Cái gì có sẵn, cái gì phải tự dựng

| Dữ liệu | Ở đâu | Cách kiểm |
|---|---|---|
| Bảng từng khách của các lần chạy thật F14/F16 (`f14-real-riders.tsv.gz`, `f16-real-riders.tsv.gz`), dòng kết quả mô phỏng đã ghi, dự đoán, dữ liệu TLC đã gộp (khoảng 32 MB) | `src/analysis/data/` | sha256 của nội dung đã giải nén trong `MANIFEST.tsv` |
| Danh sách ngày, prior F16, tỉ lệ pad, hệ số giao thông | `src/emulator/data/` | dùng trực tiếp |
| Đầu vào nhỏ của kiểm §9 (khoảng 9,2 MB) | `src/emulator/checks/data/` | các script kiểm tự so sha |
| Digest niêm phong của mọi dòng kết quả, danh sách sha của ô và mạng | `src/emulator/expected/` | là chuẩn để so |
| Mạng đường FleetPy Manhattan (4 file CSV, Zenodo 15187906, CC BY 4.0) | **tự tải** bằng `fetch_network.js` | `expected/network.sha256` |
| Các ô (cell) của 60 ngày và F16 (khoảng 1,4 GB) | **tự sinh** bằng `gen17.js` từ dữ liệu TLC công khai | `expected/wf14-60day-cells.sha256`, `expected/wf17-f16-cells.sha256` |
| Chỉ số dự báo 2019 (chế độ CL) | **tự dựng** bằng `forecast_accuracy.js` | sha256 `d3724ad8…` (= `src/analysis/expected/forecast-accuracy-index-2019.json`) |
| Chỉ số dự báo nhân quả (chế độ CLc, 1,5 MB) | `git show` từ commit `ebca912` | sha256 `d808241b…` |
| Tham số Shadow đã đóng băng, prior 60 ngày (2 file, 1,6 KB và 657 KB) | **không đi kèm repo** (mục 11) | sha256 ở bước 7 dưới đây |

Kiểm `src/analysis/data/` (in `OK` cho cả 29 file):

```bash
cd src/analysis/data
tail -n +2 MANIFEST.tsv | while IFS=$'\t' read -r f sha rest; do
  if [ "${f%.gz}" != "$f" ]; then got=$(gzip -dc "$f" | sha256sum | cut -d' ' -f1); else got=$(sha256sum < "$f" | cut -d' ' -f1); fi
  [ "$got" = "$sha" ] && echo "OK $f" || echo "BAD $f"
done
cd ../../..
```

### 3.2 Dựng thư mục dữ liệu ("data root") cho bộ mô phỏng

Bộ mô phỏng đọc mọi đầu vào lớn từ một thư mục, gọi là data root (`run.js --data <thư mục>`).
Tên các thư mục con (`wf5`, `wf14`, ...) là tên cũ của các nghiên cứu; mã đọc đúng các đường dẫn này.

```
$DATA/
  wf5/data/nyc-via/fleetpy-manhattan/*.csv          mạng đường (fetch_network.js)
  wf6/via-many-days/work/ff_apsp_u16.bin            bảng thời gian đi ngắn nhất, 4.411 × 4.411 (fetch_network.js tự dựng)
  wf14/scenarios/{weekdays-P512,matrices,provenance}/   1.081 file: các ô 60 ngày (F14 là tập con)
  f16/{weekdays-P512,matrices,provenance}/          292 file: các ô F16
  wf7/c-forecast/results/accuracy-index-2019.json   chỉ số dự báo (chế độ CL)
  wf15/frozen/shadow-params.json                    tham số Shadow đã đóng băng (không đi kèm repo)
  wf15/work/prior_prevhour.csv                      nhu cầu giờ trước, 60 ngày (không đi kèm repo)
  index-causal-pertarget.json                       chỉ số nhân quả (chế độ CLc; đặt ở đâu cũng được)
```

Các bước (thời gian đo ở máy kiểm, 16 luồng, đang chia sẻ):

```bash
DATA=$(pwd -W 2>/dev/null || pwd)/rb-data          # thư mục mới, ngoài repo cũng được

# 1. Mạng đường: chỉ đọc 4 file trong zip 409 MB trên Zenodo (khoảng 0,2 MB), rồi dựng bảng thời gian đi. Khoảng 8–16 s.
node src/emulator/fetch_network.js $DATA
(cd $DATA && sha256sum -c $OLDPWD/src/emulator/expected/network.sha256)        # 5 dòng OK

# 2. Chuyến Via từ dữ liệu TLC công khai (DuckDB). Mỗi tháng 2019-02 .. 2019-12 một lần, khoảng 1 phút mỗi tháng.
#    Các lệnh duckdb chưa chạy ở máy kiểm (xem đầu README và ghi chú dưới khối lệnh).
mkdir -p tlc
for m in 02 03 04 05 06 07 08 09 10 11 12; do
  duckdb -bail -cmd "SET VARIABLE month = '2019-$m';
    SET VARIABLE source = 'https://d37ci6vzurychx.cloudfront.net/trip-data/fhvhv_tripdata_2019-$m.parquet';
    SET VARIABLE zones = 'https://d37ci6vzurychx.cloudfront.net/misc/taxi_zone_lookup.csv';
    SET VARIABLE out_dir = 'tlc';" -f src/emulator/gen/via_month.sql
done
duckdb -bail -cmd "SET VARIABLE via_dir = 'tlc'; SET VARIABLE days = 'src/emulator/data/days_60.csv';
  SET VARIABLE out = 'tlc/via_windows_60.csv';" -f src/emulator/gen/via_windows.sql
duckdb -bail -cmd "SET VARIABLE via_dir = 'tlc'; SET VARIABLE days = 'src/emulator/data/days_F16.csv';
  SET VARIABLE out = 'tlc/via_windows_F16.csv';" -f src/emulator/gen/via_windows.sql

# 3. Các ô 60 ngày: ba lần lấy mẫu r0, r1, r2, đúng thứ tự này. Khoảng 10–25 s mỗi lần.
for k in 0 1 2; do
  node src/emulator/gen/gen17.js --days src/emulator/data/days_60.csv --windows tlc/via_windows_60.csv \
    --factors src/emulator/data/factors.json.gz --data $DATA --out $DATA/wf14/scenarios --fmax 512:96 --resample $k
done
(cd $DATA/wf14/scenarios && sha256sum -c --quiet $OLDPWD/src/emulator/expected/wf14-60day-cells.sha256) && echo "60-day cells OK"

# 4. Các ô F16 (giữ summary của từng lần lấy mẫu).
for k in 0 1 2; do
  node src/emulator/gen/gen17.js --days src/emulator/data/days_F16.csv --windows tlc/via_windows_F16.csv \
    --factors src/emulator/data/factors.json.gz --data $DATA --out $DATA/f16 --seedtag wf17-fresh --fmax 512:96 --resample $k
  cp $DATA/f16/summary-weekdays.json $DATA/f16/summary-weekdays-r$k.json
done
(cd $DATA/f16 && sha256sum -c --quiet $OLDPWD/src/emulator/expected/wf17-f16-cells.sha256) && echo "F16 cells OK"

# 5. Chỉ số dự báo (chế độ CL), dựng lại từ dữ liệu TLC Yellow đã gộp trong repo.
node src/analysis/forecast_accuracy.js --data src/analysis/data --out tlc/fa
mkdir -p $DATA/wf7/c-forecast/results && cp tlc/fa-index-2019.json $DATA/wf7/c-forecast/results/accuracy-index-2019.json

# 6. Chỉ số nhân quả (chế độ CLc), lấy từ commit ebca912.
git show ebca912:research/continuation/2026-10-26/company-emulator/sp/wf21/n7/cidx/results/index-causal-pertarget.json \
  > $DATA/index-causal-pertarget.json

# 7. Tham số Shadow và prior 60 ngày. Hai file này không đi kèm repo (mục 11); không có chúng thì không chạy được
#    các nhánh Shadow (SHF, SHHZ, SHRC). Nếu có, chép chúng vào data root rồi kiểm sha256:
mkdir -p $DATA/wf15/frozen $DATA/wf15/work
cp <thư mục chứa hai file>/shadow-params.json $DATA/wf15/frozen/
cp <thư mục chứa hai file>/prior_prevhour.csv $DATA/wf15/work/
(cd $DATA/wf15 && sha256sum -c) <<EOF
62ae26a6eb8989f0339bf44dfb37abbc70dd5411348c5f37073330dd567f1a76  frozen/shadow-params.json
d40e2f3b0c4b7ec4a4caa992f129d205cf618c3d51489776b3a5cfd7e694375b  work/prior_prevhour.csv
EOF
```

Ghi chú:
- Hệ số giao thông `src/emulator/data/factors.json.gz` đi kèm repo dưới dạng dữ liệu. Bộ dựng nó từ chuyến TLC Yellow không có trong repo.
- Lệnh `duckdb`: tháng 2019-02 tải thẳng từ nguồn công khai cho file parquet giống từng byte file đã dùng; `via_windows.sql`
  trên các file parquet đã dùng cho `via_windows.csv` giống từng byte, cả 60 ngày và F16. 10 tháng còn lại chưa tải lại.
- Chuyến trong ô là chuyến Via thật (TLC High Volume FHV 2019, mã `HV0004`, đi và đến trong Manhattan). Nút đón/trả là nút ngẫu nhiên có seed trong vùng taxi của chuyến.

---

## 4. Chạy thử trong vài phút

Ba bước. Bước 3 chỉ cần dữ liệu công khai.

**Bước 1: test C#** (2–7 phút ở máy kiểm, phần lớn là các test chạy Runner thật).

```bash
cd src/core
dotnet test RideBound.slnx -c Release
# Kết quả: 7 dòng "Passed!", tổng 879 test, 0 lỗi
# (Architecture 9, Contracts 124, Application 104, OrTools 14, Domain 235, Algorithms 174, Runner 219)
cd ../..
```

Chỉ 62 test của cấu hình cuối (Runner chạy như tiến trình con, so stdout từng byte với bản ghi ở `ebca912`):

```bash
cd src/core
dotnet test tests/RideBound.Runner.Tests -c Release --filter "FullyQualifiedName~RideBound.Runner.Tests.Final"
cd ../..
```

**Bước 2: dựng lại bảng báo cáo cuối từ dữ liệu đi kèm** (5–40 s).

```bash
node src/analysis/report_tables.js --data src/analysis/data --check src/analysis/expected
# Dòng cuối: "TOTAL 9570 values equal, 0 differences; worst relative difference 1.44e-15 ..."
# rồi một dòng liệt kê giá trị "open": chúng cần dữ liệu không đi kèm repo (xem mục 5.4).
```

**Bước 3: chạy bộ mô phỏng trên một ô** (dưới 1 phút sau khi có mạng và dữ liệu TLC tháng 2019-02).
Chỉ dùng bốn nhánh không Shadow, nên không cần tham số Shadow hay prior.

```bash
DATA=$(pwd -W 2>/dev/null || pwd)/rb-demo
mkdir -p demo
node src/emulator/fetch_network.js $DATA

# Chuyến Via của tháng 2019-02, rồi các ô của một ngày (r1 = lần lấy mẫu thứ hai).
printf 'day\n2019-02-07\n' > demo/one-day.csv
duckdb -bail -cmd "SET VARIABLE month = '2019-02';
  SET VARIABLE source = 'https://d37ci6vzurychx.cloudfront.net/trip-data/fhvhv_tripdata_2019-02.parquet';
  SET VARIABLE zones = 'https://d37ci6vzurychx.cloudfront.net/misc/taxi_zone_lookup.csv';
  SET VARIABLE out_dir = 'demo';" -f src/emulator/gen/via_month.sql
duckdb -bail -cmd "SET VARIABLE via_dir = 'demo'; SET VARIABLE days = 'demo/one-day.csv';
  SET VARIABLE out = 'demo/via_windows.csv';" -f src/emulator/gen/via_windows.sql
node src/emulator/gen/gen17.js --days demo/one-day.csv --windows demo/via_windows.csv \
  --factors src/emulator/data/factors.json.gz --data $DATA --out $DATA/wf14/scenarios --fmax 512:96 --resample 1

# Một ô, 2 cấu hình (INS, G2) × 4 nhánh = 8 dòng. --out phải là đường dẫn tuyệt đối (xem mục 5.1).
node src/emulator/run.js --data $DATA --set confirm --cells 2019-02-07-w08-r1 --arms U,EO35,EOHZ35,RCE35 \
  --workers 4 --out $(pwd -W 2>/dev/null || pwd)/demo/rows.ndjson

# So với digest niêm phong của nghiên cứu 60 ngày, chỉ các dòng của ô này.
awk -F'\t' 'NR == 1 || ($1 == "2019-02-07-w08-r1" && $4 !~ /^SH/)' src/emulator/expected/wf15-confirm-60d.tsv > demo/expected.tsv
node src/emulator/confirm_digest.js demo/expected.tsv demo/rows.ndjson
# "Mong đợi 8 dòng. Trùng hoàn toàn (kể cả từng khách): 8. ... Lệch chỉ số: 0. Thiếu: 0. ..."
```

"Trùng hoàn toàn" nghĩa là mọi chỉ số **và** dấu vân tay từng khách (`sig`) đều bằng bản đã niêm phong.

Test của bộ mô phỏng, của phần phân tích, và các test adapter không cần FleetPy:

```bash
node --test src/emulator/tests        # 30 pass, 3 skip (3 test cần data root đầy đủ)
node --test src/analysis/tests        # 34 pass
# Với data root đầy đủ: 33 pass
EMULATOR_DATA=$DATA EMULATOR_CAUSAL_INDEX=$DATA/index-causal-pertarget.json node --test src/emulator/tests

(cd src/adapter/tests && python -B -m unittest test_replay test_run_job test_run_campaign test_make_job_configs test_riders)
# "Ran 42 tests ... OK". Các test adapter còn lại cần FleetPy (mục 6, P-FLEET).
```

---

## 5. Tái lập từng bảng báo cáo

Mỗi bảng có hai đường:
- **Nhanh:** dựng bảng từ dòng kết quả đã ghi trong `src/analysis/data/` (vài giây).
- **Đầy đủ:** chạy lại bộ mô phỏng trên mọi ô (cần data root, mục 3.2), so dòng mới với digest niêm phong, rồi dựng bảng từ dòng mới.

Thư mục ra trong các ví dụ: `OUT=$(pwd -W 2>/dev/null || pwd)/out; mkdir -p $OUT`.

### 5.1 Lưu ý chung cho `run.js`

- `--out`, `--frozen`, `--prior` và `--days-file` nếu là đường dẫn tương đối thì tính từ `$DATA/wf15`, không phải từ thư mục hiện hành.
  Vì vậy dùng đường dẫn tuyệt đối cho `--out`, `--prior` và `--days-file`. `--frozen frozen/shadow-params.json` là đúng.
- `--flat`, `--fc-index` và `--scen` tính từ thư mục hiện hành.
- Driver chạy tiếp được: một dòng đã có trong file `--out` sẽ không chạy lại. Muốn chạy lại từ đầu thì xóa file `--out`.
- Tối đa 4 worker.
- Các dòng được ghi theo thứ tự worker xong việc, nên thứ tự dòng khác với file đã ghi. Bảng `.txt` và `.md` vẫn giống từng byte.
  File `.json` độ chính xác đầy đủ của `forecast_tables.js` có thể khác ở chữ số cuối, vì tổng được cộng theo thứ tự khác
  (đã kiểm: xếp dòng mới theo thứ tự của file đã ghi thì `.json` cũng giống từng byte).

Các lần chạy đầy đủ dưới đây có nhánh Shadow, nên cần hai file ở bước 7 của mục 3.2. Biến dùng chung cho các lệnh dưới:

```bash
RUN="node src/emulator/run.js --data $DATA --frozen frozen/shadow-params.json --x star --workers 4"
F14="--set confirm --days-file $(pwd -W 2>/dev/null || pwd)/src/emulator/data/days_F14.csv --panel F14"
F16="--scen $DATA/f16/weekdays-P512 --prior $(pwd -W 2>/dev/null || pwd)/src/emulator/data/prior_F16.csv --panel F16"
```

### 5.2 Báo cáo hệ thống §6: nghiên cứu Shadow 60 ngày (INS và G2, 7 nhánh)

60 ngày thường của 2019 × 2 khung giờ × 3 lần lấy mẫu = 360 ô; mỗi ô 512 yêu cầu, 62 xe.
7 nhánh: U, EO35, EOHZ35, RCE35, SHF, SHHZ, SHRC (Shadow 72 cho INS, 71 cho G2). Xem thuật ngữ ở mục 9.

Nhanh:

```bash
node src/analysis/sixty_day_tables.js --rows src/analysis/data/wf15-confirm-rows.ndjson.gz --out $OUT/sixty-day.txt
head -89 $OUT/sixty-day.txt | cmp - src/analysis/expected/confirm-shadow-report.txt && echo "lines 1-89 same"
```

- Dòng 1–89 (TOTALS, SECONDARY, S16) giống từng byte dòng 30–118 của báo cáo gốc của nghiên cứu.
- Phần `REPORT TABLES` là hai bảng §6. Phần `REPORT PAIRS` và `GUARANTEED RIDERS` là các con số phần chữ §6 trích.

Câu "đảo hứa +20–91 % ở cùng mức phục vụ" của §6 (nghiên cứu R6, đường cong theo tham số):

```bash
node src/analysis/policy_curves.js --rows src/analysis/data/wf17-r6-rows.ndjson.gz \
  --a5 src/analysis/data/wf16-a5-alpha-rows.ndjson.gz --wf14 src/analysis/data/wf14-main-r0-rows.ndjson.gz \
  --wf15 src/analysis/data/wf15-confirm-rows.ndjson.gz --out $OUT/policy-curves
cmp $OUT/policy-curves.json src/analysis/expected/policy-curves.json && echo same
diff <(tail -n +2 $OUT/policy-curves.txt) <(tail -n +2 src/analysis/expected/policy-curves.txt) && echo same   # dòng 1 có giờ chạy
```

Đầy đủ (khoảng 33 phút với 4 worker ở máy kiểm):

```bash
$RUN --set confirm --arms U,EO35,EOHZ35,RCE35,SHF,SHHZ,SHRC --part S7 --out $OUT/c60.ndjson
node src/emulator/confirm_digest.js src/emulator/expected/wf15-confirm-60d.tsv $OUT/c60.ndjson     # 5040 / 5040
node src/analysis/sixty_day_tables.js --rows $OUT/c60.ndjson --out $OUT/sixty-day-rerun.txt
cmp $OUT/sixty-day-rerun.txt $OUT/sixty-day.txt && echo same                                      # giống từng byte
```

Các dòng R6 (`+20–91 %`) chỉ dựng lại từ dòng đã ghi. Chạy lại R6 cần ngưỡng Shadow cho x = 50..90, mà nguồn ngưỡng đó đã bị bỏ.

### 5.3 Báo cáo hệ thống §7: dự báo vs đệm, hệ của ta vs đối thủ

30 ngày (F14: 14 ngày, F16: 16 ngày) × 2 khung × 3 lần lấy mẫu.

**Dự báo vs đệm** (bảng đầu §7 và phân rã):

```bash
node src/analysis/forecast_tables.js --rows src/analysis/data/wf21-n7-rows.ndjson.gz --panels src/analysis/data/panels.json \
  --flat src/analysis/data/flat_final.json --flatc src/analysis/data/flatc_final.json --out $OUT/forecast-tables
cmp $OUT/forecast-tables.txt src/analysis/expected/forecast-tables.txt && cmp $OUT/forecast-tables.json src/analysis/expected/forecast-tables.json && echo same
```

**Sai số dự báo** (câu của §7: "2,6–3,1 % so với khoảng 5 %"; mức 5 % là sai số khi giữ giao thông hiện tại ở khung w08, khung w17 là 9,3 %):

```bash
node src/analysis/forecast_accuracy.js --data src/analysis/data --out $OUT/forecast-accuracy
for s in .txt -index-2019.json -rows.json -summary.json; do cmp $OUT/forecast-accuracy$s src/analysis/expected/forecast-accuracy$s && echo "same $s"; done
```

**Hệ của ta vs đối thủ, bộ mô phỏng plain** (cột "plain" của báo cáo cuối, luận văn §4.7):

```bash
node src/analysis/ours_vs_theirs.js --rows src/analysis/data/wf20-fullsys-rows.ndjson.gz --out $OUT/ours-vs-theirs.md
cmp $OUT/ours-vs-theirs.md src/analysis/expected/ours-vs-theirs.md && echo same
```

Đầy đủ (khoảng 21 phút cho "fullsys" và 24 phút cho "n7" ở máy kiểm):

```bash
# fullsys (wf20): 5 nhánh, dự báo N và CL
$RUN $F14 --fc N  --arms SHF,SHHZ,SHRC,EOHZ35,RCE35 --out $OUT/fullsys.ndjson
$RUN $F14 --fc CL --arms SHF,SHHZ,SHRC,EOHZ35,RCE35 --out $OUT/fullsys.ndjson
$RUN $F16 --fc N  --arms SHF,SHHZ,SHRC,EOHZ35,RCE35 --out $OUT/fullsys.ndjson
$RUN $F16 --fc CL --arms SHF,SHHZ,SHRC,EOHZ35,RCE35 --out $OUT/fullsys.ndjson
node src/emulator/confirm_digest.js src/emulator/expected/wf20__fullsys__results__rows.tsv $OUT/fullsys.ndjson    # 3600 / 3600

# n7 (wf21): SHRC, SHHZ × N, CL, PAD, CLc, PADc
for FC in "--fc N" "--fc CL" "--fc PAD --flat src/emulator/data/flat_final.json" \
          "--fc CLc --fc-index $DATA/index-causal-pertarget.json" \
          "--fc PAD --flat src/analysis/data/flatc_final.json --variant PADc"; do
  $RUN $F14 $FC --arms SHRC,SHHZ --out $OUT/n7.ndjson
  $RUN $F16 $FC --arms SHRC,SHHZ --out $OUT/n7.ndjson
done
node src/emulator/confirm_digest.js src/emulator/expected/wf21__n7__results__rows.tsv $OUT/n7.ndjson              # 3600 / 3600

# bảng từ dòng mới
node src/analysis/ours_vs_theirs.js --rows $OUT/fullsys.ndjson --out $OUT/ours-vs-theirs-rerun.md       # = expected, từng byte
node src/analysis/forecast_tables.js --rows $OUT/n7.ndjson --panels src/analysis/data/panels.json \
  --flat src/analysis/data/flat_final.json --flatc src/analysis/data/flatc_final.json --out $OUT/forecast-rerun   # .txt = expected
```

### 5.4 Báo cáo cuối §3–§6: hệ thật vs dự đoán của bộ mô phỏng

So sánh chính: **hệ của ta** `FS-G2-SHRC71-CL` (Shadow + luật C + dự báo vòng kín) với **Elmi** `FS-G2-SHHZ71-N`
(Shadow + rào lỏng, không dự báo). Ba nguồn cho cùng 30 ngày: dòng của bộ mô phỏng (plain), dự đoán từng khách (adapter),
và các lần chạy thật của RideBound (real).

```bash
node src/analysis/report_tables.js --data src/analysis/data --check src/analysis/expected     # so 9.570 giá trị
node src/analysis/report_tables.js --data src/analysis/data                                  # in bảng §5
node src/analysis/report_tables.js --data src/analysis/data --out $OUT/report                # ghi các file kết quả
```

- Dùng `--check` để so. Trong 11 file do `--out` ghi, chỉ `synth-real-30d.md`, `promise.json` và `promise.md` giống
  từng byte file trong `expected/`. Các file còn lại khác vì: giá trị "open" in thành `—` (file chữ) hoặc thành `null`
  hay bị bỏ (JSON); JSON được ghi theo cách của JavaScript (thụt lề 1 dấu cách, `-27.0` thành `-27`); vài số thực khác
  ở vài chữ số cuối (giá trị p của phép kiểm dấu chính xác, và biên khoảng tin cậy trong JSON); `d1-gap.txt` đã lưu
  dùng xuống dòng CRLF.
- Giá trị "open" (397 + 1.086 + 2.732) cần dữ liệu không đi kèm repo: chuỗi thời gian của adapter cho hai nhánh tách,
  transcript và evidence từng khách. Repo không có lệnh dựng lại chúng; giá trị đã lưu nằm trong `src/analysis/expected/`.
- Dự đoán chế độ adapter (`*-adapter-*predictions.tsv.gz`) là kết quả đã niêm phong; bộ sinh chúng không công khai.
- Phép so 360 job F14/F16 (hệ thật vs dự đoán, từng khách) nằm trong `f14-riders-compare.json` và `f16-riders-compare.json`:
  F14 166/168 cặp ô-nhánh khớp hết, 57.954/58.042 khách; F16 187/192 cặp, 67.917/67.922 khách.

### 5.5 Báo cáo hệ thống §9: bộ mô phỏng khớp hệ thật

Phần chạy được từ repo là phía bộ mô phỏng. Các lần chạy lại phía C# của §9 dùng tính năng đã bỏ; chúng ở commit `ebca912`.

```bash
CELLS=$(pwd -W 2>/dev/null || pwd)/s9-cells
node src/emulator/checks/fetch_cells.js $CELLS          # 46 file (khoảng 19 MB) lấy từ commit ebca912, có kiểm sha
node src/emulator/checks/calib4083.js $CELLS            # ARCHIVED: 4083/4083 PASS
node src/emulator/checks/realjob.js $CELLS              # 6/6 jobs accept/reject sets identical; ... 584
node src/emulator/checks/bridge_reemulate.js $CELLS     # 90/90 cell-arms exact, 8005/8005 sealed rider rows equal
node src/emulator/checks/via_zone.js $CELLS $OUT/via    # riders TSV identical to the sealed predictions; 6/6; 2407/2407
node src/emulator/checks/compare_riders.js              # bridge 238/238, 22429/22429; Via-zone 6/6, 2407/2407
```

| Số trong §9 | Lệnh | Tái lập được |
|---|---|---|
| 4.083/4.083 khách | `calib4083.js` | đủ, từng khách |
| 584 khách của 6 job thật | `realjob.js` | tập nhận/từ chối 6/6 job (584 khách). Từng khách chỉ được 294/584, vì cần hai transcript không đi kèm repo (`realjob.js <cells> <transcript 1> <transcript 2>`) |
| 238 cặp Manhattan | `compare_riders.js`, `bridge_reemulate.js` | phía so sánh 238/238; phía mô phỏng chạy lại 90/90 trên 9 trong 11 ô (2 ô thiếu fixture) |
| Cặp vùng Via | `via_zone.js` | dự đoán của 12 cặp ô-nhánh giống từng byte (4.878 dòng, sha256 `c0cd48de…`). So với hệ thật: 6/6 cặp, 2.407/2.407 khách (khách thật của 6 cặp còn lại không đi kèm repo) |

Thời gian: mỗi lệnh 1–35 s ở máy kiểm.

### 5.6 Các lần chạy thật F14/F16 (Runner C# + FleetPy)

Cột "real" của mục 5.4 đến từ 168 job F14 và 192 job F16. Mỗi job là một ô × một nhánh (`FS-G2-SHRC71-CL` hoặc `FS-G2-SHHZ71-N`):
FleetPy di chuyển đội xe, Runner C# ra mọi quyết định điều phối.
Repo có kết quả đã ghi (`src/analysis/data/f14-real-riders.tsv.gz`, `f16-real-riders.tsv.gz`) và các script dưới đây,
nhưng **không có đầu vào của các job**:
- fixture, driver và world của từng ô;
- đầu vào Shadow đã niêm phong (`predictions/shadow-inputs/`), do bộ dự đoán không công khai sinh;
- hồ sơ của cổng dự báo (F14) và thư mục mạng Shadow (`D.u16`, `network.json`).

Các mẫu cấu hình thì có trong repo, ở `src/core/configs/final/` (mục 7).

Vì vậy mục này chỉ chạy được ở máy có các đầu vào đó (mục 6). Một job cần 15–25 phút và tới 6,2 GB RAM.

Thư mục dữ liệu của một panel (`$PANEL`) có `fixtures/<ô>/`, `drivers-run/<ô>.driver.json`, `worlds/<ô>.world.json`,
`predictions/shadow-inputs/<ô>.json`, với F14 thêm `predictions-forecast-gate/forecast-gate-profiles.json`, và `job-configs-v2/` do bước 2 ghi.
Tên ô của adapter có dạng `dYYYYMMDD-vf-rN-wHH`: `d20190919-vf-r0-w08` là ô `2019-09-19-w08-r0` của bộ mô phỏng.

```bash
# máy nhà: mọi lệnh trong khối này.
# 0. Runner: như ở mục 6 (dotnet publish ... -o runner -p:UseAppHost=false).

# 1. Ô -> fixture, driver, world (cần numpy). F16: ô trong $DATA/f16/weekdays-P512/.
python -B src/adapter/convert_via_full_v2.py $DATA/wf14/scenarios/weekdays-P512/2019-09-19-w08-r0.json $PANEL d20190919-vf-r0-w08
python -B src/adapter/make_run_drivers.py write $PANEL/drivers/d20190919-vf-r0-w08.driver.json \
  $PANEL/drivers-run/d20190919-vf-r0-w08.driver.json complete-graph-direct-v1 gzip-v1

# 2. Cấu hình job của mọi ô: SHRC71, SHHZ71, WP4 của từng ô; F14 thêm nhánh cổng dự báo (F16: bỏ --forecast-gate).
F=src/core/configs/final
python -B src/adapter/make_job_configs.py --data $PANEL --network <thư mục mạng Shadow> --g2-rce35 $F/templates/G2-RCE35.json \
  --g2-eohz35 $F/templates/G2-EOHZ35.json --wp4-template $F/wp4-via-g2k8-a1-fast.json --forecast-gate

# 3. Cả đợt: 168 job F14 (F16: --panel f16, 192 job). Chạy lại thì bỏ qua job đã xong; --dry-run chỉ in danh sách job.
python -B src/adapter/run_campaign.py --panel f14 --data $PANEL --out runs --logs logs \
  --fleetpy-root FleetPy --runner-root runner --dotnet dotnet --workers 2

# 4. Bảng từng khách của mọi job (đọc transcript-00.ndjson.gz nếu job còn giữ, nếu không thì evidence.json.gz).
python -B src/adapter/riders.py --root runs --out f14.tsv --sequences f14-seq.tsv
gzip -dc src/analysis/data/f14-real-riders.tsv.gz | cmp - f14.tsv && echo same
```

- Bước 1 dùng đúng hai script đã sinh fixture, driver và world của các lần chạy cuối (giữ nguyên từng byte).
  Fixture sinh lại từ ô của `gen17.js` chưa được so với fixture đã dùng.
- Bước 2 kiểm mọi đầu vào trước khi ghi file đầu tiên. Đầu vào khác các lần chạy cuối bị từ chối bằng một dòng
  `refused: <file>: <problem>` (exit 1); `--help` liệt kê các luật. Ba mẫu là file của các lần chạy cuối (sha256 ở mục 7).
  Với ba mẫu này, `commitment/RCE35.json`, `SHRC71.json` và `SHHZ71.json` mà bước 2 ghi giống từng byte các file của
  các lần chạy cuối (ba file này chỉ phụ thuộc vào mẫu; đã kiểm ở máy kiểm trên bộ dữ liệu nhỏ của
  `tests/test_make_job_configs.py`).
- Chuỗi `--network` đi nguyên vào cấu hình (`shadowAllocation.networkDirectory`), nên vào cả các hash trong frame.
  Runner mở thư mục này từ thư mục đang chạy bước 3, nên hãy dùng đường dẫn tuyệt đối.
  Cấu hình chỉ giống từng byte cấu hình đã dùng khi chuỗi này giống chuỗi cũ. Vì thế so lần chạy mới với lần chạy đã ghi
  bằng `replay.py compare`, phép so bỏ qua hash.
- Bước 3: `--workers` = min(số nhân CPU, (RAM tính bằng GB − 4) // 7). Mỗi job ghi vào `runs/<nhánh>-<ô>/`:
  `transcript-00.ndjson.gz` (xóa ở cuối job nếu job chạy với `--transcript drop`; job của các ô gate giữ nó), `run-00.json`,
  `evidence.json.gz` + `EVIDENCE.sha256`, `summary.json` (`"status": "pass"`, chỉ ghi sau khi đọc lại evidence) và
  `bundle-manifest.json` (ghi cuối). Job lỗi ghi `failure-00.json` thay cho `summary.json`. `run-00.json` ghi số file đã băm
  làm biên nhận (`artifactCount`) và biên nhận lúc đầu và lúc cuối job có bằng nhau không (`artifactReceiptsEqual`).
  Một file biên nhận đổi giữa job không làm job lỗi; khi đó giá trị này là false.
- Bước 4: với 360 job đã lưu, bảng phải giống từng byte bảng đã ghi (kiểm V-TSV). Với job chạy mới, dòng của ô-nhánh đó
  phải bằng dòng đã ghi (kiểm V3). `riders.py` nhận một hoặc nhiều `--root` (thư mục chứa các thư mục job) và chỉ đọc
  các nhánh của `--arms` (mặc định `FS-G2-SHRC71-CL,FS-G2-SHHZ71-N`). `--sequences` ghi thêm chuỗi ETA trả đã công bố
  của từng khách; không có tùy chọn xuất ETA nào khác. `riders.py` không ghi đè: `--out` và `--sequences` phải chưa có.

---

## 6. Kiểm ở máy nhà

"Máy nhà" là máy có Python 3.10, FleetPy 1.0.2, transcript và evidence của các lần chạy thật. Máy kiểm của dự án
không có chúng. Mọi lệnh của mục này là lệnh máy nhà, trừ lệnh `dotnet publish` ở cuối mục.
Trong các lệnh adapter của README, máy kiểm chỉ chạy phần không cần FleetPy và dữ liệu thật: 42 test ở mục 4, `--help`
của từng script và các phép từ chối ở mục 10.

| Id | Kiểm gì | Lệnh | Đạt khi |
|---|---|---|---|
| G7 | Byte của cấu hình cuối | so sha256 các file cấu hình của gói chạy (mẫu WP4 cuối, các file commitment, `CONFIGS-V2.json` của F14 và F16) với mục 7; so từng file WP4 của job, sau khi bỏ khối `shadowAllocation` và `planningForecast`, với mẫu; `grep -E "stopServiceDurationMs\|maximumRidersPerVehicle\|skipConstantObjectiveLevels\|solverExecutionEvidenceProfile"` trên mọi file cấu hình | sha bằng sha ở mục 7; mọi file WP4 của job bằng mẫu; không khóa nào ở trên xuất hiện; `emitSolverExecutionEvidence` chỉ có giá trị `false` |
| V1 | Phát lại transcript thật qua Runner | `python -B src/adapter/replay.py runner --transcript <job>/transcript-00.ndjson.gz --runner <thư mục publish> --policy-config <commitment.json> --wp4-config <wp4.json>`. Chạy một lần với Runner dựng từ `ebca912`, rồi với Runner dựng từ cây này | mọi frame `runnerToAdapter` bằng nhau, kể cả hash quyết định light-v1 và dòng checkpoint cuối. Tối thiểu: F14 `d20190919-vf-r0-w08` × {SHRC71-CL, SHHZ71-N} |
| P-FLEET | Test adapter với FleetPy | `RIDEBOUND_FLEETPY_ROOT=<FleetPy> python -B -m unittest discover -s src/adapter/tests -t src/adapter/tests -p "test_*.py"` | 0 bỏ qua; mỗi test cũ cho kết quả như ở `ebca912` (trừ test của mã đã bỏ); test mới (`test_replay`, `test_run_job`, `test_run_campaign`, `test_make_job_configs`, `test_riders`) đều ok. Hiện có 134 test |
| P-REPLAY | Phát lại transcript qua adapter | `python -B src/adapter/replay.py adapter --transcript <T> --out <x> -- src/adapter/run_job.py <tham số job>`. Tham số job: như lệnh `run_campaign.py --dry-run` in cho job đó, với `--transcript keep`, `--output` là thư mục mới tên `<nhánh>-<ô>`, và `--label` = `runId` của `initializeRun` đã ghi, bỏ tiền tố `wp7-medium-`. Chuỗi chạy cũ trước `run_job.py` (launcher + world wrapper) chỉ có ở các commit trước `5d9f507` | mọi frame `adapterToRunner` bằng nhau từng byte; riêng `initializeRun` được so theo giá trị JSON sau khi bỏ `coreCommitSha` và `binarySha256`. Tối thiểu: hai transcript gate F14 `d20190919-vf-r0-w08` × {SHRC71-CL, SHHZ71-N} |
| V3 | Một job thật từ đầu | `python -B src/adapter/run_campaign.py --panel f14 ... --cells gate --arms FS-G2-SHRC71-CL` (hoặc `run_job.py` với cùng tham số; 18–25 phút/job); `python -B src/adapter/replay.py compare <job mới> <job đã ghi>`; `python -B src/adapter/riders.py --root <thư mục chứa job mới> --out new.tsv` | mỗi dòng transcript bằng nhau từng byte, hoặc cùng hướng, số thứ tự, `schemaVersion` và độ dài frame, với frame bằng nhau về giá trị JSON sau khi bỏ `coreCommitSha` và mọi khóa `*Hash`/`*Sha256` (phép so này không xét thứ tự khóa); dòng khách bằng dòng của ô-nhánh đó trong `src/analysis/data/f14-real-riders.tsv.gz` |
| V-TSV | Bảng khách từ các job đã lưu | `python -B src/adapter/riders.py --root <thư mục 168 job F14> --out f14.tsv`, rồi cùng lệnh cho 192 job F16 (`--out f16.tsv`); không đặt `RB_PREFER_EVIDENCE` | sha256 của file bằng `786048fb…` (F14, 58.033 dòng khách) và `fa785fde…` (F16, 67.922 dòng), như trong `MANIFEST.tsv`. Một dòng từ chối là một phát hiện cần báo lại |
| CFG | Byte của cấu hình job | lệnh `make_job_configs.py` của mục 5.6 với `--data <panel F14> --network <đúng chuỗi networkDirectory cũ> --forecast-gate --out <thư mục mới>`; F16 không có `--forecast-gate` | mọi file bằng từng byte file trong `job-configs-v2/` đã ghi; `CONFIGS-V2.json` của F14 có sha256 `18ed3c1d…` (file này ghi ở chế độ văn bản: CRLF trên Windows) |
| LIST | Danh sách job | `python -B src/adapter/run_campaign.py --panel f14 ... --dry-run` (và `--cells gate`; F16 tương tự) | các trường của từng job bằng danh sách job đã ghi của các lần chạy cuối |
| V4 | Thời gian và bộ nhớ | đo trong V1/V3 | không tệ hơn: khoảng 25 phút/job ở máy nhà (15–21 phút trên Linux); đỉnh bộ nhớ ≤ 6,2 GB với trần heap 6 GiB, đo cả bước ghi evidence (chạy trong tiến trình của job) |

Tùy chọn: chạy lại 4 job gate trên Linux và so bảng khách (`riders.py`) với bảng đã ghi.

Kết quả ở máy nhà (ngày 10/11/2026), trên các commit ghi trong bảng:

| Id | Chạy trên | Kết quả |
|---|---|---|
| G7 | file cấu hình của gói chạy | đạt. Mọi sha bằng sha ở mục 7. Cả 362 file WP4 của job (F14 170, F16 192) bằng mẫu sau khi bỏ khối của ô. Bốn khóa: 0 lần trong 723 file. `emitSolverExecutionEvidence` chỉ có `false`, `auditHash` chỉ có `light-v1` |
| V1 | Runner dựng từ `ebca912` và từ `d35b472` | đạt: SHHZ71-N 2.849/2.849 frame, SHRC71-CL 2.878/2.878 frame bằng nhau, với cả hai Runner |
| P-REPLAY | chuỗi cũ trên `9ba5568` và `ebca912`; `run_job.py` và chuỗi cũ trên `d35b472` | đạt: hai transcript gate (8.545 và 8.632 frame) bằng nhau trừ hai khóa được phép, với mọi chuỗi |
| P-FLEET | `d35b472` (118 test) | đạt: 118/118 ok, 0 bỏ qua. Mỗi test có ở cả hai commit cho cùng kết quả như ở `ebca912`; 10 test lỗi ở `ebca912` đều là test WP9/WP13/WP14 đã bỏ |
| V3 | `d35b472`, `run_job.py`, SHRC71-CL | đạt: 8.632 dòng, 0 dòng khác sau khi bỏ các khóa hash; 1.084 s, đỉnh bộ nhớ 4.629 MiB |
| V4 | số đo của V1 và V3 | đạt: dưới 25 phút/job, đỉnh bộ nhớ dưới 6,2 GB |
| CFG | `make_job_configs.py` của `e6cd063` | đạt: F14 174/174 file, F16 195/195 file bằng từng byte |
| V-TSV | `riders.py` của `e6cd063` | đạt: F14 168 job, 58.033 dòng, `786048fb…`; F16 192 job, 67.922 dòng, `fa785fde…` |
| LIST | | chưa chạy |

Không file nào trong `src/core/configs/final/` dùng khóa hay giá trị đã bỏ (mục 10). Mẫu WP4 ghi `initialPromiseTrigger`
`booking-confirmation`, `lateBoarding` `record-v1` và `commitmentRecovery` `no-worse-than-reference-v1`; mọi chính sách ghi
`budgetBasis` `decisionInduced`; mọi chính sách có hạn chót (`dropEtaDeadlineSlackPermille`) ghi `dropEtaDeadlineRelaxed`
`true`. Theo G7, mọi file WP4 của job bằng mẫu, ngoài hai khối của ô.

Runner và adapter đã đổi sau các commit trong bảng. Vì vậy V1, P-FLEET, P-REPLAY, V3, V-TSV và V4 còn phải chạy lại trên
mã hiện tại; chúng chưa chạy. `test_mapping.py` (chạy trong P-FLEET) đọc hai file cấu hình từ `src/core/configs/test/`.

Runner cho các kiểm này:

```bash
cd src/core
dotnet publish RideBound.Runner/RideBound.Runner.csproj -c Release -o ../../runner -p:UseAppHost=false
cd ../..
```

- `-p:UseAppHost=false` không sinh file `.exe`/apphost. Adapter và `replay.py` luôn chạy Runner bằng `dotnet runner/RideBound.Runner.dll`.
- Biến môi trường của lần chạy cuối: `DOTNET_GCHeapHardLimit=0x180000000` (6 GiB), không đặt biến `RIDEBOUND_*` nào.

---

## 7. Giao diện được giữ ("API")

"API" của dự án gồm ba thứ, và cả ba giữ nguyên hành vi so với `ebca912`:
1. **Giao thức v1** giữa adapter và Runner: NDJSON, một JSON mỗi dòng, LF. Các thông điệp `hello`/`helloAck`,
   `initializeRun`/`initialized`, `eventBatch`, `decision`, `decisionApplied`, `checkpoint` (dạng stream), `restore`, `error`, `shutdown`.
   57 file schema (`src/core/protocol/schemas/v1/`) giữ nguyên từng byte.
2. **Đúng một dòng lệnh Runner**, dòng lệnh của các lần chạy cuối:
   ```
   dotnet RideBound.Runner.dll --mode commitment --policy-config <commitment.json> --wp4-config <wp4.json>
     --solver-seed-source manifest-master-seed [--maximum-line-bytes <1048576..1073741824>]
   ```
   Lần chạy cuối dùng `--maximum-line-bytes 1073741824`.
3. **Các file cấu hình cuối**: cùng byte thì cùng hash nội dung, cùng binding và cùng quyết định. Khóa lạ vẫn bị từ chối.
   Một khác biệt đã khai ở bước 2.14g, chưa làm đổi kết quả nào trên các đầu vào đã kiểm: xe mà cổng cam kết loại cả
   phương án giữ tuyến nay luôn làm phiên dừng (mục 11).

**Cấu hình cuối** (`src/core/configs/final/`):

| File | sha256 | Vai trò |
|---|---|---|
| `wp4-via-g2k8-a1-fast.json` | `820a4de7…` (1.005 byte) | mẫu WP4 cuối (G2, k = 8) |
| `commitment/SHRC71.json` | `d536e48d…` | luật C + Shadow 71; bằng file `Final/Fixtures/commitment/SHRC71.json` trong test của Runner |
| `commitment/SHHZ71.json` | `9ca0f404…` | rào lỏng + Shadow 71; bằng file `Final/Fixtures/commitment/SHHZ71.json` trong test của Runner |
| `commitment/RCE35.json` | `35511d47…` | luật C 0,35, chỉ cho cổng dự báo F14 |
| `templates/G2-RCE35.json` | `35511d47…` | mẫu luật C 0,35 cho G2, cùng byte với `commitment/RCE35.json`; `make_job_configs.py` sinh SHRC71 từ nó |
| `templates/G2-EOHZ35.json` | `112e975b…` | mẫu rào lỏng 0,35 cho G2; `make_job_configs.py` sinh SHHZ71 từ nó |
| `templates/INS-RCE35.json` | `ce0446ed…` | luật C 0,35 cho INS |
| `templates/INS-EOHZ35.json` | `ba232d84…` | rào lỏng 0,35 cho INS |

- Bốn file đầu là đúng các file của các lần chạy F14/F16; sha256 của chúng đã được kiểm ở máy nhà (G7, mục 6).
- Bốn mẫu là các file ở `research/continuation/2026-10-26/kits/via-bridge-2026-10-22/configs/` của commit `ebca912`,
  giữ nguyên byte. Ở đó hai mẫu INS tên là `RCE35.json` và `EOHZ35.json`.
- `CONFIGS-V2.json` (sha256 của mọi file cấu hình job, do `make_job_configs.py` ghi, mục 5.6) không nằm trong repo.
  Bản của F14 có sha256 `18ed3c1d…`, bản của F16 có `7a940202…`.

Kiểm sha256 (in 8 dòng `OK`):

```bash
(cd src/core/configs/final && sha256sum -c) <<EOF
820a4de7ff11f92f4176c9d169e5ca12ed03797389c02da35fbb520fdfa55f52  wp4-via-g2k8-a1-fast.json
d536e48d61ac857b011af2838030596db4d79c1f3db768862e82f84cba98bc87  commitment/SHRC71.json
9ca0f4043fd0d32fd708f40bd47349771f8e296f93d8abc3d700bb84ed180d74  commitment/SHHZ71.json
35511d472c9eb55baa81918c50635ef06c4bdb17c536e78122a71cd318b7ad83  commitment/RCE35.json
35511d472c9eb55baa81918c50635ef06c4bdb17c536e78122a71cd318b7ad83  templates/G2-RCE35.json
112e975bfd4dc4b7b2449d447e9109ed176f33965c8731ac83edc9c25ff3a8c6  templates/G2-EOHZ35.json
ce0446edf284e5e490d55f54bfb7f2e35938f064bd650d70d2c616cecd8e69d4  templates/INS-RCE35.json
ba232d845e5b99a476aa5ce12fd877b78a2b0a1fb5e57c418ab5977eb486b950  templates/INS-EOHZ35.json
EOF
```

File WP4 của một job là mẫu này cộng khối `shadowAllocation` (nhánh Shadow) và/hoặc `planningForecast` (nhánh CL),
do `make_job_configs.py` thêm. Các khóa của mẫu:
- `policyId` `ridebound-hard-vector` (C1, chính sách duy nhất còn lại), `selectionObjectiveOrder` `cost-first`,
  `emitSolverExecutionEvidence` `false`, `auditHash` `light-v1`;
- `initialPromiseTrigger` `booking-confirmation`, `commitmentRecovery` `no-worse-than-reference-v1`, `lateBoarding` `record-v1`;
- `candidateGeneration`: tối đa 100 ứng viên và 2 khách mới mỗi xe, công khám phá tối đa 10.000, `exactSmallMode` `false`,
  `scheduleStrategy` `earliest-feasible`, `retentionStrategy` `service-set-stability-portfolio-v1`,
  `waitingRiderRelocation.maximumTargetVehicles` 8 (G2K8);
- `solverExecution`: OR-Tools 9.15.6755, `randomSeed` 7 và các trần công.

Các khóa sau của file WP4 phải có và chỉ nhận đúng giá trị của mẫu; thiếu khóa hoặc giá trị khác đều bị từ chối (mục 10):
- `selectionObjectiveOrder` `cost-first`;
- `candidateGeneration.scheduleStrategy` `earliest-feasible`, `candidateGeneration.retentionStrategy`
  `service-set-stability-portfolio-v1`, `candidateGeneration.exactSmallMode` `false`;
- `initialPromiseTrigger` `booking-confirmation` (lời hứa đầu mở khi đặt xe được xác nhận), `lateBoarding` `record-v1`
  (khách lên xe trễ được ghi lại) và `commitmentRecovery` `no-worse-than-reference-v1`. Giá trị cuối này không làm gì;
  khóa vẫn bắt buộc vì các file cuối ghi nó.

`emitSolverExecutionEvidence` phải là `false` hoặc không có; Runner không ghi `executionEvidence` vào quyết định.
Ba khóa mà mẫu không có bị từ chối với mọi giá trị: `candidateGeneration.stopServiceDurationMs`,
`candidateGeneration.waitingRiderRelocation.maximumRidersPerVehicle`, `solverExecution.skipConstantObjectiveLevels`.
Không có chúng thì điểm chèn không có thời gian dừng, G2 xét mọi khách chờ đủ điều kiện, và mọi mức mục tiêu đều được giải.

**File commitment.** Mỗi phần tử của `policies` là một luật giữ lời hứa:
- `budgetBasis` phải có và chỉ nhận `decisionInduced`: mỗi lần sửa lời hứa bị tính theo phần thay đổi do điều phối gây ra.
- `freezeHorizonMs`, `freezeHorizonLocks`, `dropEtaRevisionLimitMs`, `dropEtaDeadlineTwoSided` và `dropEtaDeadlineSlackMs`
  (hạn chót ms cố định) bị từ chối với mọi giá trị.
- Hạn chót chỉ có một dạng: rào lỏng, tính bằng phần nghìn của slack. Chính sách có hạn chót
  (`dropEtaDeadlineSlackPermille`) phải ghi `dropEtaDeadlineRelaxed: true`. SHHZ71 ghi `dropEtaDeadlineSlackPermille` 1000
  và `dropEtaDeadlineRelaxed` `true`; SHRC71 và RCE35 không có hạn chót.
- Hai file phải khớp về G2: nếu file WP4 có `candidateGeneration.waitingRiderRelocation` thì mọi chính sách phải ghi
  `waitingRiderRelocation: true`, và ngược lại.

**INS.** File WP4 của INS là mẫu cuối bỏ khối `candidateGeneration.waitingRiderRelocation`; repo không có file WP4 INS
riêng. Nó đi với một mẫu INS. Hai mẫu INS khác hai mẫu G2 ở ba chỗ: thêm `vehicle` vào `finalConfirmationLocks` (khách đã
được hứa không đổi xe), `vehicle_switch_count` có `hardLimit` 0, và không có `ratchetLocks` hay `waitingRiderRelocation`.
Test của Runner chạy INS với hai bản có Shadow: `Final/Fixtures/commitment/INS-SHRC.json`, `INS-SHHZ.json` và
`Final/Fixtures/wp4/INS-*.json`.

---

## 8. Thuật toán lõi

### 8.1 Một lần quyết định (đường của các lần chạy cuối)

```text
khi nhận eventBatch(batch):                                   # RunnerSession
    kiểm thứ tự(batch)                                        # epoch, seq, thời gian liền mạch
    S = giảm(S_đã_commit, batch)                              # EventReducer; khách lên xe trễ luôn được ghi lại (lateBoarding record-v1)
    T = S.thời_gian_đi × dự_báo(profile)  nếu có planningForecast (CL)
        S.thời_gian_đi                    nếu không (N: giữ giao thông hiện tại cho mọi bin sau)
    # từ đây đến shadow.xếp_lớp: DispatchStep.Run (RideBound.Algorithms/Dispatch); lỗi -> INTERNAL_ERROR, phiên dừng
    với mỗi xe v:
        C[v] = [giữ_tuyến(v)] + chèn_tốt_nhất_trước(v, khách_chờ, tối_đa_mới = 2, công = 10.000)
        C[v] = giữ_lại(C[v], trần = 100)                      # danh mục ổn định tập khách phục vụ
        nếu G2: C[v] += cặp_dời(v, k = 8)                      # xe cho bỏ một khách chờ, một trong 8 xe nhận chèn khách đó
        C[v] = [c trong C[v] nếu cổng(c)]                      # mục 8.2
        nếu cổng loại giữ_tuyến(v): quyết định lỗi, phiên dừng (INTERNAL_ERROR)
    chọn = cp_sat_từ_vựng(C)          # thứ tự cost-first (C1). Mức 1: số khách nhận ↑; 2: chi phí vận hành ↓;
                                      # 3: số ứng viên dời ↓ (chỉ khi có G2); 4: hạng id ứng viên của từng xe.
                                      # Mỗi mức là một tổng; giải lần lượt từng mức.
                                      # Mỗi xe đúng 1 phương án, mỗi khách tối đa 1 xe.
    D = kiểm_toàn_đội(chọn)                                   # hỏng thì giữ tuyến
    sổ_lời_hứa.ghi(công_bố(D))                                # lời hứa đầu mở khi đặt xe được xác nhận
    ghi_vi_phạm_do_giao_thông(); shadow.xếp_lớp(khách_vừa_có_lời_hứa_đầu)
    h = hash(toàn bộ S_tạm)                 nếu full-v1
        hash(eventBatch, danh tính S)       nếu light-v1 (epoch, seq kế tiếp, thời gian; chế độ của lần chạy cuối)
    gửi decision(D, h); chờ decisionApplied(h); commit        # adapter xác nhận bằng đúng hash
```

### 8.2 Cổng cam kết

```text
cổng(c): với mỗi khách r đã có lời hứa mà ứng viên c chạm tới
    slack_r = max(0, giờ_đón_hứa_đầu + thời_gian_đi_tối_đa − giờ_trả_hứa_đầu)   # đo một lần, ở lời hứa đầu
    B_r     = round(permille / 1000 × slack_r)                                  # C# làm tròn xa số 0
    Shadow: B_r = 0       nếu r "được bảo đảm" (điểm chia sẻ sS ≤ θ, lớp đóng băng ở lời hứa đầu)
            B_r = slack_r nếu r "được phép"
    e = giờ trả của r nếu giữ tuyến cũ (dự chiếu lại lúc này); p = giờ trả của r theo c; Δ = p − e
    U          : nhận
    rào lỏng   (EOHZ, SHHZ): nhận nếu p ≤ max(giờ_trả_hứa_đầu + B_r, e)
    luật C     (RCE, SHRC) : nhận nếu L_r + |Δ| ≤ B_r            # L_r = tổng |Δ| đã công bố trước đó (sổ của r)
    rào chặt   (EO, SHF)   : nhận nếu p ≤ giờ_trả_hứa_đầu + B_r   # chỉ còn trong bộ mô phỏng; Runner bỏ ở bước 2.14g
    INS: thêm khóa xe: khách đã được hứa không đổi xe
```

Hệ quả: luật C chỉ tính phần thay đổi **do điều phối**. Giữ tuyến cũ luôn tốn 0 ngân sách, kể cả khi kẹt xe làm trễ.

Bản viết lại phải giữ **đúng**: định danh ứng viên (SHA-256; nó quyết định tie-break), JSON chuẩn hóa, các miền hash,
thứ tự sự kiện, cách làm tròn `B_r` và dự báo, và thứ tự các mức mục tiêu.

### 8.3 Bản đồ đọc mã

Đọc theo thứ tự. Để giải thích một quyết định, không cần đọc `RideBound.Contracts` hay `RideBound.Runner/StateAudit/`.

| # | File | Đọc để hiểu |
|---|---|---|
| 1 | `src/core/RideBound.Algorithms/Dispatch/DispatchStep.cs` | `Run`: một quyết định, từ chính sách tới lớp Shadow (mục 8.1). Phần giao thức quanh nó (lô → sự kiện miền, giảm sự kiện, hash trạng thái, hành động giao thức): `RideBound.Runner/Protocol/RunnerSession.Decision.cs`, `BuildOnlineDecision` |
| 2 | `src/core/RideBound.Algorithms/Policies/SolverBackedRidePoolingPolicy.cs` | `Decide`: sinh → cổng → ngân sách công → chọn → áp dụng (bước áp dụng: `SelectionApplier.cs`, cùng thư mục) |
| 3 | `src/core/RideBound.Algorithms/Candidates/InsertionCandidateGenerator.cs` | `GenerateForVehicle`: thứ tự khách chờ, 2 khách đầu, `EvaluateNoOp` (phương án giữ tuyến), `ExploreBestFirst`, `EvaluateRoute` |
| 4 | `src/core/RideBound.Algorithms/Commitments/HardVectorCandidateAssessor.cs` | `AssessAndFilter`; G2: `AssessRelocations`, `MergeRelocations` |
| 5 | `src/core/RideBound.Application/Commitments/CommitmentDecisionValidator.cs` | `Validate`: `OpenInitialPromise` (lời hứa mở khi đặt xe được xác nhận), `CheckRevision` (khóa, hạn chót, ngân sách; ghi lời hứa sửa vào sổ) |
| 6 | `src/core/RideBound.Domain/Commitments/CommitmentPolicy.cs` | permille → ms, ngân sách theo lớp Shadow |
| 7 | `src/core/RideBound.Domain/Commitments/CommitmentLockEvaluator.cs` | rào lỏng, khóa giờ đón |
| 8 | `src/core/RideBound.Domain/Commitments/CommitmentBudgetEvaluator.cs` | luật C, ngân sách từng khách |
| 9 | `src/core/RideBound.Algorithms/Policies/SolverBackedFleetSelection.cs` | các mức từ vựng cost-first (mục 8.1) |
| 10 | `src/core/RideBound.Solvers.OrTools/OrToolsCandidateSelectionSolver.cs` | mô hình CP-SAT |
| 11 | `src/core/RideBound.Algorithms/Shareability/ShadowAllocator.cs` | lớp Shadow đóng băng ở lời hứa đầu |
| 12 | `src/core/RideBound.Domain/Validation/TravelTimeForecast.cs` | giờ tới từng chặng theo dự báo |
| 13 | `src/emulator/engine.js` | `admits`/`charge`, `decide`, `selectPlan` |
| 14 | `src/emulator/sscore.js` | điểm chia sẻ (đọc bộ chấm tham chiếu `tests/ref_scorer.js` trước) |
| 15 | `src/emulator/forecast.js` | hồ sơ giao thông từ cùng thứ trong 4 tuần trước |

Vòng hai: `CandidatePortfolioRetainer` (`RetainRanked`: danh mục ổn định, mục 9), `WaitingRiderRelocationGenerator.cs`
(sinh ứng viên G2; một phần của lớp `InsertionCandidateGenerator`) với `WaitingRiderRelocationBuilder` (khách chờ đủ điều
kiện, xe nhận, tuyến của xe nhận), `ShareabilityScorer`, `EventReducer`.

### 8.4 Bộ mô phỏng và C# tương ứng thế nào

| Bộ mô phỏng (`engine.js`) | C# |
|---|---|
| `admits` (bảng `ADMIT`) | `CommitmentLockEvaluator` + `CommitmentBudgetEvaluator` |
| `expandRoot` | `ExploreBestFirst` |
| `retainPortfolio` | `CandidatePortfolioRetainer` |
| `candId` (`identity.js`) | `CandidateIdentity` |
| `legF` | `TravelTimeForecast` |
| `selectPlan` | `SolverBackedFleetSelection` + CP-SAT |

`selectPlan` giải cùng bài toán từ vựng bằng cách liệt kê hết. Cách này đủ nhanh vì mỗi xe chỉ nhận tối đa 2 khách mới (`MAX_NEW_REQUESTS = 2`).

Nếu muốn tự viết lại để hiểu, nên bắt đầu từ: `admits`/`charge`, `legF`, phép liệt kê trong `selectPlan`, và luật Shadow `sS ≤ θ`.

---

## 9. Thuật ngữ

| Từ | Nghĩa |
|---|---|
| Runner | Tiến trình C# điều phối. Nhận `eventBatch`, trả `decision`. |
| Adapter | Mã Python nối FleetPy với Runner: biến sự kiện của FleetPy thành thông điệp giao thức và áp quyết định vào xe. |
| Bộ mô phỏng (emulator) | Bản JavaScript của cùng luật điều phối, chạy nhanh. "plain": tự mô phỏng xe; "adapter mode": dự đoán từng khách cho một lần chạy thật. |
| Ô (cell) | Một ngày × một khung giờ × một lần lấy mẫu, ví dụ `2019-02-07-w08-r1`. `w08` = 08:00–10:00, `w17` = 17:00–19:00; `r0..r2` = lần lấy mẫu. P512 = 512 yêu cầu. |
| Job, tên ô của adapter | Một lần chạy thật: một ô × một nhánh, bằng `run_job.py`, ghi vào thư mục `<nhánh>-<ô>`. Adapter viết ô dạng `dYYYYMMDD-vf-rN-wHH`, ví dụ `d20190919-vf-r0-w08` = `2019-09-19-w08-r0`. Ô gate: hai khung giờ của `r0` ngày 2019-09-19 (F14) và 2019-09-20 (F16). |
| F14, F16, D60, D30 | Tập ngày: F14 (14 ngày) và F16 (16 ngày) là 30 ngày của §7 và báo cáo cuối; D60 là 60 ngày của §6; D30 là 30 ngày của R6 (`data/panels.json`). |
| INS | Chỉ chèn: khách đã hứa không đổi xe. |
| G2 (G2K8) | Chèn, cộng mỗi quyết định được dời tối đa một khách đang chờ sang một trong 8 xe gần nhất. Ứng viên G2 sinh ở `WaitingRiderRelocationGenerator.cs` và qua cổng cam kết theo cặp xe cho, xe nhận (`HardVectorCandidateAssessor.AssessRelocations`). |
| Nhánh (arm) | Một luật giữ lời hứa. Xem ba dòng dưới và mục 8.2. |
| U | Không giới hạn thay đổi. |
| EO35 / EOHZ35 / RCE35 | Luật của Elmi với `B_r = round(0,35 × slack_r)`: rào chặt / rào lỏng / luật C. |
| SHF / SHHZ / SHRC | Ba luật trên với ngân sách Shadow: 0 cho khách được bảo đảm, cả slack cho khách được phép. Số đi kèm (72 cho INS, 71 cho G2) là tỉ lệ khách được bảo đảm x*, tính bằng %. |
| Shadow | Cách chia khách thành "được bảo đảm" và "được phép" theo điểm chia sẻ sS (Elmi và cộng sự, 2026; PDF trong `references/`). Lớp đóng băng ở lời hứa đầu. |
| Luật C (Rule C) | Sổ cho từng khách: tổng trị tuyệt đối các lần dời giờ trả do điều phối không vượt `B_r` (`budgetBasis` `decisionInduced`). Cơ sở `customerVisible`, tính cả phần dời do giao thông, đã bỏ ở bước 2.14a. |
| Rào chặt / rào lỏng | Rào chặt: giờ trả không quá giờ hứa đầu + `B_r`. Rào lỏng: không chặt hơn giờ trả nếu giữ tuyến cũ (sàn Hoerl–Zwick). Runner chỉ còn rào lỏng (`dropEtaDeadlineRelaxed: true`), với hạn chót tính bằng phần nghìn của slack (`dropEtaDeadlineSlackPermille`); rào chặt đã bỏ ở bước 2.14g và chỉ còn trong bộ mô phỏng, cho các nhánh EO35 và SHF; hạn chót ms cố định (`dropEtaDeadlineSlackMs`) đã bỏ ở bước 2.14f. |
| slack, permille | `slack_r` = thời gian còn dư của khách ở lời hứa đầu. permille = phần nghìn của slack được dùng làm ngân sách. |
| N / CL / PAD / CLc / PADc | Chế độ dự báo: N = giữ giao thông hiện tại; CL = dự báo vòng kín (cùng thứ, 4 tuần trước); PAD = đệm phẳng có cùng độ dài lời hứa trung bình như CL; CLc = CL với chỉ số nhân quả; PADc = đệm hiệu chỉnh theo CLc. |
| served, pDisp, visRev60, late60First, TV5 | Phục vụ; số khách bị điều phối dời muộn ≥ 5 s ít nhất một lần; số lần đảo hứa (đổi ≥ 60 s rồi đổi ngược); trễ > 60 s so với lời hứa đầu; tổng dịch chuyển giờ hứa, bỏ bước < 5 s. |
| breachDeadline60, breachLedger | Trễ > 60 s so với hạn chót của nhánh; vượt sổ luật C quá 1 s. |
| sig | Dấu vân tay từng khách của một dòng kết quả (SHA-256). Hai dòng cùng `sig` là cùng kết quả cho mọi khách. |
| digest | File TSV trong `src/emulator/expected/`: các chỉ số chính + `sig` của mọi dòng đã chạy gốc. |
| data root | Thư mục chứa ô, mạng và đầu vào lớn của bộ mô phỏng (mục 3.2). `wf5`, `wf14`, ... là tên cũ của các nghiên cứu. |
| fixture | Hai nghĩa. `src/core/protocol/fixtures/`: thông điệp giao thức mẫu, đúng và sai, cho test C#. `fixtures/<ô>/` của một panel: đầu vào FleetPy của một ô, do `convert_via_full_v2.py` ghi (mục 5.6). |
| WP4, file commitment | Hai file cấu hình của Runner. "WP4" là tên cũ (gói việc 4): cấu hình bộ điều phối (sinh ứng viên, solver, Shadow, dự báo). File commitment: luật giữ lời hứa (khóa, ngân sách). Bản cuối của cả hai: mục 7. Các cấu hình mà test đọc nằm ở `src/core/configs/test/`. |
| light-v1 / full-v1 | Chế độ hash quyết định. full-v1 hash toàn bộ trạng thái mỗi quyết định; light-v1 chỉ hash lô sự kiện và danh tính trạng thái. Checkpoint luôn chứa toàn bộ trạng thái. |
| transcript, frame | Bản ghi mọi dòng hai bên gửi nhau trong một job; mỗi dòng là một frame, có `frameSha256`. |
| C1 cost-first | Chính sách điều phối duy nhất còn lại (`policyId` `ridebound-hard-vector`): cổng cam kết cứng, rồi chọn theo khách nhận ↑, chi phí ↓ (đủ các mức ở mục 8.1). File WP4 phải ghi `selectionObjectiveOrder: "cost-first"`; thứ tự stability-first đã bỏ ở bước 2.11. Mỗi mức là tổng đóng góp của các phương án được chọn; mức tổng hợp Maximum đã bỏ ở bước 2.13g. Mức đầu cũ "số phương án giữ tuyến mà cổng đã loại" (chỉ có khi phục hồi forced-reference giữ một phương án như vậy) đã bỏ cùng phục hồi đó ở bước 2.14g. |
| `initialPromiseTrigger`, `lateBoarding`, `commitmentRecovery` | Ba khóa cam kết của file WP4, mỗi khóa chỉ nhận một giá trị (mục 7). `booking-confirmation`: lời hứa đầu mở khi đặt xe được xác nhận (cách mở lúc chấp nhận, `initial-acceptance`, đã bỏ ở bước 2.14b). `record-v1`: khách lên xe sau giờ đón muộn nhất được ghi lại như một sự việc; cách cũ `reject` làm lô sự kiện lỗi (bỏ ở 2.14b; adapter bỏ theo ở 4.LB). `no-worse-than-reference-v1`: nay không làm gì; trước bước 2.14g nó bật phục hồi forced-reference: khi cổng loại cả phương án giữ tuyến của một xe, quyết định vẫn giữ phương án đó (hoặc một tuyến không tệ hơn nó) và ghi một vi phạm. |
| Danh mục ổn định tập khách phục vụ (`retentionStrategy` `service-set-stability-portfolio-v1`) | Cách giữ tối đa 100 ứng viên mỗi xe (`CandidatePortfolioRetainer`). Luôn giữ phương án giữ tuyến. Xét theo số khách mới, từ nhiều đến ít: trước hết giữ ứng viên đứng đầu thứ hạng (rẻ nhất) của mỗi tập khách phục vụ, rồi ứng viên ít làm đổi tuyến và lịch cũ nhất của mỗi tập, rồi lấp chỗ còn lại theo thứ hạng. Cách giữ cũ (`legacy-accepted-count-cost-slack`) đã bỏ ở bước 2.13b. |
| Bằng chứng thực thi solver (`executionEvidence`) | Bản ghi chi tiết từng lần chọn phương án, trước đây gửi kèm quyết định khi `emitSolverExecutionEvidence` là `true`. Đã bỏ ở bước 2.12; các lần chạy cuối không bật nó. |
| `DispatchDecision(Result)`, `SelectionApplier`, `DecisionFailureCodes` | Kết quả một lần quyết định; lớp kiểm và áp phương án đã chọn lên trạng thái; mã lỗi của một lần quyết định. Ở `ebca912` chúng tên là `RollingCostDecision(Result)`, `RollingCostPolicy`, `RollingCostFailureCodes` (đổi tên ở bước 3.09). `RollingCostWitness` và `RollingCostReasonCodes` vẫn giữ tên cũ. Tên cũ có từ chính sách B1 `rolling-cost` đã bỏ (mục 10); nay các kiểu này thuộc chính sách duy nhất còn lại, C1 cost-first. |
| `DispatchStep` | Phần điều phối của một quyết định (`RideBound.Algorithms/Dispatch/`), theo thứ tự: chính sách quyết định, kiểm cam kết lần nữa, ghi vi phạm do giao thông vào sổ sự cố (`ExogenousServiceQualityBreachBridge`, cùng thư mục), xếp lớp Shadow cho khách vừa có lời hứa đầu. Lỗi đầu tiên dừng bước; Runner trả `INTERNAL_ERROR` và dừng phiên. |
| `wp7-medium-…`, `fleetpy-manhattan-v1-medium-wp7-physical-closure` | Chuỗi định danh cũ trong giao thức. Giữ nguyên để so frame với mọi transcript đã lưu. |

---

## 10. Khóa và dòng lệnh đã bỏ

Mọi tính năng đã bỏ đều báo lỗi rõ, không bao giờ âm thầm đổi nghĩa. Thông báo luôn kèm
`removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)`.

**Runner (exit 64):**
- Dòng lệnh: không tham số, `--mode online`, `--mode conformance`, `--mode commitment` thiếu `--wp4-config` hoặc `--solver-seed-source`,
  và mọi dạng khác dòng lệnh ở mục 7. `--maximum-line-bytes` ngoài khoảng 1048576..1073741824 cũng exit 64.
- File WP4 có `policyId` hoặc khối sau:

| `policyId` / khối | Tính năng |
|---|---|
| `least-commitment-consensus`, khối `multiplePlan` | B5, nhiều kế hoạch |
| `rolling-cost` | B1 |
| `rolling-penalty` | B2 |
| `fixed-freeze-horizon`, khối `fixedFreeze` | B3 |
| `no-reassignment-repair`, khối `repair` | B4 |
| `commit-soft-hard-hybrid`, khối `warningProfiles` | C2 |

- File WP4 thiếu `selectionObjectiveOrder` hoặc có giá trị khác `cost-first` (thứ tự stability-first, bỏ ở bước 2.11).
- File WP4 có `emitSolverExecutionEvidence: true` hoặc có khóa `solverExecutionEvidenceProfile` (bằng chứng thực thi solver,
  bỏ ở bước 2.12). `emitSolverExecutionEvidence: false`, hoặc không có khóa này, vẫn chạy như cũ.
- Tùy chọn sinh ứng viên và chọn phương án (bỏ ở các bước 2.13a–2.13f):
  - `candidateGeneration.scheduleStrategy` `origin-hold-relocated-wait` (lịch origin-hold). Khóa vẫn bắt buộc và chỉ nhận
    `earliest-feasible`.
  - `candidateGeneration.retentionStrategy` `legacy-accepted-count-cost-slack`, hoặc thiếu khóa này (cách giữ ứng viên cũ;
    thiếu khóa từng chọn cách đó).
  - `candidateGeneration.exactSmallMode` `true` (chế độ exact-small). Khóa vẫn bắt buộc và chỉ nhận `false`.
  - Các khóa `candidateGeneration.stopServiceDurationMs` (thời gian dừng của điểm chèn),
    `candidateGeneration.waitingRiderRelocation.maximumRidersPerVehicle` (trần số khách dời của G2) và
    `solverExecution.skipConstantObjectiveLevels` (bỏ qua mức mục tiêu hằng), với mọi giá trị, kể cả `0` và `false`.
- Mức tổng hợp Maximum (bỏ ở bước 2.13g) không có khóa riêng. Các dạng cấu hình từng dùng nó (thứ tự stability-first,
  chính sách C2 `commit-soft-hard-hybrid`) đã bị từ chối từ trước, như ở trên.
- Ba khóa cam kết của file WP4 (bỏ ở các bước 2.14b và 2.14g), mỗi khóa thiếu hoặc có giá trị cũ:
  - `initialPromiseTrigger` `initial-acceptance`, hoặc thiếu khóa (lời hứa đầu mở lúc chấp nhận);
  - `lateBoarding` `reject`, hoặc thiếu khóa (từ chối khách lên xe trễ);
  - `commitmentRecovery` `fail-closed` hoặc `forced-reference-v1`, hoặc thiếu khóa (các chế độ phục hồi).
    `no-worse-than-reference-v1` vẫn được nhận và không làm gì.

  Ba phép kiểm này chạy sau mọi phép kiểm khác của file WP4, nên file sai ở chỗ khác vẫn nhận đúng thông báo cũ.
- File commitment (thông báo bắt đầu bằng `Invalid commitment policy configuration:`), bỏ ở các bước 2.14a–2.14g:
  - `budgetBasis` `customerVisible` (cơ sở ngân sách khách thấy). Khóa vẫn bắt buộc và chỉ nhận `decisionInduced`;
    giá trị lạ khác vẫn báo `Unknown commitment budgetBasis.`.
  - Các khóa `freezeHorizonMs` và `freezeHorizonLocks` (freeze horizon), `dropEtaRevisionLimitMs` (trần sửa mỗi quyết định),
    `dropEtaDeadlineTwoSided` (hạn chót hai phía) và `dropEtaDeadlineSlackMs` (hạn chót ms cố định), với mọi giá trị,
    kể cả `0`, `[]` và `false`.
  - `dropEtaDeadlineRelaxed: false`, hoặc hạn chót `dropEtaDeadlineSlackPermille` không có `dropEtaDeadlineRelaxed`
    (rào chặt).

  Các phép kiểm này chạy sau mọi phép kiểm khác của chính sách đó, trừ hai phép. `customerVisible` được kiểm ngay chỗ đọc
  `budgetBasis`. `dropEtaDeadlineSlackMs` được kiểm sớm hơn: ngay sau phép kiểm khóa lạ, khóa bắt buộc, `limits`,
  `materialRevisionRule`, kiểu của các khóa boolean và phần nghìn, và phép kiểm Shadow. Vì vậy một hạn chót ms cố định có
  `dropEtaDeadlineRelaxed: true` nhận câu chỉ dẫn, không nhận thông báo
  `'dropEtaDeadlineRelaxed' requires 'dropEtaDeadlineSlackPermille'.`.

  Hai phép kiểm còn giữ nay có thông báo không nêu khóa đã bỏ (vẫn exit 64):
  `'waitingRiderRelocation' requires 'finalConfirmationLocks' without 'vehicle'.` (trước đây thêm
  `and 'freezeHorizonLocks'`) và `'dropEtaDeadlineRelaxed' requires 'dropEtaDeadlineSlackPermille'.` (trước đây
  `requires 'dropEtaDeadlineSlackMs' or 'dropEtaDeadlineSlackPermille'`).
- Ngoại lệ, không phải exit 64 và không kèm câu chỉ dẫn: thông điệp `restore` với checkpoint có mục sửa lời hứa mang
  `budgetBasis` `customerVisible`, có vi phạm loại `forcedReference`/`forcedNoWorse`, hoặc có trạng thái với mục
  `planPool` (B5) bị từ chối (`Unknown budget basis.`, `Unknown commitment breach kind.`,
  `Checkpoint online state is not an exact canonical domain state.`). Runner hiện nay không ghi ra các giá trị đó; chỉ
  checkpoint do Runner trước 2.05, 2.14a hoặc 2.14g ghi mới có thể chứa chúng.
- Biến môi trường `RIDEBOUND_STATE_HASH`, `RIDEBOUND_HASH_TIMING`, `RIDEBOUND_CANONICAL_SELFCHECK` bị bỏ qua.

**Bộ mô phỏng (lỗi, exit khác 0):**
- `run.js`: `--ref`, `--cmp-ref`, `--hashes`, `--truncate`, `--repeat`, `--order`, `--engine`, `--dump`, `--dump-arms`, `--pi`,
  `--fence-tol`, `--audit`, `--score oracle`, `--score random`.
- Nhánh `NC`, `OC`, `RR`, `FZ`, `NCE`, `OCE`, `SHNC`; tùy chọn engine `shadowArms`, `pairs`, `trace`, `tieMode`, `reoptTie`,
  `fenceTolMs`, `audit`, `reopt.g1`, `reopt.dr`, `reopt.g2PenaltyMs`, `reopt.g2MaxSwitches`, khóa `ratchetFloor`/`none`.
- Kịch bản: khoảng cách hình học (haversine, euclidean, manhattan), ma trận viết trực tiếp, hồ sơ theo vùng, bao đóng `shortest-path`.
- `gen17.js`: `--set`, `--work`, `--nodetag`.

**Adapter (Python):** tính năng đã bỏ gây lỗi rõ với cùng câu chỉ dẫn:
- Cấu hình operator: bản sao có tiền tố `op_` của một khóa `ridebound_*` mà adapter đọc, ví dụ
  `op_ridebound_timeout_seconds` (`RBWP7_SETTING_PREFIX_REMOVED`; các khóa `op_` của FleetPy như `op_min_wait_time` vẫn
  dùng như cũ); `ridebound_complete_travel_snapshot_maximum_nodes` bằng 0 hoặc thiếu, tức chế độ snapshot thưa
  (`RBWP7_SPARSE_TRAVEL_SNAPSHOT_REMOVED`).
- File WP4 của job thiếu `lateBoarding` hoặc có `lateBoarding: "reject"` (`RBWP7_WP4_LATE_BOARDING_REJECT_REMOVED`, bước 4.LB),
  trước khi Runner khởi động. Giá trị lạ khác vẫn báo `RBWP7_WP4_LATE_BOARDING_INVALID`.
- `run_job.py`: driver không phải driver full-node (`routingEngine` khác `complete-graph-direct-v1` hoặc `transcriptCompression`
  khác `gzip-v1`); driver thiếu một trong `expectedRequestCount`, `expectedVehicleCount`, `expectedNodeCount`,
  `expectedDirectedArcCount`. Cả hai bị từ chối trước khi tạo thư mục ra.
- `riders.py`: thư mục job có `transcript-00.ndjson` không nén (exit 1); biến `RB_PREFER_EVIDENCE=1` (exit 2).
- Đã xóa hẳn, không còn lệnh nào gọi tới: khởi động lại/khôi phục Runner giữa job (WP7), các script WP9/WP13/WP14,
  các adapter RidePy và AMoD cũ, chuỗi chạy cũ của các lần chạy cuối (launcher, các world wrapper,
  `actual_fleetpy_medium_preflight.py`; bước 4.07b, thay bằng `run_job.py`).

Đầu vào khác các lần chạy cuối cũng bị từ chối, bằng thông báo riêng:
- `make_job_configs.py`: một dòng `refused: <file>: <problem>`, exit 1, trước khi ghi bất cứ file nào (luật trong `--help`).
- `riders.py`: bản ghi transcript có `frameLengthBytes` khác độ dài frame; khách có `lifecycle` không phải chuỗi.
  `riders.py` dừng ở job đầu tiên bị từ chối; file `--out` có thể đã được tạo một phần. Xóa nó trước khi chạy lại,
  vì `riders.py` không ghi đè file có sẵn.

Adapter không còn các phép từ chối về nguồn gốc mà đường chạy cuối không bao giờ gặp (bước 4.13): danh sách file biên nhận
rỗng, một file đổi giữa job, thư mục kịch bản không phải thư mục hoặc không có file; dạng hash checkpoint chỉ còn do client
Runner kiểm. Biên nhận và các hash vẫn được ghi (mục 5.6).

---

## 11. Giới hạn đã biết

Hành vi của `ebca912` được giữ nguyên, kể cả hai đường lỗi sau. Cả hai được ghim bằng byte trong bộ kiểm.
Các lần chạy cuối không gặp chúng.

1. **Xác nhận đặt xe và lên xe trong cùng một lô sự kiện** (cấu hình có Shadow): Runner báo `INTERNAL_ERROR`, exit 3.
   Lời hứa mở lúc xác nhận, nhưng khách đã lên xe nên tuyến không còn điểm đón để chấm điểm Shadow.
2. **Khách lên xe sớm hơn lời hứa nhiều hơn phần còn lại của chuyến:** giờ trả dự chiếu sớm hơn giờ đón đã công bố,
   phương án giữ tuyến bị loại, phiên dừng với `INTERNAL_ERROR`.

Một khác biệt so với `ebca912` đã khai ở bước 2.14g: khi cổng cam kết loại cả phương án giữ tuyến của một xe, quyết định lỗi
và phiên dừng với `INTERNAL_ERROR`. Ở `ebca912`, với `commitmentRecovery` `no-worse-than-reference-v1` (giá trị của file cuối),
phục hồi forced-reference có thể giữ phương án đó và ghi một vi phạm (mục 9). Trên mọi đầu vào đã kiểm ở máy kiểm (675 phép so
stdout với Runner `ebca912`, 100 lần chạy trên các file cấu hình cuối), phục hồi đó chạy 55 lần, đều trong các lần chạy INS
vốn đã lỗi, và không lần nào giữ phương án, nên kết quả không đổi. Các transcript thật chưa được phát lại qua Runner sau
bước này (V1, mục 6).

Giới hạn khác:
- Dự đoán chế độ adapter là kết quả đã niêm phong; bộ sinh không công khai.
- Các lần chạy thật (mục 5.6) không chạy lại được chỉ từ repo: đầu vào của các job nằm ngoài repo, và đầu vào Shadow của chúng
  do một bộ sinh không công khai ghi.
- Một phần §9 (từng khách của 584, 2 trong 11 ô Manhattan, các lần chạy lại phía C#) cần dữ liệu hoặc mã ở commit `ebca912`.
- Các dòng R6 chỉ dựng lại từ dòng đã ghi, không chạy lại được từ repo.
- `replay.py compare` cần `transcript-00.ndjson[.gz]` ở cả hai phía. Nếu thiếu: trên hai thư mục job, lệnh in
  `commit-free identical 0/0` và exit 1; trên hai thư mục gốc, một job có ở cả hai gốc mà chạy với `--transcript drop`
  (mọi ô trừ ô gate) làm lệnh dừng với `TypeError`. Trên hai thư mục gốc, lệnh chỉ so các job có `summary.json` ở cả
  hai gốc và không báo job chỉ có ở một gốc; xem số `k/k` ở dòng cuối.
- Lỗi của file `--wp4-config` (không đọc được hoặc nội dung sai) cũng bắt đầu bằng
  `Invalid commitment policy configuration:`, như ở `ebca912`; thông báo không phải lúc nào cũng nói lỗi thuộc file nào.

---

## 12. Sai lệch đã biết trong báo cáo

Mã in giá trị đúng. Báo cáo có các chỗ sau cần sửa:

| Chỗ | Báo cáo in | Giá trị đúng | Lý do |
|---|---|---|---|
| §6, bảng G2, gánh nặng của U | 9,1 | 9,0 (9,049) | làm tròn hai lần (9,049 → 9,05 → 9,1) |
| §6, bảng G2, gánh nặng của SHF71 | 12,0 | 11,9 (11,950) | làm tròn hai lần |
| §7, bảng đầu, khách trễ cả ngày, Đệm | 22,2 % | 22,1 % (22,146 %) | làm tròn hai lần |
| §6, "phục vụ −2,8" | không nói so với rào nào | so với rào lỏng (RCE35 − EOHZ35 = −2,822; so với rào chặt là −1,33) | thiếu tên phép so |
| §6, "+20–91 %" | đặt cạnh bảng 60 ngày | đến từ nghiên cứu R6 (`policy_curves.js`) | ghi nguồn |
| §9, cặp vùng Via | 6/6 cặp, 2.407 khách | 12/12 cặp, 4.878 khách, sau khi chạy nốt 6 job còn thiếu. Repo chỉ có khách thật của 6 cặp đầu, nên tái lập được 6/6 và 2.407 | số cũ |
| §9, phần "vì sao không chạy được" | trần dòng 64 MiB, hash lại tốn thời gian, hết bộ nhớ khi chép checkpoint | đã sửa: dòng tới 1 GiB, `auditHash light-v1`, checkpoint dạng stream | lỗi thời |
| §9, phép so cuối | chưa có | 360 job F14/F16: F14 166/168 cặp, 57.954/58.042 khách; F16 187/192 cặp, 67.917/67.922 khách (mục 5.4) | thiếu |
