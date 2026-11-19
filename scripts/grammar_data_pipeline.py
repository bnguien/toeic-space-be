#!/usr/bin/env python3
"""
TOEIC Space - Studychill grammar roadmap data pipeline
======================================================

Pulls the Studychill "Lộ trình ngữ pháp" content (5 stages, 44 topics: knowledge, vocabulary and exercises per topic,
one test per stage) and writes the committed import bundle data/grammar/grammar-content.json.gz.

Studychill stores the content as JSON files with Vietnamese keys (content/grammar/cd-NN/*.json). `build` maps every
key (and the enum-like values) to English once, so the Content service never stores or serves Studychill's raw JSON.
The mapping table is documented in docs/grammar.md ("Content mapping").

Commands
--------
  export  Read-only snapshot over SSH (host alias "studychill"): `tar` of the content JSON streamed to stdout, nothing
          is written on the server. Only knowledge.json, vocab.json, tests.json (per topic) and chang-NN/test.json are
          taken; the walkthrough (linh-hoi), lecture (giang*) and audio files are out of scope.
  build   Map, validate and bundle the snapshot into data/grammar/grammar-content.json.gz (deterministic: the same
          snapshot gives a byte-identical file) and print the counts.

Usage
-----
  python scripts/grammar_data_pipeline.py export
  python scripts/grammar_data_pipeline.py build                       # newest data/source/studychill_grammar_*
  python scripts/grammar_data_pipeline.py build --source data/source/studychill_grammar_20261107
"""

from __future__ import annotations

import argparse
import datetime as dt
import gzip
import io
import json
import re
import subprocess
import sys
import tarfile
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

BASE_DIR = Path(__file__).resolve().parent.parent
SOURCE_ROOT = BASE_DIR / "data" / "source"
OUTPUT_DIR = BASE_DIR / "data" / "grammar"
OUTPUT_FILE = OUTPUT_DIR / "grammar-content.json.gz"
REMOTE_ROOT = "/root/Studychill/content/grammar"
SCHEMA_VERSION = 1

# ---- Key mapping (Studychill → TOEIC Space). Context-free unless listed in CONTEXT_KEYS. ----
KEY_MAP = {
    # shared
    "cd": "topic", "chang": "stage", "ten": "title", "tieuDe": "title", "so": "number", "ma": "code", "id": "id",
    "phienBan": "version", "text": "text", "nhan": "label", "loai": "kind", "kieu": "kind", "dang": "kind",
    "mau": "tone", "part": "part", "viTri": "position",
    # knowledge
    "mucTieu": "goals", "donVi": "units", "mucCon": "subItems", "yChinh": "mainIdea", "nhanDien": "signals",
    "congThuc": "formula", "bayToeic": "toeicTraps", "bay": "trap", "vi_sao": "why", "cach_tranh": "avoid",
    "soDo": "diagram", "meoChillo": "tip", "nhacLai": "recall", "ghi": "note", "tomTat": "summary",
    "en": "sentence", "vi": "translation", "chuThich": "caption", "nhinLaNho": "memoryHook",
    "o": "cells", "to": "highlight", "tuyChon": "optional", "goc": "root", "nhanh": "branches", "con": "children",
    "cot": "columns", "hang": "rows", "tieuChi": "criterion", "batDau": "start", "nut": "nodes", "co": "yes",
    "khong": "no", "now": "now", "moc": "marks", "den": "end", "truc": "axis", "thap": "low", "cao": "high",
    "bac": "steps", "thanhVien": "members", "tuLoai": "partOfSpeech", "hauTo": "suffix", "khoi": "blocks",
    "noiTruoc": "joinBefore", "loi": "core", "lop": "layers", "phia": "side", "cauDay": "fullSentence",
    # vocab
    "soTuMoi": "newWordCount", "entries": "entries", "tu": "word", "ipa": "ipa", "nghia": "meaning",
    "hoTu": "wordFamily", "capDo": "level", "chuDe": "theme", "collocations": "collocations",
    "dungChoNguPhap": "usedInGrammar", "ghiChuBay": "trapNote", "laOnTap": "isReview", "loaiDanhTu": "countability",
    "cdGoc": "sourceTopic",
    # exercises
    "quickChecks": "quickChecks", "miniTest": "miniTest", "questions": "questions", "subQuestions": "subQuestions",
    "nguongDat": "passMark", "thoiGianPhut": "timeMinutes", "soCau": "questionCount", "maDonVi": "unitCodes",
    "doKho": "difficulty", "de": "prompt", "luaChon": "options", "dapAn": "answer", "loiGiai": "explanation",
    "dauHieu": "signal", "quyTac": "rule", "viSaoSai": "whyWrong", "phuongAn": "option", "nhoNhanh": "quickNote",
    "danhSachChapNhan": "acceptedAnswers", "suaThanh": "correction", "gachChan": "underlines", "goiY": "hint",
    "tuGoc": "baseWord", "manh": "pieces", "phanDoan": "segments", "veTrai": "left", "vePhai": "right",
    "lyDoDung": "correctReason", "dungNguPhap": "grammaticalOptions", "doanVan": "passage", "loaiVanBan": "textType",
}
# Same Studychill key, different meaning by shape.
CONTEXT_KEYS = {
    "lyDo": lambda v: "reasons" if isinstance(v, list) else "reason",  # judge reasons / whyWrong item reason
    "viDu": lambda v: "examples" if isinstance(v, list) else "example",  # unit examples / one example
}
DROPPED_KEYS = {"$schema", "ghiChuBienSoan"}

