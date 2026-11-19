# 📄 Migration Guide: 20261107162016_AddDictation

> **Ngày tạo:** 07/11/2026
> **Dịch vụ áp dụng:** `ToeicSpace.Content` (`ContentDbContext`)
> **Database:** `toeic_space_content`
> **Phụ thuộc:** `20261107111547_AddBilingualNews`

---

## 1. Tổng quan

Thêm schema cho Dictation (PBL-107). Migration chỉ **tạo 4 bảng mới**, không đổi bảng cũ. Thiết kế chi tiết (lý do từng bảng,
khóa tự nhiên, định dạng JSON) nằm ở [`docs/dictation.md`](../dictation.md).

| Bảng | Mục đích | Index chính |
|---|---|---|
| `DictationCourses` | Bài (course) theo gói/chặng TOEIC, thứ tự, cấp độ | unique `(Code)`, `(Pack, IsActive, Order)` |
| `DictationItems` | Câu nghe chép: đáp án, bản dịch, IPA, ghi chú/từ khóa/từ vựng (JSON), giọng đọc | `(CourseId, IsActive, Order)`, unique `(SourceCode)` |
| `DictationProgress` | Tiến độ học viên từng câu: điểm tốt nhất, đạt/đạt sạch, số lần làm | `(ItemId)`, unique `(LearnerId, ItemId)` |
| `DictationLearnerStates` | 1 dòng/học viên: cài đặt Basic/Pro, giọng đọc, âm thanh, sao từng màn, thời gian học theo ngày (JSON) | PK `(LearnerId)` |

Tên bảng, cột, khóa trong cột JSON và giá trị enum lưu dạng chuỗi đều là tiếng Anh.

## 2. Áp dụng

```bash
export ConnectionStrings__ContentDatabase="Server=...;Database=toeic_space_content;..."
dotnet ef database update 20261107162016_AddDictation   --project src/Services/Content/ToeicSpace.Content.Infrastructure   --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

Hoặc chạy script idempotent [`20261107_AddDictation.sql`](20261107_AddDictation.sql) (an toàn khi chạy lại).

## 3. Nạp dữ liệu

Sau khi migration chạy, nạp dữ liệu bằng endpoint Admin (idempotent, chạy lại không tạo trùng):

```bash
curl -X POST <gateway>/content/api/v1/dictation/admin/import -H "Authorization: Bearer <admin>" -F files=@data/dictation/toeic-250.json.gz  (lặp lại cho 450/650/800/990)
```

## 4. Rollback

```bash
dotnet ef database update 20261107111547_AddBilingualNews --project src/Services/Content/ToeicSpace.Content.Infrastructure --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

Rollback xóa 4 bảng trên cùng toàn bộ dữ liệu của chúng.
