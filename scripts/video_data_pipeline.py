#!/usr/bin/env python3
"""
TOEIC Space - Studychill bilingual video ("Video song ngữ") data pipeline
=========================================================================

Pulls the Studychill video catalog, its cached YouTube subtitles and the word-level lists behind the
TOEIC/B2/C1+ highlighting, and writes the committed import files in data/videos/ (format: data/videos/README.md).

Commands
--------
  export  Read-only snapshot over SSH (host alias "studychill"):
            * catalog topics + videos through read-only ORM queries fed to `manage.py shell` on stdin
              (nothing is written on the server, no database credentials needed locally);
            * the subtitle cache /app/media/coursesync_subs/ streamed as tar.gz to stdout — only language
              lists (*.langs.v2.json), English tracks (*.en*.v2.json) and Vietnamese tracks (*.vi.v2.json),
              selected on the server with `find` so other languages are never downloaded;
            * the word-level sources bundled with the Studychill backend (toeic_vocab.json, basic_words.txt).
  build   Validate and normalise the snapshot into data/videos/catalog.json, subtitles-NN.json.gz (each <= 1.9 MB
          so it passes the gateway's 2 MB body cap) and README.md, plus the embedded word-level resource of the
          Content service (Infrastructure/Services/Video/Resources/word-levels.json).
          Deterministic: the same snapshot gives byte-identical files (sorted keys/rows, gzip mtime 0).

Usage
-----
  python scripts/video_data_pipeline.py export
  python scripts/video_data_pipeline.py build                        # newest data/source/studychill_videos_*
  python scripts/video_data_pipeline.py build --source data/source/studychill_videos_20261107
"""

from __future__ import annotations

import argparse
import collections
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
OUTPUT_DIR = BASE_DIR / "data" / "videos"
# Static reference data of the reader (word levels) is embedded in the Content service, not imported into a table.
WORD_LEVELS_FILE = (BASE_DIR / "src" / "Services" / "Content" / "ToeicSpace.Content.Infrastructure" / "Services"
                    / "Video" / "Resources" / "word-levels.json")
# Studychill status strings -> the English enum names stored by TOEIC Space.
SUBTITLE_STATUS = {"ok": "available", "none": "missing", "unknown": "unknown"}
EMBED_STATUS = {"ok": "playable", "blocked": "blocked", "unknown": "unknown"}
LEVEL_SOURCE = {"est": "estimate", "subs": "subtitles"}
SCHEMA_VERSION = 1

CATALOG_SNAPSHOT = "catalog.json.gz"
SUBS_SNAPSHOT = "coursesync_subs.tar.gz"
VOCAB_SNAPSHOT = "toeic_vocab.json.gz"
BASIC_SNAPSHOT = "basic_words.txt.gz"
MANIFEST = "manifest.json"

REMOTE_REPO = "/root/Studychill/backend/apps/learning"
REMOTE_VOCAB = f"{REMOTE_REPO}/vocabulary/data/toeic_vocab.json"
REMOTE_BASIC = f"{REMOTE_REPO}/listening/data/basic_words.txt"
REMOTE_SUBS_DIR = "/app/media/coursesync_subs"

# Only the v2 cache files (v2 = per-word offsets). Patterns are matched by `find` inside the container.
REMOTE_SUBS_FIND = (
    f"cd {REMOTE_SUBS_DIR} && find . -maxdepth 1 -type f "
    "\\( -name '*.langs.v2.json' -o -name '*.en*.v2.json' -o -name '*.vi.v2.json' \\) -print0 "
    "| LC_ALL=C sort -z | tar --null -czf - --files-from=-"
)

# Compressed size cap of one subtitle chunk (gateway body cap is 2 MB, multipart adds a little).
CHUNK_MAX_BYTES = 1_900_000
# Above this total the build keeps only one English track per video (the one the reader would pick) + vi.
TOTAL_BUDGET_BYTES = 25 * 1024 * 1024

BEGIN, END = "@@VIDEO_EXPORT_BEGIN@@", "@@VIDEO_EXPORT_END@@"

