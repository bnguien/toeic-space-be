# Vocabulary Learning & Progress (Content Service, PBL-75)

## Responsibility and code map

Content owns the TOEIC roadmap vocabulary (5 stages, 100 lessons of up to 50 words, 4,954 words), every learner's
spaced-repetition progress, each learner's library ("Từ vựng của tôi": folders, saved official words and private
custom words), server-side study sessions, personal preferences and the Reel background cache. Learners are identified by the JWT `sub`;
there is no foreign key to Identity. Three study modes share **one** schedule per (learner, word):
Flashcard (flip + self-grade), Quiz (meaning → pick 1 of 4 words) and Reel (muted background video,
Vietnamese meaning, 5 s countdown, type the word).

```text
Content.API/Controllers
    VocabularyController          public catalog, quizzes, reel backgrounds
    LearnerVocabularyController   me/summary, me/stages/*, me/study-queue, me/reviews (any signed-in role)
    VocabularyLibraryController   me/library, me/folders, me/words
    StudySessionsController       me/sessions, me/preferences
    VocabularyEnrichmentController lookup, me/words/import/parse, me/words/import, me/words/{id}/ai-fill (rate limited)
    RateLimiting/VocabularyRateLimits per-user fixed windows (60/30/20/20 per minute)
    AdminVocabularyController     admin/import (Admin)
Content.Application
    Features/VocabularyCatalog    GetStages, GetStageSets, GetSetDetail
    Features/VocabularyProgress   GetLearnerSummary, GetStageSetProgress, GetStudyQueue, SubmitReview
    Features/VocabularyQuizzes    CreateQuiz
    Features/ReelBackgrounds      GetReelBackgrounds
    Features/VocabularyImport     ImportVocabularyStages
    Features/VocabularyLibrary    GetLibrary, GetSavedWords, CreateFolder, UpdateFolder, DeleteFolder, SaveWord,
                                  CreateCustomWord, UpdateSavedWord, RemoveSavedWord
    Features/StudySessions        StartStudySession, GetActiveStudySession, GetStudySession, AnswerStudySession,
                                  CompleteStudySession, AbandonStudySession
    Features/VocabularyPreferences GetPreferences, UpdatePreferences
    Features/VocabularyLookup     LookupWord
    Features/LibraryImport        ParseImport, ImportWords   (learner import; the admin roadmap import is VocabularyImport)
    Features/VocabularyLibrary    + AiFillWord
    Interfaces/Services/Enrichment IWordGlossCache, ICuratedLexicon, IDictionaryProvider, ITranslationProvider, IAiWordProvider
    Models/Enrichment             WordGloss, GlossField, GlossSource, WordKind, PhraseAnalysis, EnrichRequest/Result, EnrichmentSettings
    Services/Enrichment/WordEnricher  the tier orchestrator (+ InputNormalizer, PosNormalizer, TranslationFilter, CustomWordFiller)
    Services/Import/VocabularyTextParser  the single text/JSON list parser
    DTOs/Vocabulary, DTOs/VocabularyLibrary, DTOs/StudySessions, DTOs/VocabularyPreferences,
    DTOs/ReelBackgrounds, DTOs/VocabularyImport
    Models/Vocabulary (SetWordMembership, SetOverview), Models/VocabularyImport (stage file shape)
    Common/Json/Optional<T> (absent vs null in partial updates), Common/Validation (tzOffset, custom word rules)
    Services/ReviewRecorder        one review -> scheduler + progress + log (me/reviews and session answers)
    Services/StudyQueueService     words of a source (set, due, folder, library, word list) + study items
    Services/StudySessionPresenter StudySessionDto with live progress and completion summary
    Services/LibraryWordService, QuizQuestionBuilder, LibraryCounter, VocabularyImportParser
    Mappings/Vocabulary*, Library*, StudySession*, ReelBackground* MappingExtensions
Content.Domain
    Entities: VocabularyStage, VocabularySet, VocabularySetWord, VocabularyWord,
              LearnerWordProgress, WordReviewLog, ReelBackground, VocabularyFolder, SavedWord,
              StudySession, LearnerVocabularyPreferences
    Enums/VocabularyEnums.cs: PartOfSpeech, ReviewGrade, StudyMode, WordStatus
    Enums/StudySessionEnums.cs: StudySourceKind, StudySessionStatus, VocabularyView
    Rules/SpacedRepetitionScheduler.cs (pure, single source of truth for scheduling)
    Exceptions/VocabularyErrors.cs
Content.Infrastructure
    Persistence/Configurations/VocabularyConfiguration.cs, StudySessionConfiguration.cs, WordGlossCacheConfiguration.cs,
    Services/Enrichment/ WordGlossCacheRepository, CuratedLexicon, FreeDictionaryProvider (+ DictionaryCircuitBreaker),
        TranslationProvider, Gemini/{GeminiOptions, GeminiKeyPool, GeminiClient, GeminiWordProvider, PromptTemplate},
        Prompts/{word-batch, phrase-analysis, translation}.md (embedded resources)
    Repositories/Vocabulary*, LearnerProgressRepository, LearnerLibraryRepository, StudySessionRepository,
    VocabularyPreferencesRepository, ReelBackgroundRepository, Services/Pixabay/* (typed HttpClient + daily BackgroundService)
```

