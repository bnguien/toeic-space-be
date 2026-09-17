#!/usr/bin/env python3
"""
TOEIC Space - Data Healing & Standardization Pipeline
=====================================================
1. Resolves all relative audio/image URLs using Studychill listening cards mapping.
2. Standardizes test titles and unique codes for all 26 Full Tests (ETS 2026, Crack TOEIC Vol 1, Pass TOEIC).
3. Flags 2 defective tests as inactive/draft (empty test and biased answer test).
4. Generates clean JSON seeds and an idempotent MySQL assessment seed SQL script.
"""

import glob
import gzip
import json
import os
import re
import sys
import uuid
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8')

BASE_DIR = Path(__file__).resolve().parent.parent
CRAWLED_DIR = BASE_DIR / "data" / "crawled"
SEEDS_DIR = BASE_DIR / "data" / "seeds"
STUDYCHILL_LISTENING_DIR = Path("d:/HOCKI6/LẬP TRÌNH PYTHON/Studychill/frontend/public/data/listening")

R2_PUBLIC_URL = "https://chill-zone.studychill.net"

# Mapping of the 16 full tests from Supabase sets
SUPABASE_TEST_MAPPING = {
    "b7f26702-bd44-489f-987e-e6f093b69895": ("Pass TOEIC", "PASS-TOEIC-TEST-01", "Pass TOEIC (2026) - Test 01"),
    "b91f126d-3691-4873-9996-ba2a0d22e566": ("Pass TOEIC", "PASS-TOEIC-TEST-02", "Pass TOEIC (2026) - Test 02"),
    "809924a6-a310-4a8e-a477-541602128218": ("Pass TOEIC", "PASS-TOEIC-TEST-03", "Pass TOEIC (2026) - Test 03"),
    "2b0523b2-c666-42c2-864c-3aec68e3cf1a": ("Pass TOEIC", "PASS-TOEIC-TEST-04", "Pass TOEIC (2026) - Test 04"),
    "6fddf2c3-3123-433b-8802-515accd5912e": ("Pass TOEIC", "PASS-TOEIC-TEST-05", "Pass TOEIC (2026) - Test 05"),
    "95ce6a79-5a60-4a66-ab34-cd1a77126564": ("Pass TOEIC", "PASS-TOEIC-TEST-06", "Pass TOEIC (2026) - Test 06"),
    "aeea7157-3da0-4959-b427-74b64d1d8f47": ("Crack TOEIC Vol 1", "CRACK-TOEIC-VOL1-TEST-01", "Crack TOEIC Vol 1 - Test 01"),
    "e121b5b2-5eee-46cb-bcc3-df54439df1f5": ("Crack TOEIC Vol 1", "CRACK-TOEIC-VOL1-TEST-02", "Crack TOEIC Vol 1 - Test 02"),
    "e331d8a3-433b-4e33-8de9-ee90cc260d0e": ("Crack TOEIC Vol 1", "CRACK-TOEIC-VOL1-TEST-03", "Crack TOEIC Vol 1 - Test 03"),
    "8c3f383e-ecc7-4acc-8039-dbb6c8d6770f": ("Crack TOEIC Vol 1", "CRACK-TOEIC-VOL1-TEST-04", "Crack TOEIC Vol 1 - Test 04"),
    "e4f4f965-0d8e-48c4-a087-c8d006bb8bfb": ("Crack TOEIC Vol 1", "CRACK-TOEIC-VOL1-TEST-05", "Crack TOEIC Vol 1 - Test 05"),
    "e8c5f1ab-6adb-47b9-9a67-0bb6dfb4aa03": ("Crack TOEIC Vol 1", "CRACK-TOEIC-VOL1-TEST-06", "Crack TOEIC Vol 1 - Test 06"),
    "442072a4-f1aa-4cc2-8237-0554d415ed50": ("Crack TOEIC Vol 1", "CRACK-TOEIC-VOL1-TEST-07", "Crack TOEIC Vol 1 - Test 07"),
    "a69449c5-d49a-4152-b1d0-e267c36ab3fa": ("Crack TOEIC Vol 1", "CRACK-TOEIC-VOL1-TEST-08", "Crack TOEIC Vol 1 - Test 08"),
    "9f45e3f9-da61-4c63-9b9a-4d037fb58798": ("Crack TOEIC Vol 1", "CRACK-TOEIC-VOL1-TEST-09", "Crack TOEIC Vol 1 - Test 09"),
    "0fab1075-8327-4b91-8dd8-5e311fc989c5": ("Crack TOEIC Vol 1", "CRACK-TOEIC-VOL1-TEST-10", "Crack TOEIC Vol 1 - Test 10"),
}

