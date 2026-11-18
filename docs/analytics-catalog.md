# Analytics API — danh mục metric

API chỉ đọc để agent phân tích dữ liệu (SeasonedAgents) và admin trả lời câu hỏi kiểu data analyst.
Hiện chạy trong **Assessment**, **Classroom** (mục *Classroom — đã chạy*), **Identity** (mục *Identity — đã chạy*)
và **Content** (mục *Content — đã chạy*).

## Endpoint

Qua gateway thêm tiền tố `/assessment` (vd. `GET /assessment/api/v1/analytics/query`).

| Endpoint | Tham số | Ghi chú |
|---|---|---|
| `GET api/v1/analytics/catalog` | — | Metric, dimension, filter, giới hạn, metric planned (JSON). |
| `GET api/v1/analytics/query` | `metric` (bắt buộc), `by`, `from`, `to`, `test_id`, `part`, `section`, `difficulty`, `status`, `mode`, `limit` | Một metric, nhóm theo tối đa 2 dimension. |
| `GET api/v1/analytics/events` | `type` (bắt buộc), `from`, `to`, `cursor`, `limit` | Sự kiện ẩn danh, phân trang bằng cursor. |

- Quyền: policy `AnalyticsReader` = vai trò **Admin**. Token agent (`scope=agent.read`) chỉ được GET/HEAD.
- Mọi metric viết sẵn bằng EF Core LINQ; giá trị từ request chỉ chọn mục trong whitelist hoặc thành tham số
  có kiểu (ngày, GUID, enum). Không có SQL ghép chuỗi, không có tên cột lấy từ input.
- Sai metric / dimension / filter / giá trị → **400**, `errors` nêu tham số và các giá trị hợp lệ.
- Kết quả không bao giờ chứa tên, email, số điện thoại, mật khẩu, token hay user ID thô.

### Kết quả `query`

```json
{ "metric": "accuracy", "by": ["part"], "unit": "ratio", "countUnit": "answers",
  "from": "2026-06-17", "to": "2026-09-14",
  "rows": [ { "part": "Part5", "value": 0.7342, "n": 1830 }, { "part": "Part6", "value": null, "n": 0 } ],
  "suppressed": 1, "truncated": false, "generatedAt": "2026-09-14T08:00:00Z",
  "labels": { "<testId>": "ETS-2026-TEST-01" } }
```

- `value`: số (tỉ lệ làm tròn 4 chữ số, còn lại 2) hoặc `null` (bị ẩn hoặc không xác định, vd. mẫu số 0).
- `n`: số quan sát, đơn vị ở `countUnit`.
- **Ẩn nhóm nhỏ**: metric có `suppressed=true` (tính từ học viên) mà nhóm có **< 5 học viên khác nhau** →
  `value: null`, `n: 0`, cộng vào `suppressed`. Metric ngân hàng câu hỏi không ẩn.
- Không có `by` → luôn đúng một dòng. Metric có dimension bắt buộc (`question`, `test`, `practice_set`,
  `score_band`) tự thêm dimension đó; tổng vẫn tối đa 2.
- Sắp xếp: theo dimension (thời gian tăng dần, enum theo thứ tự số) hoặc theo giá trị với metric xếp hạng
  (nhóm bị ẩn xuống cuối). `labels`: mã đề / mã bộ luyện tập cho các ID trong `rows`.

### Giới hạn

| Mục | Giá trị |
|---|---|
| Khoảng ngày | mặc định `to` = hôm nay (UTC), `from` = `to` − 89 ngày; tối đa 366 ngày, tính cả hai đầu |
| `by` | tối đa 2 dimension |
| `limit` query | mặc định 100, tối đa 500 (`truncated=true` nếu còn dòng) |
| `limit` events | mặc định 200, tối đa 1000 |
| Thời gian chạy | 10 giây, quá hạn → 503 `analytics_timeout` |
| Số dòng nạp để tổng hợp | 250 000, vượt → 400 `analytics_range_too_large` |
| Ẩn nhóm nhỏ | < 5 học viên khác nhau |