# ---- Value mapping of enum-like fields (by English key). ----
VALUE_MAP = {
    "difficulty": {"de": "easy", "vua": "medium", "kho": "hard"},
    "textType": {"email": "email", "memo": "memo", "thong-bao": "notice", "quang-cao": "advertisement",
                 "bai-bao": "article", "thu": "letter", "huong-dan": "instructions"},
    "theme": {"van-phong": "office", "nhan-su": "hr", "tai-chinh": "finance", "mua-sam": "shopping",
              "du-lich": "travel", "san-xuat": "manufacturing", "hoi-nghi": "conference",
              "bat-dong-san": "real-estate", "y-te": "health", "cong-nghe": "technology", "giao-hang": "delivery",
              "marketing": "marketing", "nha-hang-khach-san": "hospitality"},
    "countability": {"dem-duoc": "countable", "khong-dem-duoc": "uncountable", "ca-hai": "both",
                     "chi-so-nhieu": "plural-only"},
    "side": {"truoc": "before", "sau": "after", "chen": "inside"},
}
# `kind` values depend on the object: question kinds and diagram kinds are already English identifiers.
KIND_VALUES = {
    "nguPhap": "grammar", "chenCau": "sentence",  # cloze6 sub-question
    "cauHoi": "question", "dapAn": "answer",  # decision node
    "diem": "point", "khoang": "range", "lap": "repeat",  # timeline mark
}
JUDGE_ANSWERS = {"dung": "correct", "sai": "incorrect"}
QUESTION_KINDS = {"mcq5", "pair990", "wordform", "error", "transform", "order", "parse", "match", "judge", "cloze6"}
DIAGRAM_KINDS = {"slot", "branch", "compare", "decision", "timeline", "ladder", "family", "formula", "layers", "matrix"}
CODE_KEY = re.compile(r"^CĐ\d{2}(\.\d+)*$")
UNKNOWN: set[str] = set()


def map_node(node, parent_kind: str | None = None):
    """Deep copy of a Studychill JSON node with English keys / enum values."""
    if isinstance(node, list):
        return [map_node(x, parent_kind) for x in node]
    if not isinstance(node, dict):
        return node
    out: dict = {}
    for key, value in node.items():
        if key in DROPPED_KEYS:
            continue
        if CODE_KEY.match(key):  # quickChecks: {"CĐ04.1": [...]} keeps its unit codes as data
            out[key] = map_node(value)
            continue
        if key in CONTEXT_KEYS:
            new_key = CONTEXT_KEYS[key](value)
        elif key in KEY_MAP:
            new_key = KEY_MAP[key]
        else:
            UNKNOWN.add(key)
            new_key = key
        if new_key in out:
            raise ValueError(f"key collision on '{new_key}' (from '{key}')")
        mapped = map_node(value)
        if new_key in VALUE_MAP and isinstance(mapped, str):
            mapped = VALUE_MAP[new_key].get(mapped, mapped)
        if new_key == "kind" and isinstance(mapped, str) and mapped not in QUESTION_KINDS | DIAGRAM_KINDS:
            mapped = KIND_VALUES.get(mapped, mapped)
        out[new_key] = mapped
    if out.get("kind") == "judge" and isinstance(out.get("answer"), str):
        out["answer"] = JUDGE_ANSWERS.get(out["answer"], out["answer"])
    return out


# ---- export ----