# Read-only ORM script executed by `manage.py shell` on the server (fed on stdin, never stored there).
REMOTE_EXPORT_SCRIPT = r'''
import json, datetime
from apps.learning.listening.models import CatalogTopic, CatalogVideo

TOPIC_FIELDS = ("id", "slug", "title", "position", "is_active")
VIDEO_FIELDS = ("id", "video_id", "topic_id", "title", "channel_handle", "channel_title", "duration", "level",
                "thumbnail", "view_count", "position", "is_active", "subs_status", "difficulty", "cefr", "track",
                "level_source", "embed_status", "embed_reason")

def rows(qs, fields):
    return [dict(zip(fields, r)) for r in qs.values_list(*fields)]

data = {
    "exportedAt": datetime.date.today().isoformat(),
    "counts": {
        "topicsTotal": CatalogTopic.objects.count(),
        "topicsDeleted": CatalogTopic.objects.filter(is_deleted=True).count(),
        "videosTotal": CatalogVideo.objects.count(),
        "videosDeleted": CatalogVideo.objects.filter(is_deleted=True).count(),
    },
    "topics": rows(CatalogTopic.objects.filter(is_deleted=False).order_by("position", "id"), TOPIC_FIELDS),
    "videos": rows(CatalogVideo.objects.filter(is_deleted=False).order_by("id"), VIDEO_FIELDS),
}
print("@@VIDEO_EXPORT_BEGIN@@")
print(json.dumps(data, ensure_ascii=False, default=str))
print("@@VIDEO_EXPORT_END@@")
'''


# --------------------------------------------------------------------------------------------
# export
# --------------------------------------------------------------------------------------------

def _ssh(args: argparse.Namespace, command: str, stdin: bytes | None = None) -> bytes:
    result = subprocess.run(["ssh", "-o", "BatchMode=yes", args.host, command], input=stdin,
                            capture_output=True, check=False)
    if result.returncode != 0:
        sys.exit(f"[export] ssh failed ({result.returncode}): {command}\n"
                 + result.stderr.decode("utf-8", "replace")[-2000:])
    return result.stdout


def export_snapshot(args: argparse.Namespace) -> None:
    target = SOURCE_ROOT / f"studychill_videos_{dt.date.today():%Y%m%d}"
    target.mkdir(parents=True, exist_ok=True)

    print(f"[export] {args.host}: catalog topics + videos (read-only ORM)")
    stdout = _ssh(args, f"docker exec -i {args.container} python manage.py shell",
                  REMOTE_EXPORT_SCRIPT.encode("utf-8")).decode("utf-8")
    if BEGIN not in stdout or END not in stdout:
        sys.exit("[export] remote script produced no data")
    payload = stdout.split(BEGIN, 1)[1].split(END, 1)[0].strip()
    catalog = json.loads(payload)  # fail fast on a truncated stream
    with gzip.open(target / CATALOG_SNAPSHOT, "wt", encoding="utf-8") as output:
        output.write(payload)
    print(f"         -> {CATALOG_SNAPSHOT}: {len(catalog['topics'])} topics, {len(catalog['videos'])} videos")

    print(f"[export] {args.host}: subtitle cache (langs + English + vi, tar.gz over stdout)")
    archive = _ssh(args, f"docker exec {args.container} sh -c \"{REMOTE_SUBS_FIND}\"")
    with tarfile.open(fileobj=io.BytesIO(archive), mode="r:gz") as tar:  # validate before keeping it
        members = [m for m in tar.getmembers() if m.isfile()]
    (target / SUBS_SNAPSHOT).write_bytes(archive)
    print(f"         -> {SUBS_SNAPSHOT}: {len(members)} files, {len(archive):,} bytes")

    for remote, name in ((REMOTE_VOCAB, VOCAB_SNAPSHOT), (REMOTE_BASIC, BASIC_SNAPSHOT)):
        print(f"[export] {args.host}: {remote}")
        data = _ssh(args, f"gzip -c {remote}")
        (target / name).write_bytes(data)
        print(f"         -> {name} ({len(data):,} bytes)")

    manifest = {"exportedAt": catalog["exportedAt"], "host": args.host, "counts": catalog["counts"],
                "subtitleFiles": len(members)}
    (target / MANIFEST).write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"\nSnapshot ready. Next: python scripts/video_data_pipeline.py build --source "
          f"{target.relative_to(BASE_DIR).as_posix()}")


