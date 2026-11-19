# Bilingual News ("Báo song ngữ", Content Service, PBL-107)

## Data model

Two tables. Article bodies and vocabulary are documents (always read and written as a whole), so they are JSON
columns, not child tables. Everything the reader keeps per learner lives in **one** row per (learner, article).

| Table | Natural key / unique indexes | Other indexes | Why it exists |
|---|---|---|---|
| `NewsArticles` | `Slug` | `(Status, Stage, PublishedAt)`, `(Status, Topic, PublishedAt)` | One article: source + licence, titles, `Paragraphs` JSON (`[{id, sentences:[{id, en, vi}]}]`), `Vocabulary` JSON (`[{word, ipa, pos, vi, level}]`), `WordCount`, `Minutes`, `Stage` (c1–c5), `Topic` (slug), cover (`CoverFile` = content-addressed `<hash>.webp` in the cover store, alt, credit, licence, source URL), `AudioUrl`, `Status` (Draft, Review, Published, Hidden), `Featured`. The indexes serve the listing filters (stage, topic) of published articles in publication order. |
| `NewsReadings` | `(LearnerId, ArticleId)` (FK `ArticleId` → `NewsArticles`, cascade) | `(LearnerId, CompletedAt)`, `(LearnerId, SettingsUpdatedAt)` | One learner reading one article: `OpenedAt` (first open; drives "Đang đọc dở"), `CompletedAt` ("Đã đọc"), `ReadingSeconds` (reading clock, never decreases), `KnownWords` JSON (words marked "Mình biết từ này"), `Notes` (the "Ghi chú" tab), `Dictation` JSON (`{sentenceId: {done, clean, revealed, attempts, input}}`, the "Nghe chép" progress), `Settings` JSON + `SettingsUpdatedAt` (reader settings), `UpdatedAt`. |

Not stored: XP and reward bookkeeping (dropped), looked-up words (session only, as in Studychill), saved words (they
live in the PBL-75 library), counters (done / reading counts and facets are `COUNT` queries on the indexes above).

**Reader settings without a third table.** Palette, translation mode, layout, font size, dark mode, side-panel width,
column split, view toggles, Hack não mode and voice are learner-wide. They are written to the `Settings` column of the
reading the learner is on, with `SettingsUpdatedAt`; opening any article returns the most recently written
`Settings` of the learner (index `(LearnerId, SettingsUpdatedAt)`), so settings follow the learner across articles
and devices without a separate one-row-per-learner table.

Invariants:

1. Only `Published` articles are listed, opened or graded; a hidden article keeps its readings.
2. Importing never deletes; articles are upserted by `Slug`. `WordCount` and `Minutes` (`max(1, round(words / 160))`)
   are recomputed on import, sentence ids are renumbered `p<paragraph>s<sentence>`.
3. `ReadingSeconds` only grows (max of stored and sent); completing twice keeps the first `CompletedAt`.
4. A passed dictation sentence stays passed; `unaided` = passed without having revealed the answer first.

## Responsibility and code map

Content owns the bilingual news reader ported from Studychill "Báo song ngữ" (FE `src/modules/news`, routes
`/games/news` and `/games/news/:slug`): published VOA Learning English stories with a Vietnamese translation per
sentence, prepared vocabulary and an openly licensed cover, plus everything a signed-in learner keeps per article.
XP / rewards, Turnstile and the SEO hook of Studychill are not ported. Learners are identified by the JWT `sub`.

```text
Content.API
    Controllers/NewsController           articles, articles/{slug}, articles/{slug}/dictation, lookup, covers/{file} (anonymous)
    Controllers/NewsReadingController    articles/{slug}/open|complete|state, me/merge (signed in)
    Controllers/AdminNewsController      admin/import (Admin)
    RateLimiting/NewsRateLimits          per user / IP: read 120/min, write 90/min, guest lookup 60/min
    Program.cs                           covers path excluded from the API no-store header (long cache)
Content.Application
    Features/News/Queries                GetNewsArticles, GetNewsArticle, LookupNewsWord
    Features/News/Commands               OpenNewsArticle, CompleteNewsArticle, UpdateNewsReadingState,
                                         MergeGuestReadings, CheckNewsDictation
    Features/NewsImport/Commands         ImportNewsArticles
    DTOs/News                            NewsArticleDtos, NewsReadingDtos, NewsImportDtos
    Models/News                          NewsArticleSummary/Filter, NewsCardProgress, NewsSettings
    Models/NewsImport                    NewsArticlesDocument (shape of data/news/articles.json)
    Services/NewsImportParser            validation + normalisation of imported articles (pure)
    Services/NewsReadingService          published article, get-or-open reading, state presenter
    Interfaces                           INewsArticleRepository, INewsReadingRepository, INewsCoverStore
    Common/Validation/NewsRules          shared limits + NewsReaderSettingsValidator
    Mappings/NewsMappingExtensions
Content.Domain
    Entities/NewsArticle (+ NewsParagraph, NewsSentence, NewsVocabularyItem)
    Entities/NewsReading (+ NewsDictationEntry, NewsReaderSettings)
    Enums/NewsEnums (NewsArticleStatus), Rules/NewsCatalog, Rules/DictationGrader, Exceptions/NewsErrors
Content.Infrastructure
    Persistence/Configurations/NewsConfiguration, Repositories/NewsArticleRepository, NewsReadingRepository
    Services/News/FileSystemNewsCoverStore
scripts/news_data_pipeline.py            export | build | import
data/news                                articles.json, covers/<hash>.webp, import_report.md
```