def export() -> Path:
    stamp = dt.date.today().strftime("%Y%m%d")
    target = SOURCE_ROOT / f"studychill_grammar_{stamp}"
    target.mkdir(parents=True, exist_ok=True)
    remote = (
        f"cd {REMOTE_ROOT} && tar -cf - "
        "cd-*/knowledge.json cd-*/vocab.json cd-*/tests.json chang-*/test.json"
    )
    print(f"export: ssh studychill (read-only tar) → {target}")
    raw = subprocess.run(["ssh", "-o", "BatchMode=yes", "studychill", remote], check=True, capture_output=True).stdout
    count = 0
    with tarfile.open(fileobj=io.BytesIO(raw)) as tar:
        for member in tar.getmembers():
            if not member.isfile() or not re.fullmatch(r"(cd-\d{2}/(knowledge|vocab|tests)|chang-\d{2}/test)\.json",
                                                        member.name):
                continue
            dest = target / member.name
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(tar.extractfile(member).read())
            count += 1
    print(f"export: {count} files, {len(raw) / 1e6:.1f} MB")
    return target


# ---- build ----

def _read(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def _questions(items: list[dict]) -> int:
    return sum(len(q.get("subQuestions") or []) if q.get("kind") == "cloze6" else 1 for q in items)


def build(source: Path) -> None:
    topics, stages = [], []
    counts = {"topics": 0, "units": 0, "subItems": 0, "diagrams": 0, "vocabEntries": 0, "topicQuestions": 0,
              "stageTests": 0, "stageQuestions": 0}
    question_ids: set[str] = set()
    for folder in sorted(source.glob("cd-[0-9][0-9]")):
        knowledge = map_node(_read(folder / "knowledge.json"))
        vocab = map_node(_read(folder / "vocab.json"))
        exercises = map_node(_read(folder / "tests.json"))
        topic = f"{int(exercises['topic']):02d}"
        units = knowledge.get("units") or []
        counts["units"] += len(units)
        counts["subItems"] += sum(len(u.get("subItems") or []) for u in units)
        counts["diagrams"] += sum(1 for u in units for x in [u, *(u.get("subItems") or [])] if x.get("diagram"))
        counts["vocabEntries"] += len(vocab.get("entries") or [])
        items = [q for qs in (exercises.get("quickChecks") or {}).values() for q in qs]
        items += (exercises.get("miniTest") or {}).get("questions") or []
        counts["topicQuestions"] += _questions(items)
        for q in items:
            for sub in [q, *(q.get("subQuestions") or [])] if q.get("kind") == "cloze6" else [q]:
                if sub is q and q.get("kind") == "cloze6":
                    continue
                if sub["id"] in question_ids:
                    raise ValueError(f"duplicate question id {sub['id']}")
                question_ids.add(sub["id"])
        topics.append({"topic": topic, "stage": exercises.get("stage", ""), "title": knowledge.get("title", ""),
                       "knowledge": knowledge, "vocab": vocab, "exercises": exercises})
    for path in sorted(source.glob("chang-[0-9][0-9]/test.json")):
        test = map_node(_read(path))
        counts["stageQuestions"] += _questions(test.get("questions") or [])
        for q in test.get("questions") or []:
            for sub in (q.get("subQuestions") or []) if q.get("kind") == "cloze6" else [q]:
                if sub["id"] in question_ids:
                    raise ValueError(f"duplicate question id {sub['id']}")
                question_ids.add(sub["id"])
        stages.append(test)
    counts["topics"], counts["stageTests"] = len(topics), len(stages)
    if UNKNOWN:
        raise ValueError(f"unmapped Studychill keys: {sorted(UNKNOWN)}")
    if len(topics) != 44 or len(stages) != 5:
        raise ValueError(f"expected 44 topics and 5 stage tests, got {len(topics)} / {len(stages)}")
    bundle = {"schemaVersion": SCHEMA_VERSION, "topics": topics, "stages": stages}
    raw = json.dumps(bundle, ensure_ascii=False, separators=(",", ":"), sort_keys=False).encode("utf-8")
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    buffer = io.BytesIO()
    with gzip.GzipFile(fileobj=buffer, mode="wb", mtime=0, compresslevel=9) as gz:
        gz.write(raw)
    OUTPUT_FILE.write_bytes(buffer.getvalue())
    print(f"build: {OUTPUT_FILE.relative_to(BASE_DIR)} {len(buffer.getvalue()) / 1e6:.2f} MB "
          f"(raw {len(raw) / 1e6:.2f} MB)")
    for key, value in counts.items():
        print(f"  {key}: {value}")


def newest_source() -> Path:
    candidates = sorted(SOURCE_ROOT.glob("studychill_grammar_*"))
    if not candidates:
        sys.exit("No snapshot under data/source/studychill_grammar_*; run `export` first.")
    return candidates[-1]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("export")
    b = sub.add_parser("build")
    b.add_argument("--source", type=Path)
    args = parser.parse_args()
    if args.command == "export":
        export()
    else:
        build(args.source or newest_source())


if __name__ == "__main__":
    main()