# --------------------------------------------------------------------------------------------
# helpers ported from Studychill backend/apps/learning/listening/services/subtitles.py
# --------------------------------------------------------------------------------------------

VIDEO_ID = re.compile(r"^[A-Za-z0-9_-]{1,32}$")
LANG_CODE = re.compile(r"^[A-Za-z0-9-]{1,12}$")


def language_codes(items: dict) -> list[str]:
    out: list[str] = []
    for group in ("manual", "auto"):
        for x in (items or {}).get(group) or []:
            code = x.get("code") if isinstance(x, dict) else None
            if code and code not in out:
                out.append(code)
    return out


def pick_original_language(language: str, items: dict) -> str:
    """Port of subtitles.pick_original_language: map yt-dlp's language to a code that really exists."""
    codes = language_codes(items)
    if not codes:
        return ""
    lower = {c.lower(): c for c in codes}
    lang = (language or "").strip().lower()
    base = lang.split("-")[0] if lang else ""

    def first_match(predicate):
        return next((c for c in codes if predicate(c.lower())), "")

    if lang:
        for candidate in (lang + "-orig", lang, base + "-orig", base):
            if candidate in lower:
                return lower[candidate]
        hit = first_match(lambda c: c.startswith(base + "-") and c.endswith("-orig")) or first_match(
            lambda c: c.startswith(base + "-"))
        if hit:
            return hit
    manual = [x["code"] for x in (items or {}).get("manual") or [] if isinstance(x, dict) and x.get("code")]
    return (next((c for c in manual if c.lower().split("-")[0] == "en"), "") or lower.get("en-orig", "")
            or (manual[0] if manual else "") or first_match(lambda c: c.endswith("-orig")) or lower.get("en", "")
            or codes[0])


def is_english(code: str) -> bool:
    return code.lower().split("-")[0] == "en"


def keep_auto(code: str) -> bool:
    lowered = code.lower()
    return lowered.startswith("en") or lowered.startswith("vi") or lowered.endswith("-orig")


# --------------------------------------------------------------------------------------------
# build
# --------------------------------------------------------------------------------------------

def newest_snapshot() -> Path:
    candidates = sorted(SOURCE_ROOT.glob("studychill_videos_*"))
    if not candidates:
        sys.exit("No snapshot found. Run `python scripts/video_data_pipeline.py export` first.")
    return candidates[-1]


def compact(value) -> bytes:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def gzip_bytes(data: bytes) -> bytes:
    buffer = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=buffer, compresslevel=9, mtime=0) as output:
        output.write(data)
    return buffer.getvalue()


