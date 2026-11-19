# 📄 Migration Guide: 20261118171608_AddExamAttemptFlow

> **Ngày tạo:** 18/11/2026
> **Dịch vụ áp dụng:** `ToeicSpace.Assessment` (`AssessmentDbContext`)
> **Database:** `toeic_space_assessment`
> **Phụ thuộc:** `20261108041120_AddPracticeAttempts`

---

## 1. Tổng quan

Thêm các cột cho luồng làm bài thi thử / luyện tập (xem [`docs/practice-exam-flow.md`](../practice-exam-flow.md)).
Chỉ **thêm cột và index**, không đổi hay xoá dữ liệu cũ; mọi cột mới nullable hoặc có giá trị mặc định.

| Bảng | Cột / index | Ý nghĩa |
|---|---|---|
| `ToeicAttempts` | `Title` varchar(200) NOT NULL DEFAULT '' | Tên lượt làm (vd. "Part 5 · 20 câu ngẫu nhiên · 18/11/2026") |
| | `SelectedParts` varchar(20) NULL | Các Part của lượt làm, dạng `1,2,3` |
| | `Deadline` datetime(6) NULL, `TimeLimitSeconds` int NULL | Hạn nộp do server giữ (chỉ FullTest) |
| | `ListeningCorrect`, `ReadingCorrect` int NULL | Số câu đúng từng kỹ năng khi nộp |
| | `ActiveTestId` char(36) STORED GENERATED (`TestId` khi `Status = 'InProgress'`) + unique `(UserId, ActiveTestId)` | Mỗi học viên chỉ có 1 lượt đang làm trên 1 đề (giống `ActivePracticeSetId`) |
| | index `(UserId, StartTime)` | Lịch sử làm bài, mới nhất trước |
| | index `(Status, Mode, Deadline)` | Job nền tự nộp bài thi hết giờ |
| `ToeicAttemptAnswers` | `IsFlagged` tinyint(1) NOT NULL DEFAULT 0 | Đánh dấu câu |
| | `CheckedAt` datetime(6) NULL | Câu đã "Kiểm tra" (luyện tập): đáp án đã chấm và bị khoá |

## 2. Áp dụng

```bash
export ConnectionStrings__AssessmentDatabase="Server=...;Database=toeic_space_assessment;..."
dotnet ef database update 20261118171608_AddExamAttemptFlow \
  --project src/Services/Assessment/ToeicSpace.Assessment.Infrastructure \
  --startup-project src/Services/Assessment/ToeicSpace.Assessment.Infrastructure
```

Hoặc chạy script idempotent [`20261118_AddExamAttemptFlow.sql`](20261118_AddExamAttemptFlow.sql) (an toàn khi chạy lại).

Đã kiểm tra trên bản sao local `toeic_space_assessment_practice_ui` (clone từ `toeic_space_assessment_v3`).
Bảng `ToeicAttempts` trong dữ liệu gốc đang trống nên việc thêm cột sinh (`STORED GENERATED`) không tốn thời gian.

## 3. Rollback

```bash
dotnet ef database update 20261108041120_AddPracticeAttempts --project ... --startup-project ...
```

`Down()` xoá các index và cột trên. Dữ liệu tiêu đề, cờ đánh dấu, hạn nộp của các lượt đã làm sẽ mất; điểm và đáp án
vẫn còn ở các cột cũ.
