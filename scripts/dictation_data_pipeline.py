#!/usr/bin/env python3
"""
TOEIC Space - Studychill dictation data pipeline
================================================

Pulls the dictation content Studychill serves ("Nghe chép chính tả": the TOEIC roadmap packs toeic-250 ...
toeic-990 and the topic packs), normalises it and writes the import files in data/dictation/.

Commands
--------
  export  Read-only snapshot over SSH (host alias "studychill"). Runs read-only ORM queries through
          `manage.py shell` fed on stdin, so nothing is written on the server and no database credentials
          are needed locally. Only the TOEIC roadmap packs (toeic-250 … toeic-990: active courses and their active
          sentences) leave the server — topic packs and the warm-up pack are not imported (owner decision,
          2026-11-07); learners' progress and attempts are never exported. Also copies Studychill's CEFR word list.
  build   Normalise the snapshot into data/dictation/<pack>.json (or .json.gz above 1.5 MB) plus
          import_report.md. Deterministic: the same snapshot gives byte-identical files.

Usage
-----
  python scripts/dictation_data_pipeline.py export
  python scripts/dictation_data_pipeline.py build                       # newest data/source/studychill_dictation_*
  python scripts/dictation_data_pipeline.py build --source data/source/studychill_dictation_20261107

Notes
-----
* The 2,600 legacy sentences without a course (Studychill's pre-AIO set) are not exported: every roadmap level has
  AIO courses, so Studychill's roadmap and play pages never reach them.
* Keywords ("Từ khoá" in the play screen) are computed here with Studychill's rules (dictation_coach._build_keywords):
  editor focus words first, then words of the TOEIC CEFR list at B1 or above, capped per level. A multi-word focus
  phrase whose meaning is neither in the sentence glossary nor in the CEFR list is skipped (Studychill looked it up
  through a translation API at request time).
* The per-word glossary is only used to build keywords; it is not imported (no TOEIC Space screen shows it).
"""

from __future__ import annotations

import argparse
import datetime as dt
import gzip
import json
import re
import subprocess
import sys
from collections import Counter
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

BASE_DIR = Path(__file__).resolve().parent.parent
SOURCE_ROOT = BASE_DIR / "data" / "source"
OUTPUT_DIR = BASE_DIR / "data" / "dictation"
REPORT_FILE = OUTPUT_DIR / "import_report.md"
SNAPSHOT_FILE = "studychill_dictation.jsonl.gz"
CEFR_LIST_REMOTE = "/root/Studychill/backend/apps/learning/vocabulary/data/toeic_vocab.json"
CEFR_LIST_FILE = "toeic_vocab.json.gz"
SCHEMA_VERSION = 1
# Files above this size are stored gzip-compressed: the repository stays small and every upload stays below the
# API gateway's 2 MB request body cap (see docs/dictation.md "Import").
GZIP_ABOVE_BYTES = 1536 * 1024
BEGIN, END = "@@DICTATION_EXPORT_BEGIN@@", "@@DICTATION_EXPORT_END@@"

