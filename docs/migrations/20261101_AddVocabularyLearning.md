# 📄 Migration Guide: 20261101113940_AddVocabularyLearning

> **Ngày tạo:** 01/11/2026
> **Dịch vụ áp dụng:** `ToeicSpace.Content` (`ContentDbContext`)
> **Database:** `toeic_space_content`
> **Phụ thuộc:** `20261030072935_SupportMultipleLessonVideos`

---

## 1. Tổng quan

Thêm schema cho tính năng Học từ vựng & tiến độ (PBL-75): lộ trình, tiến độ SM-2, thư viện "Từ vựng của tôi",
phiên học lưu trên server, tùy chọn cá nhân và cache tra từ (enrichment). Migration chỉ **tạo 12 bảng mới**, không đổi bảng cũ.

| Bảng | Mục đích | Index chính |
|---|---|---|
| `VocabularyStages` | 5 chặng lộ trình TOEIC | unique `Slug` |
| `VocabularySets` | Bài học (50 từ) trong chặng | unique `(StageId, Slug)`, `(StageId, Order)` |
| `VocabularyWords` | Kho từ (từ chính thức `OwnerKey = 00000000-...`, từ riêng `OwnerKey = learnerId`) | unique `(OwnerKey, NormalizedHeadword, PartOfSpeech)` |
| `VocabularySetWords` | Từ thuộc bài học + thứ tự | PK `(SetId, WordId)`, unique `(SetId, Order)` |
| `LearnerWordProgress` | Lịch ôn SM-2 của từng học viên/từ | unique `(LearnerId, WordId)`, `(LearnerId, DueOn)` |
| `WordReviewLogs` | Nhật ký ôn tập (idempotent) | unique `(LearnerId, ClientEventId)`, `(LearnerId, LocalDay)` |
| `ReelBackgrounds` | Cache video nền Pixabay cho chế độ Reel | unique `(Provider, ProviderId)`, `IsActive` |
| `VocabularyFolders` | Thư mục của học viên | unique `(LearnerId, NormalizedName)` |
| `SavedWords` | Từ đã lưu (chính thức hoặc tự thêm) | unique `(LearnerId, WordId)`, `(LearnerId, FolderId, SavedAt)`; FK thư mục `SET NULL` |
| `StudySessions` | Phiên học Flashcard/Quiz/Reel (hàng đợi JSON) | unique `(LearnerId, ActiveSlot)`, `(LearnerId, Status, UpdatedAt)` |
| `LearnerVocabularyPreferences` | Tùy chọn cá nhân (1 dòng/học viên, gồm `SidebarWidth`) | PK `LearnerId` |
| `WordGlossCache` | Cache kết quả tra từ (nghĩa, phiên âm, nguồn từng trường; TTL 30 ngày) | unique `(NormalizedText, Kind)`, `ExpiresAt` |

Enum lưu dạng chuỗi (`PartOfSpeech`, `Mode`, `Grade`, `LastGrade`), ngày học viên lưu kiểu `date`.
`LearnerId` là `sub` của Identity, **không** có khóa ngoại sang Identity.
Thiết kế chi tiết: [`../vocabulary.md`](../vocabulary.md).

---

## 2. Chạy migration

```bash
dotnet ef database update \
  --context ContentDbContext \
  --project src/Services/Content/ToeicSpace.Content.Infrastructure \
  --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

(Đặt biến môi trường `ConnectionStrings__ContentDatabase` trước khi chạy.)
Hoặc chạy script idempotent: [`20261101_AddVocabularyLearning.sql`](20261101_AddVocabularyLearning.sql).

Sau khi migrate, nạp dữ liệu từ vựng bằng API Admin `POST /content/api/v1/vocabulary/admin/import`
(xem mục *Importing the vocabulary* trong [`../vocabulary.md`](../vocabulary.md)).

---

## 3. Kiểm tra

```sql
SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId = '20261101113940_AddVocabularyLearning';
SHOW INDEX FROM VocabularyWords WHERE Key_name = 'IX_VocabularyWords_OwnerKey_NormalizedHeadword_PartOfSpeech'; -- Non_unique = 0
SHOW INDEX FROM StudySessions WHERE Key_name = 'IX_StudySessions_LearnerId_ActiveSlot';                          -- Non_unique = 0
SHOW INDEX FROM WordReviewLogs WHERE Key_name = 'IX_WordReviewLogs_LearnerId_ClientEventId';           -- Non_unique = 0
-- Sau khi import đủ 5 file:
SELECT COUNT(*) FROM VocabularyStages;  -- 5
SELECT COUNT(*) FROM VocabularySets;    -- 100
SELECT COUNT(*) FROM VocabularyWords;   -- 4954
```

## 4. Rollback

```bash
dotnet ef database update 20261030072935_SupportMultipleLessonVideos \
  --context ContentDbContext \
  --project src/Services/Content/ToeicSpace.Content.Infrastructure \
  --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

Rollback xóa toàn bộ tiến độ học từ vựng và nhật ký ôn tập.
