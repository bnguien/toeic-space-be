# Grammar Roadmap (Content Service, PBL-107)

Port of Studychill's "Lộ trình ngữ pháp": 5 stages, 44 topics (168 knowledge units, 51 sub-units, 176 diagrams),
655 vocabulary entries, 1,507 topic questions (quick checks per unit + one mini test per topic) and 5 stage tests
(335 questions). The server keeps every answer: it serves content without answers, grades, stores learner progress
(signed in) and returns signed attempt tickets to guests (graded, nothing stored). Out of scope by owner decision:
Lĩnh hội walkthrough, lecture videos, XP, community chat, "Hỏi bài".

Learners are identified by the JWT `sub`; there is no foreign key to Identity.

## Data model

Seven tables, all named `Grammar<Noun>`. Content documents are JSON (one row per topic and kind, never child tables);
per-learner data is one progress table, the question results that feed the mistake book, the attempts, and one state
row per learner. Stage-test progress and comment/review counters are computed from indexed rows instead of being
stored twice.

| Table | Natural key / indexes | Why it exists |
|---|---|---|
| `GrammarDocuments` | unique `(Kind, Key)` | The content. `Kind` = `Knowledge` / `Vocab` / `Exercises` (Key = topic `01`..`44`) or `StageTest` (Key = stage `01`..`05`): 137 rows. `Content` is the English JSON document (longtext so it stays byte-identical to `ContentHash`, SHA-256). Re-import compares hashes. |
| `GrammarLessonProgress` | unique `(LearnerId, Topic)` | One row per learner and topic: `TheoryConfirmedAt`, `VocabConfirmedAt`, `QuickCheckBest` (JSON `{unit: best}`), `MiniTestBest`, `AttemptCount`, `CompletedAt` (kept once reached), `Version` (concurrency token: two tabs). |
| `GrammarAttempts` | PK `Id` (bigint identity); index `(LearnerId, Stage)` | One submission of a block (`qc-<u>`, `mt`, `stage`): `Answers` (JSON as sent), `Results` (JSON `{questionId: correct}`), `FixedQuestionIds` (JSON, in-place fixes), `Score`/`Total` fixed at submission. Stage-test best / passed / attempts are computed from these rows (index `(LearnerId, Stage)`), so there is no stage-progress table. |
| `GrammarQuestionResults` | unique `(LearnerId, QuestionId)`; indexes `(LearnerId, LastCorrect, WrongCount)`, `(LearnerId, TrapCode, WrongCount)` | Learner × question: `FirstCorrect` (null = only revealed), `LastCorrect`, `Fixed`, `Revealed`, `WrongCount`, `UnitCode`, `TrapCode`, `Topic` (topic of the main unit). Source of the mistake book ("Sổ lỗi", review count) and the frequent traps; the indexes match those two queries. |
| `GrammarLearnerStates` | PK `LearnerId` | One row per learner: resume position (`PositionTopic`, `PositionPart`, `PositionAnchor`), page preferences (`DarkMode`, `RoadmapWidth`, `RoadmapCollapsed`) and known vocabulary words (`KnownWords` JSON `{topic: [word…]}`, ≤ 655 words). `Version` concurrency token. |
| `GrammarComments` | PK `Id` (bigint identity); indexes `(Target, ParentId, Id)`, `(LearnerId, Id)` | Comments on a topic document (`Target` = `topic-04:document`), one level of replies (`ParentId` = root), soft delete, `AuthorName` captured at write time, `LikeCount` / `ReplyCount` kept because the listing would otherwise aggregate per row. |
| `GrammarCommentLikes` | PK `(CommentId, LearnerId)`, FK → comment (cascade) | One like per learner and comment (a JSON set on the comment would lose concurrent likes). |

Rules:

1. Attempt scores are fixed at submission; in-place fixes (`retry`) never change score, best or passed.
2. A blank answer (null, empty string/array/object, an object whose values are all blank) scores 0 but does not count
   as answered: it never takes the "first answer" and never enters the mistake book.
3. Mistake book = questions answered wrong at least once whose latest answer is still wrong; a correct answer leaves it.
4. A topic is **completed** once theory and vocabulary are confirmed and the mini test is passed (`MiniTestBest ≥
   passMark`); completion is kept even if a later attempt scores lower.
