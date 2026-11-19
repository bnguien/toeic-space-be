# Practice & mock exam attempts (Assessment Service)

Learners take a full TOEIC test as a timed mock exam ("Thi thử"), practise chosen parts of a test, or practise a
practice set ("Góc luyện tập"). Every attempt works on an immutable snapshot of the content taken at start; the server
keeps the answer key and only reveals it per checked question (practice) or after submission. Behaviour, constants
and scoring are ported from Studychill (`backend/apps/learning/exam`, `services/grading.py`); StudyChill-only parts
(XP, bot guard / ticket envelope, daily reveal budget) are not.

Frontend contract: spec v1 of 2026-11-18 (section 2). Migration: [`20261118_AddExamAttemptFlow`](migrations/20261118_AddExamAttemptFlow.md).

## Modes

| Mode | Source | Timer | Answers revealed |
|---|---|---|---|
| `FullTest` | Test, always Parts 1–7 | `deadline = start + DurationMinutes` (120 min); answers accepted until deadline + 30 s; auto-submitted by the worker after deadline + 60 s | after submit only; `check` → 403 |
| `Practice` (test) | Test + chosen parts | none | per group via `check`, everything after submit |
| `Practice` (practice set) | Practice set: `All`, `Continue`, `Random`, `Mistakes` | none | same as above |
| Review | any completed attempt | — | everything |

"Luyện lại câu sai" = `POST attempts/{id}/retake {scope:"Wrong"}`: a new Practice attempt with the wrong and
unanswered questions (whole passage groups), copied from the original snapshot. "Làm lại đề" = start with
`restart: true` (the in-progress attempt becomes `Abandoned`).

## Code map

```text
Assessment.API/Controllers
    AttemptsController            tests/{id}/attempts(+/active), practice-sets/{id}/attempts(+/active),
                                  attempts, attempts/{id} (GET, DELETE), resume, answers, flags, check, submit,
                                  result, review, retake
    PracticeProgressController    me/practice/tests-progress, me/practice/sets-progress, me/practice/stats,
                                  practice-sets/{id}/my-questions
Assessment.API/Contracts          Start*AttemptRequest, SaveAnswerRequest, SetQuestionFlagRequest, CheckAnswersRequest,
                                  RetakeAttemptRequest
Assessment.Application
    Features/Attempts/Commands    StartTestAttempt, StartPracticeSetAttempt, RetakeAttempt, ResumeAttempt, SaveAnswer,
                                  SetQuestionFlag, CheckAnswers, SubmitAttempt, DiscardAttempt, CleanupAttempts
    Features/Attempts/Queries     GetAttempt, GetAttemptReview, GetAttemptResult, GetActiveTestAttempt,
                                  GetActivePracticeSetAttempt, GetAttempts, GetTestsProgress, GetPracticeSetsProgress,
                                  GetPracticeSetQuestionStatuses, GetPracticeStats
    DTOs/Attempts                 AttemptDto, AttemptQuestionDto, AttemptPassageDto, RevealDto, AttemptResultDto, …
    Models/Attempts               snapshot payloads (AttemptSnapshotDocument, QuestionSnapshot, PassageSnapshot,
                                  SetSnapshot, AttemptSourceSnapshot), AttemptContent, NewAttempt, AttemptCleanupResult
    Services/Attempts             AttemptAccess (ownership, fence, expiry, delete/abandon), AttemptSourceReader,
                                  AttemptSnapshotFactory, AttemptContentReader, AttemptDocumentFactory (leak rules),
                                  AttemptDocumentReader, AnswerHistoryReader, AttemptScoreCalculator, AttemptGrader,
                                  AttemptResultFactory, PracticeSelectionPlanner, AttemptSummaryReader, AttemptTitles,
                                  AttemptParts
    Interfaces/Storage/IAttemptMediaStore, Common/Exceptions/PracticeMediaException
Assessment.Domain
    Rules/ToeicScoreConversion (+ CefrBand), Enums: AttemptSourceKind, PracticeSelection, RetakeScope,
    QuestionProgressStatus, TestProgressStatus; ErrorCodes
Assessment.Infrastructure
    Services/Attempts/RetainedAttemptMediaStore (content-addressed R2 copies), PracticeAttemptCleanupWorker (every 60 s)
```

The former `Application/Attempts` (one handler class for seven requests + contracts) was split into these use cases
with the same behaviour: generation fence, revision CAS, mutation idempotency and `FOR UPDATE` locks.

## Endpoints

All under `/assessment/api/v1`, `[Authorize(Roles = "User")]`, `Cache-Control: no-store`, JSON camelCase, enums as
strings, parts as integers 1–7. Nullable members of the attempt DTOs are always serialized (`null`, never omitted).

