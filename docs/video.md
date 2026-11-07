# Bilingual Video ("Video song ngữ", Content Service, PBL-107)

Learners watch YouTube videos with English + Vietnamese subtitles, click words for meanings, save them to the
PBL-75 library, practise with Cloze ("Điền từ") and Rewrite ("Viết lại"), and resume where they stopped. The catalog
(3,963 curated videos in 8 topics, a listening path Easy/Medium/Hard) and the cached subtitles come from Studychill.
Learners are identified by the JWT `sub`; there is no foreign key to Identity. No XP.

## Data model (4 tables)

| Table | Natural key / indexes | Why it exists |
|---|---|---|
| `VideoCatalogItems` | unique `VideoId`; index `(TopicSlug, IsActive, SubtitleStatus, Position)`; index `(Track, IsActive, Difficulty)` | Curated videos shared by everyone. Topic is stored on the row (`TopicSlug`, `TopicTitle`, `TopicPosition`: 8 fixed topics, no separate table). Level columns (`Difficulty` 0–100, `Cefr`, `Track`, `LevelSource`, `LevelMetrics` JSON) and what the server learned (`SubtitleStatus`, `EmbedStatus`, `EmbedReason`) hide unusable videos. `IsActive` is the admin switch. |
| `VideoSubtitles` | unique `VideoId` | One row per YouTube video ever opened or imported (catalog or pasted link): title/duration snapshot, `OriginalLanguage`, the subtitle language list (`Languages` JSON, null = never listed) and every stored track (`Tracks` JSON). Document-like, so no child table. |
| `VideoWatches` | unique `(LearnerId, VideoId)`; index `(LearnerId, LastOpenedAt)`; index `(IsPublic, LastOpenedAt)`; index `(LearnerId, ProgressAt)` | One row per (learner, video): "Video của tôi" (open count, soft delete), public sharing (`IsPublic`, sharer name/avatar snapshot) and watch progress (`LastPosition`, `WatchedPercent` only grows, `CompletedAt` at ≥ 90 %, `ProgressAt`). |
| `VideoLearnerStates` | PK `LearnerId` | 1:1 learner state that Studychill kept in localStorage: reader settings (`ReaderSettings` JSON), listen mode, chosen path track. |

JSON columns (all keys English, camelCase):

```jsonc
// VideoSubtitles.Languages
{ "manual": [{ "code": "en", "name": "English" }], "auto": [{ "code": "en-orig", "name": "English (Original)" }] }
// VideoSubtitles.Tracks — cues are compact arrays [startSeconds, durationSeconds, text, wordOffsets?]
// wordOffsets (optional) = seconds from the cue start for each word of text.split(" ") (karaoke)
{ "en-orig": { "source": "imported", "cues": [[12.4, 3.1, "so today we are", [0, 0.4, 0.9, 1.2]]] },
  "vi":      { "source": "translated", "cues": [[12.4, 3.1, "vậy hôm nay chúng ta"]] } }
// VideoCatalogItems.LevelMetrics (subtitle measurements behind a scored level)
{ "words": 812, "speechSeconds": 301.5, "wordsPerMinute": 161.6, "sentenceLength": 0, "lexicalScore": 0.142,
  "contentWords": 420, "mix": { "basic": 0.61, "b1": 0.12, "b2": 0.08, "c1": 0.03, "unknownMid": 0.1, "unknownLong": 0.06 } }
// VideoLearnerStates.ReaderSettings (reader "Cài đặt đọc")
{ "fontSize": "s", "theme": "paper", "pauseEachSentence": false, "showPhonetic": false, "showGloss": true,
  "showTranslation": true, "markSavedWords": true, "zoomVideo": false, "glossLanguage": "vi", "hoverGloss": true,
  "showVideoOverlay": false, "highlightTiers": "toeic,b2,c1" }
```

Track `source`: `imported` (Studychill snapshot), `youtube` (downloaded on demand), `translated` (Vietnamese made by
the translation provider). Enum columns are stored as English strings: `SubtitleStatus` Unknown | Available |
Missing, `EmbedStatus` Unknown | Playable | Blocked, `Track` None | Easy | Medium | Hard, `LevelSource` Estimate |
Subtitles.

Not stored (Studychill ops only): XP events, watch sessions for XP, cookie files, proxy flags. Short-lived state
(catalog page cache 5/10 min, quotas, single-flight flags, preparation outcomes, word-card details 14 days) lives in
the process memory cache. The CEFR/TOEIC word-level list (2,312 words + 1,173 basic words, Studychill's bundled
`toeic_vocab.json` / `basic_words.txt`) is static reference data embedded in Infrastructure
(`Services/Video/Resources/word-levels.json`), not a table.

