# 📄 Migration Guide: 20260914082511_InitialAssessmentSchema

> **Ngày tạo:** 14/09/2026
> **Dịch vụ áp dụng:** `ToeicSpace.Assessment` (`AssessmentDbContext`)
> **Database:** `toeic_space_assessment`
> **Thay thế:** migration `20260913120159_InitialAssessmentSchema` (chưa từng được commit, đã bị xoá)

---

## 1. Tổng quan

Migration khởi tạo toàn bộ schema của Assessment Service, tách rõ **đề thi** và **bài luyện tập**:

| Bảng | Entity | Mục đích |
| :--- | :--- | :--- |
| `ToeicTests` | `ToeicTest` | Chỉ chứa **đề thi đầy đủ** 200 câu / 120 phút (ETS 2026, Crack TOEIC Vol 1, Pass TOEIC) |
| `ToeicPassages` | `ToeicPassage` | Nhóm câu hỏi Part 3/4/6/7. `TestId = NULL` nghĩa là thuộc ngân hàng luyện tập |
| `ToeicQuestions` | `ToeicQuestion` | Ngân hàng câu hỏi. Câu thuộc đề có `TestId` + `QuestionNumber`; câu chỉ dùng cho luyện tập có `TestId = NULL` |
| `ToeicPracticeSets` | `ToeicPracticeSet` | Bộ luyện tập theo **cấp độ** (`Level`), **chủ đề** (`Topic`) hoặc **luyện Part** (`PartDrill`) |
| `ToeicPracticeSetItems` | `ToeicPracticeSetItem` | Liên kết có thứ tự giữa bộ luyện tập và câu hỏi (một câu có thể nằm trong nhiều bộ) |
| `ToeicAttempts` / `ToeicAttemptAnswers` | | Lịch sử làm bài (đáp án đúng được snapshot tại thời điểm nộp) |
| `OutboxMessages` | | Transactional outbox cho integration event |

---

## 2. Quyết định thiết kế

1. **Test ra Test, Question ra Question**
   - Dữ liệu Studychill lưu 97 "test" nhưng chỉ 26 là đề thật; 71 còn lại là bộ luyện tập theo Part.
   - 20 bộ `p*-t*` trùng 100% với `p*-lv*` nên không import. Câu hỏi trùng nhau giữa các bộ được gộp thành một câu duy nhất.
2. **Quy tắc ETS nằm ở Domain** (`Domain/Rules/ToeicPartRules.cs`): section theo Part, Part 2 chỉ có A–C, Part 3/4/6/7 bắt buộc có passage, Part 1/2/5 không có passage, Part 1 bắt buộc có ảnh, khoảng số câu theo Part (1–6, 7–31, 32–70, 71–100, 101–130, 131–146, 147–200).
3. **Soft delete** bằng `DeletedAt` + global query filter trên Test, Passage, Question, PracticeSet.
4. **Chống ghi đè đồng thời**: `ToeicQuestions.Version` là concurrency token; API yêu cầu `expectedVersion` khi cập nhật.
5. **Toàn vẹn tham chiếu**:
   - `ToeicQuestions → ToeicPassages`: `Restrict` (không xoá cứng passage còn câu hỏi).
   - `ToeicQuestions/ToeicPassages → ToeicTests`: `SetNull` (xoá đề không làm mất câu hỏi của ngân hàng).
   - `ToeicPracticeSetItems → ToeicQuestions`: `Restrict`; `→ ToeicPracticeSets`: `Cascade`.
6. **Enum**: enum có thứ tự (`Part`, `DifficultyLevel`) lưu số; enum phân loại (`Section`, `Status`, `CorrectAnswer`, `Kind`) lưu chuỗi.
7. **Tìm kiếm**: FULLTEXT index `FT_ToeicQuestions_Content` trên `(QuestionText, Explanation, Transcript)`. Trên 13k câu, truy vấn mất 2–18 ms (so với khoảng 650 ms khi dùng `LIKE`).

---

## 3. Chạy migration

```bash
dotnet ef database update \
  --context AssessmentDbContext \
  --project src/Services/Assessment/ToeicSpace.Assessment.Infrastructure \
  --startup-project src/Services/Assessment/ToeicSpace.Assessment.API
```

Hoặc chạy script idempotent: [`20260914_InitialAssessmentSchema.sql`](20260914_InitialAssessmentSchema.sql).

> Nếu database local đã áp migration cũ `20260913120159_InitialAssessmentSchema`, hãy tạo database mới (hoặc xoá database cũ sau khi backup) rồi chạy lại lệnh trên. Hai migration không tương thích.

---

## 4. Nạp dữ liệu từ Studychill

Pipeline: [`scripts/toeic_data_pipeline.py`](../../scripts/toeic_data_pipeline.py)

Nguồn trong một snapshot (`data/source/studychill_YYYYMMDD/`):

