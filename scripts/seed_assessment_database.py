#!/usr/bin/env python3
"""
Seed Assessment Database (MySQL) with Clean Datasets
====================================================
Seeds:
- 97 ToeicTests
- 7,716 ToeicPassages
- 30,572 ToeicQuestions
Directly into database `toeic_space_assessment` on 127.0.0.1:3306.
"""

import gzip
import json
import os
import sys
from datetime import datetime
from pathlib import Path
import MySQLdb

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8')

BASE_DIR = Path(__file__).resolve().parent.parent
SEEDS_DIR = BASE_DIR / "data" / "seeds"

DB_HOST = os.getenv("MYSQL_HOST", "127.0.0.1")
DB_PORT = int(os.getenv("MYSQL_PORT", "3306"))
DB_USER = os.getenv("MYSQL_USER", "root")
DB_PASS = os.getenv("MYSQL_PASS", "123456")
DB_NAME = os.getenv("MYSQL_DB", "toeic_space_assessment")


def map_difficulty(val):
    if not val:
        return 3
    if isinstance(val, int):
        return max(1, min(5, val))
    val_str = str(val).lower()
    if "veryeasy" in val_str or "very_easy" in val_str:
        return 1
    if "easy" in val_str:
        return 2
    if "medium" in val_str:
        return 3
    if "veryhard" in val_str or "very_hard" in val_str:
        return 5
    if "hard" in val_str:
        return 4
    return 3