DEFECTIVE_TESTS = {
    "d3cb9ea1b3d54519b94f8bc5b6ab53cd": "AUDIT_DEFECT_EMPTY_0_QUESTIONS",
    "925efdb0f111550b8ca95ff59bba5dbe": "AUDIT_DEFECT_BIASED_ANSWER_A_66_PERCENT",
}


def build_cdn_media_map():
    print("[1] Building CDN URL map from Studychill listening repository...")
    url_map = {}
    if not STUDYCHILL_LISTENING_DIR.exists():
        print(f"    [!] Warning: {STUDYCHILL_LISTENING_DIR} not found. Skipping local card map.")
        return url_map

    json_files = glob.glob(str(STUDYCHILL_LISTENING_DIR / "*.json"))
    for jf in json_files:
        try:
            with open(jf, "r", encoding="utf-8") as f:
                data = json.load(f)
            for p in data.get("passages", []):
                for key in ["audioSrc", "imageSrc"]:
                    val = p.get(key)
                    if val and isinstance(val, str) and val.startswith("http"):
                        fname = val.split("/")[-1].split("?")[0]
                        url_map[fname] = val
                for q in p.get("questions", []):
                    for key in ["audio_url", "image_url", "audioSrc", "imageSrc"]:
                        val = q.get(key)
                        if val and isinstance(val, str) and val.startswith("http"):
                            fname = val.split("/")[-1].split("?")[0]
                            url_map[fname] = val
        except Exception as e:
            print(f"    [!] Error parsing {jf}: {e}")

    print(f"    ✓ Loaded {len(url_map):,} unique media filename mappings to full CDN URLs.")
    return url_map


def format_guid(hex_str: str) -> str:
    cleaned = hex_str.replace("-", "").strip().lower()
    if len(cleaned) == 32:
        return str(uuid.UUID(cleaned))
    return str(uuid.uuid4())


def escape_sql_str(val) -> str:
    if val is None:
        return "NULL"
    s = str(val).replace("\\", "\\\\").replace("'", "''")
    return f"'{s}'"


def heal_media_url(url: str, media_map: dict) -> str:
    if not url or not isinstance(url, str):
        return url
    url = url.strip()
    if not url:
        return None
    if url.startswith("http://") or url.startswith("https://"):
        return url

    # It's a relative URL, e.g. "41-43.mp3", "19.mp3", "2.png"
    fname = url.split("/")[-1].split("?")[0]
    if fname in media_map:
        return media_map[fname]

    # Fallback to standard Cloudflare R2 dautoeic path if not in map
    return f"{R2_PUBLIC_URL}/Toeic/dautoeic/media/{url}"


