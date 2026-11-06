# Contract fixtures

Các fixture trong thư mục này là UTF-8 không BOM và dùng LF trong source.
`RideBound.Contracts.Tests` tìm fixture từ repository root, không phụ thuộc
current working directory hoặc path separator của hệ điều hành.

Mỗi fixture protocol sau smoke harness phải khai báo support level theo
`docs/06-event-contract-and-determinism.md`:

- `schema-only`;
- `runner-executable`;
- `future-behavior`.

Golden canonical bytes/hash là source-controlled oracle. Test không được tự cập
nhật expected vector khi chạy.

Thư mục hiện có:

- `compatibility/`: hành vi version/error chính xác;
- `hello/`: capability được cung cấp và kết quả lựa chọn;
- `initialize/`: manifest bất biến và danh tính state ban đầu;
- `protocol/`: boundary envelope chung;
- `canonical/`: vector canonical byte chính xác;
- `hash/`: vector manifest/state/decision SHA-256 có input và expected hash;
- `golden/required/`: đúng 10 scenario bắt buộc; 9 scenario
  `future-behavior`, riêng duplicate là `runner-executable`;
- `runner/`: full tiny NDJSON transcript, exact output và final decision hash;
- `wp2/`: typed bootstrap/epoch-two payload cùng invalid full-vehicle case;
  executable qua mapper/atomic reducer nhưng chưa phải B1 decision;
- `harness/`: dữ liệu smoke cho fixture loader.

Q1 chỉ tính `golden/required/09-duplicate-event-idempotent` và transcript trong
`runner/` là behavior executable. Các scenario accept/reject, budget, incident
và checkpoint chỉ được nâng behavior support tại đúng WP2/WP3 ticket tương ứng.
Việc thay `fixtureIntent` bằng typed payload không tự nâng accept/reject fixture
thành runner-executable trước `RB-WP2-010`.