def main():
    print("=" * 80)
    print(" TOEIC SPACE ASSESSMENT - DIRECT HIGH-SPEED DATABASE SEEDER ")
    print("=" * 80)

    print(f"\n[1] Connecting to MySQL at {DB_HOST}:{DB_PORT}/{DB_NAME}...")
    conn = MySQLdb.connect(
        host=DB_HOST,
        port=DB_PORT,
        user=DB_USER,
        passwd=DB_PASS,
        db=DB_NAME,
        charset="utf8mb4"
    )
    cur = conn.cursor()
    cur.execute("SET FOREIGN_KEY_CHECKS = 0;")
    cur.execute("SET autocommit = 0;")

    # 1. Seed Tests
    print("\n[2] Seeding ToeicTests...")
    with open(SEEDS_DIR / "toeic_tests.json", "r", encoding="utf-8") as f:
        tests = json.load(f)

    test_sql = """
    INSERT INTO toeictests (
        Id, Title, Code, Description, Category, Year, TotalQuestions, DurationMinutes,
        TotalListeningQuestions, TotalReadingQuestions, AudioUrl, IsActive, Status,
        CreatedByUserId, ExternalId, Source, Metadata, DeletedAt, CreatedAt, UpdatedAt
    ) VALUES (
        %s, %s, %s, %s, %s, %s, %s, %s,
        %s, %s, %s, %s, %s,
        %s, %s, %s, %s, %s, %s, %s
    ) ON DUPLICATE KEY UPDATE
        Title = VALUES(Title),
        Code = VALUES(Code),
        Description = VALUES(Description),
        Category = VALUES(Category),
        TotalQuestions = VALUES(TotalQuestions),
        DurationMinutes = VALUES(DurationMinutes),
        IsActive = VALUES(IsActive),
        UpdatedAt = VALUES(UpdatedAt);
    """

    now = datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S.%f")
    test_rows = []
    for t in tests:
        q_count = int(t.get("TotalQuestions") or 200)
        is_full = (t.get("Category") == "FULL_TEST")
        test_rows.append((
            t["Id"],
            t["Title"],
            t["Code"],
            t.get("Description"),
            t.get("Category") or "PRACTICE",
            t.get("Year") or 2026,
            q_count,
            int(t.get("TimeLimitMinutes") or 120),
            100 if is_full else q_count // 2,
            100 if is_full else q_count - (q_count // 2),
            None,
            1 if t.get("IsActive", True) else 0,
            1 if t.get("IsActive", True) else 0,  # 1 = Active, 0 = Draft
            None,
            t["Id"],
            "Studychill/ETS",
            None,
            None,
            t.get("CreatedAt") or now,
            t.get("UpdatedAt") or now
        ))

    cur.executemany(test_sql, test_rows)
    conn.commit()
    print(f"    ✓ Inserted/Updated {len(test_rows)} tests in toeictests.")

    # 2. Seed Passages
    print("\n[3] Seeding ToeicPassages...")
    with gzip.open(SEEDS_DIR / "toeic_passages.json.gz", "rt", encoding="utf-8") as f:
        passages = json.load(f)

    passage_sql = """
    INSERT INTO toeicpassages (
        Id, TestId, Part, PassageType, Title, Content, AudioUrl, ImageUrl,
        Transcript, OrderIndex, ExternalId, CreatedAt, UpdatedAt
    ) VALUES (
        %s, %s, %s, %s, %s, %s, %s, %s,
        %s, %s, %s, %s, %s
    ) ON DUPLICATE KEY UPDATE
        TestId = VALUES(TestId),
        Part = VALUES(Part),
        Title = VALUES(Title),
        Content = VALUES(Content),
        AudioUrl = VALUES(AudioUrl),
        ImageUrl = VALUES(ImageUrl),
        UpdatedAt = VALUES(UpdatedAt);
    """

    passage_rows = []
    batch_size = 2000
    total_passages_inserted = 0

    for p in passages:
        passage_rows.append((
            p["Id"],
            p.get("TestId"),
            int(p.get("Part") or 3),
            p.get("PassageType") or "Conversation",
            p.get("Title"),
            p.get("Content") or "",
            p.get("AudioUrl"),
            p.get("ImageUrl"),
            None,
            int(p.get("OrderIndex") or 0),
            p["Id"],
            p.get("CreatedAt") or now,
            p.get("UpdatedAt") or now
        ))
        if len(passage_rows) >= batch_size:
            cur.executemany(passage_sql, passage_rows)
            conn.commit()
            total_passages_inserted += len(passage_rows)
            passage_rows = []

    if passage_rows:
        cur.executemany(passage_sql, passage_rows)
        conn.commit()
        total_passages_inserted += len(passage_rows)

    print(f"    ✓ Inserted/Updated {total_passages_inserted} passages in toeicpassages.")

    # 3. Seed Questions
    print("\n[4] Seeding ToeicQuestions...")
    with gzip.open(SEEDS_DIR / "toeic_questions.json.gz", "rt", encoding="utf-8") as f:
        questions = json.load(f)

    question_sql = """
    INSERT INTO toeicquestions (
        Id, TestId, PassageId, Part, Section, QuestionNumber, QuestionText,
        AudioUrl, ImageUrl, OptionA, OptionB, OptionC, OptionD, CorrectAnswer,
        Explanation, Transcript, DifficultyLevel, Topic, Status, Version,
        OrderIndex, PreferAiExplanation, CreatedByUserId, ExternalId, DeletedAt,
        CreatedAt, UpdatedAt
    ) VALUES (
        %s, %s, %s, %s, %s, %s, %s,
        %s, %s, %s, %s, %s, %s, %s,
        %s, %s, %s, %s, %s, %s,
        %s, %s, %s, %s, %s,
        %s, %s
    ) ON DUPLICATE KEY UPDATE
        TestId = VALUES(TestId),
        PassageId = VALUES(PassageId),
        Part = VALUES(Part),
        Section = VALUES(Section),
        QuestionNumber = VALUES(QuestionNumber),
        QuestionText = VALUES(QuestionText),
        AudioUrl = VALUES(AudioUrl),
        ImageUrl = VALUES(ImageUrl),
        OptionA = VALUES(OptionA),
        OptionB = VALUES(OptionB),
        OptionC = VALUES(OptionC),
        OptionD = VALUES(OptionD),
        CorrectAnswer = VALUES(CorrectAnswer),
        Explanation = VALUES(Explanation),
        Transcript = VALUES(Transcript),
        DifficultyLevel = VALUES(DifficultyLevel),
        Status = VALUES(Status),
        UpdatedAt = VALUES(UpdatedAt);
    """

    question_rows = []
    total_q_inserted = 0

    for q in questions:
        part_num = int(q.get("Part") or 1)
        section_str = "Listening" if part_num <= 4 else "Reading"
        diff_num = map_difficulty(q.get("DifficultyLevel"))
        status_num = 1  # Active

        question_rows.append((
            q["Id"],
            q.get("TestId"),
            q.get("PassageId"),
            part_num,
            section_str,
            int(q.get("QuestionNumber") or 1),
            q.get("Content") or "",
            q.get("AudioUrl"),
            q.get("ImageUrl"),
            q.get("OptionA") or "",
            q.get("OptionB") or "",
            q.get("OptionC") or "",
            q.get("OptionD") or "",
            (q.get("CorrectAnswer") or "A").strip().upper()[:1],
            q.get("Explanation"),
            q.get("Transcript"),
            diff_num,
            None,
            status_num,
            1,
            int(q.get("OrderIndex") or 0),
            0,
            None,
            q["Id"],
            None,
            q.get("CreatedAt") or now,
            q.get("UpdatedAt") or now
        ))

        if len(question_rows) >= batch_size:
            cur.executemany(question_sql, question_rows)
            conn.commit()
            total_q_inserted += len(question_rows)
            question_rows = []
            print(f"    ... seeded {total_q_inserted:,}/{len(questions):,} questions")

    if question_rows:
        cur.executemany(question_sql, question_rows)
        conn.commit()
        total_q_inserted += len(question_rows)

    print(f"    ✓ Inserted/Updated {total_q_inserted:,} questions in toeicquestions.")

    cur.execute("SET FOREIGN_KEY_CHECKS = 1;")
    conn.commit()

    # Verify counts
    print("\n[5] Verifying database row counts...")
    cur.execute("SELECT COUNT(*) FROM toeictests;")
    c_tests = cur.fetchone()[0]
    cur.execute("SELECT COUNT(*) FROM toeicpassages;")
    c_passages = cur.fetchone()[0]
    cur.execute("SELECT COUNT(*) FROM toeicquestions;")
    c_questions = cur.fetchone()[0]

    print(f"    ✓ toeictests: {c_tests:,} rows")
    print(f"    ✓ toeicpassages: {c_passages:,} rows")
    print(f"    ✓ toeicquestions: {c_questions:,} rows")

    conn.close()
    print("\n" + "=" * 80)
    print(" DATABASE SEEDING COMPLETED SUCCESSFULLY! ")
    print("=" * 80)


if __name__ == "__main__":
    main()