5. A stage test unlocks when `completed × 5 ≥ topics × 4` (80% rounded up: 9 topics → 8). Guests are always locked.
6. Progress percentage = floor(parts done × 100 / (44 × 3)); parts = theory, vocabulary, mini test passed.
7. Documents are never deleted by an import; a file only adds or updates `(Kind, Key)` rows.

## Content mapping (Studychill → TOEIC Space)

`scripts/grammar_data_pipeline.py build` maps every Studychill key once; the database and the API only contain English
keys. Unknown keys fail the build. Question kinds (`mcq5`, `pair990`, `wordform`, `error`, `transform`, `order`,
`parse`, `match`, `judge`, `cloze6`), diagram kinds (`slot`, `branch`, `compare`, `decision`, `timeline`, `ladder`,
`family`, `formula`, `layers`, `matrix`) and diagram tones (`moss`, `lime`, `gold`, `soft`) are kept as identifiers.
Unit codes (`CĐ04.1`, `CĐ04.1#2` for traps) are data and kept.

| Studychill | English | Studychill | English |
|---|---|---|---|
| `cd` | `topic` | `chang` | `stage` |
| `ten`, `tieuDe` | `title` | `so` | `number` |
| `ma` | `code` | `phienBan` | `version` |
| `loai`, `kieu`, `dang` | `kind` | `mau` | `tone` |
| `nhan` | `label` | `viTri` | `position` |
| `mucTieu` | `goals` | `donVi` | `units` |
| `mucCon` | `subItems` | `yChinh` | `mainIdea` |
| `nhanDien` | `signals` | `congThuc` | `formula` |
| `viDu` (list / single) | `examples` / `example` | `en`, `vi` | `sentence`, `translation` |
| `chuThich` | `caption` | `ghi` | `note` |
| `bayToeic` | `toeicTraps` | `bay`, `vi_sao`, `cach_tranh` | `trap`, `why`, `avoid` |
| `soDo` | `diagram` | `nhinLaNho` | `memoryHook` |
| `meoChillo` | `tip` | `nhacLai` | `recall` |
| `tomTat` | `summary` | `o`, `to`, `tuyChon` | `cells`, `highlight`, `optional` |
| `goc`, `nhanh`, `con` | `root`, `branches`, `children` | `cot`, `hang`, `tieuChi` | `columns`, `rows`, `criterion` |
| `batDau`, `nut`, `co`, `khong` | `start`, `nodes`, `yes`, `no` | `moc`, `den`, `now` | `marks`, `end`, `now` |
| `truc`, `thap`, `cao`, `bac` | `axis`, `low`, `high`, `steps` | `thanhVien`, `hauTo` | `members`, `suffix` |
| `khoi`, `noiTruoc` | `blocks`, `joinBefore` | `loi`, `lop`, `phia`, `cauDay` | `core`, `layers`, `side`, `fullSentence` |
| `soTuMoi`, `tu`, `ipa`, `nghia` | `newWordCount`, `word`, `ipa`, `meaning` | `tuLoai`, `hoTu`, `loaiDanhTu` | `partOfSpeech`, `wordFamily`, `countability` |
| `capDo`, `chuDe` | `level`, `theme` | `dungChoNguPhap`, `laOnTap`, `cdGoc`, `ghiChuBay` | `usedInGrammar`, `isReview`, `sourceTopic`, `trapNote` |
| `quickChecks`, `miniTest`, `questions`, `subQuestions` | same | `nguongDat`, `thoiGianPhut`, `soCau` | `passMark`, `timeMinutes`, `questionCount` |
| `maDonVi`, `doKho` | `unitCodes`, `difficulty` | `de`, `luaChon`, `dapAn` | `prompt`, `options`, `answer` |
| `loiGiai` | `explanation` | `dauHieu`, `quyTac`, `nhoNhanh` | `signal`, `rule`, `quickNote` |
| `viSaoSai`, `phuongAn`, `lyDo` (item) | `whyWrong`, `option`, `reason` | `lyDo` (judge list), `lyDoDung` | `reasons`, `correctReason` |
| `danhSachChapNhan`, `suaThanh` | `acceptedAnswers`, `correction` | `gachChan`, `goiY`, `tuGoc` | `underlines`, `hint`, `baseWord` |
| `manh`, `phanDoan` | `pieces`, `segments` | `veTrai`, `vePhai` | `left`, `right` |
| `doanVan`, `loaiVanBan` | `passage`, `textType` | `dungNguPhap` | `grammaticalOptions` |
| `$schema`, `ghiChuBienSoan` | dropped | | |