## Dimension và filter

| Dimension | Giá trị |
|---|---|
| `day`, `week`, `month` | theo giờ bắt đầu lượt làm (UTC): `yyyy-MM-dd`, thứ Hai đầu tuần ISO `yyyy-MM-dd`, `yyyy-MM` |
| `test`, `practice_set`, `question` | ID (xem `labels`) |
| `mode` | FullTest, Practice, PartPractice |
| `status` | lượt làm: InProgress, Completed, Abandoned · nội dung: Draft, Active, Archived |
| `part`, `section`, `difficulty`, `kind` | Part1..Part7 · Listening/Reading · VeryEasy..VeryHard · Level/Topic/PartDrill |
| `score_band` | 0-99, 100-199, …, 900-990 |

Filter: `from`, `to` (`yyyy-MM-dd`), `test_id` (GUID), `part` (1–7 hoặc Part1..Part7), `section`,
`difficulty` (1–5 hoặc tên), `status`, `mode`. Enum không phân biệt hoa thường, bỏ qua `_` và `-`.

## Metric (Assessment — đã chạy)

Ký hiệu: **A** dims = day, week, month, test, mode, status · **S** dims = day, week, month, test, mode ·
**Q** dims = day, week, month, test, mode, part, section, difficulty.
**fA** = from, to, test_id, mode, status · **fS** = from, to, test_id, mode · **fQ** = from, to, test_id, mode, part, section, difficulty.
`*` = dimension bắt buộc. Cá nhân = tính theo từng học viên rồi gộp.

### Lượt làm bài (một dòng / lượt bắt đầu trong khoảng)

| Metric | Ý nghĩa | Đơn vị (n) | Dims | Filters | Hiển thị | Cá nhân |
|---|---|---|---|---|---|---|
| `attempt_count` | Số lượt làm bài | attempts (attempts) | A | fA | admin | không |
| `active_learners` | Số học viên khác nhau | learners (learners) | A | fA | admin | không |
| `attempts_per_learner` | Lượt trung bình / học viên | attempts/learner (learners) | A | fA | admin | có |
| `completion_rate` | Completed / tổng lượt | ratio (attempts) | S | fS | admin | không |
| `abandon_rate` | Abandoned / tổng lượt | ratio (attempts) | S | fS | admin | không |
| `avg_score` | Điểm tổng TB, lượt Completed | points (attempts) | S | fS | admin | không |
| `median_score` | Trung vị điểm tổng | points (attempts) | S | fS | admin | không |
| `avg_listening_score` | Điểm Listening TB | points (attempts) | S | fS | admin | không |
| `avg_reading_score` | Điểm Reading TB | points (attempts) | S | fS | admin | không |
| `score_distribution` | Số lượt Completed theo khoảng điểm | attempts (attempts) | score_band*, test, mode, month | fS | admin | không |
| `avg_duration` | Thời gian làm bài TB (lượt Completed có ghi) | seconds (attempts) | S | fS | admin | không |
| `returning_rate` | Tỉ lệ học viên hoạt động ≥ 2 ngày khác nhau | ratio (learners) | week, month, test, mode | fS | admin | có |
| `score_gain` | Điểm lượt Completed cuối − đầu (học viên có ≥ 2 lượt) | points (learners) | test, mode | fS | admin | có |
| `test_popularity` | Học viên khác nhau theo đề, giảm dần | learners (learners) | test*, mode | from, to, mode | admin | không |

### Nhóm học viên (theo ngày làm bài đầu tiên từ trước tới nay)

| Metric | Ý nghĩa | Đơn vị (n) | Dims | Filters | Hiển thị | Cá nhân |
|---|---|---|---|---|---|---|
| `new_learners` | Học viên có lượt đầu tiên rơi vào nhóm | learners (learners) | day, week, month | from, to | admin | có |
| `retention_7d` | Tỉ lệ quay lại trong 1–7 ngày sau ngày đầu; bỏ học viên chưa đủ 7 ngày | ratio (learners) | day, week, month | from, to | admin | có |

