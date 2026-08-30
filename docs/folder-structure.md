# 📁 TOEIC Space Backend - Folder Structure

Tài liệu này giải thích ngắn gọn cấu trúc thư mục chính của TOEIC Space Backend.

## Cấu trúc tổng thể

```text
src/
├── ApiGateways/
├── BuildingBlocks/
├── Services/
└── WebApps/

tests/
docs/

ToeicSpace.slnx
docker-compose.yml
.env.example
.gitignore
README.md
```

## `ApiGateways/`

Chứa API Gateway.

```text
ApiGateways/
└── ToeicSpace.ApiGateway/
```

Dùng để nhận request từ client và route đến đúng microservice.

---

## `BuildingBlocks/`

Chứa code kỹ thuật dùng chung.

```text
BuildingBlocks/
├── ToeicSpace.BuildingBlocks/
└── ToeicSpace.BuildingBlocks.Messaging/
```

Ví dụ:

```text
CQRS
Behaviors
Exceptions
Pagination
Integration Events
MassTransit
```

Không đặt business logic riêng của service tại đây.

---

## `Services/`

Chứa các microservice nghiệp vụ.

```text
Services/
├── Identity/
├── Content/
├── Classroom/
├── Assessment/
└── Payment/
```

### Service đơn giản

```text
<Service>/
└── <Service>.API/
```

Dùng khi service nhỏ, ít business rule và có thể tổ chức trực tiếp theo feature.

### Service phức tạp

```text
<Service>/
├── <Service>.API/
├── <Service>.Application/
├── <Service>.Domain/
└── <Service>.Infrastructure/
```

Ý nghĩa:

```text
API
→ Endpoint, HTTP, startup, DI

Application
→ Use case, CQRS, validation, DTO

Domain
→ Entity, Value Object, business rule

Infrastructure
→ Database, EF Core, external service, messaging
```

---

## `WebApps/`

Chứa web application nếu có.

```text
WebApps/
└── ToeicSpace.Web/
```

WebApp giao tiếp với backend qua API Gateway.

---

## `tests/`

Chứa các project test.

```text
tests/
├── UnitTests/
└── IntegrationTests/
```

---

## `docs/`

Chứa tài liệu dự án.

```text
docs/
├── architecture.md
├── folder-structure.md
├── coding-conventions.md
├── DEVELOP_GUILINE.md
└── git.md
```

---

## File root

```text
ToeicSpace.slnx
→ Solution của backend

docker-compose.yml
→ Chạy các container local

.env.example
→ Mẫu biến môi trường

.gitignore
→ File/folder không commit

README.md
→ Tổng quan project
```

## Nguyên tắc

```text
Service nhỏ
→ 1 API project

Service phức tạp
→ API + Application + Domain + Infrastructure

Code dùng chung
→ BuildingBlocks

Điểm vào hệ thống
→ API Gateway
```