# Read-only ORM script executed by `manage.py shell` on the server (fed on stdin, never stored there).
# One JSON object per line keeps memory flat on both ends.
REMOTE_EXPORT_SCRIPT = r'''
import json, datetime
from apps.learning.listening.models.dictation_course import DictationCourse, DictationItem

COURSE_FIELDS = ("id", "code", "pack", "level", "order", "title", "item_count", "avg_words", "vocab_count")
ITEM_FIELDS = ("id", "course_id", "source_code", "order", "level", "sub_level", "topic", "text", "translation",
               "ipa", "notes", "vocab", "glossary", "focus_words", "voice_key", "rate", "max_plays")

ROADMAP = ["toeic-250", "toeic-450", "toeic-650", "toeic-800", "toeic-990"]
courses = DictationCourse.objects.filter(is_active=True, pack__in=ROADMAP).order_by("pack", "order", "id")
items = DictationItem.objects.filter(is_active=True, course__is_active=True, course__pack__in=ROADMAP).order_by(
    "course_id", "order", "id")
print("@@DICTATION_EXPORT_BEGIN@@")
print(json.dumps({"kind": "meta", "exportedAt": datetime.date.today().isoformat(),
                  "coursesActive": courses.count(), "itemsActive": items.count(),
                  "itemsWithoutCourse": DictationItem.objects.filter(is_active=True, course__isnull=True).count()}))
for row in courses.values_list(*COURSE_FIELDS):
    print(json.dumps({"kind": "course", **dict(zip(COURSE_FIELDS, row))}, ensure_ascii=False, default=str))
for row in items.values_list(*ITEM_FIELDS).iterator(chunk_size=2000):
    print(json.dumps({"kind": "item", **dict(zip(ITEM_FIELDS, row))}, ensure_ascii=False, default=str))
print("@@DICTATION_EXPORT_END@@")
'''

# Studychill: apps.learning.listening.models.dictation_course.ROADMAP_PACK_LEVEL
ROADMAP_PACK_LEVEL = {"toeic-250": 1, "toeic-450": 2, "toeic-650": 3, "toeic-800": 4, "toeic-990": 5}
TOPICS = {"office_hr", "business_finance", "shopping_service", "travel_business_trip", "restaurant_events",
          "health_realestate_media", "manufacturing_technical"}
VOICES = {"andrew", "brian", "guy", "eric", "christopher", "emma", "ava", "jenny", "aria", "ryan", "thomas",
          "sonia", "libby", "natasha", "liam", "connor"}
NOTE_TYPES = {"word": "Word", "phrase": "Phrase"}

# Studychill pack slugs are Vietnamese; TOEIC Space stores English pack keys (also the data file names).
PACK_SLUGS = {
    "cam-xuc-tinh-cach": "emotions-personality", "dam-phan": "negotiation", "du-lich": "travel",
    "email-cong-viec": "business-email", "giao-duc": "education", "giao-thong": "transport",
    "hen-ho-moi-quan-he": "dating-relationships", "hop-thuyet-trinh": "meetings-presentations",
    "it-cong-nghe": "it-technology", "marketing-sales": "marketing-sales", "mua-sam": "shopping",
    "mua-thue-nha": "housing-rental", "networking": "networking", "ngan-hang-tai-chinh": "banking-finance",
    "nha-cua-gia-dinh": "home-family", "nha-hang-am-thuc": "restaurant-food", "phong-van": "job-interviews",
    "su-kien-le-hoi": "events-festivals", "tai-chinh-ke-toan": "finance-accounting", "thoi-tiet": "weather",
    "toeic-250": "toeic-250", "toeic-450": "toeic-450", "toeic-650": "toeic-650", "toeic-800": "toeic-800",
    "toeic-990": "toeic-990", "van-phong": "office", "y-te-suc-khoe": "health", "khoi-dong-nhanh": "quick-warmup",
}

# Studychill: apps.learning.listening.services.dictation_coach
MAX_KEYWORDS = {2: 4, 3: 5, 4: 6, 5: 8}
CEFR_MIN = "B1"
_COACH_PUNCT = ".,!?;:\"'()[]{}…“”‘’"


# --------------------------------------------------------------------------------------------
# export
# --------------------------------------------------------------------------------------------

