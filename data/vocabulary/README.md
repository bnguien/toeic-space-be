# Vocabulary import data

Official vocabulary for the Vocabulary Learning feature (flashcard, quiz and Reel modes), cleaned from
Studychill's production database. Generated files: do not edit by hand, rebuild instead.

## Files

| File | Content |
|---|---|
| `01-toeic-roadmap-250.json` … `05-toeic-roadmap-990.json` | TOEIC roadmap 0–990, one file per stage |
| `import_report.md` | Counts, reconciliation with the DB, overlap, flagged rows |

Only the TOEIC roadmap is imported. Studychill's Oxford 3000/5000 lists slice the same 4,954 words differently,
and its topic sets are out of scope, so the pipeline excludes them along with the unpublished ETS sets.
Each stage is cut into numbered lessons of 50 words (`Bài 01`, `Bài 02`, …; the last one may be shorter), the same
grouping Studychill shows. Lessons have no descriptive title. Learner progress is keyed by word, not by lesson.

## Stage file format

```json
{
  "schemaVersion": 1,
  "source": "studychill",
  "stage": { "slug": "toeic-roadmap-450", "title": "…", "order": 2, "description": "…", "toeicTarget": 450 },
  "sets": [
    { "slug": "bai-01", "title": "Bài 01", "order": 1, "topic": null,
      "words": [ { "word": "…", "phonetic": "/…/", "partOfSpeech": "noun", "meaningVi": "…",
                   "definitionEn": null, "example": "…", "exampleVi": "…", "cefr": "B1", "synonyms": [] } ] }
  ]
}
```

| Field | Meaning |
|---|---|
| `stage.slug` / `set.slug` | Stable lowercase ASCII kebab-case keys (set slugs are unique within a stage) |
| `stage.order` / `set.order` | Display order, 1-based; files and arrays are already sorted by it |
| `stage.toeicTarget` | Target TOEIC score of a roadmap stage, `null` for other stages |
| `set.topic` | Vietnamese topic name (topics stage only), else `null` |
| `word` | Headword or expression as displayed |
| `phonetic` | IPA between slashes, `""` if unknown |
| `partOfSpeech` | `noun`, `verb`, `adjective`, `adverb`, `preposition`, `conjunction`, `pronoun`, `determiner`, `interjection`, `phrase`, `other` (numbers are `other`) |
| `meaningVi` | Vietnamese meaning, always present. Short (median 9 chars, max 66): primary sense first, near-synonyms after `, `, optional clarifier in `( )`. Used as quiz options |
| `definitionEn` | English definition when Studychill's word catalog (`fc_words`) has one, else `null` |
| `example` / `exampleVi` | Example sentence and its Vietnamese translation, `null` if absent |
| `cefr` | `A1`–`C2` when the word matches Studychill's CEFR list or `fc_words`, else `null` |
| `synonyms` | From `fc_words`, else `[]` |

Words are unique within a set (by lowercase form) and keep Studychill's order.

Quiz options: some words in one set share the same `meaningVi` (bike/bicycle, clear/clearly; listed in
`import_report.md`). When building distractors, exclude candidates whose `meaningVi` equals the answer's.

## Rebuild

```bash
python scripts/vocab_data_pipeline.py export   # read-only snapshot over SSH -> data/source/studychill_vocab_<date>/ (gitignored)
python scripts/vocab_data_pipeline.py build    # newest snapshot -> data/vocabulary/NN-*.json + import_report.md
```

`build` is deterministic: the same snapshot always produces byte-identical files.
