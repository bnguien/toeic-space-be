# Dictation & Speech (Content Service, PBL-107)

"Nghe chép chính tả": listen to a sentence read by a neural voice and write it down word by word. Ported from
Studychill (`apps.learning.listening`: dictation course views, selectors, coach rules, grading, word proofs, edge-tts).
Content imports **only the TOEIC roadmap** (packs `toeic-250` … `toeic-990`: 404 courses, 12,177 sentences; owner
decision 2026-11-07). Topic packs and the warm-up pack can be imported later with the same files and endpoint; the
catalog shows a pack as soon as it has active courses (`GET config` → `packs`).

The same service hosts the shared **speech endpoint** (`GET api/v1/speech`) used by News and Video word audio.

## Code map

```text
Content.API/Controllers
    DictationController         config, roadmap, courses, courses/{id}/vocab, exercises, exercises/{id}/skeleton,
                                exercises/{id}/audio, hint, check, summary (anonymous; a token adds progress)
    LearnerDictationController  me/state (GET/PUT, any signed-in role)
    AdminDictationController    admin/import (Admin; .json or .json.gz)
    SpeechController            GET api/v1/speech (anonymous, rate limited)
Content.API/RateLimiting/DictationRateLimits  per-user/IP per minute: check 30, hint 60, skeleton+vocab 80, audio 80, speech 60
Content.API/Middlewares/PublicCacheControl     lets mp3 responses keep `Cache-Control: public, max-age=604800`
Content.Application
    Features/Dictation          Queries: GetDictationConfig, GetDictationRoadmap, GetPackCourses, GetDictationExercises,
                                GetExerciseSkeleton, GetExerciseAudio, GetCourseVocab, GetDictationSummary, GetDictationHint
                                Commands: CheckDictationAnswer
    Features/DictationLearner   GetDictationLearnerState, UpdateDictationLearnerState
    Features/DictationImport    ImportDictationPacks
    Features/Speech             GetSpeechAudio
    DTOs/Dictation, DTOs/DictationImport, DTOs/Speech, Models/Dictation (DictationSettings, DictationPackFile),
    Models/Speech/SpeechJob, Mappings/Dictation*MappingExtensions, Services/DictationProofService (cached proofs)
    Interfaces: IDictationCatalogRepository, IDictationProgressRepository, IDictationImportRepository,
                ISpeechSynthesizer, ISpeechPrewarmQueue
Content.Domain
    Entities: DictationCourse, DictationItem (+ DictationKeyword, DictationNote, DictationVocabEntry records),
              DictationProgress, DictationLearnerState (+ DictationSoundPreferences)
    Enums/DictationEnums.cs: DictationNoteType, DictationStageState, DictationHintMode, DictationWordStatus
    Rules: DictationGrader (+ SequenceMatcher = Python difflib), DictationHints, DictationRules, DictationCatalog,
           WordProof, SpeechVoices
    Exceptions/DictationErrors.cs
Content.Infrastructure
    Persistence/Configurations/DictationConfiguration.cs (4 tables, JSON columns)
    Persistence/Repositories/DictationCatalogRepository, DictationProgressRepository, DictationImportRepository
    Services/Speech: SecMsGecToken (DRM), EdgeTtsClient (websocket protocol), CachedSpeechSynthesizer (disk cache,
                     single flight), SpeechPrewarmQueue + SpeechPrewarmWorker (background queue), SpeechOptions
scripts/dictation_data_pipeline.py   export (read-only SSH snapshot) → build (data/dictation/*.json.gz)
```

## Data model (4 tables)