def export_snapshot(args: argparse.Namespace) -> None:
    target = SOURCE_ROOT / f"studychill_dictation_{dt.date.today():%Y%m%d}"
    target.mkdir(parents=True, exist_ok=True)

    print(f"[export] {args.host}: active dictation courses + sentences (read-only ORM)")
    result = subprocess.run(
        ["ssh", "-o", "BatchMode=yes", args.host,
         f"docker exec -i {args.container} python manage.py shell | gzip -c"],
        input=REMOTE_EXPORT_SCRIPT.encode("utf-8"), capture_output=True, check=True)
    stdout = gzip.decompress(result.stdout).decode("utf-8")
    if BEGIN not in stdout or END not in stdout:
        sys.exit("[export] remote script produced no data:\n" + result.stderr.decode("utf-8", "replace")[-2000:])
    payload = stdout.split(BEGIN, 1)[1].split(END, 1)[0].strip()
    lines = payload.splitlines()
    for line in lines:
        json.loads(line)  # fail fast on a truncated stream
    destination = target / SNAPSHOT_FILE
    with gzip.open(destination, "wt", encoding="utf-8") as output:
        output.write("\n".join(lines) + "\n")
    print(f"         -> {destination} ({destination.stat().st_size:,} bytes, {len(lines):,} lines)")

    print(f"[export] {args.host}: CEFR word list")
    listing = subprocess.run(["ssh", "-o", "BatchMode=yes", args.host, f"gzip -c {CEFR_LIST_REMOTE}"],
                             capture_output=True, check=True).stdout
    (target / CEFR_LIST_FILE).write_bytes(listing)
    print(f"         -> {target / CEFR_LIST_FILE} ({len(listing):,} bytes)")
    print(f"\nSnapshot ready. Next: python scripts/dictation_data_pipeline.py build --source {target.relative_to(BASE_DIR)}")


# --------------------------------------------------------------------------------------------
# build helpers
# --------------------------------------------------------------------------------------------

def clean(value: object, limit: int | None = None) -> str:
    text = re.sub(r"\s+", " ", str(value or "")).strip()
    return text[:limit] if limit else text


def coach_norm(token: str) -> str:
    return token.strip(_COACH_PUNCT).lower()


def build_keywords(level: int, text: str, glossary: dict, focus_words: list, store: dict, stats: Counter) -> list:
    """Port of dictation_coach._build_keywords without the translation-API lookup for unknown phrases."""
    if level < 2:
        return []
    tokens = text.split()
    norm = [coach_norm(t) for t in tokens]
    gloss = {str(k).lower(): v for k, v in (glossary or {}).items() if isinstance(v, dict)}
    cap = MAX_KEYWORDS.get(level, 6)
    chosen: list[dict] = []
    used: set[str] = set()

    def add_phrase(phrase: list[str]) -> None:
        n = len(phrase)
        if not n or len(chosen) >= cap:
            return
        position = [i for i in range(len(norm) - n + 1) if norm[i: i + n] == phrase]
        if not position:
            return
        key = " ".join(phrase)
        if key in used:
            return
        g = gloss.get(key) or {}
        t = (store.get(key) if n == 1 else None) or {}
        meaning = clean(g.get("vi") or t.get("vi"), 200)
        if not meaning:
            if n > 1:
                stats["keywordPhrasesWithoutMeaning"] += 1
            return
        used.add(key)
        chosen.append({
            "word": clean(" ".join(tokens[position[0]: position[0] + n]).strip(_COACH_PUNCT), 120),
            "meaning": meaning,
            "partOfSpeech": clean(g.get("pos") or t.get("pos"), 32),
            "ipa": clean(g.get("ipa") or t.get("ipa"), 80),
            "cefr": clean(t.get("cefr"), 8),
        })

    for fw in focus_words or []:
        if isinstance(fw, str) and fw.strip():
            add_phrase([coach_norm(x) for x in fw.split() if coach_norm(x)])
    for w in norm:
        if len(chosen) >= cap:
            break
        if w and w not in used and w in store and (store[w].get("cefr") or "") >= CEFR_MIN:
            add_phrase([w])
    return chosen[:cap]


