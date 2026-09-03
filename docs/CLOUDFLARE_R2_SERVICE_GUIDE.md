# Hướng dẫn sử dụng Cloudflare R2 cho các Service

Project đã có module dùng chung:

```text
src/BuildingBlocks/ToeicSpace.BuildingBlocks.Storage
```

Các service sau này cần upload file lên Cloudflare R2 chỉ cần làm các bước sau.

## 1. Add reference tới Storage Building Block

Ví dụ với `Identity.API`:

```bash
dotnet add \
  src/Services/Identity/ToeicSpace.Identity.API/ToeicSpace.Identity.API.csproj \
  reference \
  src/BuildingBlocks/ToeicSpace.BuildingBlocks.Storage/ToeicSpace.BuildingBlocks.Storage.csproj
```

Ví dụ với `Assessment.API`:

```bash
dotnet add \
  src/Services/Assessment/ToeicSpace.Assessment.API/ToeicSpace.Assessment.API.csproj \
  reference \
  src/BuildingBlocks/ToeicSpace.BuildingBlocks.Storage/ToeicSpace.BuildingBlocks.Storage.csproj
```

## 2. Đăng ký R2 trong `Program.cs`

Thêm:

```csharp
using ToeicSpace.BuildingBlocks.Storage.Extensions;
```

Sau đó:

```csharp
builder.Services.AddCloudflareR2Storage(builder.Configuration);
```

Service có thể inject:

```csharp
IObjectStorageService
```

mà không cần làm việc trực tiếp với `AWSSDK.S3`.

## 3. Thêm biến môi trường vào Docker Compose

Service nào sử dụng R2 thì thêm:

```yaml
environment:
  CloudflareR2__Endpoint: ${R2_ENDPOINT}
  CloudflareR2__AccessKey: ${R2_ACCESS_KEY}
  CloudflareR2__SecretKey: ${R2_SECRET_KEY}
  CloudflareR2__BucketName: ${R2_BUCKET_NAME}
```

Ví dụ:

```yaml
identity-api:
  environment:
    ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT}
    ASPNETCORE_HTTP_PORTS: 8080

    CloudflareR2__Endpoint: ${R2_ENDPOINT}
    CloudflareR2__AccessKey: ${R2_ACCESS_KEY}
    CloudflareR2__SecretKey: ${R2_SECRET_KEY}
    CloudflareR2__BucketName: ${R2_BUCKET_NAME}
```

## 4. Khai báo R2 trong `.env`

Tại root project:

```env
R2_ENDPOINT=https://<ACCOUNT_ID>.r2.cloudflarestorage.com
R2_ACCESS_KEY=
R2_SECRET_KEY=
R2_BUCKET_NAME=toeic-space-media
```

Không commit `.env` chứa secret lên Git.

Trong `.env.example` chỉ để:

```env
R2_ENDPOINT=
R2_ACCESS_KEY=
R2_SECRET_KEY=
R2_BUCKET_NAME=toeic-space-media
```

## 5. Quy tắc sử dụng

- Không cài `AWSSDK.S3` riêng trong từng service.
- Không viết lại logic Cloudflare R2 trong từng service.
- Không hard-code Access Key hoặc Secret Key.
- Service chỉ giao tiếp thông qua `IObjectStorageService`.
- Chỉ service nào thực sự sử dụng R2 mới cần cấu hình environment R2.

Luồng chung:

```text
Service
   ↓
IObjectStorageService
   ↓
ToeicSpace.BuildingBlocks.Storage
   ↓
Cloudflare R2
```