### Câu trả lời (của các lượt bắt đầu trong khoảng; giữ cả câu đã xoá mềm)

| Metric | Ý nghĩa | Đơn vị (n) | Dims | Filters | Hiển thị | Cá nhân |
|---|---|---|---|---|---|---|
| `answer_count` | Số câu đã chấm | answers (answers) | Q | fQ | teacher | không |
| `accuracy` | Đúng / đã chấm (bỏ trống = sai) | ratio (answers) | Q | fQ | teacher | không |
| `skip_rate` | Tỉ lệ bỏ trống | ratio (answers) | Q | fQ | teacher | không |
| `avg_time_per_question` | Giây TB mỗi câu (câu có ghi thời gian) | seconds (answers) | Q | fQ | teacher | không |
| `question_p_value` | Tỉ lệ đúng từng câu, khó nhất trước | ratio (answers) | question*, part, section, difficulty | fQ | teacher | không |
| `most_missed_questions` | Số lần sai từng câu, nhiều nhất trước | answers (answers) | question*, part, section, difficulty | fQ | teacher | không |
| `practice_set_accuracy` | Tỉ lệ đúng trên câu của từng bộ (câu thuộc nhiều bộ tính cho mỗi bộ), thấp nhất trước | ratio (answers) | practice_set* | from, to, mode, part | teacher | không |
| `practice_set_usage` | Học viên khác nhau đã làm câu của bộ, nhiều nhất trước | learners (learners) | practice_set* | from, to, mode, part | teacher | không |
| `question_exposure_rate` | Tỉ lệ câu Active được trả lời ≥ 1 lần (không ẩn) | ratio (questions) | part, section, difficulty | from, to, part, section, difficulty | teacher | không |

### Ngân hàng nội dung (hiện tại, bỏ mục đã xoá mềm; không ẩn)

| Metric | Ý nghĩa | Đơn vị (n) | Dims | Filters | Hiển thị |
|---|---|---|---|---|---|
| `question_count` | Số câu hỏi (`test` = null: câu chỉ thuộc ngân hàng) | questions | part, section, difficulty, status, test | part, section, difficulty, status, test_id | public |
| `passage_count` | Số đoạn / nhóm câu | passages | part, section, test | part, section, test_id | teacher |
| `test_count` | Số đề | tests | status | status | public |
| `practice_set_count` | Số bộ luyện tập | sets | part, section, kind, status | part, section, status | public |
| `practice_set_size` | Số câu mỗi bộ, lớn nhất trước | questions | practice_set* | part, status | teacher |
| `test_completeness` | Số câu hiện có / số câu chuẩn của đề, thiếu nhất trước | ratio (questions) | test* | status | teacher |

Nhãn `visibility` (admin / teacher / self / public) là thông tin để hub quyết định ai được xem; hiện tất cả
endpoint đều chỉ cho Admin.

## Classroom — đã chạy

Cùng hợp đồng với Assessment (catalog, dạng `rows`, `suppressed`, `truncated`, `labels`, giới hạn, 400 có `errors`,
503 `analytics_timeout`, 400 `analytics_range_too_large`), quyền `AnalyticsReader` = **Admin**. Không có endpoint `events`.
Qua gateway thêm tiền tố `/classroom` (vd. `GET /classroom/api/v1/analytics/query`).

| Endpoint | Tham số | Ghi chú |
|---|---|---|
| `GET api/v1/analytics/catalog` | — | Metric, dimension, filter, giới hạn, metric planned; `eventTypes` luôn rỗng. |
| `GET api/v1/analytics/query` | `metric` (bắt buộc), `by`, `from`, `to`, `status`, `class_id`, `limit` | Tham số khác (vd. `test_id`, `part`) bị bỏ qua. |

