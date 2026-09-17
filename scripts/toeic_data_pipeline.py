#!/usr/bin/env python3
"""
TOEIC Space - Studychill TOEIC data pipeline
============================================

Replaces heal_and_standardize_toeic_data.py / process_toeic_data.py / seed_assessment_database.py.

Commands
--------
  export  Read-only snapshot of the Studychill TOEIC tables, listening cards and the Part 5/6
          sets bundled with the Studychill frontend, over SSH (host alias "studychill").
  build   Classify, clean and de-duplicate the snapshot into a seed file + audit report.
  load    Insert the seed into an Assessment database created by the EF Core migrations.

Model produced by "build"
-------------------------
  * ToeicTests         only the full 200-question tests (ETS 2026, Crack TOEIC Vol 1, Pass TOEIC).
  * ToeicPracticeSets  every part practice collection (level, topic, part drill).
  * ToeicQuestions     one row per distinct question. Questions of full tests keep TestId +
                       QuestionNumber; practice-only questions live in the bank (TestId NULL).
  * ToeicPracticeSetItems  ordered references from practice sets to questions.

Usage
-----
  python scripts/toeic_data_pipeline.py export
  python scripts/toeic_data_pipeline.py build --source data/source/studychill_20260913
  python scripts/toeic_data_pipeline.py load --database toeic_space_assessment_v2
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import gzip
import html
import json
import os
import re
import subprocess
import sys
import tarfile
import unicodedata
import uuid
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

BASE_DIR = Path(__file__).resolve().parent.parent
SOURCE_ROOT = BASE_DIR / "data" / "source"
SEEDS_DIR = BASE_DIR / "data" / "seeds"
SEED_FILE = SEEDS_DIR / "toeic_assessment_seed.json.gz"
REPORT_FILE = SEEDS_DIR / "toeic_assessment_import_report.md"
# Explanations written by the TOEIC Space team for questions no Studychill source explains.
MANUAL_EXPLANATIONS_FILE = BASE_DIR / "data" / "manual" / "explanations.json"

SNAPSHOT_DATA = "studychill_toeic_mock.json.gz"
SNAPSHOT_CARDS = "studychill_listening_cards.tar.gz"
SNAPSHOT_READING = "studychill_reading_exams.tar.gz"
SNAPSHOT_RAW_READING = "studychill_dautoeic_reading.json.gz"

# Fields kept from the raw dautoeic dataset (data/dautoeic_raw/mock_test_questions.json). The
# Studychill database never received the explanations of the Crack TOEIC / Pass TOEIC tests.
RAW_IDENTITY_FIELDS = ("id", "part", "question_text", "option_a", "option_b", "option_c", "option_d", "correct_answer")
RAW_EXPLANATION_FIELDS = ("explanation_vi", "explanation_en", "dich_nghia", "tu_vung", "dich_nghia_dap_an")

# Studychill serves these Part 5/6 sets from JSON bundled with its frontend (LOCAL_READING_EXAMS in
# ToeicPracticePage.jsx), not from the database. testId -> (kind, practice set code).
BUNDLED_READING_SETS = {
    **{f"p{part}-lv{level}": ("level", f"p{part}-lv{level}") for part in (5, 6) for level in range(1, 6)},
    "p5-t1": ("drill", "p5-economy-200"),
    "p5-t2": ("drill", "p5-economy-200-2"),
    "p6-t1": ("drill", "p6-practice-100"),
}
TRANSLATION_SPLIT = "<translation_split>"

# ETS format: part -> (first question number, last question number)
STANDARD_RANGES = {1: (1, 6), 2: (7, 31), 3: (32, 70), 4: (71, 100), 5: (101, 130), 6: (131, 146), 7: (147, 200)}
GROUPED_PARTS = {3, 4, 6, 7}
LEVEL_TARGET_SCORES = {1: 450, 2: 550, 3: 650, 4: 750, 5: 850}

# Practice sets with at most this share of unplayable questions lose those items instead of becoming Draft.
MAX_UNPLAYABLE_SHARE_TO_TRIM = 0.02

# Kind priority when the same question exists in several sources (lower wins).
SOURCE_PRIORITY = {"full": 0, "level": 1, "drill": 2, "topic": 3}

# "[Chỗ trống 3]" is the text of every blank in the bundled Part 6 sets.
JUNK_QUESTION_TEXT = re.compile(
    r"^(part\s*[12]\b.*\bmirror\b.*|câu\s*\d+|question\s*\d+|\[\s*chỗ\s+trống\s*\d*\s*\])$", re.IGNORECASE)
OPTION_LABEL = re.compile(r"^\s*\(([A-D])\)\s+")
# Same rule as RichTextRules.UnsafeMarkup in the Assessment API: event handlers only count inside a tag,
# so explanations such as "one = một người" are fine.
DANGEROUS_HTML = re.compile(
    r"<\s*/?\s*(script|style|iframe|frame|frameset|object|embed|applet|form|input|button|textarea|select|link|meta|base"
    r"|svg|math|template|noscript)\b"
    r"|<[^>]*\son[a-z]+\s*="
    r"""|<[^>]*\b(href|src|action|formaction|xlink:href)\s*=\s*["']?\s*(javascript|vbscript|data)\s*:""",
    re.IGNORECASE)
TAGS = re.compile(r"<[^>]+>")
NON_ALNUM = re.compile(r"[^a-z0-9]")


# --------------------------------------------------------------------------------------------
# export
# --------------------------------------------------------------------------------------------

def export_snapshot(args: argparse.Namespace) -> None:
    target = SOURCE_ROOT / f"studychill_{dt.date.today():%Y%m%d}"
    target.mkdir(parents=True, exist_ok=True)

    commands = {
        SNAPSHOT_DATA: (
            "docker exec studychill-backend python manage.py dumpdata "
            "toeic.ToeicMockTest toeic.ToeicMockPassage toeic.ToeicMockQuestion --indent 0 2>/dev/null | gzip -6"
        ),
        SNAPSHOT_CARDS: "tar -C /root/Studychill/frontend/public/data -czf - listening",
        SNAPSHOT_READING: "cd /root/Studychill/frontend/src/data/exams && tar -czf - toeic_part5_*.json toeic_part6_*.json",
    }

    for file_name, remote_command in commands.items():
        destination = target / file_name
        print(f"[export] {args.host}: {file_name}")
        with open(destination, "wb") as output:
            subprocess.run(["ssh", "-o", "BatchMode=yes", args.host, remote_command], stdout=output, check=True)
        print(f"         -> {destination} ({destination.stat().st_size:,} bytes)")

    # The raw dataset is 145 MB, so only its explanation fields are kept in the snapshot.
    print(f"[export] {args.host}: {SNAPSHOT_RAW_READING}")
    downloaded = target / "mock_test_questions.json"
    with open(downloaded, "wb") as output:
        subprocess.run(
            ["ssh", "-o", "BatchMode=yes", args.host, "cat /root/Studychill/data/dautoeic_raw/mock_test_questions.json"],
            stdout=output, check=True)
    kept = extract_raw_reading(downloaded, target / SNAPSHOT_RAW_READING)
    downloaded.unlink()
    print(f"         -> {target / SNAPSHOT_RAW_READING} ({kept:,} questions with explanations)")

    print(f"\nSnapshot ready. Next: python scripts/toeic_data_pipeline.py build --source {target.relative_to(BASE_DIR)}")