## Code map

```text
Content.API/Controllers
    VideosController              catalog, catalog/{slug}, catalog/unplayable, path, gloss, word-info (anonymous)
    LearnerVideosController       resolve, {videoId}/subs, me, me/{id}, me/progress, me/saved-words, me/state,
                                  {videoId}/cloze/check, {videoId}/rewrite/check (signed in)
    AdminVideosController         admin/import (Admin)
    RateLimiting/VideoRateLimits  per-user/IP one-minute windows (catalog 120, resolve 30, subtitles 60, progress 30,
                                  exercise 60, words 60, unplayable 10)
Content.Application
    Features/VideoCatalog         GetCatalogHome, GetCatalogTopicPage, GetCatalogPath, ReportUnplayable
    Features/VideoSubtitles       ResolveVideo, GetVideoSubtitles
    Features/VideoWatches         GetMyVideos, GetWatchProgress, SaveWatchProgress, SetVideoPublic, RemoveMyVideo
    Features/VideoExercises       CheckCloze, CheckRewrite
    Features/VideoWords           GlossWords, GetWordInfo, GetWordMeanings, GetVideoSavedWords
    Features/VideoLearnerStates   GetVideoLearnerState, UpdateVideoLearnerState
    Features/VideoImport          ImportVideoData
    Services/Video                SubtitleStore (JSON documents), SubtitlePreparationService (background job body),
                                  SubtitleTranslator (" ||| " blocks), CatalogVideoStatusService (subtitle/embed
                                  status, subtitle-based level), VideoGlossService (cache/free/ai on WordEnricher),
                                  WordDetailService, VideoExerciseCues, VideoQuota, VideoCatalogCache,
                                  VideoWordLevelProvider, VideoImportParser
    Interfaces/Services/Video     IYouTubeSubtitleClient, IVideoSubtitlePreparer, IVideoEmbedProbe,
                                  IWordDetailDictionary, IVideoWordLevelSource, IVideoCache
    DTOs/Videos, Models/Videos, Mappings/VideoMappingExtensions
Content.Domain
    Entities: VideoCatalogItem, VideoSubtitle, VideoWatch, VideoLearnerState
    Enums/VideoEnums.cs: VideoSubtitleStatus, VideoEmbedStatus, VideoTrack, VideoLevelSource, SubtitleTrackSource
    ValueObjects: SubtitleCue (compact array JSON), VideoLanguage
    Rules/Videos: VideoLinks, SubtitleLanguageRules, TranscriptText, VideoExerciseGrader, SubtitleTranslationBlocks,
                  ViEnglishLineDetector, VideoLevelGrader, WordLevelRules, WordDetailRules, CatalogPathOrdering,
                  WatchProgressRules, EmbedPlayabilityRules
    Exceptions/VideoErrors.cs
Content.Infrastructure
    Persistence/Configurations/VideoConfiguration.cs, Repositories/Video*Repository.cs
    Services/Video: YouTubeSubtitleClient (YoutubeExplode), VideoEmbedProbe (oEmbed + watch page),
                    FreeWordDetailDictionary (dictionaryapi.dev), VideoSubtitleQueue + VideoSubtitleWorker
                    (Channel + BackgroundService), MemoryVideoCache, EmbeddedVideoWordLevelSource,
                    Resources/word-levels.json (embedded)
```

## Behaviour (ported from Studychill `apps/learning/listening`)

- **Resolve** (`video_resolve`): the language list comes from `VideoSubtitles`; only when it is missing is YouTube
  called, and only then the quota counts (12 new videos per learner per rolling hour). No subtitles at all: a
  catalog video gets `SubtitleStatus = Missing` (hidden) unless YouTube failed temporarily, and `no_english` is
  returned; other videos get `video_unavailable`. A catalog video without real English subtitles is hidden too
  (`no_english`). The original language is mapped to a code that exists (`en-US` → `en-orig`, never the
  alphabetical first code). The learner's `VideoWatches` row is created or reopened (open count, last opened,
  un-deleted, title/duration refreshed).
- **Subtitles** (`video_subs` + `video_subs.prepare`): requested codes the video does not have are dropped (`vi` is
  always kept); nothing left → `lang_unavailable` with a suggested code. Only the store is read in the request.
  Anything missing queues one background job per (video, sorted languages) and the API answers 202 (the reader
  polls 3/5/8 s up to 120 s). The job downloads missing tracks from YouTube (per-word offsets from caption parts),
  translates `vi` from the English track when still missing (blocks of 20 lines joined with `" ||| "`, ≤ 4 blocks
  in parallel; a block that does not split back into its line count becomes empty lines, never shifted), then
  re-translates Vietnamese lines that are just English (`ViEnglishLineDetector`, once per video: checked 30 days,
  failed 6 h). The outcome is remembered (ok 6 h, failure 60 s). With English cues the catalog video is marked
  `Available`, its level is scored from the subtitles when it was still a channel estimate (`VideoLevelGrader`),
  and `levels` (transcript word → B1/B2/C1/C2/TOEIC, Studychill `vocab_levels`) is returned, cached 24 h.
