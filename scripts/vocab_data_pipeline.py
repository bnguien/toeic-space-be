#!/usr/bin/env python3
"""
TOEIC Space - Studychill vocabulary data pipeline
=================================================

Pulls the official Studychill vocabulary content (flashcard sets organised in collections), cleans it and
writes the committed import files in data/vocabulary/. The words feed the flashcard, quiz and Reel modes.

Commands
--------
  export  Read-only snapshot over SSH (host alias "studychill"). Runs read-only ORM queries through
          `manage.py shell` fed on stdin, so nothing is written on the server and no database credentials
          are needed locally. Only official content leaves the server (VocabSet.owner IS NULL): learners'
          own sets, saved words and progress are never exported.
  build   Clean, normalise and de-duplicate the snapshot into data/vocabulary/NN-<stage>.json and
          import_report.md. Deterministic: the same snapshot gives byte-identical files.

Usage
-----
  python scripts/vocab_data_pipeline.py export
  python scripts/vocab_data_pipeline.py build                       # newest data/source/studychill_vocab_*
  python scripts/vocab_data_pipeline.py build --source data/source/studychill_vocab_20261101
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import gzip
import html
import json
import re
import subprocess
import sys
import unicodedata
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

BASE_DIR = Path(__file__).resolve().parent.parent
SOURCE_ROOT = BASE_DIR / "data" / "source"
OUTPUT_DIR = BASE_DIR / "data" / "vocabulary"
REPORT_FILE = OUTPUT_DIR / "import_report.md"
SNAPSHOT_FILE = "studychill_vocab.json.gz"
# Studychill's CEFR word list (bundled with its backend, not in the DB). Copied into the snapshot by
# `export` so `build` only depends on the snapshot folder.
CEFR_LIST_REMOTE = "/root/Studychill/backend/apps/learning/vocabulary/data/toeic_vocab.json"
CEFR_LIST_FILE = "toeic_vocab.json.gz"

SCHEMA_VERSION = 1
BEGIN, END = "@@VOCAB_EXPORT_BEGIN@@", "@@VOCAB_EXPORT_END@@"

# Read-only ORM script executed by `manage.py shell` on the server (fed on stdin, never stored there).
REMOTE_EXPORT_SCRIPT = r'''
import json, datetime
from apps.learning.vocabulary.models import VocabSet, VocabSetItem, FcWord

def rows(qs, fields):
    return [dict(zip(fields, r)) for r in qs.values_list(*fields)]

SET_FIELDS = ("id", "parent_id", "title", "description", "level", "is_published", "is_deleted",
              "collection", "collection_order")
ITEM_FIELDS = ("id", "vocab_set_id", "word", "word_norm", "phonetic", "pos", "meaning", "example",
               "example_vi", "order", "is_deleted")
FCWORD_FIELDS = ("id", "exam", "level", "category", "word", "phonetic", "word_type", "vi", "en", "example",
                 "example_vi", "synonyms", "is_active", "is_deleted")

official = VocabSet.objects.filter(owner__isnull=True)

data = {
    "exportedAt": datetime.date.today().isoformat(),
    "counts": {
        "vocabSetsTotal": VocabSet.objects.count(),
        "vocabSetsOfficial": official.count(),
        "vocabSetItemsTotal": VocabSetItem.objects.count(),
        "vocabSetItemsOfficial": VocabSetItem.objects.filter(vocab_set__owner__isnull=True).count(),
        "fcWords": FcWord.objects.count(),
    },
    "vocabSets": rows(official.order_by("id"), SET_FIELDS),
    "vocabSetItems": rows(VocabSetItem.objects.filter(vocab_set__owner__isnull=True).order_by("id"), ITEM_FIELDS),
    "fcWords": rows(FcWord.objects.filter(exam="toeic").order_by("id"), FCWORD_FIELDS),
}
print("@@VOCAB_EXPORT_BEGIN@@")
print(json.dumps(data, ensure_ascii=False, default=str))
print("@@VOCAB_EXPORT_END@@")
'''

# --------------------------------------------------------------------------------------------
# Stage model
# --------------------------------------------------------------------------------------------
# Studychill groups official root sets by VocabSet.collection (+ collection_order); each root is a folder of
# lesson sets (depth 1). The "ets-2026" collection is unpublished on Studychill after a copyright complaint
# (2026-10) and is never imported.
# Only the TOEIC roadmap is imported (PBL-75 decision, 2026-11-01): Oxford 3000/5000 reuse the same words
# sliced differently, and topics are out of scope for now.
EXCLUDED_COLLECTIONS = {"ets-2026", "oxford-3000", "oxford-5000", "topics"}

# (collection, collection_order or None for "every root of the collection") -> stage
# Stage names and taglines mirror Studychill's roadmap (frontend/src/features/vocab/VocabPacks.jsx).
STAGES = [
    {"key": ("toeic-roadmap", 1), "slug": "toeic-roadmap-250", "toeicTarget": 250,
     "title": "Khởi động", "description": "Mất gốc → nền tảng"},
    {"key": ("toeic-roadmap", 2), "slug": "toeic-roadmap-450", "toeicTarget": 450,
     "title": "Phản xạ", "description": "Tình huống đời sống"},
    {"key": ("toeic-roadmap", 3), "slug": "toeic-roadmap-650", "toeicTarget": 650,
     "title": "Tăng tốc", "description": "Giao tiếp & văn phòng"},
    {"key": ("toeic-roadmap", 4), "slug": "toeic-roadmap-800", "toeicTarget": 800,
     "title": "Bứt phá", "description": "Tác phong chuyên nghiệp"},
    {"key": ("toeic-roadmap", 5), "slug": "toeic-roadmap-990", "toeicTarget": 990,
     "title": "Về đích", "description": "Học thuật & điểm tối đa"},
]

# Studychill shows each stage as numbered lessons of 50 words ("Bài 01", "Bài 02", ...) regardless of how the
# source sets were titled, and the product owner wants the same grouping.
LESSON_SIZE = 50

# Topic root (collection "topics", collection_order) -> stable topic slug + short Vietnamese name.
TOPIC_NAMES = {
    1: ("business-office", "Công sở & thương mại"),
    2: ("business", "Kinh doanh"),
    3: ("travel", "Du lịch"),
    4: ("food", "Ẩm thực"),
    5: ("health", "Sức khỏe"),
    6: ("technology", "Công nghệ"),
    7: ("education", "Giáo dục"),
    8: ("daily-life", "Đời sống hằng ngày"),
    9: ("environment", "Môi trường"),
}

POS_ENUM = ("noun", "verb", "adjective", "adverb", "preposition", "conjunction", "pronoun", "determiner",
            "interjection", "phrase", "other")
POS_ALIASES = {
    "n": "noun", "noun": "noun", "danh từ": "noun", "dt": "noun", "n.": "noun", "nouns": "noun",
    "v": "verb", "verb": "verb", "động từ": "verb", "đt": "verb", "v.": "verb", "phrasal verb": "phrase",
    "modal verb": "verb", "auxiliary verb": "verb", "linking verb": "verb", "modal": "verb", "aux": "verb",
    "adj": "adjective", "adjective": "adjective", "tính từ": "adjective", "a": "adjective", "adj.": "adjective",
    "adv": "adverb", "adverb": "adverb", "trạng từ": "adverb", "adv.": "adverb",
    "prep": "preposition", "preposition": "preposition", "giới từ": "preposition", "prep.": "preposition",
    "conj": "conjunction", "conjunction": "conjunction", "liên từ": "conjunction", "conj.": "conjunction",
    "pron": "pronoun", "pronoun": "pronoun", "đại từ": "pronoun", "pron.": "pronoun",
    "det": "determiner", "determiner": "determiner", "article": "determiner", "definite article": "determiner",
    "indefinite article": "determiner", "number": "determiner", "ordinal number": "determiner",
    "exclamation": "interjection", "interjection": "interjection", "thán từ": "interjection", "excl": "interjection",
    "phrase": "phrase", "cụm từ": "phrase", "idiom": "phrase", "phr": "phrase", "collocation": "phrase",
    "n.phr": "phrase", "v.phr": "phrase", "adj.phr": "phrase", "adv.phr": "phrase", "prep.phr": "phrase",
    # Cardinal/ordinal numbers: the target enum has no numeral; they are kept as "other" (see README).
    "numeral": "other", "num": "other",
    "other": "other", "": "other",
}
CEFR_LEVELS = ("A1", "A2", "B1", "B2", "C1", "C2")
# meaningVi is shown as a quiz option; longer values are flagged for manual shortening, not cut.
MEANING_MAX_LENGTH = 40

TAGS = re.compile(r"<[^>]+>")
SPACES = re.compile(r"\s+")
NON_SLUG = re.compile(r"[^a-z0-9]+")
LESSON_PREFIX = re.compile(r"^bài\s*\d+\s*·\s*", re.IGNORECASE)
LEADING_LESSON = re.compile(r"^(?:bài|bai|lesson|unit|part|phần)\s*(\d+)\s*[:.\-–·]?\s*", re.IGNORECASE)
LATIN_WORD = re.compile(r"^[A-Za-z][A-Za-z .,'’\-/()&!?]*$")
VIETNAMESE_CHARS = re.compile(r"[ăâđêôơưàáạảãằắặẳẵầấậẩẫèéẹẻẽềếệểễìíịỉĩòóọỏõồốộổỗờớợởỡùúụủũừứựửữỳýỵỷỹ]",
                              re.IGNORECASE)


# --------------------------------------------------------------------------------------------
# export
# --------------------------------------------------------------------------------------------

def export_snapshot(args: argparse.Namespace) -> None:
    target = SOURCE_ROOT / f"studychill_vocab_{dt.date.today():%Y%m%d}"
    target.mkdir(parents=True, exist_ok=True)

    print(f"[export] {args.host}: official vocabulary (read-only ORM)")
    result = subprocess.run(
        ["ssh", "-o", "BatchMode=yes", args.host, f"docker exec -i {args.container} python manage.py shell"],
        input=REMOTE_EXPORT_SCRIPT.encode("utf-8"), capture_output=True, check=True)
    stdout = result.stdout.decode("utf-8")
    if BEGIN not in stdout or END not in stdout:
        sys.exit("[export] remote script produced no data:\n" + result.stderr.decode("utf-8", "replace")[-2000:])
    payload = stdout.split(BEGIN, 1)[1].split(END, 1)[0].strip()
    json.loads(payload)  # fail fast on a truncated stream
    destination = target / SNAPSHOT_FILE
    with gzip.open(destination, "wt", encoding="utf-8") as output:
        output.write(payload)
    print(f"         -> {destination} ({destination.stat().st_size:,} bytes)")

    print(f"[export] {args.host}: CEFR word list")
    listing = subprocess.run(["ssh", "-o", "BatchMode=yes", args.host, f"gzip -c {CEFR_LIST_REMOTE}"],
                             capture_output=True, check=True).stdout
    (target / CEFR_LIST_FILE).write_bytes(listing)
    print(f"         -> {target / CEFR_LIST_FILE} ({len(listing):,} bytes)")

    print(f"\nSnapshot ready. Next: python scripts/vocab_data_pipeline.py build --source {target.relative_to(BASE_DIR)}")


# --------------------------------------------------------------------------------------------
# cleaning helpers
# --------------------------------------------------------------------------------------------

def clean_text(value) -> str:
    """NFC, no HTML, entities decoded, single spaces, typographic quotes kept."""
    if value is None:
        return ""
    text = unicodedata.normalize("NFC", str(value))
    text = html.unescape(TAGS.sub(" ", text))
    text = text.replace(" ", " ").replace("​", "").replace("﻿", "")
    return SPACES.sub(" ", text).strip()


def optional(value) -> str | None:
    text = clean_text(value)
    return text or None


def clean_meaning(value) -> str:
    """Vietnamese meaning used on flashcards and as quiz options: primary sense first, near-synonyms
    separated by ", " (";" unified to ","), no trailing punctuation. Content is never reworded."""
    text = clean_text(value).replace(";", ",")
    text = re.sub(r"\s*,\s*", ", ", text)
    text = re.sub(r"(, )+", ", ", text)
    return text.strip(" ,.:")


def norm_key(word: str) -> str:
    """Same idea as Studychill word_norm: lowercase, no surrounding punctuation."""
    return SPACES.sub(" ", clean_text(word).lower().replace("’", "'")).strip(" .,;:!?\"'()[]")


def slugify(text: str) -> str:
    ascii_text = unicodedata.normalize("NFKD", text.replace("đ", "d").replace("Đ", "D"))
    ascii_text = ascii_text.encode("ascii", "ignore").decode("ascii").lower()
    return NON_SLUG.sub("-", ascii_text).strip("-")


def clean_phonetic(value) -> str:
    text = clean_text(value)
    if not text:
        return ""
    core = text.strip("/[] ")
    return f"/{core}/" if core else ""


def normalise_pos(value) -> tuple[str, str | None]:
    """Returns (enum value, raw value when it is not a known label)."""
    raw = clean_text(value).lower()
    if raw in POS_ALIASES:
        return POS_ALIASES[raw], None
    first = re.split(r"[,/;|]| and ", raw)[0].strip().strip(".")
    if first in POS_ALIASES:
        return POS_ALIASES[first], None
    return "other", raw


def clean_synonyms(value, word_key: str) -> list[str]:
    if isinstance(value, str):
        try:
            value = json.loads(value)
        except ValueError:
            value = [part for part in re.split(r"[,;]", value)]
    result, seen = [], {word_key}
    for item in value or []:
        text = clean_text(item)
        key = norm_key(text)
        if text and key not in seen:
            seen.add(key)
            result.append(text)
    return result


# --------------------------------------------------------------------------------------------
# build
# --------------------------------------------------------------------------------------------

def latest_source() -> Path:
    candidates = sorted(p for p in SOURCE_ROOT.glob("studychill_vocab_*") if (p / SNAPSHOT_FILE).exists())
    if not candidates:
        sys.exit("No snapshot found. Run: python scripts/vocab_data_pipeline.py export")
    return candidates[-1]


def load_snapshot(source: Path) -> tuple[dict, list]:
    with gzip.open(source / SNAPSHOT_FILE, "rt", encoding="utf-8") as handle:
        data = json.load(handle)
    cefr_list = []
    if (source / CEFR_LIST_FILE).exists():
        with gzip.open(source / CEFR_LIST_FILE, "rt", encoding="utf-8") as handle:
            cefr_list = json.load(handle)
    return data, cefr_list


def build_lexicon(data: dict, cefr_list) -> dict:
    """word key -> {cefr, definitionEn, synonyms, pos} merged from FcWord (DB) and toeic_vocab.json."""
    lexicon: dict[str, dict] = {}
    # toeic_vocab.json: {"word": {"cefr": "B2", "ipa": "/…/", "pos": "noun", "vi": "…"}}
    for word, entry in sorted((cefr_list or {}).items()):
        key = norm_key(word)
        if not key or not isinstance(entry, dict):
            continue
        info = lexicon.setdefault(key, {})
        level = clean_text(entry.get("cefr")).upper()
        if level in CEFR_LEVELS:
            info["cefr"] = level
        if clean_phonetic(entry.get("ipa")):
            info["phonetic"] = clean_phonetic(entry.get("ipa"))
        pos, unknown = normalise_pos(entry.get("pos"))
        if clean_text(entry.get("pos")) and not unknown:
            info["pos"] = pos
    for row in sorted(data["fcWords"], key=lambda r: r["id"]):
        if row["is_deleted"] or not row["is_active"]:
            continue
        key = norm_key(row["word"])
        if not key:
            continue
        info = lexicon.setdefault(key, {})
        level = clean_text(row["level"]).upper()
        if level in CEFR_LEVELS:
            info.setdefault("cefr", level)
        if clean_phonetic(row["phonetic"]):
            info.setdefault("phonetic", clean_phonetic(row["phonetic"]))
        definition = clean_text(row["en"])
        if definition and "definitionEn" not in info:
            info["definitionEn"] = definition
        synonyms = clean_synonyms(row["synonyms"], key)
        if synonyms and "synonyms" not in info:
            info["synonyms"] = synonyms
    return lexicon


def lesson_order_key(row: dict):
    match = LEADING_LESSON.match(clean_text(row["title"]))
    return (row["collection_order"], int(match.group(1)) if match else 10**6, row["id"])


def build(args: argparse.Namespace) -> None:
    source = Path(args.source) if args.source else latest_source()
    if not source.is_absolute():
        source = BASE_DIR / source
    data, cefr_list = load_snapshot(source)
    lexicon = build_lexicon(data, cefr_list)
    print(f"[build] snapshot {source.relative_to(BASE_DIR)} (exported {data['exportedAt']})")

    sets = {row["id"]: row for row in data["vocabSets"]}
    items_by_set: dict[int, list] = collections.defaultdict(list)
    for item in data["vocabSetItems"]:
        items_by_set[item["vocab_set_id"]].append(item)
    children: dict[int, list] = collections.defaultdict(list)
    for row in sets.values():
        if row["parent_id"]:
            children[row["parent_id"]].append(row)

    report = Report()
    roots = [r for r in sets.values() if not r["parent_id"]]
    for root in sorted(roots, key=lambda r: (r["collection"], r["collection_order"], r["id"])):
        if root["collection"] in EXCLUDED_COLLECTIONS or not root["collection"]:
            report.excluded_roots.append(root)

    for path in OUTPUT_DIR.glob("[0-9][0-9]-*.json"):
        path.unlink()

    for order, stage in enumerate(STAGES, start=1):
        collection, collection_order = stage["key"]
        stage_roots = sorted(
            (r for r in roots if r["collection"] == collection
             and (collection_order is None or r["collection_order"] == collection_order)),
            key=lambda r: (r["collection_order"], r["id"]))
        published_roots = [r for r in stage_roots if r["is_published"] and not r["is_deleted"]]
        for r in stage_roots:
            if r not in published_roots:
                report.flag("root not published / deleted - skipped", stage["slug"], r["title"])
        if not published_roots:
            sys.exit(f"[build] no published root for stage {stage['slug']}")

        title, description = stage["title"], stage["description"]

        # Gather every word of the stage in source-lesson order, then cut it into lessons of LESSON_SIZE.
        stage_items, seen_keys = [], set()
        for root in published_roots:
            lessons = sorted(children.get(root["id"], []), key=lesson_order_key)
            if not lessons and items_by_set.get(root["id"]):
                lessons = [root]  # leaf root
            for lesson in lessons:
                if lesson["is_deleted"] or not lesson["is_published"]:
                    report.flag("lesson set not published / deleted - skipped", stage["slug"], lesson["title"])
                    continue
                source_title = clean_text(lesson["title"])
                words = clean_words(items_by_set.get(lesson["id"], []), lexicon, report, stage["slug"], source_title)
                if not words:
                    report.empty_sets.append((stage["slug"], source_title))
                    continue
                for word in words:
                    key = (norm_key(word["word"]), word["partOfSpeech"])
                    if key in seen_keys:
                        report.flag("duplicate word across source lessons - kept first", stage["slug"], word["word"])
                        continue
                    seen_keys.add(key)
                    stage_items.append(word)

        out_sets = []
        for start in range(0, len(stage_items), LESSON_SIZE):
            number = len(out_sets) + 1
            words = stage_items[start:start + LESSON_SIZE]
            set_title = f"Bài {number:02d}"
            out_sets.append({
                "slug": f"bai-{number:02d}",
                "title": set_title,
                "order": number,
                "topic": None,
                "words": words,
            })
            report.sets.append((stage["slug"], set_title, len(words)))
        stage_words = len(stage_items)
        report.stage_vocab[stage["slug"]].update(norm_key(w["word"]) for w in stage_items)

        document = {
            "schemaVersion": SCHEMA_VERSION,
            "source": "studychill",
            "stage": {
                "slug": stage["slug"],
                "title": title,
                "order": order,
                "description": description,
                "toeicTarget": stage["toeicTarget"],
            },
            "sets": out_sets,
        }
        path = OUTPUT_DIR / f"{order:02d}-{stage['slug']}.json"
        write_json(path, document)
        report.stages.append((order, stage["slug"], title, len(out_sets), stage_words, path.name))
        print(f"         -> {path.relative_to(BASE_DIR)}: {len(out_sets)} sets, {stage_words} words")

    report.write(data, source)
    print(f"         -> {REPORT_FILE.relative_to(BASE_DIR)}")


def clean_words(items: list, lexicon: dict, report: "Report", stage_slug: str, set_title: str) -> list:
    result, seen = [], {}
    for item in sorted(items, key=lambda i: (i["order"], i["id"])):
        if item["is_deleted"]:
            report.deleted_items += 1
            continue
        where = f"{stage_slug} / {set_title}"
        word = clean_text(item["word"])
        meaning = clean_meaning(item["meaning"])
        if meaning != clean_text(item["meaning"]):
            report.meaning_normalised += 1
        key = norm_key(word)
        if not key:
            report.flag("empty word - dropped", where, json.dumps(item["word"], ensure_ascii=False))
            continue
        if not meaning:
            report.flag("empty meaningVi - dropped", where, word)
            continue
        if key in seen:
            report.duplicates.append((where, word))
            continue
        seen[key] = True
        info = lexicon.get(key, {})
        pos, raw_pos = normalise_pos(item["pos"])
        if raw_pos:
            report.unknown_pos[raw_pos] += 1
        if not clean_text(item["pos"]):
            # Empty in Studychill: take the CEFR list's label, else multi-word entries are phrases.
            pos = info.get("pos") or ("phrase" if " " in word else "other")
            report.pos_filled[pos] += 1
        elif clean_text(item["pos"]).lower() in ("numeral", "num"):
            report.numerals += 1
        phonetic = clean_phonetic(item["phonetic"])
        if not phonetic and info.get("phonetic"):
            phonetic = info["phonetic"]
            report.phonetic_filled += 1
        example = optional(item["example"])
        example_vi = optional(item["example_vi"])

        if not LATIN_WORD.match(word):
            report.flag("word has unexpected characters", where, word)
        if not VIETNAMESE_CHARS.search(meaning) and meaning.lower() == word.lower():
            report.flag("meaningVi equals the word", where, word)
        if len(meaning) > MEANING_MAX_LENGTH:
            report.flag(f"meaningVi longer than {MEANING_MAX_LENGTH} chars (quiz option)", where,
                        f"{word}: {meaning}")
        if example and example_vi and example == example_vi:
            report.flag("exampleVi equals example", where, word)
        if example_vi and not example:
            report.flag("exampleVi without example", where, word)
        if not phonetic:
            report.missing_phonetic += 1
        if example is None:
            report.missing_example += 1

        result.append({
            "word": word,
            "phonetic": phonetic,
            "partOfSpeech": pos,
            "meaningVi": meaning,
            "definitionEn": info.get("definitionEn"),
            "example": example,
            "exampleVi": example_vi,
            "cefr": info.get("cefr"),
            "synonyms": list(info.get("synonyms", [])),
        })
        if info.get("cefr"):
            report.with_cefr += 1
    by_meaning = collections.defaultdict(list)
    for entry in result:
        by_meaning[entry["meaningVi"].lower()].append(entry["word"])
    for meaning, words in by_meaning.items():
        if len(words) > 1:
            report.same_meaning.append((f"{stage_slug} / {set_title}", meaning, words))
    return result


def write_json(path: Path, document: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    path.write_bytes(text.encode("utf-8"))


# --------------------------------------------------------------------------------------------
# report
# --------------------------------------------------------------------------------------------

class Report:
    def __init__(self) -> None:
        self.stages: list = []
        self.sets: list = []
        self.flags: list = []
        self.duplicates: list = []
        self.empty_sets: list = []
        self.excluded_roots: list = []
        self.unknown_pos: collections.Counter = collections.Counter()
        self.deleted_items = 0
        self.pos_filled: collections.Counter = collections.Counter()
        self.numerals = 0
        self.renumbered = 0
        self.stage_vocab: dict[str, set] = collections.defaultdict(set)
        self.phonetic_filled = 0
        self.missing_phonetic = 0
        self.missing_example = 0
        self.with_cefr = 0
        self.meaning_normalised = 0
        self.same_meaning: list = []

    def flag(self, reason: str, where: str, value: str) -> None:
        self.flags.append((reason, where, value))

    def write(self, data: dict, source: Path) -> None:
        total_words = sum(s[4] for s in self.stages)
        lines = [
            "# Vocabulary import report",
            "",
            f"Snapshot: `{source.relative_to(BASE_DIR).as_posix()}` (exported {data['exportedAt']}). "
            "Generated by `scripts/vocab_data_pipeline.py build`.",
            "",
            "## Source counts (Studychill DB)",
            "",
            "| Table | Rows |",
            "|---|---:|",
        ]
        labels = {
            "vocabSetsTotal": "ls_vocab_sets (all)", "vocabSetsOfficial": "ls_vocab_sets (official, owner NULL)",
            "vocabSetItemsTotal": "ls_vocab_set_items (all)",
            "vocabSetItemsOfficial": "ls_vocab_set_items (official sets)", "fcWords": "fc_words (all exams)",
        }
        for key, label in labels.items():
            lines.append(f"| {label} | {data['counts'][key]:,} |")
        excluded_items = sum(1 for i in data["vocabSetItems"]
                             if i["vocab_set_id"] in {r["id"] for r in self.excluded_roots})
        lines += [
            "",
            "## Stages",
            "",
            "| # | File | Title | Sets | Words |",
            "|---:|---|---|---:|---:|",
        ]
        for order, slug, title, n_sets, n_words, file_name in self.stages:
            lines.append(f"| {order} | `{file_name}` | {title} | {n_sets} | {n_words:,} |")
        lines += [
            f"| | | **Total** | **{sum(s[3] for s in self.stages)}** | **{total_words:,}** |",
            "",
            "Oxford 3000/5000, the roadmap and the topic sets are three ways of slicing largely the same word pool, "
            "so a word can appear in several stages (see overlap below).",
            "",
            f"Lesson titles renumbered so each stage (each topic in `topics`) starts at \"Bài 01\": "
            f"{self.renumbered:,} sets.",
            "",
            "## Word overlap between stages",
            "",
            f"Distinct words over all stages: {len(set().union(*self.stage_vocab.values())):,}.",
            "",
            "| Stage | Distinct words | Also in another stage | Also in a roadmap stage |",
            "|---|---:|---:|---:|",
        ]
        roadmap = [slug for _, slug, *_ in self.stages if slug.startswith("toeic-roadmap")]
        for _, slug, *_ in self.stages:
            words = self.stage_vocab[slug]
            others = set().union(*(v for k, v in self.stage_vocab.items() if k != slug))
            in_roadmap = set().union(*(self.stage_vocab[k] for k in roadmap if k != slug))
            lines.append(f"| {slug} | {len(words):,} | {len(words & others):,} | {len(words & in_roadmap):,} |")
        lines += [
            "",
            "## Reconciliation",
            "",
            f"- Official items in DB: {data['counts']['vocabSetItemsOfficial']:,}",
            f"- In excluded roots ({', '.join(sorted({r['collection'] or '(none)' for r in self.excluded_roots}))}): "
            f"{excluded_items:,}",
            f"- Soft-deleted items skipped: {self.deleted_items:,}",
            f"- Duplicates removed within a set: {len(self.duplicates):,}",
            f"- Rows dropped by validation: {sum(1 for f in self.flags if f[0].endswith('dropped')):,}",
            f"- Words written: {total_words:,}",
            "",
            "## Coverage",
            "",
            f"- Words with CEFR (joined from fc_words / toeic_vocab.json): {self.with_cefr:,} / {total_words:,}",
            f"- Words without phonetic: {self.missing_phonetic:,}",
            f"- Words without example: {self.missing_example:,}",
        ]
        lines.append("- Empty part of speech filled: "
                     + (", ".join(f"{k} x{v}" for k, v in sorted(self.pos_filled.items())) or "none")
                     + " (from toeic_vocab.json, else `phrase` for multi-word entries, else `other`)")
        lines.append(f"- Numerals mapped to `other` (no numeral in the enum): {self.numerals:,}")
        lines.append(f"- Phonetic filled from the CEFR list / fc_words: {self.phonetic_filled:,}")
        lines.append(f"- meaningVi punctuation normalised (`;` -> `,`, trailing punctuation): "
                     f"{self.meaning_normalised:,}")
        if self.unknown_pos:
            lines.append("- Part of speech mapped to `other`: "
                         + ", ".join(f"`{k}` x{v}" for k, v in sorted(self.unknown_pos.items())))
        lines += ["", "## Excluded root sets", ""]
        for r in self.excluded_roots:
            lines.append(f"- [{r['collection'] or 'no collection'}] {clean_text(r['title'])} "
                         f"(published={r['is_published']})")
        if self.empty_sets:
            lines += ["", "## Empty sets dropped", ""]
            lines += [f"- {stage} / {title}" for stage, title in self.empty_sets]
        lines += ["", f"## Duplicates removed ({len(self.duplicates)})", ""]
        lines += [f"- {where}: `{word}`" for where, word in self.duplicates] or ["- none"]
        lines += ["", f"## Flagged rows ({len(self.flags)})", ""]
        if self.flags:
            lines += ["| Reason | Where | Value |", "|---|---|---|"]
            for reason, where, value in sorted(self.flags):
                lines.append(f"| {reason} | {where} | {value.replace('|', '/')} |")
        else:
            lines.append("- none")
        lines += [
            "",
            f"## Same meaningVi for different words in one set ({len(self.same_meaning)})",
            "",
            "Not errors (synonyms and word-family pairs such as bike/bicycle, clear/clearly), but a quiz must not "
            "offer one of these words as a wrong option for the other.",
            "",
        ]
        lines += [f"- {where}: \"{meaning}\" = {', '.join(f'`{w}`' for w in words)}"
                  for where, meaning, words in self.same_meaning] or ["- none"]
        lines.append("")
        REPORT_FILE.write_bytes("\n".join(lines).encode("utf-8"))


# --------------------------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    export = sub.add_parser("export", help="read-only snapshot over SSH")
    export.add_argument("--host", default="studychill")
    export.add_argument("--container", default="studychill-backend")
    export.set_defaults(func=export_snapshot)
    build_cmd = sub.add_parser("build", help="snapshot -> data/vocabulary/*.json")
    build_cmd.add_argument("--source", help="snapshot folder (default: newest data/source/studychill_vocab_*)")
    build_cmd.set_defaults(func=build)
    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