# --------------------------------------------------------------------------------------------
# build helpers
# --------------------------------------------------------------------------------------------

def normalize_key(value: str | None) -> str:
    """Lowercase alphanumerics only - used to compare content across copies."""
    return NON_ALNUM.sub("", html.unescape(TAGS.sub(" ", value or "")).lower())


def clean_text(value: str | None) -> str | None:
    if value is None:
        return None
    value = value.strip()
    return value or None


def is_absolute_url(value: str | None) -> bool:
    return bool(value) and value.startswith(("http://", "https://"))


def strip_option_labels(options: dict[str, str | None]) -> tuple[dict[str, str | None], bool]:
    """Removes "(A) " style prefixes, but only when every option carries its own letter."""
    present = {key: value for key, value in options.items() if value}
    labelled = all(
        (match := OPTION_LABEL.match(value)) is not None and match.group(1) == key
        for key, value in present.items()
    )
    if not present or not labelled:
        return options, False
    return {key: (OPTION_LABEL.sub("", value, count=1) if value else value) for key, value in options.items()}, True


def option_key(value: str | None) -> str:
    """Option text without its "(A) " label, normalized - sources disagree on the label."""
    return normalize_key(OPTION_LABEL.sub("", value or "", count=1))


def explanation_key(text: str | None, options: dict[str, str | None], answer: str | None) -> tuple:
    return (normalize_key(text), *(option_key(options.get(letter)) for letter in "ABCD"), (answer or "").strip().upper())


def paragraphs(value: str | None) -> str:
    return "".join(f"<p>{html.escape(block.strip())}</p>"
                   for block in re.split(r"\n\s*\n|\r\n\s*\r\n", value or "") if block.strip())


def vocabulary_text(value: str | None) -> str | None:
    """Vocabulary comes as lines, as a JSON array or wrapped in {"vocabulary": [...]}. Clients read
    the array shape as flashcards, so the wrapper is unwrapped here."""
    value = clean_text(value)
    if not value or not value.startswith(("[", "{")):
        return value
    try:
        data = json.loads(value)
    except json.JSONDecodeError:
        return value
    if isinstance(data, dict):
        data = data.get("vocabulary") or data.get("items") or data.get("words")
    if not isinstance(data, list) or not any(isinstance(item, dict) and item.get("word") for item in data):
        return value
    return json.dumps([item for item in data if isinstance(item, dict) and item.get("word")], ensure_ascii=False)


def compose_explanation(raw: dict, answer: str, answer_text: str | None = None,
                        include_translation: bool = True) -> str | None:
    """Builds an explanation in the same shape as the Studychill ones, from the raw dataset fields."""
    if not any(clean_text(raw.get(field)) for field in RAW_EXPLANATION_FIELDS):
        return None

    # Some questions only carry the translation and the meaning of each option; those are shown as they are.
    reasoning = clean_text(raw.get("explanation_vi")) or clean_text(raw.get("explanation_en"))
    label = f"Đáp án đúng: ({answer})" + (f" — {answer_text}" if clean_text(answer_text) else "")
    sections = [
        '<div class="tp-exp-correct-card">',
        f'<div class="tp-exp-correct-badge"><span class="tp-exp-badge-icon">✓</span>'
        f'<strong>{html.escape(label)}</strong></div>',
    ]
    if reasoning:
        sections.append(f'<div class="tp-exp-correct-desc">{paragraphs(reasoning)}</div>')
    sections.append("</div>")

    for title, value, body_class in (
        # For a grouped question the translation is the one of the passage, which is shown there already.
        ("🈯 Dịch nghĩa", clean_text(raw.get("dich_nghia")) if include_translation else None, "tp-exp-trans-body"),
        ("📚 Từ vựng", vocabulary_text(raw.get("tu_vung")), "tp-exp-vocab-body"),
        ("🔤 Nghĩa các đáp án", clean_text(raw.get("dich_nghia_dap_an")), "tp-exp-vocab-body"),
    ):
        if not value:
            continue
        card = "tp-exp-trans-card" if body_class == "tp-exp-trans-body" else "tp-exp-vocab-card"
        body = paragraphs(value) if body_class == "tp-exp-trans-body" else html.escape(value)
        sections.append(
            f'<div class="{card}">'
            f'<div class="tp-exp-section-title"><span>{title}</span></div>'
            f'<div class="{body_class}">{body}</div>'
            "</div>")

    return f'<div class="tp-exp-rich-wrapper">{"".join(sections)}</div>'


def extract_raw_reading(source_path: Path, destination: Path) -> int:
    """Keeps only the identity and explanation fields of the raw dataset, so the snapshot stays small."""
    with open(source_path, encoding="utf-8") as handle:
        rows = json.load(handle)

    kept = [
        {field: item.get(field) for field in RAW_IDENTITY_FIELDS + RAW_EXPLANATION_FIELDS}
        for item in rows
        if any(clean_text(item.get(field)) for field in RAW_EXPLANATION_FIELDS)
    ]

    with gzip.open(destination, "wt", encoding="utf-8") as output:
        json.dump(kept, output, ensure_ascii=False)

    return len(kept)


def classify_test(fields: dict) -> str:
    code = (fields.get("code") or "").strip()
    metadata = fields.get("metadata") or {}
    if metadata.get("bundled_kind"):
        return metadata["bundled_kind"]
    if metadata.get("category") == "full" or code.startswith(("CRACK-TOEIC-", "PASS-TOEIC-")):
        return "full"
    if re.fullmatch(r"p[1-7]-t\d", code):
        return "legacy"
    if re.fullmatch(r"p[1-7]-lv\d", code):
        return "level"
    if re.fullmatch(r"p[1-7]-[a-z0-9-]+", code):
        return "topic"
    return "drill"


