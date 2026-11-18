# ToeicSpace — Hướng dẫn triển khai Google Meet trên domain thật

> **Mục đích:** Hướng dẫn triển khai chức năng **giáo viên kết nối tài khoản Google cá nhân** và **tự động tạo Google Meet cho buổi học Online** trên môi trường có domain/HTTPS. Tài liệu dành cho việc triển khai Backend, Docker Compose và kiểm thử sau khi deploy. Không sử dụng `localhost` làm địa chỉ OAuth callback của môi trường triển khai.
>
> **Trạng thái:** Phần kết nối OAuth đã được thử thành công trong môi trường phát triển (`connected: true`). Việc tạo Meet thật bằng ClassSession Online và kiểm thử tích hợp Frontend sẽ được thực hiện sau khi có FE. Các ví dụ domain dưới đây **chỉ là placeholder**, không phải domain đã triển khai.

## 1. Kiến trúc và luồng tích hợp

```text
Teacher / Student browser
        │ HTTPS
        ▼
https://app.example.com                 (Frontend)
        │ HTTPS API requests
        ▼
https://api.example.com                 (YARP API Gateway)
        │ /classroom/api/v1/... → /api/v1/...
        ▼
Classroom API (Docker private network)
        ├── Classroom MySQL: Classes, ClassSessions, Google connections,
        │                    Meet provisioning state, Hangfire tables
        ├── Hangfire worker: provision Meet và retry/reconcile
        └── Google OAuth / Meet REST API (HTTPS outbound)
```

- Mỗi **Teacher tự kết nối Google account của mình**. Không sử dụng một tài khoản Google chung cho toàn trung tâm.
- Với `ClassSession.Mode = Online`, Backend xử lý tạo Meet space bằng token của Teacher được phân công.
- Session `Offline` không được tạo Meet; khi chuyển Online/Offline hoặc đổi giáo viên, Backend cập nhật liên kết và trạng thái provisioning theo nghiệp vụ đã triển khai.
- Token Google được xử lý ở Backend, refresh token lưu dạng mã hóa; FE không nhận token Google.
- Hangfire sử dụng **cùng Classroom MySQL** theo tài liệu triển khai hiện tại, với các bảng prefix `Hangfire`.

## 2. Quy ước URL trên production

Ví dụ (thay toàn bộ `example.com` bằng domain thuộc quyền kiểm soát của bạn):

| Thành phần | URL minh họa |
| --- | --- |
| Frontend | `https://app.example.com` |
| Public API Gateway (YARP) | `https://api.example.com` |
| Classroom API qua Gateway | `https://api.example.com/classroom/api/v1` |
| Google OAuth callback **đăng ký với Google** | `https://api.example.com/classroom/api/v1/teachers/me/google-meet/callback` |
| Classroom API nội bộ | `http://classroom-api:8080` *(kiểm tra cổng thực tế từ Compose)* |

**Cực kỳ quan trọng:** Giá trị `GoogleMeet__RedirectUri` phải giống **100%** URI được khai báo trong Google Cloud, bao gồm scheme `https`, domain, đường dẫn và dấu `/` cuối (nếu có). URI callback là URL **mà trình duyệt của Teacher có thể mở được**, không phải hostname Docker hay private IP.

**Kiểm tra YARP trước khi deploy:** Gateway phải match `/classroom/api/v1/...`, chuyển tiếp tới Classroom API đúng đường dẫn nội bộ `/api/v1/...`. Nếu route/path transform thực tế khác, phải dùng đường dẫn public đang hoạt động và đăng ký đúng với Google. Không giả định Gateway tự thêm/xóa prefix.

## 3. Google Cloud — chuẩn bị cho production