## API reference

Gateway prefix `/content/api/v1/news`. JSON camelCase, timestamps ISO UTC.

| Method & route | Auth | Response |
|---|---|---|
| GET `articles?stage=&topic=&q=&length=&state=&page=&pageSize=` | anonymous (a token personalises) | `NewsArticleListDto { items, total, page, pageSize, facets {stage, topic}, doneCount, readingCount }`. `stage` c1–c5 and `topic` slug (unknown values do not filter), `q` ≤ 80 chars on English title, Vietnamese title or source, `length` short (< 3 min) / medium (3–6) / long (> 6), `state` reading (opened, not completed; newest open first) / done (newest completion first; guests get nothing), `page` 1..500, `pageSize` 1..60 (default 60). Order: featured, then publication date desc. Cards carry the viewer's `done` and `readingSeconds` |
| GET `articles/{slug}` | anonymous | `NewsArticleDetailDto` with `paragraphs [{id, sentences [{id, text, translation}]}]`, `vocabulary [{word, ipa, partOfSpeech, meaning, level}]`, source, licence, attribution, cover, audio |
| POST `articles/{slug}/dictation` `{ sentenceId, text, revealed }` | anonymous (a token records the attempt) | `DictationResultDto { correct, words [{text, ok}], correctCount, total, extra, recorded }` |
| POST `lookup` `{ text, context? }` | anonymous | `WordLookupDto` (PBL-75 shape) from the fast tiers only (cache, catalog, dictionary, translation, never AI). Signed-in readers call `vocabulary/lookup`, which may use AI when the fast result is incomplete |
| GET `covers/{hash}.webp` | anonymous | `image/webp`, `Cache-Control: public, max-age=31536000, immutable` |
| POST `articles/{slug}/open` | user | `NewsReadingStateDto` (first open recorded; `settings` = the learner's most recent reader settings) |
| POST `articles/{slug}/complete` | user | `NewsReadingStateDto` (`done: true`; the first completion time is kept; no reward) |
| PUT `articles/{slug}/state` `{ readingSeconds?, knownWords?, notes?, dictation?, resetDictation?, settings? }` | user | `NewsReadingStateDto`. Seconds only grow; known words (vocabulary words of the article only, ≤ 40) and notes (≤ 4000, "" clears) replace; dictation entries `{passed, unaided, revealed, attempts, input}` merge forward; settings replace |
| POST `me/merge` `{ items: [{ slug, readingSeconds, completed, knownWords?, notes?, dictation?, updatedAt? }] }` (1..300) | user | `{ merged, skipped }`: guest browser state after sign-in; clock and completion take the larger value, known words / notes keep the newer copy (`updatedAt` against the stored row), dictation only moves forward; unknown slugs are skipped |
| POST `admin/import` multipart (`.json`, `.json.gz`, `<hash>.webp`, any field name, ≤ 400 files, 64 MB) | Admin | `NewsImportResultDto { articlesCreated, articlesUpdated, articlesUnchanged, coversStored, coversUnchanged, missingCovers }` |

`NewsReadingStateDto`: `slug, done, openedAt, completedAt, readingSeconds, knownWords, notes, dictation {sentenceId:
{passed, unaided, revealed, attempts, input, result}}` (the per-word `result` is recomputed from the last input),
`dictationTotal, settings {palette, mode english|bilingual, layout inline|columns, fontSize 15..26, dark, panelWidth,
split 30..70, showSuggested, showLookedUp, showMeaning, hackMode english|vietnamese|dictation, voice},
settingsUpdatedAt, updatedAt`.

Dictation grading (`DictationGrader`, port of Studychill `grade_dictation`): words are compared by a key (lower case,
curly apostrophes straightened, everything but `a-z0-9'` removed, outer apostrophes trimmed, so `four-day` =
`fourday`) and aligned by longest common subsequence; correct = every answer word matched and no extra word.

## Error codes

| Code | Status | When |
|---|---|---|
| `validation_error` | 400 | Query/body limits (length/state values, page size, sentence id `p<n>s<n>`, settings ranges, import file types/sizes) |
| `invalid_import_file` | 400 | Unreadable JSON / gzip, schemaVersion ≠ 1, bad slug, non-https source, unknown licence/stage/topic/status, no sentence, publishing without full translation + Vietnamese title + usable cover, duplicate slug, cover not WebP or not named `<hash>.webp`. Nothing is written |
| `invalid_subject` | 401 | Token without a valid `sub` |
| `news_article_not_found` | 404 | Unknown or unpublished slug |
| `news_sentence_not_found` | 404 | Dictation on a sentence id the article does not have |
| `news_cover_not_found` | 404 | Cover file not in the store |
| `unique_constraint` | 409 | Two simultaneous first writes of one reading (open handles it; retry otherwise) |
| `rate_limited` | 429 | Per-user/IP limit reached |

## Covers

The pipeline resizes covers to at most 960 px wide WebP (quality 75), named by the first 32 hex characters of their
SHA-256. The import stores them in `News:CoverDirectory` (default `App_Data/news/covers` under the API content root;
mount a volume there in Docker). `News:CoverBaseUrl` (default `/content/api/v1/news/covers`) is the browser path
returned as `coverUrl`. The Content API and the gateway both exclude the covers path from the API `no-store` header,
so browsers cache each image for a year (a name never changes). Cover credit and licence are shown under every
article (required for CC BY / BY-SA).

## Importing the news

> **Data is not committed.** `data/news/` is gitignored (content pulled from Studychill stays out of the repository). Produce it locally with `python scripts/news_data_pipeline.py export` (read-only SSH to the Studychill VPS) and `build`, or get the files from the maintainer, then import them as below.

Data: `data/news/articles.json` (schemaVersion 1; 287 published articles, 10,718 sentences, 4,235 vocabulary items,
4.5 MB; `articles.json.gz` is written instead once the file passes 5 MB) and `data/news/covers/` (287 WebP, 21.6 MB).
`data/news/import_report.md` has the counts per stage and topic. Article shape:

```json
{ "slug": "voa-2453053", "status": "published", "featured": false,
  "source": { "name", "url", "id", "series", "level", "author", "publishedAt", "license", "attribution" },
  "titleEn", "titleVi", "summaryVi", "stage": "c3", "topic": "education",
  "paragraphs": [{ "id": "p1", "sentences": [{ "id": "p1s1", "text", "translation" }] }],
  "vocabulary": [{ "word", "ipa", "partOfSpeech": "n", "meaning", "level": "easy|medium|hard" }],
  "cover": { "file": "<hash>.webp", "width", "height", "alt", "credit", "license", "sourceUrl" },
  "audioUrl": "https://…mp3" }
```

Steps (idempotent: articles upsert by slug, identical covers are skipped):

1. Apply the migration that creates `NewsArticles` and `NewsReadings`.
2. `python scripts/news_data_pipeline.py export`: read-only snapshot over SSH (`studychill` alias) of the published rows
   of Studychill `news_article` (`manage.py shell`) and a `tar` of `/app/media/news`, into
   `data/source/studychill_news_<date>/` (gitignored).
3. `python scripts/news_data_pipeline.py build`: maps Studychill keys to English (`doan`→`paragraphs`, `cau`→`sentences`,
   `en`/`vi`→`text`/`translation`, `vocab`→`vocabulary` with `pos`/`vi`/`cap`→`partOfSpeech`/`meaning`/`level`
   easy/medium/hard; topics `kinh-doanh`→`business`, `cong-nghe`→`technology`, `doi-song`→`lifestyle`,
   `du-lich`→`travel`, `moi-truong`→`environment`, `giao-duc`→`education`), renames Studychill brand mentions to
   TOEIC Space (10 easier "-de" versions), resizes the covers and writes `data/news`.
4. `python scripts/news_data_pipeline.py import --url http://localhost:5050/content/api/v1/news/admin/import --token "$TOKEN"`:
   uploads the covers in batches under the gateway's 2 MB request cap, then the articles as gzip chunks (all 287 fit
   in one ~1.6 MB request today). Re-running reports everything unchanged. The Content API called directly accepts
   up to 64 MB per request.