def normalize_full_test(fields: dict) -> dict:
    code = fields["code"].strip()
    title = fields["title"].strip()
    metadata = fields.get("metadata") or {}

    if metadata.get("category") == "full":
        number = int(re.search(r"\d+", metadata.get("test_num") or title).group())
        series = metadata.get("series") or "ETS 2026"
        year_match = re.search(r"(20\d{2})", series)
        return {
            "code": f"{re.sub(r'[^A-Z0-9]+', '-', series.upper()).strip('-')}-TEST-{number:02d}",
            "title": f"{series} - Test {number:02d}",
            "category": series,
            "year": int(year_match.group(1)) if year_match else None,
        }

    if code.startswith("CRACK-TOEIC-"):
        return {"code": code, "title": title, "category": "Crack TOEIC Vol 1", "year": None}

    year_match = re.search(r"(20\d{2})", title)
    return {"code": code, "title": title, "category": "Pass TOEIC", "year": int(year_match.group(1)) if year_match else None}


def practice_code(fields: dict, part: int) -> str:
    code = (fields.get("code") or "").strip()
    if re.fullmatch(r"p[1-7]-[a-z0-9-]+", code):
        return code
    count = fields.get("total_questions") or 0
    if part == 5:
        return f"p5-economy-{count}"
    return f"p{part}-practice-{count}"


def resolve_media(
    url: str | None,
    card_url: str | None,
    stats: collections.Counter,
    label: str,
    fill_missing: bool = False,
) -> str | None:
    """Keeps absolute URLs, fixes relative ones from the listening card, optionally fills empty ones."""
    url = clean_text(url)
    if url is None:
        if fill_missing and is_absolute_url(card_url):
            stats[f"media_filled_{label}"] += 1
            return card_url
        return None
    if is_absolute_url(url):
        return url
    if is_absolute_url(card_url):
        stats[f"media_fixed_{label}"] += 1
        return card_url
    stats[f"media_unresolved_{label}"] += 1
    return None


# --------------------------------------------------------------------------------------------
# build
# --------------------------------------------------------------------------------------------

def load_snapshot(source: Path):
    rows = json.load(gzip.open(source / SNAPSHOT_DATA, "rt", encoding="utf-8"))
    by_model = collections.defaultdict(dict)
    for row in rows:
        by_model[row["model"]][row["pk"]] = row["fields"]

    with tarfile.open(source / SNAPSHOT_CARDS) as archive:
        cards = {
            Path(member.name).stem: json.load(archive.extractfile(member))
            for member in archive.getmembers()
            if member.isfile() and member.name.endswith(".json")
        }

    bundled = {}
    if (source / SNAPSHOT_READING).exists():
        with tarfile.open(source / SNAPSHOT_READING) as archive:
            bundled = {
                Path(member.name).name: (
                    json.load(archive.extractfile(member)),
                    dt.datetime.fromtimestamp(member.mtime, dt.timezone.utc),
                )
                for member in archive.getmembers()
                if member.isfile() and member.name.endswith(".json")
            }

    raw_explanations: dict[tuple, dict] = {}
    if (source / SNAPSHOT_RAW_READING).exists():
        for item in json.load(gzip.open(source / SNAPSHOT_RAW_READING, "rt", encoding="utf-8")):
            key = raw_explanation_key(item)
            richer = max(
                (candidate for candidate in (raw_explanations.get(key), item) if candidate),
                key=lambda candidate: sum(len(candidate.get(field) or "") for field in RAW_EXPLANATION_FIELDS))
            raw_explanations[key] = richer

    # Written by hand, so they win over the raw dataset. Matching on the question content means a
    # rewritten question keeps no stale explanation; the build reports the entry as unused instead.
    manual = {raw_explanation_key(entry): entry for entry in load_manual_explanations()}
    raw_explanations.update(manual)

    return (by_model["toeic.toeicmocktest"], by_model["toeic.toeicmockpassage"], by_model["toeic.toeicmockquestion"],
            cards, bundled, raw_explanations, manual)


def raw_explanation_key(item: dict) -> tuple:
    return explanation_key(
        item.get("question_text"),
        {letter: item.get(f"option_{letter.lower()}") for letter in "ABCD"},
        item.get("correct_answer"))


def load_manual_explanations() -> list[dict]:
    if not MANUAL_EXPLANATIONS_FILE.exists():
        return []
    return json.load(open(MANUAL_EXPLANATIONS_FILE, encoding="utf-8"))


def add_bundled_reading_sets(tests: dict, passages: dict, questions: dict, bundled: dict,
                             stats: collections.Counter, issues: list[str]) -> None:
    """Adds the bundled Part 5/6 sets in the shape of the database dump, so they go through the same cleaning."""
    for file_name, (data, modified) in sorted(bundled.items()):
        test_id = data.get("testId")
        set_pk = data.get("dbId") or data.get("uuid")
        if test_id not in BUNDLED_READING_SETS or not set_pk:
            issues.append(f"Bundled file {file_name}: unknown set '{test_id}' - skipped")
            continue
        if set_pk in tests:
            # p5-t1 / p6-t1 were also written to the database; the database copy is used.
            stats["bundled_sets_already_in_database"] += 1
            continue

        kind, code = BUNDLED_READING_SETS[test_id]
        level = int(test_id[-1]) if kind == "level" else None
        timestamp = modified.isoformat()
        tests[set_pk] = {
            "code": code,
            "title": data["title"],
            "description": data.get("description") or "",
            "duration_minutes": data.get("timeMinutes") or 60,
            "is_active": True,
            "total_questions": data.get("totalQuestions"),
            "metadata": {"bundled_kind": kind, "bundled_file": file_name},
            "created_at": timestamp,
            "updated_at": timestamp,
        }

        order = 0
        for group_index, group in enumerate(data["passages"]):
            passage_pk = None
            group_part = int(group.get("part") or data.get("part") or test_id[1])
            if group_part in GROUPED_PARTS:
                content = clean_text(group.get("passageContent"))
                translation = clean_text(group.get("passageTranslation"))
                passage_pk = str(uuid.uuid5(uuid.UUID(set_pk), f"passage:{group['groupId']}"))
                passages[passage_pk] = {
                    "test": set_pk,
                    "part": group_part,
                    "passage_type": None,
                    "title": clean_text(group.get("passageTitle")),
                    "content": f"{content}{TRANSLATION_SPLIT}{translation}" if content and translation else content,
                    "audio_url": None,
                    "image_url": None,
                    "order_index": group_index,
                    "created_at": timestamp,
                    "updated_at": timestamp,
                }

            for item in group["questions"]:
                options = {option["key"]: option.get("text") for option in item.get("options", [])}
                # p5-t2 keeps its explanation in "tip"; elsewhere "tip" repeats the explanation.
                tip = clean_text(item.get("tip"))
                explanation = clean_text(item.get("explanation")) or (tip if tip and not tip.startswith("[") else None)
                question_pk = item.get("id")
                if not question_pk or question_pk in questions:
                    question_pk = str(uuid.uuid5(uuid.UUID(set_pk), f"question:{order}"))
                    stats["bundled_question_ids_generated"] += 1
                questions[question_pk] = {
                    "test": set_pk,
                    "passage": passage_pk,
                    "part": int(item.get("part") or group_part),
                    "question_number": item.get("questionNum") or order + 1,
                    "question_text": unicodedata.normalize("NFC", item.get("text") or ""),
                    **{f"option_{letter.lower()}": options.get(letter) for letter in "ABCD"},
                    "correct_answer": item.get("correctAnswer"),
                    "explanation": explanation,
                    "transcript": None,
                    "audio_url": None,
                    "image_url": None,
                    "difficulty_level": level or 3,
                    "order_index": order,
                    "prefer_ai_explanation": False,
                    "created_at": timestamp,
                    "updated_at": timestamp,
                }
                order += 1

        stats["bundled_sets_added"] += 1
        stats["bundled_questions_added"] += order