## Data model and invariants

| Table | Natural key / unique indexes | Notes |
|---|---|---|
| `VocabularyStages` | `Slug` | `Order`, `ToeicTarget`, `IsActive` |
| `VocabularySets` | `(StageId, Slug)` | index `(StageId, Order)` (not unique: a hidden set keeps its old position) |
| `VocabularyWords` | `(OwnerKey, NormalizedHeadword, PartOfSpeech)` | word bank; official words have `OwnerKey = Guid.Empty` (so headword + POS stays unique among them), custom words carry the owning learner; `Synonyms` is JSON |
| `VocabularySetWords` | PK `(SetId, WordId)`, unique `(SetId, Order)` | 1-based position in the lesson |
| `LearnerWordProgress` | `(LearnerId, WordId)`; index `(LearnerId, DueOn)` | `Version` concurrency token |
| `WordReviewLogs` | `(LearnerId, ClientEventId)`; index `(LearnerId, LocalDay)` | append-only, interval/ease before → after |
| `ReelBackgrounds` | `(Provider, ProviderId)` | `MissCount`, `IsActive`, `LastSeenAt` |
| `VocabularyFolders` | `(LearnerId, NormalizedName)` | flat list, `Order`; name compared case-insensitively |
| `SavedWords` | `(LearnerId, WordId)`; index `(LearnerId, FolderId, SavedAt)` | `FolderId` null = unfiled (FK `SET NULL`), `Note` |
| `StudySessions` | `(LearnerId, ActiveSlot)`; index `(LearnerId, Status, UpdatedAt)` | `Queue`, `Requeued`, `Results` are JSON; `ActiveSlot` = `mode:kind:sourceId` only while active; `Version` token |
| `LearnerVocabularyPreferences` | PK `LearnerId` | one row per learner, absent = defaults |
| `WordGlossCache` | `(NormalizedText, Kind)`; index `ExpiresAt` | enrichment cache: fields, `Sources` JSON, `Version`, `ExpiresAt` (30 days) |

1. Progress is keyed by **word**, not by set: a word in two lessons has one schedule.
2. Words and sets are never deleted. A set missing from a re-imported file is deactivated (hidden from every
   endpoint); words stay in the bank, so progress and review logs always keep their word.
3. Catalog endpoints only show active stages and sets.
4. A custom word is visible only to its owner (review, quiz, study queue, sessions); for anyone else it is
   `vocabulary_word_not_found`. Deleting it deletes its progress and logs; removing a saved **official** word only
   removes it from the library and keeps its progress.
5. Deleting a folder moves its words to unfiled.

## Spaced repetition (`SpacedRepetitionScheduler`)

- Grades: `Forget`, `Hard`, `Good`, `Easy`, plus the passive `Seen` (Reel timeout).
  Quality: Easy 5, Good 4, Hard 2, Forget 1.
- q ≥ 3: repetitions + 1; interval 1 (first), 6 (second), else `round(interval × EF')` (midpoint away from zero).
  q < 3: repetitions = 0, interval = 1, lapses + 1.
