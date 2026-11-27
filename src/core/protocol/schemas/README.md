# Schema của giao thức

Thư mục `v1/` chứa 52 file JSON của giao thức v1 giữa adapter và Runner:

- 50 file `*.schema.json` (JSON Schema draft 2020-12): một file cho mỗi thông điệp, payload và kiểu dùng chung;
- `schema-inventory.json`: kiểu C# hoặc thông điệp nào ứng với schema nào;
- `compatibility-matrix.json`: Runner làm gì với phiên bản schema của bên gửi. Cùng major và minor thì nhận (patch nào
  cũng được); minor cao hơn thì từ chối thông điệp (`UNSUPPORTED_SCHEMA_MINOR`); major khác thì dừng phiên
  (`UNSUPPORTED_SCHEMA_MAJOR`).

Các file này thuộc giao thức v1. Ngoài những phần đã gỡ vì Runner và adapter không dùng (năm trường chỉ để ghi sổ của
vector cam kết, hai sự kiện `incidentOpened` / `incidentResolved`, hành động `commitmentBreachDeclared`, mã
`INCIDENT_OVERRIDE`, thông điệp `restore` và payload trả lời của nó), chúng được giữ nguyên từng byte. Vì vậy:

- `$id` của mỗi schema là một tên cố định (URL theo cách xếp thư mục cũ của repo), không phải nơi file nằm bây giờ.
  Mọi `$ref` là đường dẫn tương đối trong `v1/`.
- Các giá trị `fixtureDirectory` trong `schema-inventory.json` cũng là tên theo cách xếp cũ. Không test nào đọc chúng
  như đường dẫn.

Schema v1 chặt: phong bì chung (`protocol-envelope.schema.json`) và các payload đặt `additionalProperties: false`,
nên trường lạ bị từ chối; không schema nào nhận giá trị `null`.

Test kiểm các file này: `SchemaAssetTests` trong `src/core/tests/RideBound.Contracts.Tests/Protocol/`.
Thông điệp mẫu, đúng và sai, nằm ở `../fixtures/`.
