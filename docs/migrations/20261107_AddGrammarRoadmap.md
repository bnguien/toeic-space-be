# 📄 Migration Guide: 20261107162858_AddGrammarRoadmap

> **Ngày tạo:** 07/11/2026
> **Dịch vụ áp dụng:** `ToeicSpace.Content` (`ContentDbContext`)
> **Database:** `toeic_space_content`
> **Phụ thuộc:** `20261107162351_AddVideoLearning`

---

## 1. Tổng quan

Thêm schema cho Lộ trình ngữ pháp (PBL-107). Migration chỉ **tạo 7 bảng mới**, không đổi bảng cũ. Thiết kế chi tiết (lý do từng bảng,
khóa tự nhiên, định dạng JSON) nằm ở [`docs/grammar.md`](../grammar.md).

| Bảng | Mục đích | Index chính |
|---|---|---|
| `GrammarDocuments` | Nội dung theo chuyên đề/bài test chặng (kiến thức, từ vựng, bài tập; JSON khóa tiếng Anh) + hash nội dung | unique `(Kind, Key)` |
| `GrammarLessonProgress` | Tiến độ chuyên đề: đã hiểu kiến thức/từ vựng, điểm quick check, mini test, hoàn thành | unique `(LearnerId, Topic)` |
| `GrammarAttempts` | Lượt làm bài (khối bài tập hoặc bài test chặng): câu trả lời và kết quả (JSON), điểm | `(LearnerId, Stage)` |
| `GrammarQuestionResults` | Kết quả từng câu: đúng lần đầu/hiện tại, đã sửa, đã xem đáp án, số lần sai (Sổ lỗi, bẫy) | `(LearnerId, LastCorrect, WrongCount)`, unique `(LearnerId, QuestionId)`, `(LearnerId, TrapCode, WrongCount)` |
| `GrammarLearnerStates` | 1 dòng/học viên: vị trí học, từ đã thuộc, tùy chọn (dark mode, cột lộ trình), điểm bài test chặng (JSON) | PK `(LearnerId)` |
| `GrammarComments` | Bình luận bài học (1 cấp trả lời), tên tác giả lưu tại thời điểm viết | `(LearnerId, Id)`, `(Target, ParentId, Id)` |
| `GrammarCommentLikes` | Lượt thích bình luận | PK `(CommentId, LearnerId)` |

Tên bảng, cột, khóa trong cột JSON và giá trị enum lưu dạng chuỗi đều là tiếng Anh.

## 2. Áp dụng

```bash
export ConnectionStrings__ContentDatabase="Server=...;Database=toeic_space_content;..."
dotnet ef database update 20261107162858_AddGrammarRoadmap   --project src/Services/Content/ToeicSpace.Content.Infrastructure   --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

Hoặc chạy script idempotent [`20261107_AddGrammarRoadmap.sql`](20261107_AddGrammarRoadmap.sql) (an toàn khi chạy lại).

## 3. Nạp dữ liệu

Sau khi migration chạy, nạp dữ liệu bằng endpoint Admin (idempotent, chạy lại không tạo trùng):

```bash
curl -X POST <gateway>/content/api/v1/grammar/admin/import -H "Authorization: Bearer <admin>" -F files=@data/grammar/grammar-content.json.gz
```

## 4. Rollback

```bash
dotnet ef database update 20261107162351_AddVideoLearning --project src/Services/Content/ToeicSpace.Content.Infrastructure --startup-project src/Services/Content/ToeicSpace.Content.Infrastructure
```

Rollback xóa 7 bảng trên cùng toàn bộ dữ liệu của chúng.
