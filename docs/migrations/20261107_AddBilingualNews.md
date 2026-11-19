# 📄 Migration Guide: 20261107111547_AddBilingualNews

> **Ngày tạo:** 07/11/2026
> **Dịch vụ áp dụng:** `ToeicSpace.Content` (`ContentDbContext`)
> **Database:** `toeic_space_content`
> **Phụ thuộc:** `20261101113940_AddVocabularyLearning`

---

## 1. Tổng quan

Thêm schema cho Báo song ngữ (PBL-107). Migration chỉ **tạo 2 bảng mới**, không đổi bảng cũ. Thiết kế chi tiết (lý do từng bảng,
khóa tự nhiên, định dạng JSON) nằm ở [`docs/news.md`](../news.md).

| Bảng | Mục đích | Index chính |
|---|---|---|
| `NewsArticles` | Bài báo song ngữ: đoạn/câu (`Paragraphs` JSON), từ vựng (`Vocabulary` JSON), chặng c1–c5, chủ đề, ảnh bìa, nguồn/giấy phép | unique `(Slug)`, `(Status, Stage, PublishedAt)`, `(Status, Topic, PublishedAt)` |
| `NewsReadings` | Trạng thái đọc của học viên: mở/hoàn thành, thời gian, từ đã biết, ghi chú, chép chính tả từng câu, cài đặt trình đọc (JSON) | `(ArticleId)`, unique `(LearnerId, ArticleId)`, `(LearnerId, CompletedAt)`, `(LearnerId, SettingsUpdatedAt)` |

Tên bảng, cột, khóa trong cột JSON và giá trị enum lưu dạng chuỗi đều là tiếng Anh.

## 2. Áp dụng

```bash
export ConnectionStrings__ContentDatabase="Server=...;Database=toeic_space_content;..."
dotnet ef database update 20261107111547_AddBilingualNews   --project src/Services/Content/ToeicSpace.Content.Infrastructure   --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

Hoặc chạy script idempotent [`20261107_AddBilingualNews.sql`](20261107_AddBilingualNews.sql) (an toàn khi chạy lại).

## 3. Nạp dữ liệu

Sau khi migration chạy, nạp dữ liệu bằng endpoint Admin (idempotent, chạy lại không tạo trùng):

```bash
python scripts/news_data_pipeline.py import --url <gateway>/content/api/v1/news/admin/import --token <admin>
```

## 4. Rollback

```bash
dotnet ef database update 20261101113940_AddVocabularyLearning --project src/Services/Content/ToeicSpace.Content.Infrastructure --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

Rollback xóa 2 bảng trên cùng toàn bộ dữ liệu của chúng.