Enum values: difficulty `de/vua/kho` → `easy/medium/hard`; judge answer `dung/sai` → `correct/incorrect`; cloze
blank kind `nguPhap/chenCau` → `grammar/sentence`; decision node kind `cauHoi/dapAn` → `question/answer`; timeline mark
kind `diem/khoang/lap` → `point/range/repeat`; layer side `truoc/sau/chen` → `before/after/inside`; text type
`thong-bao/quang-cao/bai-bao/thu/huong-dan` → `notice/advertisement/article/letter/instructions`; vocabulary theme
`van-phong/nhan-su/tai-chinh/mua-sam/du-lich/san-xuat/hoi-nghi/bat-dong-san/y-te/cong-nghe/giao-hang/nha-hang-khach-san`
→ `office/hr/finance/shopping/travel/manufacturing/conference/real-estate/health/technology/delivery/hospitality`;
countability `dem-duoc/khong-dem-duoc/ca-hai/chi-so-nhieu` → `countable/uncountable/both/plural-only`.

## Code map

```text
Content.API/Controllers
    GrammarController            topics/{topic}, stages/{stage}/test, attempts, retry/reveal, single questions (anonymous)
    LearnerGrammarController     me/* (any signed-in role)
    GrammarCommentsController    comments (read anonymous, write signed in)
    AdminGrammarController       admin/import (Admin)
    RateLimiting/GrammarRateLimits  per-user windows: submit 60, answer 240, reveal 120, comment 20 per minute
Content.Application
    Features/GrammarContent      GetGrammarTopic, GetGrammarStageTest
    Features/GrammarAttempts     SubmitTopicAttempt, SubmitStageAttempt, RetryAttemptAnswer, RevealAttemptAnswer,
                                 AnswerQuestion, RevealQuestion (+ GrammarAttemptResolver, GrammarSingleQuestions)
    Features/GrammarProgress     GetGrammarProgress, GetGrammarMistakes, GetGrammarTraps, GetKnownWords,
                                 GetGrammarPreferences, CompleteTopicPart, SaveStudyPosition, SetKnownWord,
                                 UpdateGrammarPreferences
    Features/GrammarComments     GetComments, GetCommentReplies, CreateComment, DeleteComment, LikeComment
    Features/GrammarImport       ImportGrammarContent
    Services/Grammar             GrammarContentBuilder (bundle + answer stripping), GrammarContentCache (singleton,
                                 dropped after import), GrammarContentProvider, GrammarProgressService,
                                 GrammarResultRecorder, GrammarAnswerGrading
    Models/Grammar               GrammarContentBundle, GrammarTopicContent, GrammarStageContent, GrammarBlock,
                                 GrammarQuestionInfo
    Interfaces                   IGrammarContentRepository, IGrammarLearningRepository, IGrammarCommentRepository,
                                 IGuestAttemptTokens
    DTOs/Grammar, Mappings/GrammarMappingExtensions
Content.Domain
    Entities                     GrammarDocument, GrammarLessonProgress, GrammarAttempt, GrammarQuestionResult,
                                 GrammarLearnerState, GrammarComment, GrammarCommentLike
    Rules/GrammarGrader          1:1 port of Studychill's grading (JS whitespace class, normalisation, accepted answers)
    Rules/GrammarUnlockRules     80% unlock, progress percentage, completion
    Exceptions/GrammarErrors
Content.Infrastructure
    Persistence/Configurations/GrammarConfiguration.cs, Repositories/Grammar*Repository.cs
    Services/Grammar/HmacGuestAttemptTokens (+ GrammarOptions)
scripts/grammar_data_pipeline.py, data/grammar/grammar-content.json.gz
```

## Grading

`GrammarGrader` ports `grammarGrading.js` / `grading.py` one to one: lower-case, curly quotes → straight, spaces
before `, . ; : ? !` removed, whitespace collapsed (JavaScript `\s` class), trailing `. ? !` dropped. A wrong JSON shape
is simply wrong; grading never throws. The unit test grades the canonical answer of all 1,842 questions.

