# 📄 Migration Guide: 20260916232655_AddRefreshTokenHashIndex

> **Ngày tạo:** 17/09/2026
> **Dịch vụ áp dụng:** `ToeicSpace.Identity` (`IdentityDbContext`)
> **Database:** `toeic_space_identity`
> **Phụ thuộc:** `20260913065731_CreateIdentityAuthSchema`

---

## 1. Tổng quan

Thêm unique index `IX_UserTokens_TokenHash` trên `UserTokens (TokenHash)`.

Mỗi lần refresh phiên, Identity tìm refresh token theo **hash** (SHA-256, không lưu token gốc). Index này:

- giữ truy vấn refresh ở mức tra cứu index thay vì quét toàn bảng;
- đảm bảo không có hai phiên trùng hash.

Migration không đổi dữ liệu, chỉ tạo index. Bảng đang trống trên môi trường local.

Thiết kế phiên đăng nhập: xem [`../AUTHENTICATION.md`](../AUTHENTICATION.md).

---

## 2. Chạy migration

```bash
dotnet ef database update \
  --context IdentityDbContext \
  --project src/Services/Identity/ToeicSpace.Identity.Infrastructure \
  --startup-project src/Services/Identity/ToeicSpace.Identity.API
```

Hoặc chạy script idempotent: [`20260917_AddRefreshTokenHashIndex.sql`](20260917_AddRefreshTokenHashIndex.sql).

---

## 3. Kiểm tra

```sql
SHOW INDEX FROM UserTokens WHERE Key_name = 'IX_UserTokens_TokenHash';   -- 1 dòng, Non_unique = 0
```

## 4. Rollback

```bash
dotnet ef database update 20260913065731_CreateIdentityAuthSchema \
  --context IdentityDbContext \
  --project src/Services/Identity/ToeicSpace.Identity.Infrastructure \
  --startup-project src/Services/Identity/ToeicSpace.Identity.API
```