| File | Nội dung |
|---|---|
| `studychill_toeic_mock.json.gz` | Bảng `toeic_mock_*` (đề, passage, câu hỏi) |
| `studychill_listening_cards.tar.gz` | Card luyện nghe `public/data/listening/*.json`, dùng để sửa URL audio/ảnh |
| `studychill_reading_exams.tar.gz` | Bộ Part 5/6 đóng gói trong frontend (`src/data/exams/toeic_part5_*`, `toeic_part6_*`). Studychill đọc thẳng các file này (`LOCAL_READING_EXAMS`), không qua database, nên Part 5 level 1–5, Part 5 tập 2 và Part 6 level 1–5 chỉ có ở đây |
| `studychill_dautoeic_reading.json.gz` | Bản trích của `data/dautoeic_raw/mock_test_questions.json` (chỉ giữ trường nhận dạng + `explanation_vi`, `explanation_en`, `dich_nghia`, `tu_vung`, `dich_nghia_dap_an`). Database Studychill không có lời giải cho đề Crack TOEIC / Pass TOEIC, nguồn thô thì có |

Snapshot `studychill_20260913` lấy file thứ ba từ repo Studychill local (commit `83f637f`), vì lúc export chưa có bước này.

```bash
# 1. Snapshot chỉ-đọc qua SSH (host alias "studychill" trong ~/.ssh/config)
python scripts/toeic_data_pipeline.py export

# 2. Phân loại, làm sạch, gộp trùng -> data/seeds/toeic_assessment_seed.json.gz + báo cáo
python scripts/toeic_data_pipeline.py build --source data/source/studychill_20260913

# 3. Nạp vào database đã chạy migration
MYSQL_PASS=... python scripts/toeic_data_pipeline.py load --database toeic_space_assessment
```

Kết quả với snapshot `studychill_20260913` (chi tiết: [`data/seeds/toeic_assessment_import_report.md`](../../data/seeds/toeic_assessment_import_report.md)):

| | Studychill | TOEIC Space |
|---|---:|---:|
| Đề thi đầy đủ | 26 | 26 (Active) |
| Bộ luyện tập | 71 trong database + 11 file JSON | 62 (33 Active, 29 Draft) |
| Passage | 7,716 + 352 | 3,177 |
| Câu hỏi | 30,572 + 4,255 | 16,488 (5,200 thuộc đề, 11,288 ngân hàng) |

Pipeline sửa các lỗi của script cũ:
- Lấy lại nội dung câu hỏi (cột nguồn là `question_text`).
- Lấy lại `duration_minutes`, `is_active` và `passage_type`.
- Bỏ nhãn rác của Part 1/2, placeholder `[Chỗ trống N]` của Part 6 và tiền tố "(A)" trong đáp án.
- Gỡ các passage chỉ bọc một câu hỏi của Part 1/2/5.
- Bộ JSON Part 5/6: bản dịch đoạn văn được nối vào `Content` sau `<translation_split>`; bộ Part 5 tập 2 lưu lời giải ở trường `tip`.
- Lời giải trống của đề Crack TOEIC / Pass TOEIC (1.134 câu Part 5/6/7) được dựng từ nguồn thô dautoeic. 3 câu không nguồn nào có (đề đã được viết lại thành bản khác nhưng lời giải cũ không cập nhật theo) được viết tay trong [`data/manual/explanations.json`](../../data/manual/explanations.json) — file này khớp theo nội dung câu hỏi + đáp án, nếu đề bị sửa thì build báo trong mục Issues thay vì áp lời giải cũ. Kết quả: **16.488/16.488 câu đều có lời giải**.
- Kiểm tra HTML nguy hiểm dùng cùng quy tắc với `RichTextRules` của API.

Còn **2.424 câu nghe** không có audio xác minh được: nguồn chỉ lưu tên file tương đối như `65-67.mp3`, trùng giữa hàng chục đề. Các câu này được giữ ở trạng thái `Draft`, và các bộ luyện tập chứa chúng cũng ở `Draft`, cho đến khi bổ sung media.

---

## 5. Kiểm tra sau khi nạp

```sql
-- 26 đề, mỗi đề đúng 200 câu và đúng số câu theo Part
SELECT t.Code, COUNT(*) AS Questions
FROM ToeicTests t JOIN ToeicQuestions q ON q.TestId = t.Id
GROUP BY t.Id
HAVING COUNT(*) <> 200;             -- kỳ vọng: 0 dòng

-- Không có câu Part 3/4/6/7 thiếu passage
SELECT COUNT(*) FROM ToeicQuestions WHERE Part IN (3,4,6,7) AND PassageId IS NULL;   -- 0
```

Smoke test API: `python scripts/test_assessment_api.py` (đặt `ASSESSMENT_API_URL`; token ES256 dev được ký bằng private key của Identity trong user-secrets, xem [`20260917_AddRefreshTokenHashIndex.md`](20260917_AddRefreshTokenHashIndex.md)).