| Kind | Answer value |
|---|---|
| `mcq5`, `pair990`, cloze6 blank | `"A"`..`"D"` (case-sensitive) |
| `wordform` | text, compared with `acceptedAnswers` (or `answer`) |
| `transform` | the whole sentence or the part written after `hint` |
| `error` | `{ "part": "B", "correction": "goes" }` (correction compared with `correction` + `acceptedAnswers`) |
| `judge` | `{ "verdict": "correct" \| "incorrect", "reason": "A".."C" }` |
| `order` | the sentence, or piece indexes `[2, 0, 1]` (each once); compared case-insensitively without commas |
| `parse` | labels in segment order `["S", "V", "O"]` |
| `match` | `{ "1": "C", "2": "A" }` or letters in left order `["C", "A"]` |

Limits: 150 answers per submission, 600 characters of JSON per value; question ids outside the block → 400.

## API reference

Gateway prefix `/content/api/v1/grammar`. JSON camelCase. Answers never leave the server except through
`reveal`. Topic = `01`..`44`, stage = `01`..`05`, block = `qc-<unit>` | `mt`.

| Method & route | Auth | Body → response |
|---|---|---|
| GET `topics/{topic}` | anonymous | `{topic, stage, title, version, knowledge, vocab, exercises}` without answer fields (`answer`, `acceptedAnswers`, `correction`, `explanation`, `grammaticalOptions`, `correctReason`; question `toeicTraps`). `ETag` = content version, `If-None-Match` → 304. The shared security headers keep `Cache-Control: no-store`; the FE caches 5 minutes in TanStack Query. |
| GET `stages/{stage}/test` | anonymous (token adds the status) | stage test without answers + `status {unlocked, best, passed, total, passMark, requirement {completed, required, total}}` |
| POST `topics/{topic}/attempts` | anonymous | `{block, answers: {questionId: value}}` → `{attemptId, results: {questionId: {correct, explanation?: {signal, rule}}}, score, total, passMark, best, saved, lesson?}` (guest: `attemptId = "g.…"`, `best = null`, `saved = false`) |
| POST `stages/{stage}/attempts` | signed in + unlocked | `{answers}` → as above with `stage` status; locked or guest → 403 `stage_locked` |
| POST `attempts/{attemptId}/retry` | anonymous / owner | `{questionId, value}` → `{correct, explanation?, saved}` |
| POST `attempts/{attemptId}/reveal` | anonymous / owner | `{questionId}` → `{questionId, answer, explanation {signal, rule, whyWrong[{option, reason}], trap, quickNote}, correction?, acceptedAnswers?, correctReason?, grammaticalOptions?, toeicTraps?}`; signed in: marks the question revealed |
| POST `questions/{questionId}/answer` | anonymous | `{value}` → `{correct, explanation?, saved}` (mistake book; no attempt row; stage questions only when unlocked) |
| POST `questions/{questionId}/reveal` | anonymous | → reveal shape (same lock rule) |
| GET `me/progress` | user | `{stats {completedTopics, reviewCount, progressPercent}, current {topic, part, anchor} \| null, lessons {topic: Lesson}, stages {stage: Stage}}` — `Lesson = {theoryDone, vocabDone, quickCheck {correct, total} \| null, quickCheckUnits {unit: {correct \| null, total}}, miniTest \| null, miniTestPassed, passMark, completed}` |
| POST `me/topics/{topic}/complete` | user | `{part: "theory" \| "vocab"}` → `{lesson}` (idempotent) |
| PUT `me/position` | user | `{topic, part: "theory" \| "vocab" \| "exercises", anchor ≤ 120}` → `{current}` |
| GET `me/known-words?topic=` / PUT `me/known-words` | user | `[word…]` (alphabetical) / `{topic, word, known}` → same (word must belong to the topic) |
| GET `me/mistakes` | user | `[{unitCode, unitTitle, topic, questions [{questionId, block, wrongCount, topic, stage}]}]` in unit-code order, ≤ 500 questions |
| GET `me/traps` | user | top 10 `[{trapCode, description, unitCode, wrongCount, questionCount}]` |
| GET / PUT `me/preferences` | user | `{darkMode: bool \| null, roadmapWidth: 300..720 \| null, roadmapCollapsed}` |
| GET `comments?target=topic-04:document&before=` | anonymous | `{items [CommentDto + replies (first 3)], next, total}` newest first, 20 per page |
| GET `comments/{id}/replies` | anonymous | `CommentDto[]` (≤ 200, oldest first) |
| POST `comments` | user | `{target, content (≤ 1500 after normalisation), parentId?, authorName?}` → 201 `CommentDto` |
| DELETE `comments/{id}` | owner or Admin | `{id, deleted}`; deleting a root hides its replies |
| POST `comments/{id}/like` | user | `{liked}` → `{id, liked, likes}` |
| POST `admin/import` | Admin | multipart bundle files → `{topics, stageTests, documentsCreated, documentsUpdated, unchanged, questions}` |