def card_question_key(card_question: dict) -> tuple:
    options = {option["key"]: option.get("text") for option in card_question.get("options", [])}
    return (
        normalize_key(card_question.get("text")),
        *(normalize_key(options.get(letter)) for letter in "ABCD"),
        (card_question.get("correctAnswer") or "").strip().upper(),
    )


def source_question_key(fields: dict) -> tuple:
    return (
        normalize_key(fields["question_text"]),
        *(normalize_key(fields[f"option_{letter}"]) for letter in "abcd"),
        (fields["correct_answer"] or "").strip().upper(),
    )


def match_cards(tests: dict, questions: dict, cards: dict, stats: collections.Counter) -> dict[str, tuple[dict, int]]:
    """Maps question pk -> (card passage, position in card) for every set that has a listening card."""
    matches: dict[str, tuple[dict, int]] = {}
    questions_by_test = collections.defaultdict(list)
    for pk, fields in questions.items():
        questions_by_test[fields["test"]].append((pk, fields))

    for test_pk, test in tests.items():
        card = cards.get((test.get("code") or "").strip())
        if card is None:
            continue

        index = collections.defaultdict(list)
        position = 0
        for card_passage in card.get("passages", []):
            for card_question in card_passage.get("questions", []):
                index[card_question_key(card_question)].append((card_passage, card_question, position))
                position += 1

        used: set[int] = set()
        for pk, fields in sorted(questions_by_test[test_pk], key=lambda item: (item[1]["order_index"], item[1]["question_number"])):
            candidates = [c for c in index.get(source_question_key(fields), []) if c[2] not in used]
            if not candidates:
                stats["card_unmatched"] += 1
                continue
            preferred = [c for c in candidates if str(c[1].get("questionNum")) == str(fields["question_number"])]
            card_passage, _, card_position = (preferred or candidates)[0]
            used.add(card_position)
            matches[pk] = (card_passage, card_position)
            stats["card_matched"] += 1

    return matches