def clean_notes(notes: object, stats: Counter) -> list:
    out = []
    for note in notes if isinstance(notes, list) else []:
        if not isinstance(note, dict):
            stats["notesDropped"] += 1
            continue
        text = clean(note.get("text"), 200)
        explanation = clean(note.get("explanationVi"), 600)
        kind = NOTE_TYPES.get(note.get("type"), "Word")
        # Studychill shows every note with an explanation, even when its `text` is blank.
        if not explanation:
            stats["notesDropped"] += 1
            continue
        out.append({"id": clean(note.get("id"), 20) or f"n{len(out)}", "text": text, "type": kind,
                    "explanation": explanation})
    return out


def clean_vocab(vocab: object, stats: Counter) -> list:
    out = []
    for entry in vocab if isinstance(vocab, list) else []:
        if not isinstance(entry, dict) or not clean(entry.get("word")) or not clean(entry.get("vi")):
            stats["vocabDropped"] += 1
            continue
        out.append({"word": clean(entry.get("word"), 80), "lemma": clean(entry.get("lemma"), 80),
                    "meaning": clean(entry.get("vi"), 200), "partOfSpeech": clean(entry.get("pos"), 32),
                    "ipa": clean(entry.get("ipa"), 80)})
    return out


def newest_snapshot() -> Path:
    candidates = sorted(p for p in SOURCE_ROOT.glob("studychill_dictation_*") if (p / SNAPSHOT_FILE).exists())
    if not candidates:
        sys.exit("[build] no snapshot found: run `export` first")
    return candidates[-1]


# --------------------------------------------------------------------------------------------
# build
# --------------------------------------------------------------------------------------------