def build_catalog(snapshot: dict, problems: list[str]) -> tuple[dict, collections.Counter]:
    topics = sorted(snapshot["topics"], key=lambda t: (t["position"], t["id"]))
    slug_by_id = {t["id"]: t["slug"] for t in topics}
    position_by_id = {t["id"]: t["position"] for t in topics}
    stats: collections.Counter = collections.Counter()
    videos = []
    seen: set[str] = set()
    for v in snapshot["videos"]:
        vid = v["video_id"]
        if not VIDEO_ID.match(vid or "") or vid in seen:
            problems.append(f"catalog: invalid or duplicate video id {vid!r}")
            continue
        if v["topic_id"] not in slug_by_id:
            problems.append(f"catalog: {vid} points to an unknown/deleted topic {v['topic_id']}")
            continue
        seen.add(vid)
        videos.append({
            "videoId": vid,
            "topic": slug_by_id[v["topic_id"]],
            "title": v["title"] or vid,
            "channelHandle": v["channel_handle"] or "",
            "channelTitle": v["channel_title"] or "",
            "duration": int(v["duration"] or 0),
            "level": v["level"] or "",
            "thumbnail": v["thumbnail"] or f"https://i.ytimg.com/vi/{vid}/hqdefault.jpg",
            "viewCount": int(v["view_count"] or 0),
            "position": int(v["position"] or 0),
            "isActive": bool(v["is_active"]),
            "subtitleStatus": SUBTITLE_STATUS.get(v["subs_status"] or "unknown", "unknown"),
            "embedStatus": EMBED_STATUS.get(v["embed_status"] or "unknown", "unknown"),
            "embedReason": v["embed_reason"] or "",
            "difficulty": int(v["difficulty"] or 0),
            "cefr": v["cefr"] or "",
            "track": v["track"] or "",
            "levelSource": LEVEL_SOURCE.get(v["level_source"] or "est", "estimate"),
            "_sort": (position_by_id[v["topic_id"]], v["position"] or 0, vid),
        })
        stats[f"track:{v['track'] or '-'}"] += 1
        stats[f"subs:{v['subs_status']}"] += 1
        stats[f"embed:{v['embed_status']}"] += 1
        stats[f"source:{v['level_source']}"] += 1
        stats["active" if v["is_active"] else "inactive"] += 1
    videos.sort(key=lambda v: v.pop("_sort"))
    catalog = {
        "schemaVersion": SCHEMA_VERSION,
        "topics": [{"slug": t["slug"], "title": t["title"], "position": t["position"], "isActive": bool(t["is_active"])}
                   for t in topics],
        "videos": videos,
    }
    return catalog, stats


def build_word_levels(vocab: dict, basic_text: str) -> dict:
    levels = {}
    for word in sorted(vocab):
        entry = vocab[word]
        if not isinstance(entry, dict) or not entry:
            continue  # Studychill's vocab_levels treats a falsy entry as "not in the store"
        levels[word] = str(entry.get("cefr") or "").strip().upper()
    basic = sorted({w for line in basic_text.splitlines() if not line.lstrip().startswith("#")
                    for w in line.lower().split()})
    return {"schemaVersion": SCHEMA_VERSION, "levels": levels, "basic": basic}


def check_cues(video_id: str, lang: str, cues, problems: list[str], counters: collections.Counter) -> bool:
    if not isinstance(cues, list):
        problems.append(f"{video_id}.{lang}: cues is not a list")
        return False
    for i, cue in enumerate(cues):
        ok = (isinstance(cue, list) and len(cue) in (3, 4) and all(isinstance(x, (int, float)) for x in cue[:2])
              and isinstance(cue[2], str))
        if not ok:
            problems.append(f"{video_id}.{lang}: cue {i} has an invalid shape")
            return False
        if len(cue) == 4:
            offsets = cue[3]
            if not isinstance(offsets, list) or not all(isinstance(x, (int, float)) for x in offsets):
                problems.append(f"{video_id}.{lang}: cue {i} has invalid offsets")
                return False
            counters["cuesWithOffsets"] += 1
            if len(offsets) != len(cue[2].split(" ")):
                counters["offsetMismatches"] += 1
        counters["cues"] += 1
    return True


def read_subtitles(path: Path, problems: list[str], counters: collections.Counter):
    """{videoId: {"langs": dict|None, "tracks": {lang: cues}}} from the snapshot tar."""
    videos: dict[str, dict] = collections.defaultdict(lambda: {"langs": None, "tracks": {}})
    with tarfile.open(path, mode="r:gz") as tar:
        for member in tar:
            if not member.isfile():
                continue
            name = Path(member.name).name
            if not name.endswith(".v2.json"):
                continue
            stem = name[: -len(".v2.json")]
            if "." not in stem:
                problems.append(f"subs: unexpected file name {name}")
                continue
            video_id, lang = stem.rsplit(".", 1)
            if not VIDEO_ID.match(video_id) or not LANG_CODE.match(lang):
                problems.append(f"subs: unexpected file name {name}")
                continue
            try:
                data = json.loads(tar.extractfile(member).read().decode("utf-8"))
            except (ValueError, UnicodeDecodeError) as exc:
                problems.append(f"subs: {name} does not parse ({exc})")
                continue
            if lang == "langs":
                if not isinstance(data, dict):
                    problems.append(f"subs: {name} is not an object")
                    continue
                videos[video_id]["langs"] = data
            elif is_english(lang) or lang == "vi":
                if not check_cues(video_id, lang, data, problems, counters):
                    continue
                if not data:
                    counters["emptyTracksSkipped"] += 1
                    continue
                videos[video_id]["tracks"][lang] = data
            else:
                counters["otherLangSkipped"] += 1
    return dict(videos)