def build_seed(args: argparse.Namespace) -> None:
    source = (BASE_DIR / args.source).resolve() if not Path(args.source).is_absolute() else Path(args.source)
    tests, passages, questions, cards, bundled, raw_explanations, manual_explanations = load_snapshot(source)
    used_manual_keys: set[tuple] = set()
    stats: collections.Counter = collections.Counter()
    issues: list[str] = []

    print(f"[build] snapshot {source.name}: {len(tests)} tests, {len(passages):,} passages, {len(questions):,} questions, "
          f"{len(cards)} cards, {len(bundled)} bundled reading files, {len(raw_explanations):,} raw explanations")
    add_bundled_reading_sets(tests, passages, questions, bundled, stats, issues)

    kinds = {pk: classify_test(fields) for pk, fields in tests.items()}
    card_matches = match_cards(tests, questions, cards, stats)

    # ---- 1. clean every source question and resolve its media --------------------------------
    questions_by_passage = collections.defaultdict(list)
    for pk, fields in questions.items():
        if fields["passage"]:
            questions_by_passage[fields["passage"]].append(pk)

    cleaned_questions: dict[str, dict] = {}
    for pk, fields in questions.items():
        kind = kinds[fields["test"]]
        if kind == "legacy":
            continue

        part = int(fields["part"])
        card_passage = card_matches.get(pk, (None, None))[0] or {}
        source_passage = passages.get(fields["passage"]) if fields["passage"] else None

        text = clean_text(fields["question_text"])
        if text and (part == 1 or JUNK_QUESTION_TEXT.match(text)):
            stats["junk_question_text_removed"] += 1
            text = None

        options, stripped = strip_option_labels({letter: clean_text(fields[f"option_{letter.lower()}"]) for letter in "ABCD"})
        if stripped:
            stats["option_labels_stripped"] += 1
        if part == 2:
            options["D"] = None

        answer = (fields["correct_answer"] or "").strip().upper()
        if answer not in ("ABC" if part == 2 else "ABCD") or len(answer) != 1:
            issues.append(f"Question {pk} ({tests[fields['test']]['code']}): invalid answer '{answer}' - skipped")
            continue

        explanation = clean_text(fields["explanation"])
        explanation_generated = False
        if not explanation:
            # The Crack TOEIC / Pass TOEIC tests never got their explanations into the database.
            explanation_lookup = explanation_key(fields["question_text"], options, answer)
            raw = raw_explanations.get(explanation_lookup)
            passage_translated = bool(
                source_passage and TRANSLATION_SPLIT.strip("<>") in (source_passage["content"] or ""))
            explanation = compose_explanation(
                raw, answer, options.get(answer),
                include_translation=not (part in GROUPED_PARTS and passage_translated)) if raw else None
            explanation_generated = explanation is not None
            if explanation and explanation_lookup in manual_explanations:
                used_manual_keys.add(explanation_lookup)
                stats["explanation_written_by_hand"] += 1
            else:
                stats["explanation_from_raw_dataset" if explanation else "explanation_still_missing"] += 1
        transcript = clean_text(fields["transcript"])
        # For Part 1/2 the card item carries the question's own audio and photo; for grouped parts the
        # card media belongs to the passage, so it is only used to repair relative question URLs.
        standalone = part not in GROUPED_PARTS
        audio = resolve_media(fields["audio_url"], card_passage.get("audioSrc"), stats, "question_audio", fill_missing=standalone)
        image = resolve_media(fields["image_url"], card_passage.get("imageSrc"), stats, "question_image", fill_missing=standalone)
        passage_pk = fields["passage"]

        # Part 1, 2 and 5 are standalone in TOEIC Space: unwrap single-question passages.
        if source_passage is not None and standalone:
            audio = audio or resolve_media(source_passage["audio_url"], None, stats, "question_audio")
            image = image or resolve_media(source_passage["image_url"], None, stats, "question_image")
            wrapper_content = clean_text(source_passage["content"])
            if wrapper_content:
                if part == 5:
                    explanation = f"{explanation}\n{wrapper_content}" if explanation else wrapper_content
                else:
                    transcript = transcript or wrapper_content
            passage_pk = None
            stats["wrapper_passages_unwrapped"] += 1

        if part in GROUPED_PARTS and passage_pk is None:
            issues.append(f"Question {pk} ({tests[fields['test']]['code']}): Part {part} without passage - skipped")
            continue

        for field_name, value in (("explanation", explanation), ("transcript", transcript), ("question_text", text)):
            if value and DANGEROUS_HTML.search(value):
                raise SystemExit(f"Unsafe HTML found in {field_name} of question {pk}; aborting.")

        cleaned_questions[pk] = {
            "source_pk": pk,
            "source_test": fields["test"],
            "kind": kind,
            "part": part,
            "passage": passage_pk,
            "question_number": int(fields["question_number"]),
            "question_text": text,
            "audio_url": audio,
            "image_url": image,
            "options": options,
            "answer": answer,
            "explanation": explanation,
            "explanation_generated": explanation_generated,
            "transcript": transcript,
            "difficulty": min(5, max(1, int(fields["difficulty_level"] or 3))),
            "order_index": int(fields["order_index"]),
            "prefer_ai_explanation": bool(fields["prefer_ai_explanation"]),
            "card_position": card_matches.get(pk, (None, None))[1],
            "created_at": fields["created_at"],
            "updated_at": fields["updated_at"],
        }

    for key, entry in manual_explanations.items():
        if key not in used_manual_keys:
            issues.append(
                f"Manual explanation for {entry.get('test_code')} question {entry.get('question_number')} was not used: "
                f"the question no longer matches (text, options or answer changed) - review data/manual/explanations.json")

    # ---- 2. clean grouped passages ----------------------------------------------------------
    cleaned_passages: dict[str, dict] = {}
    for pk, fields in passages.items():
        member_pks = [q for q in questions_by_passage.get(pk, []) if q in cleaned_questions]
        if not member_pks:
            stats["passages_without_questions_dropped"] += 1
            continue
        part = int(fields["part"])
        if part not in GROUPED_PARTS:
            continue

        card_audio = collections.Counter(card_matches[q][0].get("audioSrc") for q in member_pks if q in card_matches)
        card_image = collections.Counter(card_matches[q][0].get("imageSrc") for q in member_pks if q in card_matches)
        if len([url for url in card_audio if url]) > 1:
            issues.append(f"Passage {pk}: questions map to different card audios - most common used")

        content = clean_text(fields["content"])
        if content and DANGEROUS_HTML.search(content):
            raise SystemExit(f"Unsafe HTML found in content of passage {pk}; aborting.")

        cleaned_passages[pk] = {
            "source_pk": pk,
            "source_test": fields["test"],
            "kind": kinds[fields["test"]],
            "part": part,
            "passage_type": clean_text(fields["passage_type"]) or ("conversation" if part == 3 else "talk" if part == 4 else "text"),
            "title": clean_text(fields["title"]),
            "content": content,
            "audio_url": resolve_media(fields["audio_url"], card_audio.most_common(1)[0][0] if card_audio else None, stats, "passage_audio"),
            "image_url": resolve_media(fields["image_url"], card_image.most_common(1)[0][0] if card_image else None, stats, "passage_image"),
            "order_index": int(fields["order_index"]),
            "questions": member_pks,
            "created_at": fields["created_at"],
            "updated_at": fields["updated_at"],
        }

    # ---- 3. de-duplicate: a unit is a passage group or a standalone question --------------------
    def dedup_key(question: dict) -> tuple:
        text = normalize_key(question["question_text"]) if question["part"] >= 2 else ""
        listening_script = normalize_key(question["transcript"])[:400] if question["part"] <= 2 else ""
        return (question["part"], text, *(normalize_key(question["options"][letter]) for letter in "ABCD"), question["answer"], listening_script)

    units = []
    for passage_pk, passage in cleaned_passages.items():
        member_keys = tuple(sorted(dedup_key(cleaned_questions[q]) for q in passage["questions"]))
        units.append({"key": ("group", member_keys), "passage": passage_pk, "questions": passage["questions"], "kind": passage["kind"]})
    for question_pk, question in cleaned_questions.items():
        if question["passage"] is None:
            units.append({"key": ("single", dedup_key(question)), "passage": None, "questions": [question_pk], "kind": question["kind"]})

    groups = collections.defaultdict(list)
    for unit in units:
        groups[unit["key"]].append(unit)

    canonical_question: dict[str, str] = {}   # source question pk -> canonical question pk
    kept_questions: dict[str, dict] = {}
    kept_passages: dict[str, dict] = {}

    for duplicates in groups.values():
        duplicates.sort(key=lambda unit: (SOURCE_PRIORITY[unit["kind"]], unit["questions"][0]))
        canonical = duplicates[0]
        by_key = {dedup_key(cleaned_questions[q]): q for q in canonical["questions"]}

        for unit in duplicates:
            for question_pk in unit["questions"]:
                target_pk = by_key[dedup_key(cleaned_questions[question_pk])]
                canonical_question[question_pk] = target_pk
                if unit is not canonical:
                    stats["duplicate_questions_merged"] += 1
                    merge_explanation(cleaned_questions[target_pk], cleaned_questions[question_pk])
                    merge_richer(cleaned_questions[target_pk], cleaned_questions[question_pk], ("transcript",), ("question_text", "audio_url", "image_url"))
            if unit is not canonical and unit["passage"]:
                stats["duplicate_passages_merged"] += 1
                merge_richer(cleaned_passages[canonical["passage"]], cleaned_passages[unit["passage"]], ("content",), ("audio_url", "image_url", "title"))

        for question_pk in canonical["questions"]:
            kept_questions[question_pk] = cleaned_questions[question_pk]
        if canonical["passage"]:
            kept_passages[canonical["passage"]] = cleaned_passages[canonical["passage"]]

    # ---- 4. full tests ----------------------------------------------------------------------
    seed_tests = []
    full_test_status = {}
    for pk, fields in tests.items():
        if kinds[pk] != "full":
            continue
        normalized = normalize_full_test(fields)
        test_questions = [q for q in kept_questions.values() if q["kind"] == "full" and q["source_test"] == pk]
        problems = full_test_problems(test_questions)
        active = bool(fields["is_active"]) and not problems
        if problems:
            issues.append(f"Test {normalized['code']} kept as Draft: {'; '.join(problems)}")
        full_test_status[pk] = active
        seed_tests.append({
            "Id": pk,
            "Code": normalized["code"],
            "Title": normalized["title"],
            "Description": clean_text(fields["description"]) or f"Đề thi TOEIC Listening & Reading đầy đủ 200 câu - {normalized['title']}.",
            "Category": normalized["category"],
            "Year": normalized["year"],
            "TotalQuestions": 200,
            "DurationMinutes": 120,
            "TotalListeningQuestions": 100,
            "TotalReadingQuestions": 100,
            "AudioUrl": None,
            "IsActive": True,
            "Status": "Active" if active else "Draft",
            "ExternalId": pk,
            "Source": "studychill",
            "Metadata": json.dumps(fields.get("metadata") or {}, ensure_ascii=False),
            "CreatedAt": fields["created_at"],
            "UpdatedAt": fields["updated_at"],
        })
    seed_tests.sort(key=lambda test: (test["Category"], test["Code"]))

    # ---- 5. practice sets ---------------------------------------------------------------------
    seed_sets, seed_items = [], []
    skipped_sets = []
    for pk, fields in tests.items():
        kind = kinds[pk]
        code = (fields.get("code") or "").strip()
        if kind == "full":
            continue
        if kind == "legacy":
            skipped_sets.append(code)
            continue

        members = [q for q in cleaned_questions.values() if q["source_test"] == pk]
        if not members:
            issues.append(f"Practice set {code or pk} has no usable questions - skipped")
            continue
        parts = {q["part"] for q in members}
        if len(parts) != 1:
            issues.append(f"Practice set {code or pk} mixes parts {sorted(parts)} - skipped")
            continue
        part = parts.pop()

        members.sort(key=lambda q: (q["card_position"] if q["card_position"] is not None else 10**9, q["order_index"], q["question_number"]))
        ordered_ids, seen = [], set()
        for member in members:
            target = canonical_question[member["source_pk"]]
            if target in seen:
                stats["practice_items_duplicate_in_set"] += 1
                continue
            seen.add(target)
            ordered_ids.append(target)

        card = cards.get(code, {})
        level = int(code[-1]) if kind == "level" else None
        set_kind = {"level": "Level", "topic": "Topic", "drill": "PartDrill"}[kind]
        seed_sets.append({
            "Id": pk,
            "Code": practice_code(fields, part),
            "Title": clean_text(card.get("fullName")) or fields["title"].strip(),
            "Description": clean_text(card.get("description")) or clean_text(fields["description"]),
            "Kind": set_kind,
            "Part": part,
            "Level": level,
            "TargetScore": LEVEL_TARGET_SCORES.get(level) if level else None,
            "DurationMinutes": int(fields["duration_minutes"]) or 60,
            "Status": "Active" if fields["is_active"] else "Draft",
            "OrderIndex": (level or 0) if kind == "level" else 0,
            "ExternalId": pk,
            "Source": "studychill",
            "CreatedAt": fields["created_at"],
            "UpdatedAt": fields["updated_at"],
        })
        seed_items.extend({"PracticeSetId": pk, "QuestionId": question_id, "OrderIndex": index} for index, question_id in enumerate(ordered_ids))

    seed_sets.sort(key=lambda s: (s["Part"], s["Kind"], s["OrderIndex"], s["Code"]))
    for index, practice_set in enumerate(seed_sets):
        if practice_set["Kind"] != "Level":
            practice_set["OrderIndex"] = index

    # ---- 6. questions and passages rows ------------------------------------------------------
    seed_passages = []
    for pk, passage in kept_passages.items():
        is_full = passage["kind"] == "full"
        seed_passages.append({
            "Id": pk,
            "TestId": passage["source_test"] if is_full else None,
            "Part": passage["part"],
            "PassageType": passage["passage_type"],
            "Title": passage["title"],
            "Content": passage["content"],
            "AudioUrl": passage["audio_url"],
            "ImageUrl": passage["image_url"],
            "Transcript": None,
            "OrderIndex": passage["order_index"],
            "ExternalId": pk,
            "CreatedAt": passage["created_at"],
            "UpdatedAt": passage["updated_at"],
        })

    seed_questions = []
    for pk, question in kept_questions.items():
        is_full = question["kind"] == "full"
        seed_questions.append({
            "Id": pk,
            "TestId": question["source_test"] if is_full else None,
            "PassageId": question["passage"],
            "Part": question["part"],
            "Section": "Listening" if question["part"] <= 4 else "Reading",
            "QuestionNumber": question["question_number"] if is_full else None,
            "QuestionText": question["question_text"],
            "AudioUrl": question["audio_url"],
            "ImageUrl": question["image_url"],
            "OptionA": question["options"]["A"] or "",
            "OptionB": question["options"]["B"] or "",
            "OptionC": question["options"]["C"] or "",
            "OptionD": question["options"]["D"],
            "CorrectAnswer": question["answer"],
            "Explanation": question["explanation"],
            "Transcript": question["transcript"],
            "DifficultyLevel": question["difficulty"],
            "Topic": None,
            "Status": "Active",
            "Version": 1,
            "OrderIndex": question["question_number"] if is_full else question["order_index"],
            "PreferAiExplanation": question["prefer_ai_explanation"],
            "ExternalId": pk,
            "CreatedAt": question["created_at"],
            "UpdatedAt": question["updated_at"],
        })

    # ---- 7. questions that cannot be played are kept in the bank as Draft ------------------------
    passage_audio = {p["Id"]: p["AudioUrl"] for p in seed_passages}
    unplayable = set()
    for question in seed_questions:
        missing_audio = question["Part"] <= 4 and not (question["AudioUrl"] or passage_audio.get(question["PassageId"]))
        missing_photo = question["Part"] == 1 and not question["ImageUrl"]
        if missing_audio or missing_photo:
            question["Status"] = "Draft"
            unplayable.add(question["Id"])
            stats["questions_draft_missing_audio" if missing_audio else "questions_draft_missing_photo"] += 1

    if unplayable & {q["Id"] for q in seed_questions if q["TestId"]}:
        raise SystemExit("A full test question has no playable media; the full tests must stay complete.")

    items_per_set = collections.Counter(item["PracticeSetId"] for item in seed_items)
    unplayable_per_set = collections.Counter(item["PracticeSetId"] for item in seed_items if item["QuestionId"] in unplayable)
    removed_from_set = set()
    for practice_set in seed_sets:
        count = unplayable_per_set[practice_set["Id"]]
        if not count:
            continue
        if count <= max(1, round(items_per_set[practice_set["Id"]] * MAX_UNPLAYABLE_SHARE_TO_TRIM)):
            # A handful of broken items: drop them from the set, keep the questions in the bank as Draft.
            removed_from_set.add(practice_set["Id"])
            stats["practice_items_removed_missing_media"] += count
            issues.append(f"Practice set {practice_set['Code']}: removed {count} questions without verifiable audio/photo")
            continue
        if practice_set["Status"] == "Active":
            practice_set["Status"] = "Draft"
            stats["practice_sets_draft_missing_media"] += 1
        issues.append(
            f"Practice set {practice_set['Code']} kept as Draft: {count}/{items_per_set[practice_set['Id']]} questions have no "
            f"verifiable audio/photo (the source only stores relative file names such as '65-67.mp3')")

    if removed_from_set:
        seed_items = [item for item in seed_items if not (item["PracticeSetId"] in removed_from_set and item["QuestionId"] in unplayable)]
        order = collections.Counter()
        for item in seed_items:
            item["OrderIndex"] = order[item["PracticeSetId"]]
            order[item["PracticeSetId"]] += 1

    seed = {
        "generated_at": dt.datetime.now(dt.timezone.utc).isoformat(),
        "source": source.name,
        "tests": seed_tests,
        "passages": seed_passages,
        "questions": seed_questions,
        "practice_sets": seed_sets,
        "practice_set_items": seed_items,
    }

    SEEDS_DIR.mkdir(parents=True, exist_ok=True)
    with gzip.open(SEED_FILE, "wt", encoding="utf-8") as output:
        json.dump(seed, output, ensure_ascii=False)

    write_report(source, tests, passages, questions, kinds, seed, stats, issues, skipped_sets)
    print(f"[build] tests={len(seed_tests)} practice_sets={len(seed_sets)} passages={len(seed_passages):,} "
          f"questions={len(seed_questions):,} items={len(seed_items):,}")
    print(f"[build] seed   -> {SEED_FILE.relative_to(BASE_DIR)}")
    print(f"[build] report -> {REPORT_FILE.relative_to(BASE_DIR)}")