- **Catalog**: visible = active, subtitles not `Missing`, embed not `Blocked`. Home: topics by `TopicPosition`,
  12 videos each (known subtitles first, then position) and per-track counts, cached 5 min. Topic pages 1–30.
  Path: one track ordered by difficulty; inside a band of 6 points topics interleave, shortest first; ≤ 300
  videos, cached 10 min.
- **Unplayable** (`video_embed`): IFrame codes 100/101/150 trigger an oEmbed + watch-page probe (cached 6 h,
  inconclusive 10 min); browser codes (2, 5, 153) never hide; inconclusive never hides.
- **Progress** (`watch_progress`): position/duration clamped to 24 h; the percent only grows; ≥ 90 % sets
  `CompletedAt`; the row must exist (opened through resolve).
- **My / shared videos**: mine = not deleted, last opened first (≤ 200). Shared = other learners' public rows,
  grouped by video, represented by the first sharer (stable name) with `shareCount`; videos I have are excluded.
  Sharing a video someone already shares returns `duplicate {count, firstBy}`.
- **Exercises**: cue indexes count the cleaned cues exactly like the reader (`TranscriptText.CleanCues`). Cloze:
  case/punctuation/diacritics-insensitive, sound tokens and words under 3 letters skipped, ≤ 12 blanks. Rewrite:
  typed words equal the spoken words (apostrophes ignored). No XP.
- **Gloss**: `cache` = stored glosses only; `free` = `WordEnricher` without AI (cache → official catalog →
  dictionary ∥ translation, 8 s); `ai` = signed in only, AI for the first 10 words, the rest free with a 4 s
  deadline; ≤ 80 words. **Word info**: dictionary synonyms/antonyms/one sense per part of speech/US IPA + official
  synonyms and word family, cached 14 days (10 min when the dictionary failed), plus the free gloss as `base`.
- **Learner state**: `PUT me/state` is partial; the reader and the catalog push their part (reader settings +
  listen mode, path track). The FE keeps localStorage for guests and, on sign-in, the newer copy wins (server copy
  adopted, or the later local change pushed).

## Permissions

