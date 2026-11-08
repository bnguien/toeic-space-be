# 📄 Migration Guide: 20261107162351_AddVideoLearning

> **Ngày tạo:** 07/11/2026
> **Dịch vụ áp dụng:** `ToeicSpace.Content` (`ContentDbContext`)
> **Database:** `toeic_space_content`
> **Phụ thuộc:** `20261107162016_AddDictation`

---

## 1. Tổng quan

Thêm schema cho Video song ngữ (PBL-107). Migration chỉ **tạo 4 bảng mới**, không đổi bảng cũ. Thiết kế chi tiết (lý do từng bảng,
khóa tự nhiên, định dạng JSON) nằm ở [`docs/video.md`](../video.md).

| Bảng | Mục đích | Index chính |
|---|---|---|
| `VideoCatalogItems` | Danh mục video YouTube: chủ đề, kênh, thời lượng, track Dễ/Vừa/Khó, CEFR, độ khó, trạng thái phụ đề/nhúng | `(TopicSlug, IsActive, SubtitleStatus, Position)`, `(Track, IsActive, Difficulty)`, unique `(VideoId)` |
| `VideoSubtitles` | Phụ đề theo video: các track (cue `[start, duration, text, offsets]` JSON), danh sách ngôn ngữ, bản dịch tiếng Việt đã dịch | unique `(VideoId)` |
| `VideoWatches` | Video của học viên: mở gần nhất, vị trí xem, % đã xem, hoàn thành, công khai/chia sẻ | `(IsPublic, LastOpenedAt)`, `(LearnerId, LastOpenedAt)`, `(LearnerId, ProgressAt)`, unique `(LearnerId, VideoId)` |
| `VideoLearnerStates` | 1 dòng/học viên: cài đặt trình đọc, chế độ nghe, track lộ trình, chủ đề mở rộng (JSON) | PK `(LearnerId)` |

Tên bảng, cột, khóa trong cột JSON và giá trị enum lưu dạng chuỗi đều là tiếng Anh.

## 2. Áp dụng

```bash
export ConnectionStrings__ContentDatabase="Server=...;Database=toeic_space_content;..."
dotnet ef database update 20261107162351_AddVideoLearning   --project src/Services/Content/ToeicSpace.Content.Infrastructure   --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

Hoặc chạy script idempotent [`20261107_AddVideoLearning.sql`](20261107_AddVideoLearning.sql) (an toàn khi chạy lại).

## 3. Nạp dữ liệu

Sau khi migration chạy, nạp dữ liệu bằng endpoint Admin (idempotent, chạy lại không tạo trùng):

```bash
for f in data/videos/catalog.json data/videos/subtitles-*.json.gz; do curl -X POST <gateway>/content/api/v1/videos/admin/import -H "Authorization: Bearer <admin>" -F "files=@$f"; done
```

## 4. Rollback

```bash
dotnet ef database update 20261107162016_AddDictation --project src/Services/Content/ToeicSpace.Content.Infrastructure --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

Rollback xóa 4 bảng trên cùng toàn bộ dữ liệu của chúng.