1. Mở [Google Cloud Console](https://console.cloud.google.com/) và chọn đúng project dành cho ToeicSpace. **Khuyến nghị tách Google Cloud project cho Development và Production** khi đưa hệ thống vào vận hành thật.
2. Tại **APIs & Services → Library**, bật **Google Meet REST API** (`meet.googleapis.com`).
3. Trong **Google Auth Platform → Branding**, kiểm tra tên ứng dụng `ToeicSpace`, email liên hệ, và chuẩn bị **Application home page**, **Privacy Policy URL** (cùng Terms of Service nếu áp dụng) chạy trên domain hợp lệ.
4. Trong **Audience**, chọn loại người dùng phù hợp. Với Teacher dùng Gmail cá nhân hoặc tài khoản ngoài một Google Workspace organization, sử dụng **External**.
5. Trong **Data Access**, chỉ khai báo các scopes Backend đang sử dụng:
   - `openid`
   - `https://www.googleapis.com/auth/userinfo.email`
   - `https://www.googleapis.com/auth/meetings.space.created`
6. Trong **Clients**, tạo hoặc chỉnh **OAuth 2.0 Client ID** loại **Web application**. Ở **Authorized redirect URIs**, thêm chính xác:

   ```text
   https://api.example.com/classroom/api/v1/teachers/me/google-meet/callback
   ```

7. Nếu ứng dụng còn trạng thái **Testing**, chỉ các Gmail trong **Test users** mới sử dụng được. Với External + scope Meet, refresh token cấp khi ở chế độ Testing thường hết hạn sau **7 ngày**. Không dùng trạng thái Testing làm cấu hình vận hành dài hạn.
8. Trước khi phục vụ giáo viên bên ngoài danh sách test, chuyển sang quy trình **Publishing/Production** phù hợp và chuẩn bị **OAuth app verification** theo yêu cầu của Google: scope `meetings.space.created` là **sensitive**. Google có thể yêu cầu xác minh domain, quyền sở hữu domain, chính sách quyền riêng tư, thông tin ứng dụng và mô tả cách dùng scope. **Không coi việc nhấn Publish là đồng nghĩa đã được xác minh.**
9. Lưu `Client ID` và `Client Secret` của OAuth Client dùng cho production trong secret manager hoặc kho bí mật của máy chủ; không lưu vào Git.

> **Không cần** Google Calendar API, Gmail API, Google Drive API hay service account chỉ để thực hiện luồng tạo Meet space theo tài khoản Teacher.

## 4. Chuẩn bị domain, HTTPS và Reverse Proxy

- Cấu hình DNS của `api.example.com` tới reverse proxy/load balancer hoặc dịch vụ hosting nhận request cho API Gateway.
- Bật HTTPS với chứng chỉ TLS hợp lệ (ví dụ chứng chỉ do hosting cấp hoặc Let's Encrypt).
- Cho phép inbound `443`; chỉ mở port public thực sự cần thiết. Không cần public MySQL, Hangfire storage hoặc cổng nội bộ Classroom API.
- Bảo đảm YARP / reverse proxy chuyển request callback **bao gồm `code` và `state` query parameters**, không tự thay đổi hoặc loại bỏ chúng.
- Cấu hình **trusted forwarded headers** (`X-Forwarded-Proto`, `X-Forwarded-Host`...) đúng proxy đáng tin cậy để ứng dụng không nhầm URL công khai HTTPS thành HTTP nội bộ. Không tin các forwarded headers từ client tùy ý.
- Kiểm tra các chính sách redirect, cookie, HSTS, CORS (FE → API) theo cấu hình hiện có. Với flow OAuth hiện tại callback dùng one-time server-side state, không yêu cầu JWT trong redirect của Google.
- Đảm bảo container Classroom có thể outbound HTTPS tới `accounts.google.com`, `oauth2.googleapis.com` và `meet.googleapis.com` (DNS và firewall).

## 5. Biến môi trường Docker Compose

### 5.1. Biến nguồn trong `.env` của Compose

Đặt trong file `.env` được nạp bởi Docker Compose trên server triển khai (hoặc inject từ secret manager):

```dotenv
# Ví dụ production. KHÔNG commit giá trị thật.
GOOGLE_MEET_CLIENT_ID=<PRODUCTION_GOOGLE_OAUTH_CLIENT_ID>
GOOGLE_MEET_CLIENT_SECRET=<PRODUCTION_GOOGLE_OAUTH_CLIENT_SECRET>
GOOGLE_MEET_REDIRECT_URI=https://api.example.com/classroom/api/v1/teachers/me/google-meet/callback
GOOGLE_MEET_ENCRYPTION_KEY=<BASE64_PERSISTENT_32_BYTE_KEY>
GOOGLE_MEET_STUDENT_JOIN_MINUTES_BEFORE=15
GOOGLE_MEET_STUDENT_JOIN_MINUTES_AFTER=30
```

Tạo encryption key **một lần** trên môi trường triển khai (ví dụ):

```bash
openssl rand -base64 32
```

Giữ khóa này ổn định giữa các lần deploy và tất cả Classroom replicas. **Không tạo khóa mới tự động mỗi lần restart**: đổi khóa mà chưa re-encrypt token cũ sẽ khiến các kết nối Google hiện tại không giải mã được.

> **Lỗi đã gặp khi cấu hình local:** File `.env` có tên `GoogleMeet__ClientId`, trong khi Compose tham chiếu `${GOOGLE_MEET_CLIENT_ID}`. Do tên không khớp, Compose đưa chuỗi rỗng vào container, và API trả `503 google_not_configured` với chi tiết `Configure GoogleMeet:ClientId.`. Hãy phân biệt **biến nguồn của Compose** với **biến cấu hình .NET bên trong container**.

### 5.2. Ánh xạ trong Docker Compose

Trong **khối `environment` đang có** của service `classroom-api`, thêm các mappings (không tạo thêm block `environment` thứ hai):

```yaml
services:
  classroom-api:
    environment:
      GoogleMeet__ClientId: ${GOOGLE_MEET_CLIENT_ID:?Missing GOOGLE_MEET_CLIENT_ID}
      GoogleMeet__ClientSecret: ${GOOGLE_MEET_CLIENT_SECRET:?Missing GOOGLE_MEET_CLIENT_SECRET}
      GoogleMeet__RedirectUri: ${GOOGLE_MEET_REDIRECT_URI:?Missing GOOGLE_MEET_REDIRECT_URI}
      GoogleMeet__EncryptionKey: ${GOOGLE_MEET_ENCRYPTION_KEY:?Missing GOOGLE_MEET_ENCRYPTION_KEY}
      GoogleMeet__StudentJoinMinutesBefore: ${GOOGLE_MEET_STUDENT_JOIN_MINUTES_BEFORE:-15}
      GoogleMeet__StudentJoinMinutesAfter: ${GOOGLE_MEET_STUDENT_JOIN_MINUTES_AFTER:-30}
```

Đây là **đoạn cấu hình cần chèn**, không phải toàn bộ file Compose. Giữ nguyên ports, profiles, database, networks, environment và dependency hiện tại. Với production nên dùng secret manager / Docker secrets hoặc cơ chế inject an toàn của hosting thay vì đặt secrets trong file nếu hạ tầng hỗ trợ; tránh để biến bí mật xuất hiện trong logs hoặc tài liệu CI.

### 5.3. Cách chạy production

Trong thư mục chứa Compose đã cấu hình:

```bash
# Xem danh sách service thực tế trước khi chạy
docker compose config --services

# Dùng file env production nếu KHÔNG dùng .env mặc định:
docker compose --env-file .env.production up -d --build --force-recreate classroom-api
```

Nếu Compose dùng `profiles`, `-f` hoặc tên service khác, bổ sung tùy theo cấu hình project. `--force-recreate` cần thiết khi đã thay biến môi trường. Đảm bảo đường dẫn `.env.production` đúng và không commit secrets.

**Kiểm tra biến mà không để lộ giá trị:**

```bash
docker compose --env-file .env.production exec classroom-api sh -c '
for k in GoogleMeet__ClientId GoogleMeet__ClientSecret GoogleMeet__RedirectUri GoogleMeet__EncryptionKey; do
  eval "v=\${$k}"
  if [ -n "$v" ]; then echo "$k: CONFIGURED"; else echo "$k: EMPTY"; fi
done'
```

Không dùng `docker compose config`, `env` đầy đủ, `user-secrets list` hoặc `printenv` để gửi screenshot/chia sẻ công khai vì có thể lộ credential.

## 6. Database migration và Hangfire

- Sao lưu Classroom MySQL trước khi cập nhật schema production; kiểm tra migration tương thích dữ liệu hiện có.
- Đảm bảo Classroom connection string `ConnectionStrings__ClassroomDatabase` đúng DB cần triển khai. MySQL phải chỉ truy cập từ private network hoặc trusted hosts.
- Theo tài liệu Backend, lệnh migration hiện tại là:

```bash
dotnet ef database update \
  --project src/Services/Classroom/ToeicSpace.Classroom.Infrastructure \
  --startup-project src/Services/Classroom/ToeicSpace.Classroom.Infrastructure
```

- Lệnh trên cần chạy trong môi trường có .NET SDK/EF tools, source và connection string tương ứng; **không giả định** image runtime production có `dotnet ef`.
- Xác nhận Hangfire tables được tạo trong cùng Classroom MySQL và Hangfire worker thực sự chạy. Các job tạo Meet, retry và reconciliation phải còn hoạt động sau restart.
- Nếu có nhiều Classroom replicas, bảo đảm Hangfire/MySQL locking, token encryption key dùng chung và các cơ chế chống duplicate provisioning đã được cấu hình theo implementation.

## 7. API public qua Gateway

Các route **nội bộ Classroom** theo tài liệu Backend được public qua prefix `/classroom` nếu YARP đã cấu hình như thiết kế hiện tại:

| Mục đích | Method | Public URL mẫu | Ai sử dụng |
| --- | --- | --- | --- |
| Bắt đầu OAuth | `POST` | `https://api.example.com/classroom/api/v1/teachers/me/google-meet/connect` | Teacher + ToeicSpace JWT |
| Google callback | `GET` | `https://api.example.com/classroom/api/v1/teachers/me/google-meet/callback` | Trình duyệt theo redirect Google |
| Xem Google connection | `GET` | `https://api.example.com/classroom/api/v1/teachers/me/google-meet/status` | Teacher |
| Ngắt kết nối | `DELETE` | `https://api.example.com/classroom/api/v1/teachers/me/google-meet` | Teacher |
| Xem provision status | `GET` | `https://api.example.com/classroom/api/v1/class-sessions/{sessionId}/meet/status` | Theo phân quyền session |
| Lấy link Meet | `GET` | `https://api.example.com/classroom/api/v1/class-sessions/{sessionId}/meet/link` | Theo phân quyền session/time window |
| Thử lại job lỗi | `POST` | `https://api.example.com/classroom/api/v1/class-sessions/{sessionId}/meet/retry` | Admin |

**Trước khi hoàn tất deploy**, kiểm tra routes thực tế của YARP bằng request không bí mật và log route/backend; không tự suy diễn nếu deployment có thêm prefix ingress khác.

## 8. Luồng FE khi tích hợp sau deploy

1. Teacher đăng nhập ToeicSpace và FE lấy **ToeicSpace access token** theo cơ chế hiện có.
2. FE gọi `POST /classroom/api/v1/teachers/me/google-meet/connect` qua Gateway, với Bearer token của Teacher.
3. Backend trả `authorizationUrl`; **FE chuyển trình duyệt tới URL này**.
4. Teacher chọn đúng Google account và cấp các quyền được yêu cầu. Google chuyển về **HTTPS public callback** đã đăng ký, mang `code` và `state`.
5. Backend kiểm tra state một lần, đổi code lấy token, lưu refresh token dạng mã hóa và trả kết quả callback. Theo implementation hiện tại, callback trả **JSON `{ "connected": true }`** chứ chưa redirect về UI; có thể giữ như vậy để kiểm thử, còn việc redirect về trang FE cần xem xét riêng khi phát triển FE (không tự ý thêm open redirect).
6. FE gọi `GET .../google-meet/status` xác nhận Teacher đã kết nối.
7. Admin/Teacher tạo hoặc chuyển Session sang **Online** bằng API ClassSession hiện có; không tự thêm Room cho Online Session.
8. Hangfire tạo Meet trong background. FE đọc `GET .../meet/status` và chỉ hiển thị nút tham gia khi trạng thái `Ready`.
9. FE lấy meeting URL qua `GET .../meet/link` khi được phép. Student phải có enrollment hợp lệ và trong cửa sổ thời gian đã cấu hình.

> Cửa sổ cấp link của ToeicSpace **không tự đặt giới hạn thời gian truy cập ở Google Meet** đối với người đã nhận được URL. Không xem URL là bí mật đủ mạnh để thay thế kiểm soát vào phòng của Google.

## 9. Kiểm tra sau khi deploy (smoke test)

- [ ] Domain `api.example.com` có HTTPS/chứng chỉ hợp lệ; request qua YARP tới Classroom thành công.
- [ ] Google Meet REST API được bật trên đúng Google Cloud project dùng bởi production OAuth Client.
- [ ] `GoogleMeet__RedirectUri` trùng tuyệt đối với **Authorized redirect URI** trên Google Cloud.
- [ ] OAuth app Branding/Audience/Scopes sẵn sàng cho nhóm Teacher dự kiến, các yêu cầu xác minh đã hoàn tất khi cần.
- [ ] 4 biến cấu hình cốt lõi được nạp vào container **với giá trị không rỗng**.
- [ ] Migration đã áp dụng, Hangfire worker khỏe, có kết nối Classroom MySQL.
- [ ] Đăng nhập Teacher; gọi `connect` trả `authorizationUrl` (không phải 503).
- [ ] Mở URL trong browser, hoàn thành Google consent, callback trả `connected: true`.
- [ ] API `status` xác nhận đúng Google account đã kết nối.
- [ ] Tạo một **Session Online test độc lập**, xác nhận Hangfire đưa trạng thái tới `Ready` và trả meeting URL hợp lệ.
- [ ] Teacher có thể mở Meet; Student hợp lệ được lấy URL trong thời gian cho phép; user khác không xem được.
- [ ] Kiểm thử Offline/Cancelled Session không công khai link; ngắt kết nối/đổi Teacher không dùng lại link cũ làm link đang hoạt động.
- [ ] Chạy lại Newman regression tests hiện có và kiểm tra logs không chứa JWT, refresh token, client secret hay OAuth code.

**Lưu ý:** Đạt `connected: true` chỉ xác nhận OAuth kết nối; **chưa** chứng minh Meet provisioning/Google API thực sự thành công.

## 10. Sự cố thường gặp

| Hiện tượng | Nguyên nhân / kiểm tra |
| --- | --- |
| `503 google_not_configured` / `Configure GoogleMeet:ClientId` | Compose có tên biến nhưng value rỗng; kiểm tra `.env` và `${GOOGLE_MEET_CLIENT_ID}`; recreate container |
| `redirect_uri_mismatch` | URI trong Google Cloud khác `GoogleMeet__RedirectUri`; kiểm tra HTTPS, domain, prefix `/classroom`, dấu `/` cuối |
| `403 access_denied` khi Google consent | Gmail chưa trong Test users, app chưa publish/verify, hoặc người dùng từ chối quyền |
| Callback 404 | Gateway chưa chuyển đúng route, reverse proxy rewrite sai prefix hoặc ingress chặn query |
| Callback có code/state nhưng không kết nối | Kiểm tra state đã hết 10 phút/bị sử dụng rồi, server clocks, DB, token exchange; tuyệt đối không log raw code/token |
| `NeedsConnection` | Assigned Teacher chưa kết nối Google hoặc token đã bị thu hồi/hết hiệu lực |
| `Pending`/`Provisioning` kéo dài | Hangfire worker/storage/lease/reconciliation, network ra Google |
| `Failed` + `uncertain_google_result` | Google creation có kết quả không chắc chắn; **không retry tự động** vì có thể đã tạo space; Admin xem xét trước khi retry |
| Production Teacher mất kết nối sau khoảng 7 ngày | OAuth app External vẫn ở Testing và refresh token bị giới hạn; kiểm tra publishing/verification và reconnect |
| Classroom API có auth nhưng callback không hoạt động | Callback từ Google không có ToeicSpace Bearer token; phải sử dụng cơ chế one-time OAuth state của Backend thay vì yêu cầu JWT trên callback |

## 11. Bảo mật và vận hành

- **Không commit** `.env`, OAuth Client Secret, EncryptionKey, access/refresh token hoặc file credentials vào repository.
- Giới hạn quyền người vận hành, phân quyền truy cập secret manager và database; bảo mật backups.
- Không in secrets ra terminal logs/CI/CD artifacts; nếu lộ Client Secret cần **rotate**. Đổi EncryptionKey phải có kế hoạch re-encrypt hoặc yêu cầu Teacher reconnect.
- Đăng ký OAuth callback trên một host ổn định. Khi đổi domain/host/path, cập nhật Google Cloud và cấu hình backend **trước khi** bắt đầu lại luồng OAuth.
- Theo dõi Hangfire job failures, Google API 401/403/429, connection status, retry/backoff, và mức sử dụng API.
- Nếu đưa ra ngoài cho nhiều người dùng, hoàn tất Google verification khi cần; chuẩn bị website và Privacy Policy nêu rõ lý do dùng quyền Google Meet và cách thu hồi kết nối.

## 12. Tài liệu tham chiếu

- [Google OAuth 2.0 — Web server applications](https://developers.google.com/identity/protocols/oauth2/web-server)
- [Google Meet API — Create a space](https://developers.google.com/workspace/meet/api/reference/rest/v2/spaces/create)
- [Google Meet scopes / authorization](https://developers.google.com/workspace/meet/api/guides/authenticate-authorize)
- [Google OAuth production readiness](https://developers.google.com/identity/protocols/oauth2/production-readiness/policy-compliance)
- [Google sensitive scope verification](https://developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification)
- Tài liệu Backend trong repo: `classroom-google-meet.md` (nếu đã được lưu trong repository).

---

**Checklist phát hành tối thiểu:** Domain HTTPS → Gateway callback route → Google Cloud OAuth scopes/client + verification khi cần → production secrets/Compose → migration & Hangfire → Teacher OAuth → Online Session tạo Meet thật → FE kiểm thử join link/phân quyền.