- Lớp đã xoá mềm (`Deleted`) không bao giờ được tính, kể cả ghi danh và phân công của lớp đó; `status=Deleted` → 400.
- Trạng thái ghi danh là trạng thái **hiện tại**: `Active` khi `Status = Active` và chưa có `WithdrawnAt`, còn lại là `Withdrawn`.
- `labels`: mã lớp (`Code`) cho các ID lớp trong `rows`. Không bao giờ có tên, email, ID học viên hay ID giáo viên.
- `teacher`: mã ẩn danh = 8 ký tự hex đầu của SHA-256(`"teacher:" + ID giáo viên`), không có nhãn. Đây là băm không khoá,
  chỉ để phân biệt các dòng; ai đã có danh sách ID giáo viên vẫn dò ngược được.
- Metric không theo khoảng ngày (`active_classes`, `enrollment_status_mix`, `teacher_load`) trả `from`/`to` = null và
  từ chối `from`/`to` (400).

| Dimension | Giá trị |
|---|---|
| `day`, `week`, `month` | `yyyy-MM-dd`, thứ Hai đầu tuần ISO `yyyy-MM-dd`, `yyyy-MM` (UTC); mốc thời gian ghi ở từng metric |
| `status` | lớp: Draft, Active, Completed, Archived · ghi danh: Active, Withdrawn |
| `class` | ID lớp (xem `labels`) |
| `teacher` | mã ẩn danh 8 hex |

Filter: `from`, `to` (`yyyy-MM-dd`), `status` (tên hoặc số của enum, không phân biệt hoa thường, bỏ qua `_` và `-`;
nghĩa theo metric: trạng thái lớp hoặc ghi danh), `class_id` (GUID).

| Metric | Ý nghĩa | Mốc thời gian | Đơn vị (n) | Dims | Filters | Hiển thị | Ẩn < 5 |
|---|---|---|---|---|---|---|---|
| `class_count` | Số lớp được tạo trong khoảng | `Class.CreatedAt` | classes (classes) | day, week, month, status | from, to, status (lớp) | admin | không |
| `active_classes` | Số lớp đang mở hiện tại: Draft (sắp mở) + Active (đang học); không theo khoảng | `Class.StartDate` (month; lớp chưa có ngày → `null`) | classes (classes) | status, month | — | admin | không |
| `class_enrollment` | Số lượt ghi danh tạo trong khoảng | `ClassEnrollment.EnrolledAt` | enrollments (enrollments) | day, week, month, status, class | from, to, status (ghi danh), class_id | admin | không |
| `enrollment_status_mix` | Số lượt ghi danh hiện có theo trạng thái; không theo khoảng | — | enrollments (enrollments) | status*, class | class_id | admin | không |
| `enrolled_learners` | Số học viên khác nhau có lượt ghi danh tạo trong khoảng | `EnrolledAt` | learners (learners) | day, week, month | from, to, status (ghi danh) | admin | có |
| `withdraw_rate` | Withdrawn / tổng lượt ghi danh của các lớp **bắt đầu** trong khoảng | `Class.StartDate` | ratio (enrollments) | month, class | from, to, class_id | admin | có (theo học viên khác nhau) |
| `class_size_avg` | Số ghi danh Active trung bình mỗi lớp, trên các lớp bắt đầu trong khoảng (lớp trống = 0) | `Class.StartDate` | learners/class (classes) | month | from, to | admin | có (theo học viên khác nhau) |
| `teacher_load` | Số lớp đang mở (Draft + Active) mỗi giáo viên đang được phân công, nhiều nhất trước | — (hiện tại) | classes (teachers) | teacher* | — | admin | không |

## Identity — đã chạy

Cùng hợp đồng với Assessment (dạng `rows`, `suppressed`, `truncated`, `labels`, giới hạn, 400 có `errors`,
503 `analytics_timeout`, 400 `analytics_range_too_large`), quyền `AnalyticsReader` = **Admin**. Không có endpoint `events`.
Qua gateway thêm tiền tố `/identity` (vd. `GET /identity/api/v1/analytics/query`).

| Endpoint | Tham số | Ghi chú |
|---|---|---|
| `GET api/v1/analytics/catalog` | — | `service: "identity"`, metric, dimension, filter, giới hạn (thêm `inactivityDays`). |
| `GET api/v1/analytics/query` | `metric` (bắt buộc), `by`, `from`, `to`, `role`, `status`, `provider`, `limit` | Tham số khác bị bỏ qua. |

