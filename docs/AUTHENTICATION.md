# 🔐 Xác thực & bảo mật dữ liệu

Tài liệu mô tả cách frontend đăng nhập, gọi API ngân hàng đề và các lớp bảo vệ dữ liệu (đặc biệt là **đáp án**).

---

## 1. Luồng tổng quan

```text
Browser ──(same origin)──▶ Vite dev proxy / nginx ──▶ API Gateway :5050 ──▶ Identity   /identity/*
                                                                        └─▶ Assessment /assessment/*
```

| Bước | Endpoint | Kết quả |
|---|---|---|
| Đăng nhập | `POST /identity/api/auth/login` | Access token (body) + refresh cookie |
| Gọi API | `Authorization: Bearer <access token>` | Assessment kiểm tra chữ ký, hạn, vai trò |
| Hết hạn access token | `POST /identity/api/auth/refresh` + header `X-CSRF-Protection: 1` | Token mới, cookie được xoay vòng |
| Đăng xuất | `POST /identity/api/auth/logout` + header `X-CSRF-Protection: 1` | Thu hồi refresh token, xoá cookie |
| Hồ sơ | `GET /identity/api/auth/me` | Thông tin tài khoản, đọc lại từ DB |

Load đề cho trang quản trị:

| Màn hình | API |
|---|---|
| Danh sách đề (lọc trạng thái, tìm kiếm) | `GET /assessment/api/v1/tests?Status=&Search=&Page=&PageSize=` |
| Câu hỏi theo Part của một đề | `GET /assessment/api/v1/questions?TestId={id}&Part=Part5&Page=&PageSize=` |
| Câu hỏi theo Part trong ngân hàng | `GET /assessment/api/v1/questions?Part=Part5` |
| Chi tiết câu hỏi (có đáp án) | `GET /assessment/api/v1/questions/{id}` |
| Tổng quan ngân hàng đề (số liệu theo Part, trạng thái, độ khó) | `GET /assessment/api/v1/bank/overview` |

Server lọc theo `Part` trong truy vấn SQL: client chỉ nhận câu hỏi của Part đang xem, có phân trang (tối đa 100/trang).

---

## 2. Token

| | Access token | Refresh token |
|---|---|---|
| Dạng | JWT **ES256**, `typ: at+jwt`, có `kid` | 256 bit ngẫu nhiên (opaque) |
| Thời hạn | 10 phút | 7 ngày tuyệt đối, hết hạn sau 24 giờ không dùng |
| Lưu ở client | **Chỉ trong bộ nhớ** (Zustand), không localStorage | Cookie `__Secure-ts_rt`: `HttpOnly; Secure; SameSite=Strict; Path=/identity/api/auth` |
| Lưu ở server | Không | Chỉ **SHA-256 hash** trong `UserTokens` |
| Claim | `sub`, `role`, `jti`, `iss`, `aud`, `iat`, `nbf`, `exp` (không có email/tên) | — |

**Vì sao ES256 thay cho HS256:** chỉ Identity giữ private key. Assessment và các service sau này chỉ có public key, nên lộ cấu hình của một service không cho phép giả mạo token admin. Bên kiểm tra chỉ chấp nhận `ES256` + `at+jwt`, đúng `iss`/`aud`, còn hạn (lệch tối đa 30 giây); token HS256 ký bằng public key (tấn công algorithm confusion) bị từ chối.

**Xoay vòng refresh token:** mỗi token chỉ dùng được một lần. Nếu một token đã dùng bị gửi lại (dấu hiệu bị đánh cắp), **mọi phiên** của tài khoản bị thu hồi. Hai request refresh cùng lúc chỉ một request thắng (cập nhật nguyên tử). Frontend dùng Web Locks để các tab refresh lần lượt.

**Giới hạn phiên:** tối đa 5 phiên hoạt động/tài khoản; phiên cũ nhất bị thu hồi. Đổi vai trò hoặc khoá tài khoản có hiệu lực chậm nhất ở lần refresh kế tiếp (≤ 10 phút).

---

## 3. Các lớp bảo vệ

