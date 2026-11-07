# Video song ngữ — import data

Produced by `python scripts/video_data_pipeline.py export` (read-only snapshot of the Studychill VPS) and
`python scripts/video_data_pipeline.py build` from `data/source/studychill_videos_20261107`. Do not edit by hand:
re-run the pipeline. The build is deterministic (sorted rows, compact JSON, gzip mtime 0).

## Files

| File | Size |
|---|---|
| `catalog.json` | 1.77 MB (1,852,773 bytes) |
| `subtitles-01.json.gz` | 1.81 MB (1,894,930 bytes) |
| `subtitles-02.json.gz` | 1.81 MB (1,894,255 bytes) |
| `subtitles-03.json.gz` | 1.81 MB (1,897,276 bytes) |
| `subtitles-04.json.gz` | 1.81 MB (1,899,945 bytes) |
| `subtitles-05.json.gz` | 1.80 MB (1,892,138 bytes) |
| `subtitles-06.json.gz` | 1.80 MB (1,891,289 bytes) |
| `subtitles-07.json.gz` | 1.80 MB (1,886,579 bytes) |
| `subtitles-08.json.gz` | 1.81 MB (1,897,668 bytes) |
| `subtitles-09.json.gz` | 1.81 MB (1,899,482 bytes) |
| `subtitles-10.json.gz` | 1.81 MB (1,892,894 bytes) |
| `subtitles-11.json.gz` | 1.81 MB (1,899,956 bytes) |
| `subtitles-12.json.gz` | 1.81 MB (1,897,663 bytes) |
| `subtitles-13.json.gz` | 1.77 MB (1,853,071 bytes) |
| `subtitles-14.json.gz` | 1.06 MB (1,115,060 bytes) |
| **Total** | **26.29 MB (27,564,979 bytes)** |

Every file is below 1.9 MB, so each one passes the gateway's 2 MB request-body cap when uploaded alone.

## `catalog.json`

`{ "schemaVersion": 1, "topics": [...], "videos": [...] }` — Studychill `ls_catalog_topic` / `ls_catalog_video`
(soft-deleted rows excluded; inactive rows kept with `isActive: false`).

- topic: `slug` (natural key), `title`, `position`, `isActive`
- video: `videoId` (natural key), `topic` (slug), `title`, `channelHandle`, `channelTitle`, `duration` (s), `level`
  (channel CEFR), `thumbnail`, `viewCount`, `position` (order inside the topic), `isActive`, `subtitleStatus`
  (`unknown` | `available` | `missing`), `embedStatus` (`unknown` | `playable` | `blocked`), `embedReason`,
  `difficulty` (0–100), `cefr`, `track` (`easy` | `medium` | `hard` | `""`), `levelSource` (`estimate` = from the
  channel level, `subtitles` = scored from subtitles). Studychill's `ok`/`none`/`est`/`subs` are renamed here.

Counts: 8 topics, 3,963 videos — active 3,505, inactive
458; track easy 1,115 / medium 1,708 /
hard 1,140 / none 0; subs ok 3,324 / unknown
633 / none 6; embed ok 3,505 /
unknown 458 / blocked 0; level source subs
3,327 / est 636.

## Word levels (embedded, not imported)

Written to `src/Services/Content/ToeicSpace.Content.Infrastructure/Services/Video/Resources/word-levels.json` and
embedded in the Content service: `{ "schemaVersion": 1, "levels": { "<word>": "<CEFR or empty>" }, "basic": ["<word>", ...] }`

- `levels`: every entry of Studychill `vocabulary/data/toeic_vocab.json` (the TOEIC word store behind
  `dictionary.vocab_levels` and `video_level.py`), CEFR uppercased; empty = in the store without a CEFR (shown as
  "TOEIC"). A1/A2 entries are kept: they block the base-form search exactly like in Studychill.
  2,312 words ((none) 87, A2 6, B1 535, B2 1,377, C1 306, C2 1).
- `basic`: Studychill `listening/data/basic_words.txt` (A1–A2 words of 5+ letters used by subtitle difficulty
  scoring), lowercased, de-duplicated, sorted: 1,173 words.

## `subtitles-NN.json.gz`

`{ "schemaVersion": 1, "languages": [...], "tracks": [...] }` (gzip). A video's language list and its tracks are in
the same chunk; catalog videos come first (catalog order), then other cached videos (links learners pasted) by id.

- language list (`*.langs.v2.json` of the Studychill cache): `videoId`, `manual` `[{code, name}]` (complete),
  `auto` (trimmed to codes starting with `en`/`vi` or ending with `-orig` — enough for `pick_original_language` and
  `has_english`; the full YouTube list has ~156 machine translations), `title`, `duration`, `language`.
- track: `videoId`, `lang`, `cues` = `[[start s, duration s, text, offsets?], ...]` exactly as cached by Studychill
  (v2: `offsets` = per-word start offsets in seconds from the cue start, matching `text.split(" ")`).

Counts: 3,384 videos (3,334 catalog,
50 other), 3,384 language lists, 3,464
tracks (`en` 656, `en-CA` 3, `en-GB` 90, `en-US` 29, `en-orig` 2,586, `vi` 100). Only one English track per video is kept (the one the reader picks) because the full set exceeded 25 MB.
Catalog videos without a cached English track: 636
(249 of them visible in the catalog) — their subtitles are fetched from
YouTube on first open.

## Import

Apply the migration first, then upload one file per request (any multipart field name; `.json` and `.json.gz`
are both accepted; re-importing is idempotent — natural keys `videoId` for catalog rows and subtitle documents, (`videoId`, `lang`) for tracks):

```bash
TOKEN=<admin access token>
for f in data/videos/catalog.json data/videos/subtitles-*.json.gz; do
  curl -sS -X POST http://localhost:5050/content/api/v1/videos/admin/import \
    -H "Authorization: Bearer $TOKEN" -F "files=@$f"
done
```

Directly against the Content API (no gateway) use `http://localhost:<content-port>/api/v1/videos/admin/import`.