| Table | Key / unique indexes | Other indexes | Why it exists |
|---|---|---|---|
| `DictationCourses` | `Id` (int identity); unique `Code` (natural key, e.g. `sc_crs_t250_001`) | `(Pack, IsActive, Order)` | One course = one roadmap stage / topic lesson. `Pack`, `Level`, `Order`, `Title`, `ItemCount`, `WordsPerSentence`, `VocabCount` are precomputed at import so maps never read sentences. |
| `DictationItems` | `Id` (int identity, short ids in play URLs); unique `SourceCode` (e.g. `sc_stmt_t250_001_01`); FK `CourseId` (restrict) | `(CourseId, IsActive, Order)` | One sentence. `Text` (the answer), `Translation`, `Ipa`, `Topic`, `VoiceKey`, `Rate`, `MaxPlays`, `Level` (copied from the course for unlock rules). Document data in JSON columns: `Keywords` `[{word, meaning, partOfSpeech, ipa, cefr}]`, `Notes` `[{id, text, type: Word/Phrase, explanation}]`, `Vocab` `[{word, lemma, meaning, partOfSpeech, ipa}]`. `UpdatedAt` is part of the word-proof salt. |
| `DictationProgress` | `Id`; unique `(LearnerId, ItemId)`; FK `ItemId` (cascade) | — (the unique index serves learner lookups) | Best result of a learner on a sentence: `BestAccuracy`, `BestScore`, `Passed`, `PassedClean`, `AttemptCount`, `LastAttemptAt`, `Version` (concurrency token, two tabs). Drives unlocks, roadmap stars/states and pack counts. |
| `DictationLearnerStates` | PK `LearnerId` | — | One row per learner for what Studychill kept in the browser (and synced via learnerSync): `Stars` (JSON map stage key → 1–3), `DailySeconds` (JSON map local day → seconds, last 14 days), `BasicSettings` / `ProSettings` (JSON play settings), `Voice`, `Sound` (JSON `{enabled, volume, tick, chime}`), client timestamps `SettingsUpdatedAt` / `VoiceUpdatedAt` / `SoundUpdatedAt` (Unix ms), `Version`. |