| Lớp | Biện pháp |
|---|---|
| Đăng nhập | PBKDF2-SHA512 210.000 vòng, so sánh constant-time; email không tồn tại vẫn tốn cùng thời gian; thông báo lỗi chung |
| Brute force | Khoá email 15 phút sau 5 lần sai (Redis, key là hash email, tính cả email không tồn tại); 5 lần/phút mỗi IP cho mỗi endpoint đăng nhập; gateway 20 lần/phút mỗi IP |
| CSRF | Cookie `SameSite=Strict` + header bắt buộc `X-CSRF-Protection` cho refresh/logout |
| XSS | Token không nằm trong storage; CSP chặt ở nginx (`script-src 'self'`, `connect-src 'self'`, `frame-ancestors 'none'`) |
| Phân quyền | Mặc định mọi endpoint yêu cầu đăng nhập (fallback policy). Ngân hàng câu hỏi, đáp án, bản nháp: chỉ `Admin`/`Teacher`. Danh sách câu hỏi không bao giờ chứa đáp án |
| Chống cào dữ liệu | Assessment: 240 request/phút mỗi tài khoản, 120/phút mỗi IP ẩn danh; gateway: 300/phút mỗi IP |
| Kiểm toán | Mỗi request của Admin/Teacher được ghi log: user, IP, method, path, status (không ghi query string) |
| HTTP | `Cache-Control: no-store`, `nosniff`, `X-Frame-Options: DENY`, CSP cho API, ẩn header `Server`, body tối đa 2 MB |
| CORS | Chỉ origin trong `Cors:AllowedOrigins` (dev: `localhost:5173`); frontend dùng cùng origin nên không cần CORS |
| IP thật | `X-Forwarded-For` chỉ được tin từ gateway (loopback hoặc `ForwardedHeaders:KnownNetworks`); gateway ghi đè header do client gửi |

---

## 4. Cài đặt local

### 4.1. Sinh cặp khoá (một lần cho mỗi máy)

```bash
dotnet run scripts/GenerateJwtKeys.cs -- --user-secrets
```

Lệnh lưu `Jwt:PrivateKey` vào user-secrets của Identity và `Jwt:PublicKey` vào user-secrets của Assessment. Service không khởi động nếu thiếu khoá.

### 4.2. Tạo tài khoản quản trị đầu tiên

Đăng ký công khai chỉ tạo học viên. Admin đầu tiên được tạo khi Identity khởi động:

```bash
dotnet user-secrets set --id toeicspace-identity-local "BootstrapAdmin:Email" "you@example.com"
dotnet user-secrets set --id toeicspace-identity-local "BootstrapAdmin:Password" "<mật khẩu ≥ 12 ký tự>"
dotnet run --project src/Services/Identity/ToeicSpace.Identity.API
# Sau khi thấy log "Created bootstrap administrator", xoá mật khẩu khỏi cấu hình:
dotnet user-secrets remove --id toeicspace-identity-local "BootstrapAdmin:Password"
```

Mật khẩu cần ít nhất 12 ký tự và 3 trong 4 nhóm: chữ thường, chữ hoa, số, ký tự đặc biệt. Tài khoản đã tồn tại sẽ không bị sửa.

### 4.3. Chạy

```bash
dotnet run --project src/Services/Identity/ToeicSpace.Identity.API
dotnet run --project src/Services/Assessment/ToeicSpace.Assessment.API
dotnet run --project src/ApiGateways/ToeicSpace.ApiGateway
# toeic-space-fe
npm run dev          # mở http://localhost:5173/login
```

Smoke test Assessment (ký token ES256 bằng khoá trong user-secrets): `python scripts/test_assessment_api.py`.

---

## 5. Docker / production

`.env`:

```bash
dotnet run scripts/GenerateJwtKeys.cs      # in ra JWT_PRIVATE_KEY / JWT_PUBLIC_KEY
```

| Biến | Service |
|---|---|
| `JWT_PRIVATE_KEY` | **Chỉ** `identity-api` |
| `JWT_PUBLIC_KEY` | `assessment-api` (và các service kiểm tra token sau này) |
| `TRUSTED_PROXY_NETWORK` | Dải mạng của gateway / reverse proxy |
| `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD` | Lần chạy đầu, sau đó để trống |

Checklist trước khi public:

- [ ] `ASPNETCORE_ENVIRONMENT=Production` (tắt Swagger, bật HSTS ở gateway).
- [ ] Chạy sau HTTPS; frontend và API **cùng domain** (nginx proxy `/identity/`, `/assessment/` tới gateway, xem `toeic-space-fe/nginx.conf`).
- [ ] Không publish cổng của `identity-api`/`assessment-api` ra ngoài, chỉ gateway.
- [ ] Nếu có load balancer trước gateway, thêm dải mạng của nó vào `ForwardedHeaders:KnownNetworks` của gateway.
- [ ] Xoay khoá JWT: sinh cặp mới, cập nhật cả hai biến, restart. Mọi người dùng phải đăng nhập lại.