- Kết quả không bao giờ có tên, email, số điện thoại hay ID tài khoản; `labels` luôn là `{}`.
- Vai trò và trạng thái là giá trị **hiện tại** của tài khoản (không lưu lịch sử đổi vai trò / trạng thái).
- Metric chụp tại một ngày (`total_accounts`, `inactive_accounts`) chỉ nhận `to` (mặc định hôm nay), trả `from = null`,
  `from` → 400. Metric hiện tại (`account_role_mix`, `account_status_mix`, `oauth_accounts`, `teacher_count`,
  `student_count`) trả `from`/`to` = null và từ chối cả hai.
- Ẩn nhóm < 5 **tài khoản khác nhau** chỉ áp cho `logins`, `login_users` (hoạt động cá nhân). Không có `by` mà không có
  dữ liệu → một dòng `value: 0`, không tính là ẩn.
- **Đăng nhập lấy từ refresh token** (`UserTokens`, loại `RefreshToken`). Mỗi lần đăng nhập mở một phiên = chuỗi token
  cùng `ExpiresAt` (làm mới phiên giữ nguyên hạn), nên `logins` = số phiên có token đầu tiên rơi vào khoảng; làm mới phiên
  không tính. `login_users` và `inactive_accounts` coi cả làm mới phiên là hoạt động.
- **Giới hạn dữ liệu**: token hết hạn bị xoá khi tài khoản đăng nhập lại (`EnforceSessionLimitAsync`), nên với tài khoản
  quay lại, lịch sử cũ hơn một vòng đời phiên (7 ngày) bị thiếu → `logins` / `login_users` của khoảng cũ có thể thấp hơn
  thực tế, `inactive_accounts` với `to` trong quá khứ có thể cao hơn. Số liệu gần hiện tại thì chính xác. Muốn đủ lịch sử
  cần một bảng nhật ký đăng nhập riêng (chưa có).

| Dimension | Giá trị |
|---|---|
| `day`, `week`, `month` | theo giờ tạo tài khoản / giờ đăng nhập (UTC): `yyyy-MM-dd`, thứ Hai đầu tuần ISO `yyyy-MM-dd`, `yyyy-MM` |
| `role` | User (học viên), Admin, Teacher |
| `status` | Active, Inactive, Suspended |
| `provider` | Google |

Filter: `from`, `to` (`yyyy-MM-dd`), `role` (tên hoặc số 1–3; `student` / `learner` = User), `status` (tên hoặc số 1–3),
`provider` (Google hoặc 1). Không phân biệt hoa thường, bỏ qua `_` và `-`.

| Metric | Ý nghĩa | Mốc thời gian | Đơn vị (n) | Dims | Filters | Hiển thị | Ẩn < 5 |
|---|---|---|---|---|---|---|---|
| `new_registrations` | Số tài khoản tạo trong khoảng, bỏ tài khoản đã xoá mềm | `User.CreatedAt` | accounts (accounts) | day, week, month, role | from, to, role | admin | không |
| `total_accounts` | Số tài khoản tồn tại tại ngày `to`: tạo trước hết ngày `to`, chưa xoá mềm tính tới lúc đó | ngày `to` | accounts (accounts) | role, status | to, role, status | admin | không |
| `account_role_mix` | Tài khoản hiện có (chưa xoá mềm) theo vai trò | — (hiện tại) | accounts (accounts) | role* | status | admin | không |
| `account_status_mix` | Tài khoản hiện có theo trạng thái | — (hiện tại) | accounts (accounts) | status* | role | admin | không |
| `oauth_accounts` | Tài khoản hiện có đã liên kết đăng nhập ngoài (mỗi tài khoản tính 1 lần / nhà cung cấp) | — (hiện tại) | accounts (accounts) | provider* | provider | admin | không |
| `teacher_count` | Tài khoản Teacher hiện có | — (hiện tại) | accounts (accounts) | status | — | admin | không |
| `student_count` | Tài khoản User (học viên) hiện có | — (hiện tại) | accounts (accounts) | status | — | admin | không |
| `logins` | Số lượt đăng nhập (phiên mới) trong khoảng | token đầu tiên của phiên | logins (logins) | day, week, month, role | from, to, role | admin | có |
| `login_users` | Số tài khoản khác nhau đăng nhập hoặc làm mới phiên trong khoảng | `UserToken.CreatedAt` | accounts (accounts) | day, week, month, role | from, to, role | admin | có |
| `inactive_accounts` | Tài khoản tồn tại tại ngày `to`, tạo cách đó ≥ 30 ngày và không có refresh token nào trong 30 ngày tính tới hết ngày `to` | ngày `to` | accounts (accounts) | role | to, role | admin | không |