def language_entry(video_id: str, items: dict) -> dict:
    def codes(group: list, keep) -> list[dict]:
        out = []
        for x in group or []:
            if isinstance(x, dict) and x.get("code") and keep(x["code"]):
                out.append({"code": x["code"], "name": x.get("name") or x["code"]})
        return sorted(out, key=lambda x: x["code"])

    return {
        "videoId": video_id,
        "manual": codes(items.get("manual"), lambda _: True),
        "auto": codes(items.get("auto"), keep_auto),
        "title": items.get("title") or "",
        "duration": int(items.get("duration") or 0),
        "language": items.get("language") or "",
    }


def preferred_english(entry: dict | None, tracks: dict) -> str:
    english = sorted(lang for lang in tracks if is_english(lang))
    if not english:
        return ""
    if entry:
        picked = pick_original_language(entry["language"], entry)
        if picked in tracks:
            return picked
    for candidate in ("en-orig", "en", "en-US", "en-GB"):
        if candidate in tracks:
            return candidate
    return english[0]


def pack_chunks(groups: list[tuple[str, dict | None, dict]]) -> list[bytes]:
    """Greedy packing of whole videos into gzip chunks of at most CHUNK_MAX_BYTES (deterministic)."""
    def payload(items):
        return compact({
            "schemaVersion": SCHEMA_VERSION,
            "languages": [entry for _, entry, _ in items if entry],
            "tracks": [{"videoId": vid, "lang": lang, "cues": tracks[lang]}
                       for vid, _, tracks in items for lang in sorted(tracks)],
        })

    sizes = [len(gzip_bytes(payload([g]))) for g in groups]
    chunks: list[bytes] = []
    start = 0
    while start < len(groups):
        # Separately compressed videos over-estimate the combined size: the upper bound aims above the cap, then
        # a binary search finds the largest prefix whose real compressed size fits (size grows with the prefix).
        hi, estimate = start, 0
        while hi < len(groups) and (hi == start or estimate + sizes[hi] <= CHUNK_MAX_BYTES * 1.4):
            estimate += sizes[hi]
            hi += 1
        lo, best = start + 1, None
        while lo <= hi:
            mid = (lo + hi) // 2
            data = gzip_bytes(payload(groups[start:mid]))
            if len(data) <= CHUNK_MAX_BYTES or mid == start + 1:
                best = (mid, data)
                lo = mid + 1
            else:
                hi = mid - 1
        end, data = best
        if len(data) > CHUNK_MAX_BYTES:
            print(f"  ! video {groups[start][0]} alone is {len(data):,} bytes compressed (> cap)")
        chunks.append(data)
        start = end
    return chunks


