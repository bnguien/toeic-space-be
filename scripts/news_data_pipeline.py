#!/usr/bin/env python3
"""
TOEIC Space - Studychill bilingual news ("Báo song ngữ") data pipeline
======================================================================

Pulls the published bilingual articles of Studychill (VOA Learning English stories with a Vietnamese
translation per sentence, prepared vocabulary and an openly licensed cover photo), normalises them and writes
the committed import files in data/news/.

Commands
--------
  export  Read-only snapshot over SSH (host alias "studychill"). Article rows are read with read-only ORM
          queries through `manage.py shell` fed on stdin (nothing is written on the server, no database
          credentials needed locally); the cover images are streamed with `tar -c` to stdout. Only published,
          non-deleted articles leave the server; learners' reading progress is never exported.
  build   Normalise the snapshot into data/news/articles.json (gzip-compressed as .json.gz when bigger than
          5 MB), resize every cover to at most 960 px wide WebP (quality 75) into data/news/covers/<hash>.webp
          (content-addressed, so the API can cache them forever) and write data/news/import_report.md.
          Deterministic: the same snapshot gives the same files.
  import  Upload data/news to the Content API admin import endpoint (Admin token) in batches that fit the
          gateway's 2 MB request cap: covers first, then the articles. Idempotent.

Usage
-----
  python scripts/news_data_pipeline.py export
  python scripts/news_data_pipeline.py build                        # newest data/source/studychill_news_*
  python scripts/news_data_pipeline.py build --source data/source/studychill_news_20261107
  python scripts/news_data_pipeline.py import --url http://localhost:5050/content/api/v1/news/admin/import \
      --token "$TOKEN"
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import gzip
import hashlib
import io
import json
import re
import subprocess
import sys
import tarfile
import urllib.error
import urllib.request
import uuid
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

BASE_DIR = Path(__file__).resolve().parent.parent
SOURCE_ROOT = BASE_DIR / "data" / "source"
OUTPUT_DIR = BASE_DIR / "data" / "news"
COVERS_DIR = OUTPUT_DIR / "covers"
REPORT_FILE = OUTPUT_DIR / "import_report.md"
SNAPSHOT_FILE = "studychill_news.json.gz"
COVERS_TAR = "studychill_news_media.tar"
REMOTE_MEDIA_ROOT = "/app/media"

SCHEMA_VERSION = 1
GZIP_THRESHOLD = 5 * 1024 * 1024
COVER_MAX_WIDTH = 960
COVER_QUALITY = 75
# Through the gateway one request may carry at most 2 MB (BuildingBlocks AddKestrelHardening).
BATCH_BYTES = 1_800_000
BEGIN, END = "@@NEWS_EXPORT_BEGIN@@", "@@NEWS_EXPORT_END@@"

# Read-only ORM script executed by `manage.py shell` on the server (fed on stdin, never stored there).
REMOTE_EXPORT_SCRIPT = r'''
import json, datetime
from apps.learning.reading.models.news import NewsArticle

FIELDS = ("id", "slug", "source_name", "source_url", "source_id", "source_series", "source_level", "author",
          "published_at", "license", "attribution", "title_en", "title_vi", "summary_vi", "content", "vocab",
          "word_count", "minutes", "stage", "topic", "cover_url", "cover_alt", "cover_credit", "cover_license",
          "cover_source_url", "audio_url", "status", "featured", "created_at", "updated_at")

qs = NewsArticle.active.filter(status="published").order_by("id")
data = {
    "exportedAt": datetime.date.today().isoformat(),
    "counts": {
        "articlesTotal": NewsArticle.objects.count(),
        "articlesPublished": qs.count(),
    },
    "articles": [dict(zip(FIELDS, r)) for r in qs.values_list(*FIELDS)],
}
print("@@NEWS_EXPORT_BEGIN@@")
print(json.dumps(data, ensure_ascii=False, default=str))
print("@@NEWS_EXPORT_END@@")
'''

STAGES = ("c1", "c2", "c3", "c4", "c5")
# Studychill keys are Vietnamese; TOEIC Space uses English identifiers (topic slugs, vocabulary levels).
TOPICS = {"kinh-doanh": "business", "cong-nghe": "technology", "doi-song": "lifestyle", "du-lich": "travel",
          "moi-truong": "environment", "giao-duc": "education"}
LICENSES = ("public_domain", "cc0", "cc_by", "cc_by_sa", "unsplash", "pexels", "original")
VOCAB_LEVELS = {"de": "easy", "vua": "medium", "kho": "hard"}
COVER_PATH = re.compile(r"^/media/(news/[0-9a-f]{2}/[0-9a-f]{64}\.webp)$")
# Studychill brand names are renamed (the easier "-de" versions are written by Studychill, e.g. author
# "StudyChill (phỏng theo VOA Learning English)").
BRAND_RENAMES = ((re.compile(r"study\s*chill", re.IGNORECASE), "TOEIC Space"),
                 (re.compile(r"chillo", re.IGNORECASE), "Oysteic"))


# --------------------------------------------------------------------------------------------
# export
# --------------------------------------------------------------------------------------------

def ssh(host: str, command: str, stdin: bytes | None = None) -> bytes:
    return subprocess.run(["ssh", "-o", "BatchMode=yes", host, command], input=stdin, capture_output=True,
                          check=True).stdout


def export_snapshot(args: argparse.Namespace) -> None:
    target = SOURCE_ROOT / f"studychill_news_{dt.date.today():%Y%m%d}"
    target.mkdir(parents=True, exist_ok=True)

    print(f"[export] {args.host}: published news articles (read-only ORM)")
    result = subprocess.run(
        ["ssh", "-o", "BatchMode=yes", args.host, f"docker exec -i {args.container} python manage.py shell"],
        input=REMOTE_EXPORT_SCRIPT.encode("utf-8"), capture_output=True, check=True)
    stdout = result.stdout.decode("utf-8")
    if BEGIN not in stdout or END not in stdout:
        sys.exit("[export] remote script produced no data:\n" + result.stderr.decode("utf-8", "replace")[-2000:])
    payload = stdout.split(BEGIN, 1)[1].split(END, 1)[0].strip()
    data = json.loads(payload)  # fail fast on a truncated stream
    destination = target / SNAPSHOT_FILE
    with gzip.open(destination, "wt", encoding="utf-8") as output:
        output.write(payload)
    print(f"         -> {destination} ({destination.stat().st_size:,} bytes, {len(data['articles'])} articles)")

    print(f"[export] {args.host}: cover images ({REMOTE_MEDIA_ROOT}/news, tar to stdout)")
    archive = ssh(args.host, f"docker exec {args.container} tar -C {REMOTE_MEDIA_ROOT} -cf - news")
    (target / COVERS_TAR).write_bytes(archive)
    print(f"         -> {target / COVERS_TAR} ({len(archive):,} bytes)")

    print(f"\nSnapshot ready. Next: python scripts/news_data_pipeline.py build --source {target.relative_to(BASE_DIR)}")


# --------------------------------------------------------------------------------------------
# build
# --------------------------------------------------------------------------------------------

def latest_source() -> Path:
    candidates = sorted(p for p in SOURCE_ROOT.glob("studychill_news_*") if (p / SNAPSHOT_FILE).exists())
    if not candidates:
        sys.exit("No snapshot found. Run: python scripts/news_data_pipeline.py export")
    return candidates[-1]


def clip(value, limit: int) -> str:
    return " ".join(str(value or "").split())[:limit]


def iso_utc(value) -> str | None:
    if not value:
        return None
    parsed = dt.datetime.fromisoformat(str(value).replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=dt.timezone.utc)
    return parsed.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def normalise_paragraphs(raw, report: "Report", slug: str) -> list:
    """Same rules as the Studychill importer: empty sentences dropped, ids p<i>s<k> renumbered."""
    paragraphs = []
    for i, paragraph in enumerate(raw or [], 1):
        sentences = []
        for k, sentence in enumerate((paragraph or {}).get("cau") or [], 1):
            en = clip(sentence.get("en"), 1200)
            if not en:
                continue
            vi = clip(sentence.get("vi"), 1200)
            if not vi:
                report.flag("sentence without translation", slug, en[:80])
            sentences.append({"id": f"p{i}s{k}", "text": en, "translation": vi})
        if sentences:
            paragraphs.append({"id": f"p{i}", "sentences": sentences})
    return paragraphs


def normalise_vocabulary(raw) -> list:
    result, seen = [], set()
    for item in raw if isinstance(raw, list) else []:
        word = clip((item or {}).get("word"), 60)
        if not word or word.lower() in seen:
            continue
        seen.add(word.lower())
        result.append({
            "word": word,
            "ipa": clip(item.get("ipa"), 80),
            "partOfSpeech": clip(item.get("pos"), 20),
            "meaning": clip(item.get("vi"), 120),
            "level": VOCAB_LEVELS.get(item.get("cap"), "medium"),
        })
    return result[:40]


def rebrand(value):
    """Renames Studychill brand mentions in every string of an article -> (value, changed?)."""
    if isinstance(value, str):
        renamed = value
        for pattern, replacement in BRAND_RENAMES:
            renamed = pattern.sub(replacement, renamed)
        return renamed, renamed != value
    if isinstance(value, list):
        pairs = [rebrand(v) for v in value]
        return [v for v, _ in pairs], any(c for _, c in pairs)
    if isinstance(value, dict):
        pairs = {k: rebrand(v) for k, v in value.items()}
        return {k: v for k, (v, _) in pairs.items()}, any(c for _, c in pairs.values())
    return value, False


def resize_cover(data: bytes) -> tuple[bytes, int, int]:
    from PIL import Image

    image = Image.open(io.BytesIO(data))
    image = image.convert("RGB")
    if image.width > COVER_MAX_WIDTH:
        image = image.resize((COVER_MAX_WIDTH, round(image.height * COVER_MAX_WIDTH / image.width)), Image.LANCZOS)
    output = io.BytesIO()
    image.save(output, "WEBP", quality=COVER_QUALITY, method=6)
    return output.getvalue(), image.width, image.height


def build(args: argparse.Namespace) -> None:
    source = Path(args.source).resolve() if args.source else latest_source()
    with gzip.open(source / SNAPSHOT_FILE, "rt", encoding="utf-8") as handle:
        data = json.load(handle)
    report = Report()
    report.source_counts = data.get("counts", {})

    members: dict[str, bytes] = {}
    tar_path = source / COVERS_TAR
    if tar_path.exists():
        with tarfile.open(tar_path) as archive:
            for member in archive.getmembers():
                if member.isfile() and member.name.endswith(".webp"):
                    members[member.name.lstrip("./")] = archive.extractfile(member).read()
    report.original_cover_bytes = sum(len(v) for v in members.values())

    COVERS_DIR.mkdir(parents=True, exist_ok=True)
    articles, covers_written = [], set()
    for row in sorted(data["articles"], key=lambda r: r["slug"]):
        slug = row["slug"]
        if row.get("stage") not in STAGES or row.get("topic") not in TOPICS:
            report.flag("invalid stage/topic", slug, f"{row.get('stage')} / {row.get('topic')}")
            continue
        if row.get("license") not in LICENSES:
            report.flag("invalid licence", slug, str(row.get("license")))
            continue
        paragraphs = normalise_paragraphs(row.get("content"), report, slug)
        if not paragraphs:
            report.flag("no sentences", slug, row.get("title_en", ""))
            continue

        cover = None
        match = COVER_PATH.match(row.get("cover_url") or "")
        if match and row.get("cover_license") in LICENSES and match.group(1) in members:
            webp, width, height = resize_cover(members[match.group(1)])
            name = f"{hashlib.sha256(webp).hexdigest()[:32]}.webp"
            target = COVERS_DIR / name
            if not target.exists() or target.read_bytes() != webp:
                target.write_bytes(webp)
            covers_written.add(name)
            cover = {
                "file": name,
                "width": width,
                "height": height,
                "alt": clip(row.get("cover_alt"), 200),
                "credit": clip(row.get("cover_credit"), 300),
                "license": row.get("cover_license"),
                "sourceUrl": clip(row.get("cover_source_url"), 500),
            }
        else:
            report.flag("cover missing or unusable", slug, row.get("cover_url") or "(none)")

        article = {
            "slug": slug,
            "status": "published",
            "featured": bool(row.get("featured")),
            "source": {
                "name": clip(row.get("source_name"), 80),
                "url": clip(row.get("source_url"), 400),
                "id": clip(row.get("source_id"), 40),
                "series": clip(row.get("source_series"), 80),
                "level": clip(row.get("source_level"), 40),
                "author": clip(row.get("author"), 160),
                "publishedAt": iso_utc(row.get("published_at")),
                "license": row.get("license"),
                "attribution": clip(row.get("attribution"), 300),
            },
            "titleEn": clip(row.get("title_en"), 240),
            "titleVi": clip(row.get("title_vi"), 240),
            "summaryVi": clip(row.get("summary_vi"), 400),
            "stage": row["stage"],
            "topic": TOPICS[row["topic"]],
            "paragraphs": paragraphs,
            "vocabulary": normalise_vocabulary(row.get("vocab")),
            "cover": cover,
            "audioUrl": clip(row.get("audio_url"), 500),
        }
        article, renamed = rebrand(article)
        if renamed:
            report.rebranded += 1
        articles.append(article)
        report.add(article, row)

    # Covers of articles that are gone are removed so the folder mirrors articles.json.
    for stale in COVERS_DIR.glob("*.webp"):
        if stale.name not in covers_written:
            stale.unlink()
            report.removed_covers += 1

    document = {
        "schemaVersion": SCHEMA_VERSION,
        "source": "studychill",
        "exportedAt": data.get("exportedAt"),
        "articles": articles,
    }
    text = (json.dumps(document, ensure_ascii=False, indent=1) + "\n").encode("utf-8")
    plain, packed = OUTPUT_DIR / "articles.json", OUTPUT_DIR / "articles.json.gz"
    if len(text) > GZIP_THRESHOLD:
        packed.write_bytes(gzip.compress(text, mtime=0))
        plain.unlink(missing_ok=True)
        report.articles_file = (packed, len(text))
    else:
        plain.write_bytes(text)
        packed.unlink(missing_ok=True)
        report.articles_file = (plain, len(text))
    report.cover_bytes = sum((COVERS_DIR / n).stat().st_size for n in covers_written)
    report.cover_count = len(covers_written)
    report.write(data, source)
    print(REPORT_FILE.read_text(encoding="utf-8"))


# --------------------------------------------------------------------------------------------
# import (upload to the admin endpoint)
# --------------------------------------------------------------------------------------------

def multipart(files: list[tuple[str, bytes, str]]) -> tuple[bytes, str]:
    boundary = f"----toeicspace{uuid.uuid4().hex}"
    body = io.BytesIO()
    for name, content, content_type in files:
        body.write(f"--{boundary}\r\n".encode())
        body.write(f'Content-Disposition: form-data; name="files"; filename="{name}"\r\n'.encode())
        body.write(f"Content-Type: {content_type}\r\n\r\n".encode())
        body.write(content)
        body.write(b"\r\n")
    body.write(f"--{boundary}--\r\n".encode())
    return body.getvalue(), f"multipart/form-data; boundary={boundary}"


def post_batch(args: argparse.Namespace, files: list[tuple[str, bytes, str]]) -> dict:
    body, content_type = multipart(files)
    request = urllib.request.Request(args.url, data=body, method="POST", headers={
        "Content-Type": content_type, "Authorization": f"Bearer {args.token}"})
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        sys.exit(f"[import] HTTP {error.code}: {error.read().decode('utf-8', 'replace')[:2000]}")


def batches(items: list[tuple[str, bytes, str]]):
    current, size = [], 0
    for item in items:
        if current and size + len(item[1]) > BATCH_BYTES:
            yield current
            current, size = [], 0
        current.append(item)
        size += len(item[1])
    if current:
        yield current


def load_articles_document() -> dict:
    packed, plain = OUTPUT_DIR / "articles.json.gz", OUTPUT_DIR / "articles.json"
    if packed.exists():
        return json.loads(gzip.decompress(packed.read_bytes()).decode("utf-8"))
    if plain.exists():
        return json.loads(plain.read_text(encoding="utf-8"))
    sys.exit("No data/news/articles.json(.gz). Run build first.")


def import_data(args: argparse.Namespace) -> None:
    document = load_articles_document()
    covers = sorted({a["cover"]["file"] for a in document["articles"] if a.get("cover")})
    cover_files = [(name, (COVERS_DIR / name).read_bytes(), "image/webp") for name in covers]
    totals = collections.Counter()
    for batch in batches(cover_files):
        result = post_batch(args, batch)
        totals["coversStored"] += result.get("coversStored", 0)
        totals["coversUnchanged"] += result.get("coversUnchanged", 0)
    print(f"[import] covers: stored {totals['coversStored']}, unchanged {totals['coversUnchanged']}")

    # Articles are re-packed into gzip chunks that each stay under the batch size.
    chunk: list = []
    chunks: list[list] = []
    for article in document["articles"]:
        candidate = chunk + [article]
        packed = gzip.compress(json.dumps({"schemaVersion": SCHEMA_VERSION, "articles": candidate},
                                          ensure_ascii=False).encode("utf-8"), mtime=0)
        if chunk and len(packed) > BATCH_BYTES:
            chunks.append(chunk)
            chunk = [article]
        else:
            chunk = candidate
    if chunk:
        chunks.append(chunk)
    for index, part in enumerate(chunks, 1):
        payload = gzip.compress(json.dumps({"schemaVersion": SCHEMA_VERSION, "articles": part},
                                           ensure_ascii=False).encode("utf-8"), mtime=0)
        result = post_batch(args, [(f"articles-{index:02d}.json.gz", payload, "application/gzip")])
        for key in ("articlesCreated", "articlesUpdated", "articlesUnchanged"):
            totals[key] += result.get(key, 0)
        for slug in result.get("missingCovers", []):
            print(f"[import] warning: cover of {slug} is not stored on the server")
    print(f"[import] articles: created {totals['articlesCreated']}, updated {totals['articlesUpdated']}, "
          f"unchanged {totals['articlesUnchanged']} ({len(chunks)} request(s))")


# --------------------------------------------------------------------------------------------
# report
# --------------------------------------------------------------------------------------------

class Report:
    def __init__(self) -> None:
        self.source_counts: dict = {}
        self.flags: list = []
        self.stages = collections.Counter()
        self.topics = collections.Counter()
        self.sentences = 0
        self.vocabulary = 0
        self.with_audio = 0
        self.untranslated_articles = 0
        self.minutes_mismatch = 0
        self.original_cover_bytes = 0
        self.cover_bytes = 0
        self.cover_count = 0
        self.removed_covers = 0
        self.rebranded = 0
        self.articles_file: tuple[Path, int] | None = None
        self.count = 0

    def flag(self, reason: str, where: str, value: str) -> None:
        self.flags.append((reason, where, value))

    def add(self, article: dict, row: dict) -> None:
        self.count += 1
        self.stages[article["stage"]] += 1
        self.topics[article["topic"]] += 1
        sentences = [s for p in article["paragraphs"] for s in p["sentences"]]
        self.sentences += len(sentences)
        self.vocabulary += len(article["vocabulary"])
        self.with_audio += bool(article["audioUrl"])
        if any(not s["translation"] for s in sentences):
            self.untranslated_articles += 1
        words = sum(len(s["text"].split()) for s in sentences)
        if max(1, round(words / 160)) != row.get("minutes"):
            self.minutes_mismatch += 1

    def write(self, data: dict, source: Path) -> None:
        path, raw_size = self.articles_file
        lines = [
            "# News import report",
            "",
            f"Snapshot: `{source.relative_to(BASE_DIR).as_posix()}` (exported {data.get('exportedAt')}). "
            "Generated by `scripts/news_data_pipeline.py build`.",
            "",
            "## Source (Studychill DB `news_article`)",
            "",
            f"- Rows: {self.source_counts.get('articlesTotal', '?')}, published and not deleted: "
            f"{self.source_counts.get('articlesPublished', '?')}",
            "",
            "## Output",
            "",
            f"- `{path.relative_to(BASE_DIR).as_posix()}`: {self.count} articles, {self.sentences:,} sentences, "
            f"{self.vocabulary:,} vocabulary items, {self.with_audio} with a source audio URL; "
            f"{path.stat().st_size:,} bytes on disk ({raw_size:,} bytes of JSON)",
            f"- `data/news/covers/`: {self.cover_count} WebP covers (≤ {COVER_MAX_WIDTH} px wide, quality "
            f"{COVER_QUALITY}), {self.cover_bytes:,} bytes (Studychill originals: {self.original_cover_bytes:,} bytes)",
            f"- Articles with an untranslated sentence: {self.untranslated_articles}",
            f"- Articles whose stored minutes differ from round(words / 160): {self.minutes_mismatch} "
            "(the importer recomputes word count and minutes)",
            f"- Stale covers removed: {self.removed_covers}",
            f"- Articles with a Studychill brand mention renamed to TOEIC Space: {self.rebranded}",
            "",
            "## Stages",
            "",
            "| Stage | Articles |",
            "|---|---:|",
            *[f"| {s} | {self.stages[s]} |" for s in STAGES],
            "",
            "## Topics",
            "",
            "| Topic | Articles |",
            "|---|---:|",
            *[f"| {t} | {self.topics[t]} |" for t in TOPICS.values()],
            "",
            "## Flags",
            "",
        ]
        if self.flags:
            lines += ["| Reason | Article | Value |", "|---|---|---|"]
            lines += [f"| {r} | {w} | {v.replace('|', '/')} |" for r, w, v in self.flags]
        else:
            lines.append("None.")
        REPORT_FILE.write_bytes(("\n".join(lines) + "\n").encode("utf-8"))


# --------------------------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    export = sub.add_parser("export", help="read-only snapshot over SSH")
    export.add_argument("--host", default="studychill")
    export.add_argument("--container", default="studychill-backend")
    export.set_defaults(func=export_snapshot)
    build_cmd = sub.add_parser("build", help="snapshot -> data/news/articles.json + covers")
    build_cmd.add_argument("--source", help="snapshot folder (default: newest data/source/studychill_news_*)")
    build_cmd.set_defaults(func=build)
    upload = sub.add_parser("import", help="upload data/news to the admin import endpoint")
    upload.add_argument("--url", required=True, help=".../content/api/v1/news/admin/import")
    upload.add_argument("--token", required=True, help="Admin access token")
    upload.set_defaults(func=import_data)
    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