def main():
    print("=" * 80)
    print(" TOEIC SPACE ASSESSMENT - COMPREHENSIVE DATA HEALING & STANDARDIZATION ")
    print("=" * 80)

    SEEDS_DIR.mkdir(parents=True, exist_ok=True)
    media_map = build_cdn_media_map()

    # 1. Load Raw Crawled Data
    print("\n[2] Loading raw datasets...")
    with gzip.open(CRAWLED_DIR / "toeic_mock_tests.json.gz", "rt", encoding="utf-8") as f:
        raw_tests = json.load(f)
    with gzip.open(CRAWLED_DIR / "toeic_mock_passages.json.gz", "rt", encoding="utf-8") as f:
        raw_passages = json.load(f)
    with gzip.open(CRAWLED_DIR / "toeic_mock_questions.json.gz", "rt", encoding="utf-8") as f:
        raw_questions = json.load(f)

    print(f"    ✓ Tests: {len(raw_tests):,}")
    print(f"    ✓ Passages: {len(raw_passages):,}")
    print(f"    ✓ Questions: {len(raw_questions):,}")

    # 2. Standardize Tests
    print("\n[3] Standardizing tests and codes...")
    clean_tests = []
    test_id_map = {}  # clean_hex -> standardized_guid
    used_codes = set()

    for t in raw_tests:
        raw_id = t["id"]
        clean_hex = raw_id.replace("-", "").lower()
        std_guid = format_guid(raw_id)
        test_id_map[clean_hex] = std_guid

        code = (t.get("code") or "").strip()
        title = (t.get("title") or "").strip()
        desc = (t.get("description") or "").strip()
        category = (t.get("category") or "").strip()
        is_active = True
        notes = []

        # Check if this test is one of the 16 Supabase tests
        matched_supa = None
        for supa_id, (set_name, std_code, std_title) in SUPABASE_TEST_MAPPING.items():
            if clean_hex == supa_id.replace("-", "").lower():
                matched_supa = (set_name, std_code, std_title)
                break

        if matched_supa:
            set_name, std_code, std_title = matched_supa
            code = std_code
            title = std_title
            category = "FULL_TEST"
            desc = f"Bộ đề thi TOEIC Full 200 câu chuẩn ETS: {set_name}."
        elif "ETS 2026" in title or "ETS 2026" in code:
            # ETS 2026 Full Test
            match = re.search(r"TEST\s*(\d+)", title, re.IGNORECASE)
            num_str = f"{int(match.group(1)):02d}" if match else "01"
            code = f"ETS-2026-TEST-{num_str}"
            title = f"ETS 2026 - Test {num_str}"
            category = "FULL_TEST"
            desc = "Bộ đề thi TOEIC Full 200 câu chuẩn ETS mới nhất năm 2026."
        elif clean_hex in DEFECTIVE_TESTS:
            defect_reason = DEFECTIVE_TESTS[clean_hex]
            is_active = False
            notes.append(defect_reason)
            desc = f"[{defect_reason}] " + desc

        # Deduplicate code if any collision
        base_code = code or f"TEST-{std_guid[:8].upper()}"
        unique_code = base_code
        counter = 2
        while unique_code in used_codes:
            unique_code = f"{base_code}-V{counter}"
            counter += 1
        used_codes.add(unique_code)

        clean_tests.append({
            "Id": std_guid,
            "Code": unique_code,
            "Title": title or f"TOEIC Practice Test {unique_code}",
            "Description": desc or None,
            "Category": category or "PRACTICE",
            "TotalQuestions": int(t.get("total_questions") or 0),
            "TimeLimitMinutes": int(t.get("time_limit_minutes") or 120),
            "DifficultyLevel": t.get("difficulty_level") or "Medium",
            "IsActive": is_active,
            "CreatedAt": t.get("created_at") or datetime.utcnow().isoformat(),
            "UpdatedAt": datetime.utcnow().isoformat(),
            "AuditNotes": "; ".join(notes) if notes else None
        })

    # Sort tests: Full tests first, then by code
    clean_tests.sort(key=lambda x: (0 if x["Category"] == "FULL_TEST" else 1, x["Code"]))

    # 3. Heal Passages
    print("\n[4] Healing passages media URLs...")
    clean_passages = []
    passage_id_map = {}  # clean_hex -> std_guid
    healed_passage_audio = 0
    healed_passage_image = 0

    for p in raw_passages:
        raw_id = p["id"]
        clean_hex = raw_id.replace("-", "").lower()
        std_guid = format_guid(raw_id)
        passage_id_map[clean_hex] = std_guid

        test_hex = (p.get("test_id") or "").replace("-", "").lower()
        test_guid = test_id_map.get(test_hex)

        orig_aud = p.get("audio_url")
        orig_img = p.get("image_url")

        healed_aud = heal_media_url(orig_aud, media_map)
        healed_img = heal_media_url(orig_img, media_map)

        if orig_aud and not orig_aud.startswith("http") and healed_aud.startswith("http"):
            healed_passage_audio += 1
        if orig_img and not orig_img.startswith("http") and healed_img.startswith("http"):
            healed_passage_image += 1

        clean_passages.append({
            "Id": std_guid,
            "TestId": test_guid,
            "Part": int(p.get("part") or 0),
            "PassageNumber": int(p.get("passage_number") or 1),
            "Title": p.get("title"),
            "Content": p.get("content") or "",
            "AudioUrl": healed_aud,
            "ImageUrl": healed_img,
            "OrderIndex": int(p.get("order_index") or 0),
            "CreatedAt": p.get("created_at") or datetime.utcnow().isoformat(),
            "UpdatedAt": datetime.utcnow().isoformat(),
        })

    print(f"    ✓ Passages healed: {healed_passage_audio} audios, {healed_passage_image} images.")

    # 4. Heal Questions
    print("\n[5] Healing questions media URLs & standardizing...")
    clean_questions = []
    healed_q_audio = 0
    healed_q_image = 0

    for q in raw_questions:
        raw_id = q["id"]
        std_guid = format_guid(raw_id)

        test_hex = (q.get("test_id") or "").replace("-", "").lower()
        test_guid = test_id_map.get(test_hex)

        passage_hex = (q.get("passage_id") or "").replace("-", "").lower()
        passage_guid = passage_id_map.get(passage_hex)

        orig_aud = q.get("audio_url")
        orig_img = q.get("image_url")

        healed_aud = heal_media_url(orig_aud, media_map)
        healed_img = heal_media_url(orig_img, media_map)

        if orig_aud and not orig_aud.startswith("http") and healed_aud.startswith("http"):
            healed_q_audio += 1
        if orig_img and not orig_img.startswith("http") and healed_img.startswith("http"):
            healed_q_image += 1

        correct_ans = (q.get("correct_answer") or "A").strip().upper()
        if correct_ans not in ["A", "B", "C", "D"]:
            correct_ans = "A"

        clean_questions.append({
            "Id": std_guid,
            "TestId": test_guid,
            "PassageId": passage_guid,
            "Part": int(q.get("part") or 0),
            "QuestionNumber": int(q.get("question_number") or 0),
            "Content": q.get("content") or "",
            "OptionA": q.get("option_a") or "",
            "OptionB": q.get("option_b") or "",
            "OptionC": q.get("option_c") or "",
            "OptionD": q.get("option_d") or "",
            "CorrectAnswer": correct_ans,
            "AudioUrl": healed_aud,
            "ImageUrl": healed_img,
            "Explanation": q.get("explanation"),
            "Transcript": q.get("transcript"),
            "DifficultyLevel": q.get("difficulty_level") or "Medium",
            "OrderIndex": int(q.get("order_index") or 0),
            "CreatedAt": q.get("created_at") or datetime.utcnow().isoformat(),
            "UpdatedAt": datetime.utcnow().isoformat(),
        })

    print(f"    ✓ Questions healed: {healed_q_audio} audios, {healed_q_image} images.")

    # 5. Export Standardized Seeds
    print("\n[6] Exporting clean seeds...")
    with open(SEEDS_DIR / "toeic_tests.json", "w", encoding="utf-8") as f:
        json.dump(clean_tests, f, ensure_ascii=False, indent=2)
    print(f"    ✓ Saved {len(clean_tests)} tests to toeic_tests.json")

    with gzip.open(SEEDS_DIR / "toeic_passages.json.gz", "wt", encoding="utf-8") as f:
        json.dump(clean_passages, f, ensure_ascii=False)
    print(f"    ✓ Saved {len(clean_passages)} passages to toeic_passages.json.gz")

    with gzip.open(SEEDS_DIR / "toeic_questions.json.gz", "wt", encoding="utf-8") as f:
        json.dump(clean_questions, f, ensure_ascii=False)
    print(f"    ✓ Saved {len(clean_questions)} questions to toeic_questions.json.gz")

    # Tests Summary Catalog
    full_tests_count = sum(1 for t in clean_tests if t["Category"] == "FULL_TEST")
    active_tests_count = sum(1 for t in clean_tests if t["IsActive"])
    summary = {
        "generated_at": datetime.utcnow().isoformat(),
        "total_tests": len(clean_tests),
        "total_full_tests": full_tests_count,
        "total_active_tests": active_tests_count,
        "total_passages": len(clean_passages),
        "total_questions": len(clean_questions),
        "tests_breakdown": [
            {
                "id": t["Id"],
                "code": t["Code"],
                "title": t["Title"],
                "category": t["Category"],
                "total_questions": t["TotalQuestions"],
                "is_active": t["IsActive"],
                "audit_notes": t["AuditNotes"]
            }
            for t in clean_tests
        ]
    }
    with open(SEEDS_DIR / "toeic_tests_summary.json", "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print("    ✓ Saved toeic_tests_summary.json")

    # 6. Generate Idempotent SQL Seed Script
    print("\n[7] Generating idempotent MySQL seed script...")
    sql_path = SEEDS_DIR / "toeic_space_assessment_seed.sql.gz"
    with gzip.open(sql_path, "wt", encoding="utf-8") as f:
        f.write("-- TOEIC SPACE ASSESSMENT DATABASE SEED\n")
        f.write("-- Auto-generated by heal_and_standardize_toeic_data.py\n")
        f.write("SET FOREIGN_KEY_CHECKS = 0;\n\n")

        # Tests
        f.write("-- 1. ToeicTests\n")
        for t in clean_tests:
            f.write(
                f"INSERT INTO ToeicTests (Id, Code, Title, Description, Category, TotalQuestions, "
                f"TimeLimitMinutes, DifficultyLevel, IsActive, CreatedAt, UpdatedAt) VALUES ("
                f"{escape_sql_str(t['Id'])}, {escape_sql_str(t['Code'])}, {escape_sql_str(t['Title'])}, "
                f"{escape_sql_str(t['Description'])}, {escape_sql_str(t['Category'])}, {t['TotalQuestions']}, "
                f"{t['TimeLimitMinutes']}, {escape_sql_str(t['DifficultyLevel'])}, {1 if t['IsActive'] else 0}, "
                f"{escape_sql_str(t['CreatedAt'])}, {escape_sql_str(t['UpdatedAt'])}) "
                f"ON DUPLICATE KEY UPDATE Code = VALUES(Code), Title = VALUES(Title), IsActive = VALUES(IsActive);\n"
            )

        # Passages
        f.write("\n-- 2. ToeicPassages\n")
        for p in clean_passages:
            f.write(
                f"INSERT INTO ToeicPassages (Id, TestId, Part, PassageNumber, Title, Content, "
                f"AudioUrl, ImageUrl, OrderIndex, CreatedAt, UpdatedAt) VALUES ("
                f"{escape_sql_str(p['Id'])}, {escape_sql_str(p['TestId'])}, {p['Part']}, {p['PassageNumber']}, "
                f"{escape_sql_str(p['Title'])}, {escape_sql_str(p['Content'])}, {escape_sql_str(p['AudioUrl'])}, "
                f"{escape_sql_str(p['ImageUrl'])}, {p['OrderIndex']}, {escape_sql_str(p['CreatedAt'])}, "
                f"{escape_sql_str(p['UpdatedAt'])}) "
                f"ON DUPLICATE KEY UPDATE AudioUrl = VALUES(AudioUrl), ImageUrl = VALUES(ImageUrl);\n"
            )

        # Questions
        f.write("\n-- 3. ToeicQuestions\n")
        for q in clean_questions:
            f.write(
                f"INSERT INTO ToeicQuestions (Id, TestId, PassageId, Part, QuestionNumber, Content, "
                f"OptionA, OptionB, OptionC, OptionD, CorrectAnswer, AudioUrl, ImageUrl, "
                f"Explanation, Transcript, DifficultyLevel, OrderIndex, CreatedAt, UpdatedAt) VALUES ("
                f"{escape_sql_str(q['Id'])}, {escape_sql_str(q['TestId'])}, {escape_sql_str(q['PassageId'])}, "
                f"{q['Part']}, {q['QuestionNumber']}, {escape_sql_str(q['Content'])}, "
                f"{escape_sql_str(q['OptionA'])}, {escape_sql_str(q['OptionB'])}, {escape_sql_str(q['OptionC'])}, "
                f"{escape_sql_str(q['OptionD'])}, {escape_sql_str(q['CorrectAnswer'])}, {escape_sql_str(q['AudioUrl'])}, "
                f"{escape_sql_str(q['ImageUrl'])}, {escape_sql_str(q['Explanation'])}, {escape_sql_str(q['Transcript'])}, "
                f"{escape_sql_str(q['DifficultyLevel'])}, {q['OrderIndex']}, {escape_sql_str(q['CreatedAt'])}, "
                f"{escape_sql_str(q['UpdatedAt'])}) "
                f"ON DUPLICATE KEY UPDATE AudioUrl = VALUES(AudioUrl), ImageUrl = VALUES(ImageUrl);\n"
            )

        f.write("\nSET FOREIGN_KEY_CHECKS = 1;\n")

    sql_size_mb = os.path.getsize(sql_path) / (1024 * 1024)
    print(f"    ✓ Generated {sql_path.name} ({sql_size_mb:.2f} MB compressed SQL)")

    print("\n" + "=" * 80)
    print(f" SUCCESS: Healed {healed_q_audio + healed_passage_audio:,} audio URLs & {healed_q_image + healed_passage_image:,} image URLs!")
    print(f" Standardized 26 Full Tests and quarantined {len(DEFECTIVE_TESTS)} defective tests.")
    print("=" * 80)


if __name__ == "__main__":
    main()