## Content — đã chạy

Cùng hợp đồng với Assessment (catalog, dạng `rows`, `suppressed`, `truncated`, `labels`, giới hạn, 400 có `errors`,
503 `analytics_timeout`, 400 `analytics_range_too_large`), quyền `AnalyticsReader` = **Admin**. Không có endpoint `events`.
Qua gateway thêm tiền tố `/content` (vd. `GET /content/api/v1/analytics/query`).

| Endpoint | Tham số | Ghi chú |
|---|---|---|
| `GET api/v1/analytics/catalog` | — | Metric, dimension, filter, giới hạn, metric planned (không có `eventTypes`). |
| `GET api/v1/analytics/query` | `metric` (bắt buộc), `by`, `from`, `to`, `status`, `level`, `course_id`, `limit` | Tham số khác (vd. `test_id`, `part`) bị bỏ qua. |

- Truy vấn cố định trong `ContentAnalyticsRepository` (EF Core LINQ, `AsNoTracking`); bảng lớn (`WordReviewLogs`,
  `DictationProgress`, `GrammarQuestionResults`, `SavedWords`) được gom theo (học viên, ngày, …) ngay trong MySQL rồi mới
  gom theo dimension trong bộ nhớ. ID học viên chỉ dùng để đếm học viên khác nhau, không bao giờ ra response.
- `labels`: mã khoá học (`Course.Code`), mã khoá Dictation (`DictationCourse.Code`), slug chặng từ vựng, tiêu đề video
  **trong danh mục** (video ngoài danh mục không có nhãn), slug bài báo. Giá trị `document` đã là mã đọc được nên không có nhãn.
- Không có `by` mà không có dữ liệu → một dòng; metric tính từ học viên khi đó bị ẩn (0 học viên < 5).
- Metric nội dung (`course_count`, `lesson_count`) không theo khoảng ngày: trả `from`/`to` = null và từ chối `from`/`to` (400).
- **Giới hạn dữ liệu**: các bảng tiến độ chỉ lưu lần cuối (`DictationProgress.LastAttemptAt`, `GrammarQuestionResult.AnsweredAt`,
  `GrammarLessonProgress.UpdatedAt`, `VideoWatch.ProgressAt`) nên mỗi dòng tiến độ rơi vào ngày của lần cập nhật **cuối**;
  lịch sử cũ hơn bị dồn về ngày đó. `study_time` chỉ đo phiên học từ vựng (Flashcard / Quiz / Reel) — Dictation, ngữ pháp,
  video, báo không có đồng hồ phiên theo ngày trên server. `video_watch_time` là điểm xem xa nhất (% × thời lượng), không
  phải tổng thời gian xem thật.