def build(args: argparse.Namespace) -> None:
    source = (BASE_DIR / args.source) if args.source else newest_snapshot()
    if not source.is_dir():
        sys.exit(f"Snapshot folder not found: {source}")
    print(f"[build] snapshot {source.relative_to(BASE_DIR).as_posix()}")
    # README.md is written last and signals a complete build to anyone waiting on data/videos/.
    (OUTPUT_DIR / "README.md").unlink(missing_ok=True)
    problems: list[str] = []
    counters: collections.Counter = collections.Counter()

    with gzip.open(source / CATALOG_SNAPSHOT, "rt", encoding="utf-8") as handle:
        catalog_snapshot = json.load(handle)
    catalog, catalog_stats = build_catalog(catalog_snapshot, problems)

    with gzip.open(source / VOCAB_SNAPSHOT, "rt", encoding="utf-8") as handle:
        vocab = json.load(handle)
    with gzip.open(source / BASIC_SNAPSHOT, "rt", encoding="utf-8") as handle:
        basic_text = handle.read()
    word_levels = build_word_levels(vocab, basic_text)

    subs = read_subtitles(source / SUBS_SNAPSHOT, problems, counters)
    catalog_ids = [v["videoId"] for v in catalog["videos"]]
    catalog_set = set(catalog_ids)

    # Catalog videos first (catalog order), then the other cached videos (pasted links) by id.
    ordered = [vid for vid in catalog_ids if vid in subs] + sorted(vid for vid in subs if vid not in catalog_set)
    groups: list[tuple[str, dict | None, dict]] = []
    for vid in ordered:
        item = subs[vid]
        entry = language_entry(vid, item["langs"]) if item["langs"] else None
        tracks = dict(sorted(item["tracks"].items()))
        if entry is None and not tracks:
            continue
        groups.append((vid, entry, tracks))

    single_english = False
    raw_total = sum(len(gzip_bytes(compact(t))) for _, _, tracks in groups for t in tracks.values())
    if raw_total > TOTAL_BUDGET_BYTES:
        single_english = True
        trimmed = []
        for vid, entry, tracks in groups:
            keep = preferred_english(entry, tracks)
            dropped = [lang for lang in tracks if is_english(lang) and lang != keep]
            counters["englishVariantsDropped"] += len(dropped)
            trimmed.append((vid, entry, {k: v for k, v in tracks.items() if k not in dropped}))
        groups = trimmed

    chunks = pack_chunks(groups)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for old in OUTPUT_DIR.glob("subtitles-*.json.gz"):
        old.unlink()
    (OUTPUT_DIR / "catalog.json").write_bytes(compact(catalog) + b"\n")
    (OUTPUT_DIR / "word-levels.json").unlink(missing_ok=True)
    WORD_LEVELS_FILE.parent.mkdir(parents=True, exist_ok=True)
    WORD_LEVELS_FILE.write_bytes(compact(word_levels) + b"\n")
    chunk_files = []
    for n, data in enumerate(chunks, start=1):
        name = f"subtitles-{n:02d}.json.gz"
        (OUTPUT_DIR / name).write_bytes(data)
        chunk_files.append((name, len(data)))

    # ---- report ----------------------------------------------------------------------------
    track_langs = collections.Counter(lang for _, _, tracks in groups for lang in tracks)
    with_english = {vid for vid, _, tracks in groups if any(is_english(lang) for lang in tracks)}
    with_list = {vid for vid, entry, _ in groups if entry}
    catalog_by_status = collections.Counter()
    for v in catalog["videos"]:
        catalog_by_status[(v["subtitleStatus"], v["videoId"] in with_english)] += 1
    catalog_missing = [v for v in catalog["videos"] if v["videoId"] not in with_english]
    visible_missing = [v for v in catalog_missing if v["isActive"] and v["subtitleStatus"] != "missing"
                       and v["embedStatus"] != "blocked"]
    tracks_only = [vid for vid, entry, tracks in groups if entry is None]
    list_only = [vid for vid, entry, tracks in groups if entry and not tracks]
    files = [("catalog.json", (OUTPUT_DIR / "catalog.json").stat().st_size)] + chunk_files
    total = sum(size for _, size in files)

    report = {
        "topics": len(catalog["topics"]),
        "videos": len(catalog["videos"]),
        "catalogStats": dict(sorted(catalog_stats.items())),
        "videosInSubtitleFiles": len(groups),
        "catalogVideosInSubtitleFiles": sum(1 for vid, _, _ in groups if vid in catalog_set),
        "otherVideosInSubtitleFiles": sum(1 for vid, _, _ in groups if vid not in catalog_set),
        "languageLists": len(with_list),
        "tracksByLang": dict(sorted(track_langs.items())),
        "tracks": sum(track_langs.values()),
        "viTracks": track_langs.get("vi", 0),
        "videosWithEnglish": len(with_english),
        "catalogWithoutEnglishTrack": len(catalog_missing),
        "visibleCatalogWithoutEnglishTrack": len(visible_missing),
        "catalogBySubsStatusAndCache": {f"{k[0]}/{'cached' if k[1] else 'not-cached'}": n
                                        for k, n in sorted(catalog_by_status.items())},
        "videosWithTracksButNoLanguageList": len(tracks_only),
        "videosWithLanguageListOnly": len(list_only),
        "singleEnglishTrack": single_english,
        "wordLevels": len(word_levels["levels"]),
        "wordLevelsByCefr": dict(sorted(collections.Counter(v or "(none)" for v in word_levels["levels"].values()).items())),
        "basicWords": len(word_levels["basic"]),
        "counters": dict(sorted(counters.items())),
        "files": files,
        "totalBytes": total,
        "problems": len(problems),
    }
    print(json.dumps(report, indent=2, ensure_ascii=False))
    for p in problems[:40]:
        print("  !", p)

    write_readme(report, source, files, total)
    print(f"\n[build] data/videos: {len(files)} files, {total:,} bytes")