def build(args: argparse.Namespace) -> None:
    source = (BASE_DIR / args.source) if args.source else newest_snapshot()
    print(f"[build] snapshot {source.relative_to(BASE_DIR)}")
    meta: dict = {}
    courses: dict[int, dict] = {}
    items_by_course: dict[int, list] = {}
    with gzip.open(source / SNAPSHOT_FILE, "rt", encoding="utf-8") as handle:
        for line in handle:
            row = json.loads(line)
            kind = row.pop("kind")
            if kind == "meta":
                meta = row
            elif kind == "course":
                courses[row["id"]] = row
            elif kind == "item":
                items_by_course.setdefault(row["course_id"], []).append(row)
    with gzip.open(source / CEFR_LIST_FILE, "rt", encoding="utf-8") as handle:
        store = {str(k).lower(): v for k, v in json.load(handle).items() if isinstance(v, dict)}

    stats: Counter = Counter()
    packs: dict[str, list] = {}
    for course in sorted(courses.values(), key=lambda c: (c["pack"], c["order"], c["id"])):
        rows = sorted(items_by_course.get(course["id"], []), key=lambda r: (r["order"], r["id"]))
        items = []
        for row in rows:
            text = clean(row["text"], 1000)
            if not text:
                stats["itemsWithoutText"] += 1
                continue
            topic = row["topic"] if row["topic"] in TOPICS else "office_hr"
            voice = row["voice_key"] if row["voice_key"] in VOICES else ""
            keywords = build_keywords(course["level"], text, row.get("glossary") or {}, row.get("focus_words") or [],
                                      store, stats)
            stats["keywords"] += len(keywords)
            items.append({
                "sourceCode": row["source_code"],
                "order": row["order"],
                "topic": topic,
                "text": text,
                "translation": clean(row["translation"], 1000),
                "ipa": clean(row["ipa"], 2000),
                "voice": voice,
                "rate": int(row["rate"] or 0),
                "maxPlays": row["max_plays"],
                "keywords": keywords,
                "notes": clean_notes(row.get("notes"), stats),
                "vocab": clean_vocab(row.get("vocab"), stats),
            })
        if not items:
            stats["coursesWithoutItems"] += 1
            continue
        vocab_count = sum(len(i["vocab"]) for i in items)
        avg_words = round(sum(len(i["text"].split()) for i in items) / len(items), 1)
        if course["pack"] not in ROADMAP_PACK_LEVEL:
            stats["coursesOutsideRoadmap"] += 1
            continue
        if course["pack"] not in PACK_SLUGS:
            sys.exit(f"[build] unknown Studychill pack '{course['pack']}': add it to PACK_SLUGS")
        packs.setdefault(PACK_SLUGS[course["pack"]], []).append({
            "code": course["code"],
            "order": course["order"],
            "level": course["level"],
            "title": clean(course["title"], 200),
            "avgWords": avg_words,
            "vocabCount": vocab_count,
            "items": items,
        })
        stats["courses"] += 1
        stats["items"] += len(items)
        if course["item_count"] != len(items):
            stats["courseItemCountDiffers"] += 1

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for old in list(OUTPUT_DIR.glob("*.json")) + list(OUTPUT_DIR.glob("*.json.gz")):
        old.unlink()
    files = []
    for pack in sorted(packs):
        document = {
            "schemaVersion": SCHEMA_VERSION,
            "source": "studychill",
            "pack": pack,
            "roadmapLevel": ROADMAP_PACK_LEVEL.get(pack),
            "courses": packs[pack],
        }
        raw = (json.dumps(document, ensure_ascii=False, indent=1) + "\n").encode("utf-8")
        if len(raw) > GZIP_ABOVE_BYTES:
            path = OUTPUT_DIR / f"{pack}.json.gz"
            # mtime=0 + no filename keeps the archive byte-identical between runs.
            with open(path, "wb") as fh, gzip.GzipFile(filename="", mode="wb", fileobj=fh, mtime=0, compresslevel=9) as gz:
                gz.write(raw)
        else:
            path = OUTPUT_DIR / f"{pack}.json"
            path.write_bytes(raw)
        files.append((path.name, len(packs[pack]), sum(len(c["items"]) for c in packs[pack]), len(raw),
                      path.stat().st_size))
        print(f"         -> {path.relative_to(BASE_DIR)} ({path.stat().st_size:,} bytes)")

    lines = [
        "# Dictation import report",
        "",
        f"Snapshot: `{source.name}` (exported {meta.get('exportedAt', '?')}). Built by `scripts/dictation_data_pipeline.py build`.",
        "",
        f"- Studychill active courses: {meta.get('coursesActive', '?')}, active sentences: {meta.get('itemsActive', '?')}"
        f" (TOEIC roadmap packs only; {meta.get('itemsWithoutCourse', '?')} legacy sentences without a course and the topic packs"
        " are not exported)",
        f"- Imported: {stats['courses']} courses, {stats['items']} sentences, {stats['keywords']} keywords",
        f"- Dropped: {stats['itemsWithoutText']} empty sentences, {stats['coursesWithoutItems']} empty courses,"
        f" {stats['notesDropped']} invalid notes, {stats['vocabDropped']} invalid vocab entries",
        f"- Focus phrases without a known meaning (no keyword): {stats['keywordPhrasesWithoutMeaning']}",
        f"- Courses whose stored item_count differs from their active sentences: {stats['courseItemCountDiffers']}",
        "",
        "| File | Courses | Sentences | JSON bytes | File bytes |",
        "|---|---:|---:|---:|---:|",
        *[f"| `{name}` | {c} | {i} | {raw:,} | {size:,} |" for name, c, i, raw, size in files],
        "",
    ]
    REPORT_FILE.write_bytes("\n".join(lines).encode("utf-8"))
    print(f"         -> {REPORT_FILE.relative_to(BASE_DIR)}")
    print(f"[build] {stats['courses']} courses, {stats['items']} sentences in {len(files)} files")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    export = sub.add_parser("export", help="read-only snapshot over SSH")
    export.add_argument("--host", default="studychill")
    export.add_argument("--container", default="studychill-backend")
    export.set_defaults(func=export_snapshot)
    build_cmd = sub.add_parser("build", help="snapshot -> data/dictation/*.json")
    build_cmd.add_argument("--source", help="snapshot folder (default: newest data/source/studychill_dictation_*)")
    build_cmd.set_defaults(func=build)
    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