| Dimension | Giá trị |
|---|---|
| `day`, `week`, `month` | `yyyy-MM-dd`, thứ Hai đầu tuần ISO `yyyy-MM-dd`, `yyyy-MM` (UTC); mốc thời gian ghi ở từng metric |
| `status` | khoá học: Draft, Published, Archived · bài học: Draft, Published |
| `level` | Beginner, Intermediate, Advanced |
| `exam_type` | ListeningReading, SpeakingWriting, FourSkills |
| `course` | GUID khoá học (`lesson_count`) hoặc số ID khoá Dictation (`dictation_*`), xem `labels` |
| `kind` | chế độ phiên học: Flashcard, Quiz, Reel |
| `source` | nguồn từ của phiên học: Set, Due, Folder, Library, Words, Mistakes |
| `stage` | GUID chặng lộ trình từ vựng (xem `labels`); từ thuộc nhiều chặng tính cho chặng sớm nhất; `null` = từ tự thêm |
| `document` | `topic-01`..`topic-44` (chủ đề ngữ pháp) hoặc `stage-01`..`stage-05` (test chặng) |
| `video` | ID video YouTube |
| `article` | GUID bài báo song ngữ (xem `labels`) |

Filter: `from`, `to` (`yyyy-MM-dd`), `status` (nghĩa theo metric: trạng thái khoá học hoặc bài học), `level` (1–3 hoặc tên),
`course_id` (GUID khoá học cho `lesson_count`, số nguyên dương ID khoá Dictation cho `dictation_*`). Enum không phân biệt hoa
thường, bỏ qua `_` và `-`.

| Metric | Ý nghĩa | Mốc thời gian | Đơn vị (n) | Dims | Filters | Hiển thị | Ẩn < 5 |
|---|---|---|---|---|---|---|---|
| `course_count` | Số khoá học hiện có | `Course.CreatedAt` (chỉ khi nhóm theo thời gian) | courses (courses) | day, week, month, status, level, exam_type | status, level | admin | không |
| `lesson_count` | Số bài học hiện có của từng khoá (qua module) | — (hiện tại) | lessons (lessons) | course*, status | course_id (GUID), status | admin | không |
| `study_sessions` | Số phiên học từ vựng bắt đầu trong khoảng | `StudySession.StartedAt` | sessions (sessions) | day, week, month, kind, source | from, to | admin | có |
| `study_time` | Tổng phút học: từ lúc bắt đầu tới lần trả lời / hoàn thành cuối, mỗi phiên tối đa 180 phút | `StartedAt` | minutes (learners) | day, week, month, kind, source | from, to | admin | có |
| `active_learners` | Học viên khác nhau có hoạt động: phiên học, ôn từ, lưu từ, Dictation, ngữ pháp (lượt nộp, câu trả lời, tiến độ chủ đề), video (mở, tiến độ), báo (mở, đọc xong) | ngày của từng hoạt động | learners (learners) | day, week, month | from, to | admin | có |
| `vocabulary_reviews` | Số lượt ôn từ (mọi điểm, kể cả Seen của Reel) | `WordReviewLog.ReviewedAt` | reviews (reviews) | day, week, month, stage | from, to | admin | có |
| `vocabulary_review_accuracy` | Good/Easy trên lượt ôn có chấm (bỏ Seen) | `ReviewedAt` | ratio (reviews) | day, week, month, stage | from, to | admin | có |
| `vocabulary_review_retention` | Tỉ lệ nhớ: lượt ôn từ đã có lịch (`IntervalBefore > 0`, bỏ Seen) không bị Forget | `ReviewedAt` | ratio (reviews) | day, week, month, stage | from, to | admin | có |
| `vocabulary_words_learned` | Cặp học viên × từ đạt mức thuộc (interval từ < 21 lên ≥ 21 ngày) trong khoảng; thuộc lại lần hai tính 1 | `ReviewedAt` của lượt vượt ngưỡng | words (learners) | day, week, month, stage | from, to | admin | có |
| `saved_words` | Số từ lưu vào sổ từ | `SavedWord.SavedAt` | words (words) | day, week, month | from, to | admin | có |
| `dictation_attempts` | Số câu Dictation (học viên × câu) có lần nộp cuối trong khoảng | `DictationProgress.LastAttemptAt` | items (items) | day, week, month, course | from, to, course_id (số) | admin | có |
| `dictation_completion` | Câu đã đạt (`Passed`) / câu đã làm | `LastAttemptAt` | ratio (items) | month, course | from, to, course_id (số) | admin | có |
| `grammar_attempts` | Số lượt nộp quick check / mini test / test chặng | `GrammarAttempt.CreatedAt` | attempts (attempts) | day, week, month, document | from, to | admin | có |
| `grammar_accuracy` | Đúng ngay lần đầu / câu đã trả lời (bỏ câu chỉ xem đáp án); `document` = chủ đề chính của câu | `GrammarQuestionResult.AnsweredAt` (lần trả lời cuối) | ratio (answers) | day, week, month, document | from, to | admin | có |
| `grammar_lesson_completion` | Chủ đề đã hoàn thành / chủ đề đã bắt đầu | `GrammarLessonProgress.UpdatedAt` | ratio (lessons) | month, document | from, to | admin | có |
| `video_watches` | Lượt mở video lần đầu (học viên × video), kể cả video đã xoá khỏi danh sách cá nhân | `VideoWatch.CreatedAt` | watches (watches) | day, week, month, video | from, to | admin | có |
| `video_watch_time` | Tổng giây đã xem = % xa nhất × thời lượng | `VideoWatch.ProgressAt` | seconds (learners) | day, week, month, video | from, to | admin | có |
| `news_reads` | Lượt mở bài báo song ngữ (học viên × bài) | `NewsReading.OpenedAt` | reads (reads) | day, week, month, article | from, to | admin | có |
| `news_read_completion` | Bài đã đọc xong / bài đã mở | `OpenedAt` | ratio (reads) | month, article | from, to | admin | có |