def _mb(size: int) -> str:
    return f"{size / 1_048_576:.2f} MB" if size >= 100_000 else f"{size / 1024:.1f} KB"


def write_readme(report: dict, source: Path, files: list[tuple[str, int]], total: int) -> None:
    stats = report["catalogStats"]
    file_rows = "\n".join(f"| `{name}` | {_mb(size)} ({size:,} bytes) |" for name, size in files)
    lang_rows = ", ".join(f"`{k}` {v:,}" for k, v in report["tracksByLang"].items())
    cefr_rows = ", ".join(f"{k} {v:,}" for k, v in report["wordLevelsByCefr"].items())
    readme = f"""# Video song ngữ — import data

Produced by `python scripts/video_data_pipeline.py export` (read-only snapshot of the Studychill VPS) and
`python scripts/video_data_pipeline.py build` from `{source.relative_to(BASE_DIR).as_posix()}`. Do not edit by hand:
re-run the pipeline. The build is deterministic (sorted rows, compact JSON, gzip mtime 0).

## Files

| File | Size |
|---|---|
{file_rows}
| **Total** | **{_mb(total)} ({total:,} bytes)** |

Every file is below 1.9 MB, so each one passes the gateway's 2 MB request-body cap when uploaded alone.

## `catalog.json`

`{{ "schemaVersion": 1, "topics": [...], "videos": [...] }}` — Studychill `ls_catalog_topic` / `ls_catalog_video`
(soft-deleted rows excluded; inactive rows kept with `isActive: false`).

- topic: `slug` (natural key), `title`, `position`, `isActive`
- video: `videoId` (natural key), `topic` (slug), `title`, `channelHandle`, `channelTitle`, `duration` (s), `level`
  (channel CEFR), `thumbnail`, `viewCount`, `position` (order inside the topic), `isActive`, `subtitleStatus`
  (`unknown` | `available` | `missing`), `embedStatus` (`unknown` | `playable` | `blocked`), `embedReason`,
  `difficulty` (0–100), `cefr`, `track` (`easy` | `medium` | `hard` | `""`), `levelSource` (`estimate` = from the
  channel level, `subtitles` = scored from subtitles). Studychill's `ok`/`none`/`est`/`subs` are renamed here.

Counts: {report['topics']} topics, {report['videos']:,} videos — active {stats.get('active', 0):,}, inactive
{stats.get('inactive', 0):,}; track easy {stats.get('track:easy', 0):,} / medium {stats.get('track:medium', 0):,} /
hard {stats.get('track:hard', 0):,} / none {stats.get('track:-', 0):,}; subs ok {stats.get('subs:ok', 0):,} / unknown
{stats.get('subs:unknown', 0):,} / none {stats.get('subs:none', 0):,}; embed ok {stats.get('embed:ok', 0):,} /
unknown {stats.get('embed:unknown', 0):,} / blocked {stats.get('embed:blocked', 0):,}; level source subs
{stats.get('source:subs', 0):,} / est {stats.get('source:est', 0):,}.

## Word levels (embedded, not imported)

Written to `src/Services/Content/ToeicSpace.Content.Infrastructure/Services/Video/Resources/word-levels.json` and
embedded in the Content service: `{{ "schemaVersion": 1, "levels": {{ "<word>": "<CEFR or empty>" }}, "basic": ["<word>", ...] }}`

- `levels`: every entry of Studychill `vocabulary/data/toeic_vocab.json` (the TOEIC word store behind
  `dictionary.vocab_levels` and `video_level.py`), CEFR uppercased; empty = in the store without a CEFR (shown as
  "TOEIC"). A1/A2 entries are kept: they block the base-form search exactly like in Studychill.
  {report['wordLevels']:,} words ({cefr_rows}).
- `basic`: Studychill `listening/data/basic_words.txt` (A1–A2 words of 5+ letters used by subtitle difficulty
  scoring), lowercased, de-duplicated, sorted: {report['basicWords']:,} words.

## `subtitles-NN.json.gz`

`{{ "schemaVersion": 1, "languages": [...], "tracks": [...] }}` (gzip). A video's language list and its tracks are in
the same chunk; catalog videos come first (catalog order), then other cached videos (links learners pasted) by id.

- language list (`*.langs.v2.json` of the Studychill cache): `videoId`, `manual` `[{{code, name}}]` (complete),
  `auto` (trimmed to codes starting with `en`/`vi` or ending with `-orig` — enough for `pick_original_language` and
  `has_english`; the full YouTube list has ~156 machine translations), `title`, `duration`, `language`.
- track: `videoId`, `lang`, `cues` = `[[start s, duration s, text, offsets?], ...]` exactly as cached by Studychill
  (v2: `offsets` = per-word start offsets in seconds from the cue start, matching `text.split(" ")`).

Counts: {report['videosInSubtitleFiles']:,} videos ({report['catalogVideosInSubtitleFiles']:,} catalog,
{report['otherVideosInSubtitleFiles']:,} other), {report['languageLists']:,} language lists, {report['tracks']:,}
tracks ({lang_rows}). {"Only one English track per video is kept (the one the reader picks) because the full set exceeded 25 MB." if report['singleEnglishTrack'] else "Every cached English variant is kept."}
Catalog videos without a cached English track: {report['catalogWithoutEnglishTrack']:,}
({report['visibleCatalogWithoutEnglishTrack']:,} of them visible in the catalog) — their subtitles are fetched from
YouTube on first open.

## Import

Apply the migration first, then upload one file per request (any multipart field name; `.json` and `.json.gz`
are both accepted; re-importing is idempotent — natural keys `videoId` for catalog rows and subtitle documents, (`videoId`, `lang`) for tracks):

```bash
TOKEN=<admin access token>
for f in data/videos/catalog.json data/videos/subtitles-*.json.gz; do
  curl -sS -X POST http://localhost:5050/content/api/v1/videos/admin/import \\
    -H "Authorization: Bearer $TOKEN" -F "files=@$f"
done
```

Directly against the Content API (no gateway) use `http://localhost:<content-port>/api/v1/videos/admin/import`.
"""
    (OUTPUT_DIR / "README.md").write_text(readme, encoding="utf-8")


# --------------------------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    export = sub.add_parser("export", help="read-only snapshot from the Studychill VPS")
    export.add_argument("--host", default="studychill")
    export.add_argument("--container", default="studychill-backend")
    export.set_defaults(func=export_snapshot)
    build_cmd = sub.add_parser("build", help="snapshot -> data/videos")
    build_cmd.add_argument("--source", help="snapshot folder (default: newest data/source/studychill_videos_*)")
    build_cmd.set_defaults(func=build)
    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
