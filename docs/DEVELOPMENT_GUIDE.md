# 📕 DEVELOP_GUIDE

> Chạy các lệnh tại thư mục gốc chứa `ToeicSpace.slnx` và `docker-compose.yml`.

## 1. Lần đầu clone project

```bash
git clone <repository-url>
cd toeic-space-be

cp .env.example .env

dotnet restore ToeicSpace.slnx
dotnet build ToeicSpace.slnx

docker compose up -d --build
docker compose ps
```

---

## 2. Khi viết code và chạy code hằng ngày

### Chạy toàn bộ bằng Docker

```bash
docker compose up -d
```

### Chạy một API trực tiếp với Hot Reload

```bash
dotnet watch --project <api-project-path> run
```

Ví dụ:

```bash
dotnet watch --project src/Services/Identity/ToeicSpace.Identity.API/ToeicSpace.Identity.API.csproj run
```

### Chạy bình thường

```bash
dotnet run --project <api-project-path>
```

### Build để kiểm tra lỗi

```bash
dotnet build ToeicSpace.slnx
```
### Rebuild container sau khi sửa Dockerfile / dependency

```bash
docker compose up -d --build <service-name>
```

### Dừng môi trường local

```bash
docker compose down
```

---

## 3. Migration
### Add migration

```bash
dotnet ef migrations add <MigrationName> \
  --context <DbContextName> \
  --project <infrastructure-project> \
  --startup-project <api-project> \
  --output-dir Data/Migrations
```

Ví dụ:

```bash
dotnet ef migrations add InitialCreate \
  --context IdentityDbContext \
  --project src/Services/Identity/ToeicSpace.Identity.Infrastructure \
  --startup-project src/Services/Identity/ToeicSpace.Identity.API \
  --output-dir Data/Migrations
```

### Remove migration cuối cùng

```bash
dotnet ef migrations remove \
  --context <DbContextName> \
  --project <infrastructure-project> \
  --startup-project <api-project>
```

### Update database

```bash
dotnet ef database update \
  --context <DbContextName> \
  --project <infrastructure-project> \
  --startup-project <api-project>
```
