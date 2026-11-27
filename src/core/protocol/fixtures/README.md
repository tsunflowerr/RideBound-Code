# Thông điệp mẫu của giao thức

Thư mục này chứa các thông điệp mẫu của giao thức v1, đúng và sai, cho test C#. Mọi file là UTF-8 không BOM, dòng kết
thúc bằng LF. `src/.gitattributes` đánh dấu thư mục này `-text`, nên git không bao giờ đổi byte của chúng.

Test tìm file qua `FixtureLoader` (`src/core/tests/RideBound.Contracts.Tests/Fixtures/`): bắt đầu từ thư mục chứa
`RideBound.slnx`, rồi vào `protocol/fixtures`. Vì vậy test không phụ thuộc thư mục hiện hành hay dấu phân cách đường
dẫn của hệ điều hành.

Giá trị mong đợi (byte chuẩn hóa, hash) được ghi sẵn trong file. Test chỉ so, không bao giờ tự ghi lại chúng.

| Thư mục | Nội dung | Test đọc |
|---|---|---|
| `canonical/` | một phong bì có khóa không theo thứ tự, và byte chuẩn hóa mong đợi (hex) | `CanonicalJsonTests` |
| `compatibility/` | phiên bản schema và trường lạ: 1 đúng, 3 sai | `ProtocolVersionCompatibilityTests` |
| `golden/required/` | 1 kịch bản (09), có `input.json` và `metadata.json` | `GoldenFixtureTests`, `RunnerSessionTests` |
| `harness/` | một file nhỏ để thử `FixtureLoader` | `FixtureLoaderTests` |
| `hash/` | các cặp đầu vào và SHA-256 mong đợi của manifest và trạng thái | `GoldenFixtureTests` |
| `hello/` | `hello`, `helloAck` (cả dạng hạ cấp), một capability lạ | `HelloMessageTests`, `SchemaAssetTests`, `CapabilityNegotiatorTests`, `InitializeRunValidatorTests`, `Wp4RunnerIntegrationTests` |
| `initialize/` | `initializeRun`, `initialized`, một manifest có `roundingRule` sai | `InitializeRunMessageTests`, `ProtocolHashTests`, `SchemaAssetTests`, `InitializeRunValidatorTests`, `Wp4RunnerIntegrationTests` |
| `protocol/` | phong bì đúng, loại thông điệp lạ, `eventSeq` đặt ở phong bì | `ProtocolEnvelopeTests` |
| `wp2/` | lô sự kiện khởi đầu và lô của epoch thứ hai, một xe có tuyến thiếu `mutableSuffix` | `OnlineEventContractTests`, `OnlineEventMapperTests`, `Wp4RunnerIntegrationTests` |

`metadata.json` của kịch bản 09 (sự kiện lặp, xử lý một lần) ghi `supportLevel` = `runner-executable`: Runner chạy kịch
bản này trong test.

`GoldenFixtureTests` kiểm đúng các con số này. Tên `wp2` là tên cũ (gói việc 2); nó được giữ vì test đọc đúng đường
dẫn này.