- `EF' = EF + (0.1 − (5−q)(0.08 + (5−q)·0.02))`, floor 1.3, start 2.5. EF' is computed **before** the interval, so
  Easy lengthens more than Good (Good keeps EF, Easy +0.1, Hard −0.32, Forget −0.54). Interval cap 365 days.
- Good/Easy lengthen at most once per learner-local day: a Good/Easy on a day whose last grade was Good/Easy keeps
  the schedule (still logged, `LastReviewedAt` updated). Forget/Hard always apply; after them a Good relearns from
  repetition 1.
- `Seen` creates a new word due tomorrow (interval 1, repetitions 0) and leaves an existing word untouched.
- `dueOn = localToday + interval`; the local day comes from `tzOffset` (minutes east of UTC, −720..840, default 0).
- Status: `New` (no row), `Learning` (interval < 21), `Mastered` (interval ≥ 21). Due: `dueOn ≤ localToday`.
- Preview: days until the next review for each of the four grades if chosen now; `0` means the schedule is kept.
- Streak: consecutive local days with ≥ 1 review log, ending today or yesterday. Each log stores the local day of
  the tzOffset sent with that review; "today" uses the tzOffset of the summary request.

Client-side mapping: Flashcard = learner's choice; Quiz = Good if the first pick is correct, else Forget;
Reel = Good if typed correctly before reveal, Hard if typed wrong, Seen if time ran out without input.

## Permissions

| Area | Who |
|---|---|
| Stages, sets, set detail, quizzes, reel backgrounds | anonymous |
| `me/*` (summary, progress, study queue, reviews, library, sessions, preferences) | any authenticated role (User, Teacher, Admin); learner = JWT `sub` |
| `admin/import` | `Admin` policy |

## API reference

Routes through the gateway: `/content/api/v1/vocabulary/...`. JSON camelCase, enums as strings,
dates `yyyy-MM-dd`, timestamps ISO UTC.