def merge_explanation(target: dict, other: dict) -> None:
    """A Studychill explanation always wins over one composed from the raw dataset; else the longest."""
    if not other["explanation"]:
        return
    if not target["explanation"] or (target["explanation_generated"] and not other["explanation_generated"]):
        target["explanation"] = other["explanation"]
        target["explanation_generated"] = other["explanation_generated"]
        return
    if target["explanation_generated"] == other["explanation_generated"] \
            and len(other["explanation"]) > len(target["explanation"]):
        target["explanation"] = other["explanation"]


def merge_richer(target: dict, other: dict, longest_fields: tuple, first_non_empty_fields: tuple) -> None:
    for field in longest_fields:
        if len(other.get(field) or "") > len(target.get(field) or ""):
            target[field] = other[field]
    for field in first_non_empty_fields:
        if not target.get(field) and other.get(field):
            target[field] = other[field]


def full_test_problems(test_questions: list[dict]) -> list[str]:
    problems = []
    if len(test_questions) != 200:
        problems.append(f"{len(test_questions)} questions instead of 200")
    numbers = collections.Counter(q["question_number"] for q in test_questions)
    duplicates = sorted(number for number, count in numbers.items() if count > 1)
    if duplicates:
        problems.append(f"duplicate numbers {duplicates[:10]}")
    for part, (first, last) in STANDARD_RANGES.items():
        in_part = [q for q in test_questions if q["part"] == part]
        if len(in_part) != last - first + 1:
            problems.append(f"Part {part} has {len(in_part)} questions")
        if any(not first <= q["question_number"] <= last for q in in_part):
            problems.append(f"Part {part} numbers outside {first}-{last}")
    return problems