`*` = dimension bắt buộc. Tất cả metric Content là **admin** và không tính theo từng học viên (`personal = false`).
`course_lesson_progress` vẫn *Planned*: Content chưa có bảng tiến độ học viên theo bài học của khoá học.

## Events — chỉ Assessment (`GET api/v1/analytics/events`)

Sự kiện của các lượt **bắt đầu** trong `[from, to]`, sắp theo (giờ bắt đầu lượt, ID dòng); `nextCursor = null` ở trang cuối.
Trả `type`, `from`, `to`, `rows`, `nextCursor`, `generatedAt`. Trường null bị bỏ khỏi JSON.

| Trường | `attempt` | `answer` |
|---|---|---|
| `learner` | bí danh học viên | bí danh học viên |
| `attempt` | bí danh lượt làm | bí danh lượt làm (ghép với sự kiện attempt) |
| `testId`, `mode`, `startedAt` | có | có (của lượt làm) |
| `status`, `endedAt`, `durationSeconds` | có | — |
| `totalScore`, `listeningScore`, `readingScore`, `totalCorrect`, `totalQuestions` | có | — |
| `questionId`, `part`, `section`, `difficulty` | — | có |
| `answered`, `correct`, `timeSpentSeconds` | — | có |

- Bí danh = 16 ký tự hex đầu của HMAC-SHA256(khoá, user ID dạng `D`); lượt làm dùng `"attempt:" + ID`.
  Ổn định giữa các lần gọi, khác nhau giữa các môi trường có khoá khác.
- Không xuất đáp án đúng và lựa chọn của học viên (cùng với `correct` sẽ lộ đáp án).
- Khoá đọc từ cấu hình **`Analytics:PseudonymKey`** (biến môi trường `Analytics__PseudonymKey`), tối thiểu 32 ký tự.
  Thiếu khoá → **503** `analytics pseudonym key not configured`. Hệ thống không tự sinh hay ghi khoá.

## Planned (chưa có, `metric=` trả 400 "planned")

| Metric | Service | Nguồn dữ liệu | Hiển thị |
|---|---|---|---|
| `course_lesson_progress` | content | Course / Lesson — chưa có bảng tiến độ học viên theo bài học khoá học | teacher |
| `class_avg_score` | classroom | ClassEnrollment + lượt làm Assessment (hai database khác nhau, cần ghép qua bí danh hoặc sự kiện) | teacher |
| `my_score_trend` | assessment | lượt làm của chính học viên (token học viên) | self |

Metric planned mới nên dùng cùng
khung (catalog + whitelist + ẩn nhóm < 5 + bí danh HMAC cùng khoá để ghép học viên giữa các service).