| Method & route | Auth | Response |
|---|---|---|
| GET `stages` | anonymous | `StageDto[]` ordered by `order` |
| GET `stages/{stageId}/sets` | anonymous | `SetSummaryDto[]` ordered by `order`; `previewWords` = first 3 headwords, `lastWord` = last headword in lesson order (FE shows "first → last") |
| GET `sets/{setId}` | anonymous | `SetDetailDto`; `previousSetId`/`nextSetId` follow the whole roadmap (cross stage) |
| GET `me/summary?tzOffset=` | user | `LearnerSummaryDto` incl. `lessonCount`, `lessonsCompleted`, `libraryWordCount`, `nextSet`, `activeSession`, `dailyGoal`, per stage `setCount`/`setsCompleted` |
| GET `me/stages/{stageId}/sets?tzOffset=` | user | `SetProgressDto[]` (only sets with progress, lesson order) |
| GET `me/study-queue?setId=&due=&folderId=&library=&limit=&tzOffset=` | user | `StudyItemDto[]` (each with `savedWordId`); exactly one of `setId` / `due=true` / `folderId` / `library=true`; `setId` = every word of the lesson in order; `folderId` / `library` = every saved word, newest first; `due=true` orders by `dueOn`, then oldest review, `limit` 1..100 (default 20); limit applies to `due` only |
| POST `quizzes` `{ wordIds }` (1..50) | anonymous (a token adds the caller's custom words) | `QuizQuestionDto[]` in request order |
| GET `me/library?tzOffset=` | user | `LibraryDto` (`total`, `unfiled` counts, folders with counts) |
| POST `me/folders` `{ name }` (1..60) | user | `FolderDto`, 201 |
| PUT `me/folders/{folderId}` `{ name?, order? }` | user | `FolderDto` |
| DELETE `me/folders/{folderId}` | user | 204; its words become unfiled |
| GET `me/words?folderId=` or `unfiled=true`, `search=&page=&pageSize=&tzOffset=` | user | `PagedResult<SavedWordDto>`, newest first; `search` matches headword or meaning; `pageSize` up to 100 |
| POST `me/words` `{ wordId, folderId?, note? }` | user | `SavedWordDto`, 201 new / 200 already saved (only the fields sent change; `"folderId": null` = unfiled) |
| POST `me/words/custom` `{ headword, phonetic?, partOfSpeech, meaningVi, definitionEn?, example?, exampleVi?, folderId?, note? }` | user | `SavedWordDto`, 201 |
| PUT `me/words/{savedWordId}` `{ folderId?, note?, custom? }` | user | `SavedWordDto`; `custom` only for custom words |
| DELETE `me/words/{savedWordId}` | user | 204 |
| POST `me/sessions` `{ mode, one of setId / folderId / library / due / wordIds, restart?, limit?, tzOffset }` | user | `StudySessionDto`, 201 new / 200 resumed |
| GET `me/sessions/active?mode=&setId=&folderId=&library=&due=&tzOffset=` | user | most recent matching active `StudySessionDto`, or 204 (all filters optional) |
| GET `me/sessions/{id}?tzOffset=` | user | `StudySessionDto` |
| POST `me/sessions/{id}/answers` `{ wordId, grade, clientEventId, tzOffset }` | user | `StudySessionDto` with `review: ReviewResultDto` |
| POST `me/sessions/{id}/complete?tzOffset=` | user | `StudySessionDto` with `summary` |
| DELETE `me/sessions/{id}` | user | 204 (abandon) |
| GET `me/preferences` | user | `PreferencesDto` (defaults when nothing saved) |
| PUT `me/preferences` (partial) | user | `PreferencesDto` |
| POST `lookup` `{ text, context?, allowAi }` | user, 60/min | `WordLookupDto` (fields, `sources`, `complete`, `aiUsed`, `existing`, `officialWordId`) |
| POST `me/words/import/parse` `{ text }` (≤ 20 000 chars) or `{ json }` (≤ 200 000 chars) | user, 30/min | `ImportPreviewDto` |
| POST `me/words/import` `{ items (1..50), folderId?, createFoldersFromCategories }` | user, 20/min | `ImportResultDto` (`created`, `updated`, `failed[]`, `items`) |
| POST `me/words/{savedWordId}/ai-fill?tzOffset=` | user, 20/min | saved word fields + `filled: string[]` (custom words only) |
| POST `me/reviews` `ReviewRequest` | user | `ReviewResultDto`; replaying a `clientEventId` returns the current state with `duplicate: true` |
| GET `reel-backgrounds?count=40` (1..60) | anonymous | `ReelBackgroundDto[]`, random active clips |
| POST `admin/import` multipart files | Admin | `VocabularyImportResultDto` |

Quiz options: the answer plus 3 distractors, taken in this order: same part of speech from the answer's (earliest)
official stage; the caller's own custom words of that part of speech; any official word of that part of speech
(random sample); then any word of the stage, the caller's other custom words and the other words of the request. Never a duplicate
headword, never a word whose `meaningVi` equals the prompt (case/whitespace-insensitive). Options are shuffled.

## Error codes

| Code | Status | When |
|---|---|---|
| `validation_error` | 400 | Request validation (tzOffset, limit, count, wordIds, setId/due, import files) |
| `invalid_import_file` | 400 | Unreadable JSON, wrong schemaVersion, invalid slug/partOfSpeech/lengths, duplicate word in a set or stage uploaded twice. Nothing is written |
| `invalid_subject` | 401 | Token without a valid `sub` |
| `vocabulary_stage_not_found` | 404 | Unknown or inactive stage |
| `vocabulary_set_not_found` | 404 | Unknown or inactive set |
| `vocabulary_word_not_found` | 404 | Unknown word in a review or quiz |
| `client_event_conflict` | 409 | `clientEventId` already used by this learner for another word |
| `unique_constraint` / `concurrency_conflict` | 409 | Two simultaneous first reviews of one word or one event, or two simultaneous session starts; retry |
| `vocabulary_folder_not_found` | 404 | Folder unknown or owned by someone else |
| `folder_name_taken` | 409 | Another folder of the learner has the same name (case-insensitive) |
| `saved_word_not_found` | 404 | Saved word unknown or owned by someone else |
| `word_not_custom` | 400 | `custom` block sent for an official word |
| `custom_word_exists` | 409 | The learner already has a custom word with this headword and part of speech |
| `study_session_not_found` | 404 | Session unknown or owned by someone else |
| `study_session_not_active` | 409 | Answer/complete on a completed, abandoned or expired session |
| `study_session_word_mismatch` | 409 | Answer is not for the current card |
| `study_session_empty` | 400 | The source has no words (e.g. nothing due) |
| `invalid_import_json` | 400 | `import/parse` JSON cannot be read |
| `rate_limited` | 429 | Per-user limit of an enrichment endpoint reached (`Retry-After` header) |
| `word_not_custom` | 400 | Also: `ai-fill` on an official word |

Import row failure codes (in `failed[]`, the batch continues): `no_latin_letter`, `too_many_words`, `too_long`
(custom headwords ≤ 80), `meaning_missing` (no Vietnamese meaning found by any tier). Preview rejected-line codes:
`no_latin_letter`, `too_many_words`, `too_long`, `invalid_item`.

## Learner summary

- `lessonCount` = active lessons; a lesson is **completed** when every word has a progress row (any status).
- `nextSet` = first lesson in roadmap order (stage order, then lesson order) that is not completed, else null.
- `libraryWordCount` = saved words; `learned`/`learning`/`mastered`/`due` include custom words.
- `activeSession` = most recently used active session (idle 7 days or less); `dailyGoal` from preferences (default 20).

## Study sessions

Every Flashcard / Quiz / Reel session is stored, so a learner resumes on any device.

- Sources: `setId`, `folderId`, `library=true`, `due=true` (`limit` 1..100, default 20) or `wordIds` (1..100).
  The queue is fixed at start (same order as `me/study-queue`).
- At most **one active session per (learner, mode, source)** (unique `(LearnerId, ActiveSlot)`). Starting again
  returns it (200) unless `restart=true`, which abandons it and starts a new one (201). Word-list sessions always
  start fresh. A session idle for more than 7 days reads as `Abandoned` and is replaced on the next start.
- An answer must be for the current card (`queue[position]`). It records the review through the same
  `ReviewRecorder` as `me/reviews` (mode = the session's mode), stores the last grade per word, counts
  `correctCount` (Good/Easy) and `reviewCount`, and moves `position` on. The first Forget/Hard of a word re-inserts
  it 4 cards later (or at the end); Seen is not re-queued. Replaying a `clientEventId` returns the session unchanged
  with `review.duplicate = true`.
- `complete` sets `Completed` and returns `summary { remembered (last grade Good/Easy), needReview, wordIds }`
  (`wordIds` = words to review again); completing again returns the same summary. `DELETE` abandons an active session.
- `items` always carry live progress, preview and `savedWordId`.

## Preferences

`GET/PUT me/preferences`; PUT is partial (only properties present change; ids accept `null` to clear).

| Field | Values | Default |
|---|---|---|
| `fontScale` | 0.875, 1, 1.125, 1.25 | 1 |
| `sidebarCollapsed`, `snowfall`, `autoPlayAudio` | bool | false, true, true |
| `defaultMode` | Flashcard, Quiz, Reel | Flashcard |
| `dailyGoal` | 5..200 | 20 |
| `lastStageId`, `lastFolderId`, `lastFolderPickerId` | Guid or null (not checked for existence: UI hints) | null |
| `lastView` | Roadmap, Library | Roadmap |
| `sidebarWidth` | 220..420 px or null (layout default) | null |

## Word enrichment ("Thêm từ vựng")

One pipeline (`WordEnricher`) serves lookup, import and AI fill. Tiers run in order and **only fill blank fields**;
fields the learner typed are never replaced (and never cached). The pipeline stops once a gloss is complete
(word: `meaningVi` and (`phonetic` or `partOfSpeech`); phrase: `meaningVi`), except that AI fill keeps going while
meaning, phonetic, part of speech, example or example translation is blank.

1. **Cache** `WordGlossCache`, by normalized text + kind, 30 days (`Enrichment:CacheTtlDays`), only glosses with a
   Vietnamese meaning; `WordGloss.CacheVersion` bump invalidates everything. Writes use their own DbContext scope.
2. **Catalog**: official words, exact normalized headword (prefers the part-of-speech hint); sets `officialWordId`.
3. **Dictionary** dictionaryapi.dev (single words, URL-encoded, 2 s): phonetic, part of speech, first two definitions
   `(pos) def` joined by ` · ` (≤ 320), first example (≤ 240). Never used as `meaningVi`. Circuit breaker: 3
   consecutive failures open it for 5 minutes.
4. **Translation** (in parallel with 3 when the meaning is blank; also example → exampleVi): Google Cloud v2 (only
   with `GoogleTranslate:ApiKey`) → Google gtx → MyMemory, 4 s each, browser User-Agent for the free endpoints.
   Empty results, echoes of the input and provider warnings ("MYMEMORY WARNING", "QUERY LENGTH LIMIT", "INVALID
   LANGUAGE PAIR", …) are rejected.
5. **AI (Gemini)** when allowed and configured: word batches of ≤ 30 per prompt for blank fields; phrases (2–5 words)
   always get the phrase analysis (`phraseType` idiom | phrasal_verb | collocation | fixed | literal, `usage`,
   `explanation`, `literalMeaning`); an example still without translation is translated by AI.

Every filled field is listed in `sources` (`cache`, `catalog`, `dictionary`, `google`, `mymemory`, `ai`).
Part-of-speech labels from any tier or file are normalised (`n`/`noun`→Noun, `v`→Verb, `adj`→Adjective, …,
Vietnamese names too, `phrasal verb`/`idiom`/multi-word→Phrase, else Other). Input is trimmed, whitespace collapsed,
capped at 100 characters; 1 token = word, 2–5 = phrase, otherwise (or without a latin letter) rejected.

Deadlines (`Enrichment` section): lookup 6 s (`LookupDeadlineSeconds`), lookup with AI 12 s
(`LookupAiDeadlineSeconds`), whole import request 20 s (`ImportDeadlineSeconds`), AI fill 12 s
(`AiFillDeadlineSeconds`). On a deadline the request returns what is known; it never fails.

**Gemini key pool** (`GeminiKeyPool`, in-memory singleton): keys from `Gemini:ApiKeys` (comma/newline list) plus
`Gemini:ApiKey` prepended when not listed. Each call starts at the next key; healthy keys first, then suspended keys
by soonest release. Cooldowns per (key, model): 429/RESOURCE_EXHAUSTED/quota 30 min, 401/403/PERMISSION_DENIED/
API_KEY_INVALID 6 h, NOT_FOUND/INVALID_ARGUMENT 2 min, network/5xx 10 min after 3 consecutive failures; a success
resets the key. Logs show `#index`, never a key; HttpClient request logging is off for Gemini and translation.
Calls: `POST …/v1beta/models/{model}:generateContent`, header `x-goog-api-key`, temperature 0.1, 4096 tokens,
JSON output, `thinkingBudget: 0` for `gemini-2.5*`; interactive 8 s (two keys at most), batch 25 s (all keys).
Output is unfenced, cut to the outer `{…}` and truncated (meaningVi 200, phonetic 60, partOfSpeech 32,
example/exampleVi 250). Prompts are embedded files under `Infrastructure/Services/Enrichment/Prompts/`.

**List import**: `import/parse` (the only parser; the FE renders its preview) accepts text lines (list markers
stripped; first separator among TAB, ` => `, ` -> `, ` = `, ` : `, ` - `, ` – `, ` — ` splits headword and meaning;
otherwise `, ; ， 、 / | +` separate bare headwords; a trailing `(n)`/`[adj]` sets the part of speech) or JSON
(`[{category, words}]`, `{categories}`, `{words|data|vocabulary}`, `{word: meaning}`, with field aliases and ```json
fences). Repeated headwords are marked `duplicate`, words already in the library `existing`. `import` saves an
official match (same headword, same POS when given) as the official word; anything else creates the learner's custom
word or fills blanks of the existing one (`updated`). With `createFoldersFromCategories` categories become folders
(reused case-insensitively); other items go to `folderId` or stay unfiled. A row that fails never aborts the batch.

### Configuration

| Setting | Env (docker-compose / .env) | Default |
|---|---|---|
| `Gemini:ApiKey` | `GEMINI_API_KEY` | empty (AI tier off) |
| `Gemini:ApiKeys` | `GEMINI_API_KEYS` | empty |
| `Gemini:InteractiveModel` | `DICT_AI_MODEL` | = batch model |
| `Gemini:BatchModel` | `FLASHCARD_AI_MODEL` | `gemini-2.5-flash` |
| `Gemini:InteractiveTimeoutSeconds` / `BatchTimeoutSeconds` | — | 8 / 25 |
| `GoogleTranslate:ApiKey` | `GOOGLE_TRANSLATE_API_KEY` | empty (paid tier skipped) |
| `Enrichment:CacheTtlDays`, `*DeadlineSeconds` | — | 30, 6 / 12 / 20 / 12 |

## Importing the vocabulary

Data lives in `data/vocabulary/0[1-5]-toeic-roadmap-*.json` (format: `data/vocabulary/README.md`): one file per
stage (`Khởi động`, `Phản xạ`, `Tăng tốc`, `Bứt phá`, `Về đích`), each cut into numbered lessons of 50 words
(`bai-01`, `Bài 01`, …; the last lesson may be shorter): 18/16/14/26/26 lessons. The import
upserts stages by slug, sets by (stage, slug) and words by (normalized headword, partOfSpeech) inside one
transaction per call, and is idempotent: re-importing the same files reports everything as `unchanged`.
Per stage it returns `setsCreated`, `setsUpdated` (field, word-list/order change, deactivation or reactivation),
`wordsCreated`, `wordsUpdated` and `unchanged` (sets + words that already matched). A word shared by two files
of the same call is counted once, in the first stage. Limits: 1–10 `.json` files, 5 MB each.

```bash
TOKEN=<admin access token>
curl -X POST http://localhost:5050/content/api/v1/vocabulary/admin/import \
  -H "Authorization: Bearer $TOKEN" \
  -F "files=@data/vocabulary/01-toeic-roadmap-250.json" \
  -F "files=@data/vocabulary/02-toeic-roadmap-450.json" \
  -F "files=@data/vocabulary/03-toeic-roadmap-650.json" \
  -F "files=@data/vocabulary/04-toeic-roadmap-800.json" \
  -F "files=@data/vocabulary/05-toeic-roadmap-990.json"
```

Any multipart field name is accepted (`files`, `files[]`). Apply the migration first
(`docs/migrations/20261101_AddVocabularyLearning.md`).

**Through the gateway, send one file per request.** The gateway's Kestrel hardening caps request bodies at 2 MB
(`AddKestrelHardening` in BuildingBlocks), so all five files in one request (about 2.1 MB) return `413`. The Content
API itself accepts up to 10 files of 5 MB each, which is useful when calling it directly (e.g. `localhost:6004`).
Separate requests are safe: each file is its own transaction and the import is idempotent.

```bash
for f in data/vocabulary/0*-toeic-roadmap-*.json; do
  curl -X POST http://localhost:5050/content/api/v1/vocabulary/admin/import \n    -H "Authorization: Bearer $TOKEN" -F "files=@$f"
done
```

## Reel backgrounds (Pixabay)

`PixabayReelBackgroundRefresher` runs at startup and then every 24 h. It queries the Pixabay videos API for calm
scenes (`nature, ocean, forest, mountain, sky, rain, river, snow, sunset, flowers`, `safesearch=true`,
`video_type=film`), skips clips shorter than 12 s or tagged with people (`people, person, woman, man, girl, boy,
child`, plurals, `kid, baby, family, couple`), takes the `small` rendition (then `medium`, then `tiny`) with its
thumbnail as poster, and upserts by (`pixabay`, id). Rows not returned by a fully successful refresh get
`MissCount + 1` and are deactivated after 7 misses; a failed query never counts misses. Ten requests per day with
a 1 s pause stay far below Pixabay's 100 requests / 60 s, and results are cached ≥ 24 h as Pixabay requires.

Configuration: `Pixabay:ApiKey` (env `Pixabay__ApiKey`, docker `PIXABAY_API_KEY`), `Pixabay:Enabled`
(default `true`). Without a key the service logs one warning and does nothing; the key is never logged
(HttpClient request logging is disabled for this client). Show the `author` and link `pageUrl` as attribution.