def write_report(source, tests, passages, questions, kinds, seed, stats, issues, skipped_sets) -> None:
    kind_counts = collections.Counter(kinds.values())
    bundled_files = sorted((fields.get("metadata") or {}).get("bundled_file") for fields in tests.values()
                           if (fields.get("metadata") or {}).get("bundled_file"))
    test_rows = "\n".join(
        f"| {t['Code']} | {t['Title']} | {t['Category']} | {t['Status']} |" for t in seed["tests"]
    )
    items_per_set = collections.Counter(item["PracticeSetId"] for item in seed["practice_set_items"])
    set_rows = "\n".join(
        f"| {s['Code']} | Part {s['Part']} | {s['Kind']} | {s['Level'] or ''} | {items_per_set[s['Id']]} | {s['Status']} | {s['Title']} |"
        for s in seed["practice_sets"]
    )
    bank_questions = sum(1 for q in seed["questions"] if q["TestId"] is None)
    issue_lines = "\n".join(f"- {issue}" for issue in issues) or "- None"
    stat_lines = "\n".join(f"| {key} | {value:,} |" for key, value in sorted(stats.items()))

    REPORT_FILE.write_text(f"""# TOEIC Space - Assessment import report

> Generated by `scripts/toeic_data_pipeline.py build` at {seed['generated_at']}
> Source snapshot: `data/source/{source.name}` (Studychill `toeic_mock_*` tables, listening cards and the
> Part 5/6 sets bundled with the Studychill frontend - read-only export)

## 1. Source vs. result

| | Source (Studychill) | TOEIC Space |
|---|---:|---:|
| Full tests | {kind_counts['full']} | {len(seed['tests'])} |
| Practice collections (database "tests" + {len(bundled_files)} bundled JSON sets) | {len(tests) - kind_counts['full']} | {len(seed['practice_sets'])} practice sets |
| Legacy duplicate sets (`p*-t*`) | {kind_counts['legacy']} | skipped |
| Passages | {len(passages):,} | {len(seed['passages']):,} |
| Questions | {len(questions):,} | {len(seed['questions']):,} ({len(seed['questions']) - bank_questions:,} in tests, {bank_questions:,} bank-only) |
| Practice set items | - | {len(seed['practice_set_items']):,} |

## 2. Full tests

| Code | Title | Category | Status |
|---|---|---|---|
{test_rows}

## 3. Practice sets

| Code | Part | Kind | Level | Questions | Status | Title |
|---|---|---|---|---:|---|---|
{set_rows}

Skipped legacy sets (100% identical copies of the `p*-lv*` sets, `p3-t2` has no questions): {', '.join(sorted(skipped_sets))}

Bundled JSON sets (`frontend/src/data/exams`, served by Studychill without the database): {', '.join(bundled_files) or 'none'}

## 4. Cleaning statistics

| Step | Count |
|---|---:|
{stat_lines}

## 5. Issues

{issue_lines}
""", encoding="utf-8")