Not ported (Studychill-only): `DictationAttempt` (every submission; only fed the coach statistics that no screen shows
— the attempt count lives in `DictationProgress`), `DictationItemWord` (word index for Vocab Challenge), XP, daily
budget / bot guard tables, the per-word glossary (only used to compute keywords at build time) and the 2,600 legacy
sentences without a course (unreachable in Studychill's UI). Learner ids are the JWT `sub`; no FK to Identity.

Invariants: courses and sentences are never deleted; an import deactivates what left its file. Every JSON key and
stored enum string is English.

## Rules (`Domain/Rules`)

- **Tokens** (`DictationGrader.Tokens`): whitespace tokens whose core (outer `[^0-9a-zA-ZÀ-ɏ']` stripped) is not
  empty. The skeleton, the word proofs, hints and grading all use this list, so indexes match the client's slots
  (Studychill dropped zero-length tokens only in the client, so hint/proof indexes drifted after a lone "–").
- **Normalisation**: strip outer punctuation, lower case, remove diacritics (same as the client's `fold`).
- **Grading**: Python `difflib.SequenceMatcher(autojunk=False)` opcodes over normalised tokens → each answer word
  `Correct` / `Wrong` (+ typed) / `Missing`, plus `Extra` words. Accuracy = correct / total × 100 (1 decimal, half
  to even). **A sentence passes only when every word is correct** (Studychill passed at 90 % on the server while the
  client required every word). Score = 10 on a pass, else round(accuracy / 10).
- **Unlocks** (`DictationRules.PassesUnlock`): levels 1–2 any pass; from level 3 only a clean pass (no hint, keyword
  or meaning opened before submitting — client-declared `assisted`, or `hintCount > 0`). Meaning/keywords are gated
  until the first submission from level 3 (client).
- **Stage stars** (roadmap): when every sentence of a stage is passed, 3 / 2 / 1 stars for an average best accuracy
  ≥ 95 / ≥ 85 / below. Stage states: `Done` (all passed), `Current` (first not done of the level), `Locked`.
- **Word proofs**: salt = HMAC-SHA256(`Dictation:ProofSecret`, `dictation-proof-v1:{id}:{UpdatedAt:O}`)[..16];
  digest = PBKDF2-HMAC-SHA256(normalised word, salt ‖ 2-byte big-endian index, 20 000 rounds, 16 bytes), base64.
  Cached per (sentence, update) in memory. Without a configured secret a random one is generated per process.
- **Keywords**: computed by the data pipeline with Studychill's coach rules (editor focus words first, then words of
  the TOEIC CEFR list at B1+, max 4/5/6/8 for levels 2/3/4/5; none on level 1).

## API reference

Routes through the gateway: `/content/api/v1/...`. JSON camelCase, enums as strings.

| Method & route | Auth | Response |
|---|---|---|
| GET `dictation/config` | anonymous | `DictationConfigDto`: levels, topics, 16 voices, `defaultVoice` `andrew`, `rateMin/Max` −40/40, `passScore` 10, `passThreshold` 100, `rules`, `packs` (packs with content: course and sentence counts) |
| GET `dictation/roadmap` | anonymous (+progress) | `DictationRoadmapDto`: 5 levels × stages (`number`, `courseId`, `title`, `itemIds`, `itemCount`, `passedCount`, `passedCleanCount`, `stars`, `state`, `wordsPerSentence`, `vocabCount`) |
| GET `dictation/courses?pack=` | anonymous (+progress) | `PackCoursesDto` (courses in pack order with `passedCount`); 400 `validation_error` for a bad slug |
| GET `dictation/courses/{id}/vocab` | anonymous, 80/min | `CourseVocabDto` (pre-learn words with their 1-based `sentence`) |
| GET `dictation/exercises?level=&course=` | anonymous (+progress) | `DictationExercisesDto`: sentences of the course with `unlocked` (guests: all), `passed`, `passedClean`, `bestAccuracy`, `bestScore`, `attemptCount`; without `course` the list is empty |
| GET `dictation/exercises/{id}/skeleton?voice=&next=` | anonymous, 80/min | `DictationSkeletonDto`: `words` `[{before, length, after}]`, `translation`, `keywords`, `proof`, `ipa`, `notes`; queues the audio of `next` (≤ 4 ids, else the following sentences) in `voice` |
| GET `dictation/exercises/{id}/audio?voice=&rate=` | anonymous, 80/min | `audio/mpeg` (range requests, `Cache-Control: public, max-age=604800`); 503 `SPEECH_UNAVAILABLE` |
| POST `dictation/hint` `{itemId, userText, mode: FirstLetter\|RevealWord}` | anonymous, 60/min | `DictationHintDto` `{index, total, mode, length, letter \| word}` at the first wrong/missing word |
| POST `dictation/check` `{itemId, userText, voice?, hintCount?, assisted?, dryRun?}` | anonymous (+save), 30/min | `DictationCheckDto`; signed in: progress saved (a dry run only when correct); a wrong dry run hides `correctText`, `translation` and the text of wrong/missing words |
| GET `dictation/summary` | anonymous (+progress) | `{authenticated, passedCount, totalCount, accuracy}` |
| GET `dictation/me/state` | user | `DictationLearnerStateDto` (defaults when nothing saved) |
| PUT `dictation/me/state` (partial) | user | merged state: `stars` / `dailySeconds` keep the larger value per key; `settings` (+`settingsUpdatedAt`), `voice` (+`voiceUpdatedAt`), `sound` (+`soundUpdatedAt`) replace only when the timestamp is newer |
| POST `dictation/admin/import` multipart | Admin | `{packs: [{pack, coursesCreated, coursesUpdated, itemsCreated, itemsUpdated, deactivated, unchanged}]}` |
| GET `speech?text=&voice=&rate=` | anonymous, 60/min | `audio/mpeg` for any text ≤ 200 characters; voice keys as `config.voices`; 503 `SPEECH_UNAVAILABLE` |

The play client merges its local state into `me/state` on sign-in (Studychill learnerSync rules); guests keep it in
`localStorage` (`ts-dictation-*`).

## Error codes

| Code | Status | When |
|---|---|---|
| `validation_error` | 400 | Bad pack slug, text missing / > 200 chars, ids ≤ 0, invalid state maps, import file checks |
| `dictation_item_not_found` | 404 | Unknown or inactive sentence |
| `dictation_course_not_found` | 404 | Unknown or inactive course (vocab) |
| `dictation_item_empty` | 400 | Hint on a sentence without words |
| `invalid_import_file` | 400 | Unreadable JSON, schemaVersion ≠ 1, missing/repeated codes, empty sentence, pack uploaded twice, > 24 MB decompressed |
| `invalid_subject` | 401 | `me/state` with a token without a valid `sub` |
| `unique_constraint` / `concurrency_conflict` | 409 | Two first submissions of one sentence at the same time; retry |
| `rate_limited` | 429 | Per-minute limit reached (`Retry-After`) |
| `SPEECH_UNAVAILABLE` | 503 | edge-tts upstream failed (the client falls back to `speechSynthesis`) |

## Speech service (edge-tts port)

`EdgeTtsClient` reproduces edge-tts 7.3.1: `wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1`
with `TrustedClientToken`, `ConnectionId`, `Sec-MS-GEC` (`SecMsGecToken`: Windows file time rounded to 5 min +
token → CS64 + SHA-256 from edge-tts' precomputed mid-state, `<16 hex>-<64 hex>`) and `Sec-MS-GEC-Version
2-143.0.3650.75`; Edge headers (Origin `chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold`, user agent, `muid`
cookie). Messages: `speech.config` (`audio-24khz-48kbitrate-mono-mp3`, sentence boundaries) then SSML
(`<voice name='Microsoft Server Speech Text to Speech Voice (en-US, AndrewNeural)'><prosody pitch='+0Hz' rate='+0%'
volume='+0%'>…`), binary frames `Path:audio` until `turn.end`. A 403 handshake adjusts the clock skew from the server
`Date` header and retries once; a dropped stream is retried once. Timeouts: connect 5 s, receive 10 s, total 15 s.

`CachedSpeechSynthesizer`: file `{SHA-256(voice|rate|text)}.mp3` in `Speech:CacheDirectory` (default
`App_Data/speech-cache` under the content root, gitignored), temp file + atomic move, files < 1 KB re-rendered, one
synthesis per key at a time. `SpeechPrewarmWorker` renders the next sentences queued by `skeleton`
(`Dictation:PrewarmAhead`, default 4, max 8; dedupe 10 minutes; bounded queue drops when full).

Configuration: `Dictation:ProofSecret` (env `Dictation__ProofSecret`; set one in production so proofs stay stable
across instances), `Dictation:ProofIterations` (20 000), `Dictation:PrewarmAhead` (4), `Speech:CacheDirectory`,
`Speech:*TimeoutSeconds`, `Speech:ClientCacheSeconds` (604 800). No API key is needed.

## Importing the data

> **Data is not committed.** `data/dictation/` is gitignored (content pulled from Studychill stays out of the repository). Produce it locally with `python scripts/dictation_data_pipeline.py export` (read-only SSH to the Studychill VPS) and `build`, or get the files from the maintainer, then import them as below.

Data lives in `data/dictation/<pack>.json.gz` (schemaVersion 1; `import_report.md` lists counts and sizes), produced by:

```bash
python scripts/dictation_data_pipeline.py export   # read-only snapshot from the Studychill VPS into data/source/ (gitignored)
python scripts/dictation_data_pipeline.py build    # → data/dictation/*.json.gz + import_report.md (deterministic)
```

Apply the migration, then upload **one file per request** (the gateway caps bodies at 2 MB; every file is < 1 MB):

```bash
TOKEN=<admin access token>
for f in data/dictation/toeic-*.json.gz; do
  curl -X POST http://localhost:5050/content/api/v1/dictation/admin/import \
    -H "Authorization: Bearer $TOKEN" -F "files=@$f"
done
```

Expected first run: toeic-250 100/2908, toeic-450 90/2699, toeic-650 79/2398, toeic-800 78/2402, toeic-990 57/1770
(courses/sentences created). Re-running reports everything as `unchanged` (idempotent upsert by `code` /
`sourceCode`). The importer accepts plain `.json` too (gzip is detected by its magic bytes).