| Method & path | Body | Returns |
|---|---|---|
| `POST tests/{testId}/attempts` | `{ mode: "FullTest"\|"Practice", parts?: int[], restart?: bool }` | `AttemptDto` |
| `GET tests/{testId}/attempts/active` | — | `{ attemptId, mode, parts, answeredCount, totalQuestions, deadline, serverNow }` |
| `POST practice-sets/{id}/attempts` | `{ expectedGeneration, selection?: "All"\|"Continue"\|"Random"\|"Mistakes", count?: 5..200, restart?: bool }` | `AttemptDto` |
| `GET practice-sets/{id}/attempts/active` | — | `{ generation, attemptId }` |
| `GET attempts/{id}` | — | `AttemptDto` (owner only, any status) |
| `POST attempts/{id}/resume` | — | `AttemptDto` (refreshes activity) |
| `PUT attempts/{id}/answers/{questionId}` | `{ answer, expectedRevision, mutationId, timeSpentSeconds?: 0..3600 }` | `{ questionId, userAnswer, revision, lastActivityAt, timeSpentSeconds }` |
| `PUT attempts/{id}/flags/{questionId}` | `{ flagged }` | `{ questionId, flagged }` |
| `POST attempts/{id}/check` | `{ questionIds: Guid[1..60] }` | `RevealDto` |
| `POST attempts/{id}/submit` | — | `AttemptResultDto` |
| `GET attempts/{id}/result` | — | `AttemptResultDto` |
| `GET attempts/{id}/review` | — | `AttemptDto` with everything revealed |
| `POST attempts/{id}/retake` | `{ scope: "Wrong"\|"All" }` | `AttemptDto` (new Practice attempt) |
| `DELETE attempts/{id}` | — | 204 (deletes an in-progress attempt) |
| `GET attempts?mode=&kind=&sourceId=&status=&page=&pageSize=` | — | `PaginatedResult<AttemptSummaryDto>` newest first |
| `GET me/practice/tests-progress` | — | per touched test: status, active attempt, counts, best total, last completed |
| `GET me/practice/sets-progress?part=` | — | per published set: total / answered / correct / wrong questions, active attempt |
| `GET practice-sets/{id}/my-questions` | — | per set question: `Unanswered`/`Correct`/`Wrong`, last graded time |
| `GET me/practice/stats` | — | tests completed/total, answered, correct rate, 7 parts, last 20 FullTest scores, 8 recent |

`questionId` in answers/flags/check/reveal is the **snapshot** question id (`AttemptQuestionDto.id`);
`sourceQuestionId` is the bank question.

### Errors (`code` in Problem Details)

| HTTP | Code | When |
|---|---|---|
| 400 | `validation_error` | invalid body (mode, parts, count, option for the part, question not in the attempt…) |
| 403 | `check_not_allowed` | `check` on a FullTest |
| 403 | `forbidden` | caller is not a learner (content managers do not take attempts) |
| 404 | `attempt_unavailable` | unknown / other user's / expired attempt |
| 404 | `not_found` | test or practice set not published |
| 409 | `attempt_not_active` | mutation on a completed or abandoned attempt |
| 409 | `attempt_time_up` | answer or flag on a FullTest after deadline + 30 s |
| 409 | `answer_locked` | answer change on a checked question |
| 409 | `answer_revision_conflict` / `mutation_conflict` | stale `expectedRevision` / reused `mutationId` with another answer |
| 409 | `attempt_not_completed` | result, review or retake before submission |
| 409 | `nothing_to_practice` | empty selection (no mistakes, everything answered correctly, no questions in the parts) |
| 409 | `stale_start_generation` | practice-set start with an old `expectedGeneration` |
| 409 | `concurrency_conflict` | the source changed while media were being prepared — start again |
| 409 | `media_preservation_failed` (+ `reason`, `sourceReference`) | a source media file could not be retained |
| 409 | `legacy_attempt_unsupported` | attempt row without a snapshot |

## Rules

**Snapshot and media.** Start reads the source, preserves its media outside database locks
(`IAttemptMediaStore.PreserveAsync`: content-addressed R2 copies served by `practice-media/{sha256}`), then — inside
the per-learner/source fence — locks the test or set row, its questions and passages (`FOR UPDATE`, sorted ids),
re-reads the source and compares it with the first read before writing `ToeicAttemptSnapshots` (source metadata +
passages) and one `ToeicAttemptQuestions` row per question (payload with answer key, explanation, transcript).
Grading, reveal, review and retake only use the snapshot. A practice-set selection preserves only the media of the
selected questions. The total media budget is 75 s for up to 50 files, plus one second per extra file (max 240 s);
each transfer keeps its own 30 s deadline. Measured locally: a cold full test (92 files) 19.7 s, warm 2.4–5 s.

**Lifecycle fence.** Every mutation runs in a READ COMMITTED transaction that first locks the
`PracticeAttemptStates` row of (learner, source id) — a test id or a practice set id. One in-progress attempt per
learner and source is enforced by the unique `(UserId, ActivePracticeSetId)` and `(UserId, ActiveTestId)` indexes.
Starting again returns the in-progress attempt (a test start refreshes its activity; a practice-set retry does not,
as before); `restart` abandons it first. Deleting or abandoning increments the fence generation, so a practice-set
start with an old `expectedGeneration` is rejected.

**Expiry.** Practice attempts inactive for 7 days are deleted (on access, or by the worker). FullTest attempts never
expire by inactivity; the worker submits them 60 s after the deadline. `expiresAt` = last activity + 7 days.