# --------------------------------------------------------------------------------------------
# load
# --------------------------------------------------------------------------------------------

TABLE_COLUMNS = {
    "ToeicTests": ["Id", "Code", "Title", "Description", "Category", "Year", "TotalQuestions", "DurationMinutes",
                   "TotalListeningQuestions", "TotalReadingQuestions", "AudioUrl", "IsActive", "Status",
                   "ExternalId", "Source", "Metadata", "CreatedAt", "UpdatedAt"],
    "ToeicPassages": ["Id", "TestId", "Part", "PassageType", "Title", "Content", "AudioUrl", "ImageUrl", "Transcript",
                      "OrderIndex", "ExternalId", "CreatedAt", "UpdatedAt"],
    "ToeicQuestions": ["Id", "TestId", "PassageId", "Part", "Section", "QuestionNumber", "QuestionText", "AudioUrl",
                       "ImageUrl", "OptionA", "OptionB", "OptionC", "OptionD", "CorrectAnswer", "Explanation",
                       "Transcript", "DifficultyLevel", "Topic", "Status", "Version", "OrderIndex",
                       "PreferAiExplanation", "ExternalId", "CreatedAt", "UpdatedAt"],
    "ToeicPracticeSets": ["Id", "Code", "Title", "Description", "Kind", "Part", "Level", "TargetScore",
                          "DurationMinutes", "Status", "OrderIndex", "ExternalId", "Source", "CreatedAt", "UpdatedAt"],
    "ToeicPracticeSetItems": ["PracticeSetId", "QuestionId", "OrderIndex"],
}
SEED_KEYS = {
    "ToeicTests": "tests",
    "ToeicPassages": "passages",
    "ToeicQuestions": "questions",
    "ToeicPracticeSets": "practice_sets",
    "ToeicPracticeSetItems": "practice_set_items",
}


def to_mysql_datetime(value: str) -> str:
    return dt.datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(dt.timezone.utc).strftime("%Y-%m-%d %H:%M:%S.%f")


def load_seed(args: argparse.Namespace) -> None:
    import MySQLdb  # imported lazily so export/build work without the driver

    seed = json.load(gzip.open(SEED_FILE, "rt", encoding="utf-8"))
    connection = MySQLdb.connect(
        host=args.host, port=args.port, user=args.user, passwd=args.password, db=args.database, charset="utf8mb4"
    )
    cursor = connection.cursor()

    cursor.execute("SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%InitialAssessmentSchema'")
    if cursor.fetchone()[0] == 0:
        raise SystemExit(f"Database '{args.database}' has no InitialAssessmentSchema migration. Run 'dotnet ef database update' first.")

    cursor.execute("SELECT (SELECT COUNT(*) FROM ToeicTests) + (SELECT COUNT(*) FROM ToeicQuestions) + (SELECT COUNT(*) FROM ToeicPracticeSets)")
    existing = cursor.fetchone()[0]
    if existing:
        if not args.replace:
            raise SystemExit(f"Database '{args.database}' already contains content. Use --replace to delete and reload it.")
        cursor.execute("SELECT COUNT(*) FROM ToeicAttempts")
        if cursor.fetchone()[0]:
            raise SystemExit("Refusing to replace content: the database already has learner attempts.")
        for table in ("ToeicPracticeSetItems", "ToeicPracticeSets", "ToeicQuestions", "ToeicPassages", "ToeicTests", "OutboxMessages"):
            cursor.execute(f"DELETE FROM `{table}`")
        print(f"[load] removed existing content from {args.database}")

    for table, columns in TABLE_COLUMNS.items():
        rows = []
        for record in seed[SEED_KEYS[table]]:
            row = []
            for column in columns:
                value = record.get(column)
                if column in ("CreatedAt", "UpdatedAt") and value:
                    value = to_mysql_datetime(value)
                elif isinstance(value, bool):
                    value = int(value)
                row.append(value)
            rows.append(row)

        column_list = ", ".join(f"`{column}`" for column in columns)
        placeholders = ", ".join(["%s"] * len(columns))
        statement = f"INSERT INTO `{table}` ({column_list}) VALUES ({placeholders})"
        for start in range(0, len(rows), 1000):
            cursor.executemany(statement, rows[start:start + 1000])
        print(f"[load] {table}: {len(rows):,} rows")

    connection.commit()

    for table in TABLE_COLUMNS:
        cursor.execute(f"SELECT COUNT(*) FROM `{table}`")
        expected = len(seed[SEED_KEYS[table]])
        actual = cursor.fetchone()[0]
        if actual != expected:
            raise SystemExit(f"Row count mismatch for {table}: expected {expected}, found {actual}")

    connection.close()
    print(f"[load] done - {args.database} matches the seed")


# --------------------------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    subparsers = parser.add_subparsers(dest="command", required=True)

    export_parser = subparsers.add_parser("export", help="snapshot Studychill data over SSH (read-only)")
    export_parser.add_argument("--host", default="studychill", help="SSH host alias")
    export_parser.set_defaults(handler=export_snapshot)

    build_parser = subparsers.add_parser("build", help="build the seed and the audit report")
    build_parser.add_argument("--source", required=True, help="snapshot folder, e.g. data/source/studychill_20260913")
    build_parser.set_defaults(handler=build_seed)

    load_parser = subparsers.add_parser("load", help="load the seed into MySQL")
    load_parser.add_argument("--host", default=os.getenv("MYSQL_HOST", "127.0.0.1"))
    load_parser.add_argument("--port", type=int, default=int(os.getenv("MYSQL_PORT", "3306")))
    load_parser.add_argument("--user", default=os.getenv("MYSQL_USER", "root"))
    load_parser.add_argument("--password", default=os.getenv("MYSQL_PASS", ""))
    load_parser.add_argument("--database", default=os.getenv("MYSQL_DB", "toeic_space_assessment"))
    load_parser.add_argument("--replace", action="store_true", help="delete existing content first (refused if attempts exist)")
    load_parser.set_defaults(handler=load_seed)

    args = parser.parse_args()
    args.handler(args)


if __name__ == "__main__":
    main()