| Area | Who |
|---|---|
| catalog, catalog/{slug}, catalog/unplayable, path, gloss (cache/free), word-info | anonymous |
| gloss level `ai` | signed in (anonymous falls back to `free`) |
| resolve, subs, me/*, cloze/rewrite check | any signed-in role; learner = JWT `sub` |
| admin/import | `Admin` policy |

## API reference

Routes through the gateway: `/content/api/v1/videos/...`. JSON camelCase.

| Method & route | Auth | Response |
|---|---|---|
| POST `resolve` `{ url }` | user | `{ videoId, title, duration, language, manual: [{code,name}], auto: [...] }` |
| GET `{videoId}/subs?langs=en-orig,vi` (≤ 2) | user | 200 `{ videoId, cues: {code: Cue[]}, counts, unavailable, levels }` · 202 `{ status: "processing", retryAfter: 3, videoId }` |
| GET `catalog` | anonymous | `{ topics: [{ slug, title, tracks: {easy,medium,hard}, videos, hasMore, nextOffset }] }` |
| GET `catalog/{slug}?offset=0&limit=12` | anonymous | `{ slug, title, videos, hasMore, nextOffset }` |
| POST `catalog/unplayable` `{ videoId, code }` | anonymous | `{ hidden }` |
| GET `path?track=easy` (easy, medium, hard) | anonymous | `{ track, tracks, videos: [catalog card + topic, topicTitle] }` |
| GET `me` | user | `{ mine: WatchedVideoDto[], shared: WatchedVideoDto[] }` |
| PATCH `me/{id}` `{ isPublic, displayName?, avatarUrl? }` | user | `{ video, duplicate: {count, firstBy} or null }` |
| DELETE `me/{id}` | user | 204 |
| GET `me/progress` | user | `{ items: [{ videoId, title, duration, position, percent, completed, updatedAt }] }` |
| PUT `me/progress/{videoId}` `{ position, duration }` | user | `{ videoId, position, percent, completed }` |
| GET `me/saved-words` | user | `{ total, items: [{ savedWordId, wordId, headword, normalizedHeadword, folderId, note, meaningVi, phonetic, partOfSpeech, isCustom, savedAt }] }` |
| GET `me/state` | user | `{ readerSettings (object or null), listenMode, pathTrack, updatedAt }` |
| PUT `me/state` `{ readerSettings?, listenMode?, pathTrack? }` | user | same |
| POST `{videoId}/cloze/check` `{ lang, cue, answers: {tokenIndex: text} }` | user | `{ results: {tokenIndex: bool} }` |
| POST `{videoId}/rewrite/check` `{ lang, cue, words }` | user | `{ correct }` |
| POST `gloss` `{ words, level }` | anonymous | `{ gloss: {word: { meaning, ipa, partOfSpeech, example, exampleTranslation, definition, source, officialWordId }}, missing, level }` |
| GET `word-info?w=word` | anonymous | `{ word, ipa, partOfSpeech, synonyms, antonyms, definitions: [{partOfSpeech, definition, example}], family: [{word, meaning, partOfSpeech}], base }` |
| GET `word-info?words=a,b` (≤ 20) | anonymous | `{ items: {word: gloss} }` |
| POST `admin/import` multipart | Admin | `{ videos, subtitles, tracks: {created, updated, unchanged}, files }` |

`WatchedVideoDto`: `{ id, videoId, title, duration, language, isPublic, openCount, mine, userName, userAvatar,
shareCount, thumbnail, lastOpenedAt }`. The access token has no name claim, so `userName`/`userAvatar` are the
snapshot the client sends when sharing (fallback "Học viên TOEIC Space").

## Error codes

| Code | Status | When |
|---|---|---|
| `invalid_video_url` | 400 | No YouTube id in the link |
| `video_quota_exceeded` | 429 | More than 12 new videos per hour (resolve/subs that must call YouTube) |
| `no_english` | 404 | Catalog video without English subtitles (hidden); extension `videoId` |
| `video_unavailable` | 404 | No subtitles at all / removed / YouTube failed (non-catalog); extension `videoId` |
| `lang_unavailable` | 400 | Requested codes not in the language list; extensions `unavailable`, `suggested` |
| `upstream_unavailable` | 503 | The background job produced nothing (YouTube blocked, translation down); extension `unavailable` |
| `video_topic_not_found` | 404 | Unknown topic slug |
| `watched_video_not_found` | 404 | Progress/share/delete/exercise on a video the learner never opened |
| `subtitle_cue_not_found` | 404 | Exercise cue index out of range |
| `invalid_word` | 400 | word-info `w` is not a single word |
| `validation_error` | 400 | Request validation (track, langs, answers, listenMode, ids) |
| `invalid_import_file` | 400 | Unreadable JSON/gzip, wrong schemaVersion, unknown topic, invalid id; nothing written |
| `invalid_subject` | 401 | Token without a valid `sub` |
| `rate_limited` | 429 | Per-user limit (`Retry-After`) |

YouTube and translation failures are always coded results, never 500.

## Configuration

No new settings. YouTube is reached through YoutubeExplode with its own client headers; oEmbed/watch page and
dictionaryapi.dev use a browser User-Agent; translation reuses `ITranslationProvider` (`GoogleTranslate:ApiKey`
optional) and AI reuses the Gemini settings of the vocabulary feature. Short caches live in process memory (one
API instance); a scaled-out deployment would only repeat some YouTube/translation work.

## Importing the data

Data: `data/videos/catalog.json` (3,963 catalog videos, 8 topics) and `data/videos/subtitles-01..14.json.gz`
(3,384 subtitle documents, 3,464 tracks: `en-orig` 2,586, `en` 656, `en-GB` 90, `en-US` 29, `en-CA` 3, `vi` 100;
1,005,817 cues, 787,068 with word offsets). Format, counts and sizes: `data/videos/README.md`. Produced by
`python scripts/video_data_pipeline.py export` (read-only SSH snapshot of the Studychill VPS into the gitignored
`data/source/studychill_videos_<date>/`) and `python scripts/video_data_pipeline.py build` (also writes the
embedded word-level resource). Apply the migration, then upload one file per request (each file ≤ 1.9 MB so it
passes the gateway's 2 MB cap; any multipart field name; re-importing reports everything as unchanged):

```bash
TOKEN=<admin access token>
for f in data/videos/catalog.json data/videos/subtitles-*.json.gz; do
  curl -sS -X POST http://localhost:5050/content/api/v1/videos/admin/import \
    -H "Authorization: Bearer $TOKEN" -F "files=@$f"
done
```

Catalog rows are upserted by `videoId` with every field of the snapshot. Subtitle documents are upserted by
`videoId`: the language list is replaced and every track present in the file replaces the stored track of that
language; tracks the file does not contain (downloaded from YouTube or translated later) are kept.
