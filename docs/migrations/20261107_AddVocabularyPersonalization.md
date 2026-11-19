# 📄 Migration Guide: 20261107102903_AddVocabularyPersonalization

> **Ngày tạo:** 07/11/2026
> **Dịch vụ áp dụng:** `ToeicSpace.Content` (`ContentDbContext`)
> **Database:** `toeic_space_content`
> **Phụ thuộc:** `20261101113940_AddVocabularyLearning`

---

## 1. Tổng quan

Bổ sung cho tính năng Học từ vựng (PBL-75, contract v4): **sửa nghĩa riêng**, **từ sai** và **cài đặt học**.
Migration chỉ **thêm cột/index** vào 3 bảng có sẵn, không tạo bảng mới, không xóa dữ liệu.

| Bảng | Thay đổi | Mục đích |
|---|---|---|
| `SavedWords` | + `MyPhonetic` varchar(100), `MyPartOfSpeech` varchar(24), `MyMeaningVi` varchar(200), `MyExample` varchar(300), `MyExampleVi` varchar(300), `PersonalizedAt` datetime(6) — tất cả NULL | Bản sửa riêng của học viên cho từ chính thức (NULL = dùng bản gốc). Từ chung không bao giờ bị sửa |
| `SavedWords` | `Note` varchar(300) → varchar(1000) | Ghi chú dài hơn |
| `LearnerWordProgress` | + `MistakePending` tinyint(1) NOT NULL DEFAULT 0, `MistakeCount` int NOT NULL DEFAULT 0, `LastMistakeAt` datetime(6) NULL; index `IX_LearnerWordProgress_LearnerId_MistakePending_LastMistakeAt` | Danh sách "Từ sai": trả lời sai (Forget) thì giữ lại tới khi trả lời đúng (Good/Easy) |
| `LearnerVocabularyPreferences` | + `StudySettings` json NULL | Cài đặt học (thời gian Reel/Trắc nghiệm, gợi ý, âm thanh, kiểu thẻ, nền); NULL = mặc định |

**Back-fill dữ liệu cũ** (trong cùng migration): với mỗi dòng `LearnerWordProgress` có log `Forget` trong
`WordReviewLogs`, đặt `MistakeCount` = số lần Forget, `LastMistakeAt` = lần Forget gần nhất và
`MistakePending` = 1 nếu sau lần đó **không** có Good/Easy. Tiến độ SM-2 (interval, dueOn, …) không bị đụng tới.

Thiết kế chi tiết: [`../vocabulary.md`](../vocabulary.md) (mục *Personal word edits*, *Mistakes*, *Study settings*).

---

## 2. Chạy migration

```bash
dotnet ef database update \
  --context ContentDbContext \
  --project src/Services/Content/ToeicSpace.Content.Infrastructure \
  --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

(Đặt biến môi trường `ConnectionStrings__ContentDatabase` trước khi chạy.)
Hoặc chạy script idempotent: [`20261107_AddVocabularyPersonalization.sql`](20261107_AddVocabularyPersonalization.sql).

---

## 3. Kiểm tra

```sql
SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId = '20261107102903_AddVocabularyPersonalization';
SHOW COLUMNS FROM SavedWords LIKE 'My%';                                   -- 5 dòng
SHOW COLUMNS FROM SavedWords LIKE 'Note';                                  -- varchar(1000)
SHOW COLUMNS FROM LearnerVocabularyPreferences LIKE 'StudySettings';       -- json, NULL
SHOW INDEX FROM LearnerWordProgress
  WHERE Key_name = 'IX_LearnerWordProgress_LearnerId_MistakePending_LastMistakeAt';   -- 3 dòng
-- Back-fill: số từ sai đang chờ theo học viên
SELECT LearnerId, SUM(MistakePending) AS Pending, SUM(MistakeCount) AS Mistakes
FROM LearnerWordProgress GROUP BY LearnerId;
```

## 4. Rollback

```bash
dotnet ef database update 20261101113940_AddVocabularyLearning \
  --context ContentDbContext \
  --project src/Services/Content/ToeicSpace.Content.Infrastructure \
  --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

Rollback xóa các bản sửa nghĩa riêng, cờ "từ sai" và cài đặt học; `Note` bị thu về varchar(300) (ghi chú dài hơn
300 ký tự sẽ làm lệnh rollback lỗi hoặc bị cắt, tùy `sql_mode`). Tiến độ học và nhật ký ôn tập được giữ nguyên.
