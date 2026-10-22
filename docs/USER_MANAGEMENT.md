# Quản lý người dùng (PBL-16)

Admin quản lý tài khoản của Identity service: xem, tìm kiếm, tạo, sửa, đổi role, khoá/mở khoá, đăng xuất mọi thiết bị, xoá mềm và khôi phục. Tính năng **không thêm cột hay migration**, chỉ dùng bảng `Users` và `UserTokens` hiện có.

## Dữ liệu và ý nghĩa

| Trường | Ý nghĩa | Ghi chú |
|---|---|---|
| `Role` | `User` (học viên), `Teacher`, `Admin` | Có trong access token, nên đổi role là phải thu hồi phiên |
| `Status = Active` | Được đăng nhập | |
| `Status = Inactive` | **Chưa xác thực email** | Job `CleanupInactiveAccounts` xoá cứng sau thời hạn. Admin không được đặt trạng thái này |
| `Status = Suspended` | Bị khoá | Trạng thái duy nhất dùng cho "khoá" |
| `EmailVerifiedAt` | Thời điểm xác thực email | Tài khoản do admin tạo được coi là đã xác thực |
| `DeletedAt` | Xoá mềm | Tài khoản giữ email và SĐT (unique index); dùng lại bằng cách khôi phục |
| `UserTokens` (RefreshToken) | Phiên đăng nhập | `LastActiveAt` = token mới nhất; `ActiveSessionCount` = token chưa dùng, chưa thu hồi, chưa hết hạn |

**Admin hiệu lực** = `Role = Admin`, `Status = Active`, đã xác thực, chưa xoá (`UserMappingExtensions.IsEffectiveAdministrator`).

## Quy tắc

1. Mọi endpoint yêu cầu role `Admin` (`[Authorize(Roles = "Admin")]`).
2. Admin không đổi role, khoá, xoá hay thu hồi phiên của **chính mình** (`USER_CANNOT_MODIFY_SELF`, 403). Sửa họ tên/SĐT của mình vẫn được.
3. Không hành động nào được làm mất **admin hiệu lực cuối cùng** (`USER_LAST_ADMINISTRATOR`, 409): hạ role, khoá, xoá.
4. Ba hành động trên chạy trong transaction `Serializable`. Trên MySQL lệnh đếm admin giữ shared lock, nên hai admin hạ quyền nhau cùng lúc sẽ deadlock; bên thua nhận `USER_CONCURRENT_UPDATE` (409) thay vì cả hai cùng thành công.
5. Đổi role, khoá, xoá đều thu hồi mọi refresh token. Access token đang có vẫn dùng được tới khi hết hạn (10 phút); `/auth/me` và refresh đã kiểm tra trạng thái nên bị chặn ngay.
6. Chỉ chuyển giữa `Active` ↔ `Suspended`, và chỉ với tài khoản đã xác thực (`USER_NOT_VERIFIED`, 409). Kích hoạt tài khoản chưa xác thực sẽ bỏ qua bước chứng minh sở hữu email.
7. Tài khoản đã xoá chỉ nhận lệnh khôi phục (`USER_DELETED`, 409). Khôi phục giữ nguyên role và trạng thái cũ.
8. Tạo tài khoản: `Active`, `EmailVerifiedAt = now`, server sinh **mật khẩu tạm** 14 ký tự (đủ luật mật khẩu đăng ký). Mật khẩu chỉ trả về **một lần** trong response; người dùng đổi lại bằng chức năng đổi mật khẩu.
9. Email/SĐT được chuẩn hoá như khi đăng ký; trùng (kể cả với tài khoản đã xoá) → `USER_EMAIL_ALREADY_EXISTS` / `USER_PHONE_ALREADY_EXISTS` (409). Email không sửa được vì là định danh đăng nhập.

## API

Qua gateway: `/identity/api/v1/admin/users`. Lỗi dùng ProblemDetails với `code`.

| Method | Route | Body | Kết quả |
|---|---|---|---|
| GET | `/` | query: `search`, `role`, `status`, `deleted`, `sort` (`Newest`/`Oldest`/`Name`/`LastActive`), `page`, `pageSize` (1–100) | `PaginatedResult<ManagedUserResult>` |
| GET | `/stats` | query: `role` (tuỳ chọn), `tzOffset` (phút lệch UTC, Việt Nam = 420) | `{ total, active, suspended, unverified, deleted, createdLast30Days, registrations }` |
| GET | `/{id}` | | `ManagedUserDetailResult` (cả tài khoản đã xoá) |
| POST | `/` | `{ fullName, email, phone?, role }` | 201 `{ user, temporaryPassword }` |
| PUT | `/{id}` | `{ fullName, phone? }` (`phone: null` xoá SĐT) | detail |
| PUT | `/{id}/role` | `{ role }` | detail |
| PUT | `/{id}/status` | `{ status: "Active" \| "Suspended" }` | detail |
| POST | `/{id}/sessions/revoke` | | 204 |
| DELETE | `/{id}` | | 204 (xoá mềm) |
| POST | `/{id}/restore` | | detail |

`role`, `status` là tên enum (không phân biệt hoa thường); giá trị số như `"2"` bị từ chối.

- `sort=LastActive`: theo lần đăng nhập / làm mới phiên gần nhất, tài khoản chưa đăng nhập xếp cuối.
- `registrations`: 14 phần tử `{ date, count }` (cũ → mới), mỗi ngày tính theo múi giờ `tzOffset` của người xem, gồm cả tài khoản đã xoá (vì việc đăng ký vẫn đã xảy ra).

## Code

```text
Identity.Application/Features/UserManagement/
├── Common/        UserAdministrationRules (luật dùng chung), ManagedUserReader (detail + phiên)
├── Models/        UserSearchCriteria
├── Results/       ManagedUserResult, ManagedUserDetailResult, CreateUserResult, UserStatisticsResult
├── Commands/      CreateUser, UpdateUser, ChangeUserRole, ChangeUserStatus,
│                  RevokeUserSessions, DeleteUser, RestoreUser
└── Queries/       GetUsers, GetUserById, GetUserStatistics
Identity.API/Controllers/AdminUsersController.cs
Identity.Infrastructure/Services/Security/TemporaryPasswordGenerator.cs
```

Test: `tests/UnitTests/ToeicSpace.Identity.Application.UnitTests/UserManagement` (SQLite, repository thật).

## Chưa làm

- Email chào mừng kèm mật khẩu tạm và bắt buộc đổi mật khẩu lần đầu (cần thêm cột và luồng FE).
- Lý do khoá và lịch sử thao tác admin (audit log) — hiện chỉ ghi structured log.