**Leak rules while not completed** (`AttemptDocumentFactory`). The document never contains the answer key,
explanations, transcripts or translations outside `reveal`; `reveal` only holds checked questions (+ their
passages). Part 1/2 `questionText` is null (the prompt is audio). Part 1–4 passage `content` is null (Part 3/4
content is the script); Part 6/7 `content` is the English part before `<translation_split>`. Once completed, the
question text and the English passage content (including Part 3/4 scripts) are shown, and `reveal` has every
question: correct answer, the learner's answer, `isCorrect` (null = skipped), explanation, the full stored transcript
and up to 5 graded answers to the same bank question in the learner's other attempts.

`GET practice-sets/{id}/content` now strips answers, explanations, transcripts, translations and listening scripts
for callers that cannot manage content (`includesAnswers: false`); content managers keep the full preview. The
content cache prefix moved to `assessment:content:v3` so learner entries cached with answers are never served.

**Answers.** Saving checks, in order: time-up (FullTest), status, the question belongs to the attempt, option valid
for the part (Part 2 has A–C), mutation replay (same `mutationId` → same response, applied once), `answer_locked`,
revision CAS. `timeSpentSeconds` is added to the question's total. Flags create the answer row if needed without
changing its revision. `check` (Practice only) sets `CheckedAt`, grades and locks the questions; checking again
keeps the first `CheckedAt` (idempotent). An unanswered checked question is graded as skipped.

**Practice-set selection** (`PracticeSelectionPlanner`). Units are passage groups (Part 3/4/6/7) or single questions
in set order; units are added until `count` (default 20) is reached, so the count is a soft target.
`Continue` = units with a never-answered question in set order; `Random` = never-answered units first (shuffled),
then the rest; `Mistakes` = units with a question whose latest graded answer is wrong (no count → all). "Answered"
means any saved choice in any attempt; "graded" means an answer of a submitted attempt or a checked answer.
Titles: `Part 5 · 20 câu ngẫu nhiên · 18/11/2026` (Vietnam date), `Part 3 · Ôn câu sai`, `Part 1 · Học tiếp`; `All`
keeps the set title. Test attempts are named after the test (`… · Part 3, 5` for practice).

**Submit** grades from the snapshot key, stores `TotalCorrect`, `ListeningCorrect`, `ReadingCorrect`,
`ListeningScore`, `ReadingScore`, `TotalScore` (0 when not scaled), `EndTime`, `DurationSeconds` (FullTest:
`min(now, deadline) − start`; Practice: sum of `timeSpentSeconds`, or wall clock when none was reported) and
completes the attempt. Submitting a completed attempt returns the same result. A FullTest submission publishes
`ExamAttemptSubmittedIntegrationEvent` through the transactional outbox (same transaction).

## Scoring (`Domain/Rules/ToeicScoreConversion`)

Per section: `ratio = correct / total`; `ratio ≤ 0.05 → 5`, `ratio ≥ 0.96 → 495`, otherwise
`round((ratio × 490 + 5) / 5) × 5` (banker's rounding like Python) clamped to 5–495. Scaled scores are only reported
for a test attempt that covers the whole section (Parts 1–4 for Listening, 5–7 for Reading); the total (L + R) and
the CEFR band only when both are covered. CEFR from the total: C2 ≥ 905 "Thành thạo cao cấp", C1 ≥ 785 "Vững vàng
cao cấp", B2 ≥ 605 "Trung cấp trên", B1 ≥ 405 "Trung cấp cơ bản", A2 ≥ 255 "Sơ cấp", else A1 "Cơ bản".

## Verification

- Unit tests: `tests/UnitTests/ToeicSpace.Assessment.Application.UnitTests/Attempts` (84 cases: conversion table
  checked against `grading.py`, start/resume/restart, leak rules, save/flag/check rules, grading and idempotency,
  late submission, retake, selection, auto-submit and expiry, history/progress/stats).
- Opt-in MySQL suite (`PBL26_MYSQL_CONNECTION`) passes with the new model.
- Local smoke test (worktree API on a spare port, cloned database, throwaway signing key, in-memory bus): 44/44
  checks covering FullTest start/answers/flag/check 403/submit/result/review, Part 5 practice + check + retake,
  practice-set Random/Mistakes/Continue/All, history, progress, stats, ownership and the worker's auto-submit.

## Known data issues (not code defects)

- `PASS-TOEIC-TEST-01` and `-02` use 6 SVG chart images (Part 3/4); the media store only accepts raster images and
  audio, so a FullTest (or Part 3/4 practice) on these two tests fails with `media_preservation_failed`
  (`reason: unrecognized_media_content`). 18 practice-set questions reference the same files. Convert the charts to
  PNG in the content (preferred) or add sanitized SVG support to the media store.
- Some Part 3/4 practice sets still contain a source file that returns 404 (see
  [`PRACTICE_MEDIA_STATIC_REVIEW.md`](PRACTICE_MEDIA_STATIC_REVIEW.md)); `All` sessions on those sets fail, smaller
  `Continue`/`Random` sessions usually avoid the file.