`CommentDto = {id, parentId, content, createdAt, likes, liked, replyCount, author {id, name}, mine, replies?}`.
The author name is the token's `name` claim when present; Identity currently issues only `sub`, `role`, `jti`, so the
FE sends the signed-in user's display name (`authorName`, ≤ 80 chars, default "Học viên"). Comment rules: 5 s cooldown
per learner, the same text on the same target within 2 minutes is rejected, replies to a reply attach to its root.

Guest attempt tickets: `g.` + base64url(payload) + `.` + HMAC-SHA256, valid 6 hours. Configure `Grammar:GuestTokenKey`
(base64, ≥ 32 bytes; env `Grammar__GuestTokenKey`) when the Content API runs on several instances; without it each
process uses a random key and tickets end on restart. A guest ticket sent with a token, a bad/expired ticket, or
another learner's attempt id → 404 `grammar_attempt_not_found`.

## Error codes

| Code | Status | When |
|---|---|---|
| `validation_error` | 400 | Bad topic/block/part/word, unknown question in a block, too many / too long answers, comment rules |
| `invalid_import_file` | 400 | Unreadable bundle, wrong `schemaVersion`, missing knowledge/vocab/exercises, duplicate question id |
| `invalid_subject` | 401 | Token without a valid `sub` |
| `stage_locked` | 403 | Stage test (or one of its questions) before 80% of the stage's topics are completed; always for guests |
| `grammar_comment_forbidden` | 403 | Deleting another learner's comment |
| `grammar_topic_not_found` / `grammar_stage_not_found` | 404 | Unknown topic / stage |
| `grammar_attempt_not_found` | 404 | Unknown, foreign or expired attempt / ticket |
| `grammar_question_not_found` | 404 | Unknown question id (single question endpoints) |
| `grammar_comment_not_found` | 404 | Comment deleted or unknown |
| `grammar_content_unavailable` | 503 | Nothing imported yet |
| `rate_limited` | 429 | Grammar rate limit reached (`Retry-After`) |

## Importing the content

1. Snapshot (read-only, the server is never written): `python scripts/grammar_data_pipeline.py export` — streams
   `tar` of `cd-*/{knowledge,vocab,tests}.json` and `chang-*/test.json` from `/root/Studychill/content/grammar` into
   `data/source/studychill_grammar_<date>/` (gitignored).
2. Build: `python scripts/grammar_data_pipeline.py build` → `data/grammar/grammar-content.json.gz` (0.58 MB gzip,
   2.38 MB JSON; committed). The build maps keys/values (table above), fails on unknown keys or duplicate question ids
   and prints the counts (44 topics, 168 units, 51 sub-units, 176 diagrams, 655 vocabulary entries, 1,507 topic
   questions, 5 stage tests, 335 stage questions).
3. Apply the migration, then import (idempotent: a second run reports `unchanged: 137`):

```bash
TOKEN=<admin access token>
curl -X POST http://localhost:5050/content/api/v1/grammar/admin/import \
  -H "Authorization: Bearer $TOKEN" -F "files=@data/grammar/grammar-content.json.gz"
```

The gzip bundle (0.58 MB) passes the gateway's 2 MB body limit. The importer also accepts plain `.json` bundles
(up to 5 files, 10 MB each, 60 MB uncompressed) and invalidates the in-memory content cache after saving.
