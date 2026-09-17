import json
import gzip
import uuid
import re
import os
import sys
from datetime import datetime
from collections import defaultdict, Counter

sys.stdout.reconfigure(encoding='utf-8')

CRAWLED_DIR = os.path.abspath('data/crawled')
SEEDS_DIR = os.path.abspath('data/seeds')
os.makedirs(SEEDS_DIR, exist_ok=True)

def to_guid(hex_str):
    if not hex_str:
        return None
    try:
        clean = hex_str.replace('-', '').strip()
        if len(clean) == 32:
            return str(uuid.UUID(clean))
        return hex_str
    except Exception:
        return hex_str

def escape_sql(val):
    if val is None:
        return "NULL"
    if isinstance(val, bool):
        return "1" if val else "0"
    if isinstance(val, (int, float)):
        return str(val)
    # string escaping
    s = str(val)
    s = s.replace('\\', '\\\\').replace("'", "\\'")
    return f"'{s}'"

def parse_year(title, code):
    match = re.search(r'\b(202\d|201\d)\b', f"{title} {code}")
    if match:
        return int(match.group(1))
    return None

def classify_category(title, code, part_counts):
    upper = f"{title} {code}".upper()
    if "ETS" in upper:
        return "ETS"
    if "ECONOMY" in upper:
        return "Economy"
    if "HACKER" in upper:
        return "Hacker"
    code_lower = code.lower()
    if "-lv" in code_lower:
        return "SkillLevelPractice"
    if any(k in code_lower for k in ['-meeting', '-graphic', '-news', '-store', '-tour', '-travel', '-voicemail', '-shipping', '-restaurant', '-maintenance', '-hr', '-statement', '-negative', '-modal', '-choice', '-auxbe', '-why', '-who', '-where', '-when', '-what', '-how', '-single', '-scene', '-multi']):
        return "TopicPractice"
    if any(code_lower.startswith(p) for p in ['p1-', 'p2-', 'p3-', 'p4-', 'p5-', 'p6-', 'p7-']):
        return "PartPractice"
    if "PART 6" in upper or "PART 5" in upper or "PART 7" in upper:
        return "PartPractice"
    return "GeneralPractice"

def generate_unique_code(title, raw_code, test_id, seen_codes):
    t_clean = title.strip()
    c_clean = raw_code.strip()
    
    # Check ETS 2026
    m_ets = re.search(r'ETS\s*(\d{4})\s*-\s*TEST\s*(\d+)', t_clean, re.IGNORECASE)
    if m_ets:
        code = f"ETS-{m_ets.group(1)}-TEST-{int(m_ets.group(2)):02d}"
    elif re.match(r'ETS-TEST-\d+', c_clean, re.IGNORECASE):
        num = re.search(r'\d+', c_clean).group()
        code = f"ETS-TEST-{int(num):02d}"
    elif c_clean.startswith('p') and ('-lv' in c_clean or '-t' in c_clean):
        # e.g. p1-lv1, p1-t1
        m_part = re.match(r'(p\d)-(lv\d|t\d)', c_clean, re.IGNORECASE)
        m_test = re.search(r'TEST\s*(\d+)', t_clean, re.IGNORECASE)
        test_num = f"-{int(m_test.group(1)):02d}" if m_test else ""
        code = f"{c_clean.upper()}{test_num}"
    elif c_clean.startswith('p'):
        code = c_clean.upper().replace('-', '-TOPIC-')
    elif "PART 6 PRACTICE" in c_clean.upper() or "PART 6" in t_clean.upper():
        code = "P6-PRACTICE-100Q"
    elif "PART 5" in t_clean.upper() and "ECONOMY" in t_clean.upper():
        code = "P5-ECONOMY-200Q"
    else:
        # Fallback slug
        slug = re.sub(r'[^A-Za-z0-9]+', '-', c_clean or t_clean).strip('-').upper()
        code = slug[:50] if slug else f"TEST-{test_id[:8].upper()}"

    # Ensure absolute uniqueness
    final_code = code
    counter = 1
    while final_code in seen_codes:
        suffix = chr(64 + counter) if counter <= 26 else f"-{counter}"
        final_code = f"{code}-{suffix}"
        counter += 1

    seen_codes.add(final_code)
    return final_code

def map_difficulty(level):
    # Level from VPS: 1..5
    # Enums: 1=Easy, 2=Medium, 3=Hard, 4=VeryHard
    if not level:
        return 2 # Medium
    try:
        lvl = int(level)
        if lvl <= 1: return 1 # Easy
        if lvl == 2: return 2 # Medium
        if lvl == 3: return 2 # Medium
        if lvl == 4: return 3 # Hard
        if lvl >= 5: return 4 # VeryHard
    except Exception:
        pass
    return 2

def main():
    print("=" * 60)
    print("TOEIC SPACE - DATA TRANSFORMATION & CLASSIFICATION PIPELINE")
    print("=" * 60)

    # 1. Load Raw Data
    print("\n[1/5] Loading raw JSON files...")
    with gzip.open(os.path.join(CRAWLED_DIR, 'toeic_mock_tests.json.gz'), 'rt', encoding='utf-8') as f:
        raw_tests = json.load(f)
    print(f"  Loaded {len(raw_tests)} tests.")

    with gzip.open(os.path.join(CRAWLED_DIR, 'toeic_mock_passages.json.gz'), 'rt', encoding='utf-8') as f:
        raw_passages = json.load(f)
    print(f"  Loaded {len(raw_passages)} passages.")

    with gzip.open(os.path.join(CRAWLED_DIR, 'toeic_mock_questions.json.gz'), 'rt', encoding='utf-8') as f:
        raw_questions = json.load(f)
    print(f"  Loaded {len(raw_questions)} questions.")

    # 2. Index Questions & Passages by Test ID
    print("\n[2/5] Indexing relationships & analyzing test structures...")
    questions_by_test = defaultdict(list)
    for q in raw_questions:
        t_id = to_guid(q['test_id'])
        questions_by_test[t_id].append(q)

    passages_by_test = defaultdict(list)
    for p in raw_passages:
        t_id = to_guid(p['test_id'])
        passages_by_test[t_id].append(p)

    # 3. Process Tests
    print("\n[3/5] Normalizing, classifying & standardizing Tests...")
    seen_codes = set()
    cleaned_tests = []
    tests_summary = []

    now_iso = datetime.utcnow().isoformat()

    for rt in raw_tests:
        test_guid = to_guid(rt['id'])
        title = rt['title'].strip()
        raw_code = (rt.get('code') or '').strip()
        test_questions = questions_by_test.get(test_guid, [])
        test_passages = passages_by_test.get(test_guid, [])

        # Count listening vs reading
        listening_q = sum(1 for q in test_questions if int(q.get('part') or 0) <= 4)
        reading_q = sum(1 for q in test_questions if int(q.get('part') or 0) >= 5)
        total_q = len(test_questions) if test_questions else int(rt.get('total_questions') or 200)

        # Part breakdown
        part_counts = Counter(int(q.get('part') or 0) for q in test_questions)

        # Classify
        category = classify_category(title, raw_code, part_counts)
        year = parse_year(title, raw_code)
        clean_code = generate_unique_code(title, raw_code, test_guid, seen_codes)

        # Audio URL for test (if test has full audio in metadata or passages)
        test_audio = None
        if rt.get('metadata') and isinstance(rt['metadata'], dict):
            test_audio = rt['metadata'].get('full_audio_url') or rt['metadata'].get('audio_url')

        clean_test = {
            "id": test_guid,
            "title": title,
            "code": clean_code,
            "description": rt.get('description') or None,
            "category": category,
            "year": year,
            "total_questions": total_q,
            "duration_minutes": int(rt.get('duration_minutes') or 120),
            "total_listening_questions": listening_q,
            "total_reading_questions": reading_q,
            "audio_url": test_audio,
            "is_active": bool(rt.get('is_active', True)),
            "status": "Active" if rt.get('is_active', True) else "Draft",
            "created_by_user_id": None,
            "external_id": test_guid,
            "source": "Studychill",
            "metadata": json.dumps(rt.get('metadata') or {}, ensure_ascii=False) if rt.get('metadata') else None,
            "deleted_at": None,
            "created_at": rt.get('created_at') or now_iso,
            "updated_at": rt.get('updated_at') or now_iso
        }
        cleaned_tests.append(clean_test)

        tests_summary.append({
            "id": test_guid,
            "code": clean_code,
            "title": title,
            "category": category,
            "year": year,
            "total_questions": total_q,
            "listening_questions": listening_q,
            "reading_questions": reading_q,
            "passages_count": len(test_passages),
            "is_active": clean_test['is_active'],
            "part_breakdown": dict(sorted(part_counts.items()))
        })

    # 4. Process Passages
    print("\n[4/5] Normalizing & validating Passages (Preserving Audio/Image links)...")
    cleaned_passages = []
    valid_test_guids = set(t['id'] for t in cleaned_tests)

    for rp in raw_passages:
        p_guid = to_guid(rp['id'])
        t_guid = to_guid(rp['test_id'])
        part = int(rp.get('part') or 0)

        # Audio and Image strictly preserved
        audio_url = (rp.get('audio_url') or '').strip() or None
        image_url = (rp.get('image_url') or '').strip() or None
        content = (rp.get('content') or '').strip() or None
        title = (rp.get('title') or '').strip() or None
        transcript = (rp.get('transcript') or '').strip() or None

        clean_passage = {
            "id": p_guid,
            "test_id": t_guid,
            "part": part,
            "passage_type": (rp.get('passage_type') or '').strip() or None,
            "title": title,
            "content": content,
            "audio_url": audio_url,
            "image_url": image_url,
            "transcript": transcript,
            "order_index": int(rp.get('order_index') or 0),
            "external_id": p_guid,
            "created_at": rp.get('created_at') or now_iso,
            "updated_at": rp.get('updated_at') or now_iso
        }
        cleaned_passages.append(clean_passage)

    # 5. Process Questions
    print("\n[5/5] Normalizing & validating Questions (Preserving Audio/Image links)...")
    cleaned_questions = []
    valid_passage_guids = set(p['id'] for p in cleaned_passages)

    for rq in raw_questions:
        q_guid = to_guid(rq['id'])
        t_guid = to_guid(rq['test_id'])
        p_guid = to_guid(rq.get('passage_id'))
        if p_guid and p_guid not in valid_passage_guids:
            p_guid = None  # Prevent foreign key orphan

        part = int(rq.get('part') or 0)
        section = "Listening" if part <= 4 else "Reading"

        # Audio and image strictly preserved
        audio_url = (rq.get('audio_url') or '').strip() or None
        image_url = (rq.get('image_url') or '').strip() or None

        q_num = rq.get('question_number')
        q_num_int = int(q_num) if q_num is not None else None

        # Clean answer
        raw_ans = (rq.get('correct_answer') or '').strip().upper()
        ans_match = re.search(r'[ABCD]', raw_ans)
        correct_ans = ans_match.group(0) if ans_match else (raw_ans[:1] if raw_ans else "A")

        # Options
        opt_a = (rq.get('option_a') or '').strip()
        opt_b = (rq.get('option_b') or '').strip()
        opt_c = (rq.get('option_c') or '').strip()
        opt_d = (rq.get('option_d') or '').strip()
        if part == 2:
            opt_d = None  # Standard TOEIC Part 2 only has 3 choices: A, B, C

        q_text = (rq.get('question_text') or '').strip() or None
        explanation = (rq.get('explanation') or '').strip() or None
        transcript = (rq.get('transcript') or '').strip() or None

        diff_level = map_difficulty(rq.get('difficulty_level'))

        clean_q = {
            "id": q_guid,
            "test_id": t_guid,
            "passage_id": p_guid,
            "part": part,
            "section": section,
            "question_number": q_num_int,
            "question_text": q_text,
            "audio_url": audio_url,
            "image_url": image_url,
            "option_a": opt_a,
            "option_b": opt_b,
            "option_c": opt_c,
            "option_d": opt_d,
            "correct_answer": correct_ans,
            "explanation": explanation,
            "transcript": transcript,
            "difficulty_level": diff_level,
            "topic": (rq.get('topic') or '').strip() or None,
            "status": "Active",
            "version": 1,
            "order_index": int(rq.get('order_index') or 0),
            "prefer_ai_explanation": bool(rq.get('prefer_ai_explanation', False)),
            "created_by_user_id": None,
            "external_id": q_guid,
            "deleted_at": None,
            "created_at": rq.get('created_at') or now_iso,
            "updated_at": rq.get('updated_at') or now_iso
        }
        cleaned_questions.append(clean_q)

    # Save Outputs
    print("\n[Output] Writing standardized datasets to disk...")
    
    # 1. Tests JSON
    tests_out = os.path.join(SEEDS_DIR, 'toeic_tests.json')
    with open(tests_out, 'w', encoding='utf-8') as f:
        json.dump(cleaned_tests, f, ensure_ascii=False, indent=2)
    print(f"  * Written {len(cleaned_tests)} tests -> {tests_out}")

    # 2. Tests Summary Catalog JSON
    summary_out = os.path.join(SEEDS_DIR, 'toeic_tests_summary.json')
    with open(summary_out, 'w', encoding='utf-8') as f:
        json.dump(tests_summary, f, ensure_ascii=False, indent=2)
    print(f"  * Written tests summary catalog -> {summary_out}")

    # 3. Passages JSON (compressed & uncompressed)
    passages_out_gz = os.path.join(SEEDS_DIR, 'toeic_passages.json.gz')
    with gzip.open(passages_out_gz, 'wt', encoding='utf-8') as f:
        json.dump(cleaned_passages, f, ensure_ascii=False)
    print(f"  * Written {len(cleaned_passages)} passages -> {passages_out_gz}")

    # 4. Questions JSON (compressed)
    questions_out_gz = os.path.join(SEEDS_DIR, 'toeic_questions.json.gz')
    with gzip.open(questions_out_gz, 'wt', encoding='utf-8') as f:
        json.dump(cleaned_questions, f, ensure_ascii=False)
    print(f"  * Written {len(cleaned_questions)} questions -> {questions_out_gz}")

    # 5. Generate Production-Grade Seed SQL Script
    print("\n[Output] Generating SQL Seed Script (toeic_space_assessment_seed.sql.gz)...")
    sql_out_gz = os.path.join(SEEDS_DIR, 'toeic_space_assessment_seed.sql.gz')
    with gzip.open(sql_out_gz, 'wt', encoding='utf-8') as sf:
        sf.write("-- ====================================================================\n")
        sf.write("-- TOEIC SPACE ASSESSMENT SEED DATA\n")
        sf.write(f"-- Generated At: {now_iso}\n")
        sf.write(f"-- Tests: {len(cleaned_tests)} | Passages: {len(cleaned_passages)} | Questions: {len(cleaned_questions)}\n")
        sf.write("-- ====================================================================\n\n")
        sf.write("SET NAMES utf8mb4;\n")
        sf.write("SET FOREIGN_KEY_CHECKS = 0;\n\n")

        # Tests
        sf.write("-- --------------------------------------------------------------------\n")
        sf.write("-- 1. ToeicTests\n")
        sf.write("-- --------------------------------------------------------------------\n")
        for t in cleaned_tests:
            sf.write(f"INSERT INTO `ToeicTests` (`Id`, `Title`, `Code`, `Description`, `Category`, `Year`, `TotalQuestions`, `DurationMinutes`, `TotalListeningQuestions`, `TotalReadingQuestions`, `AudioUrl`, `IsActive`, `Status`, `CreatedByUserId`, `ExternalId`, `Source`, `Metadata`, `DeletedAt`, `CreatedAt`, `UpdatedAt`) VALUES ({escape_sql(t['id'])}, {escape_sql(t['title'])}, {escape_sql(t['code'])}, {escape_sql(t['description'])}, {escape_sql(t['category'])}, {escape_sql(t['year'])}, {t['total_questions']}, {t['duration_minutes']}, {t['total_listening_questions']}, {t['total_reading_questions']}, {escape_sql(t['audio_url'])}, {escape_sql(t['is_active'])}, {escape_sql(t['status'])}, {escape_sql(t['created_by_user_id'])}, {escape_sql(t['external_id'])}, {escape_sql(t['source'])}, {escape_sql(t['metadata'])}, {escape_sql(t['deleted_at'])}, {escape_sql(t['created_at'])}, {escape_sql(t['updated_at'])});\n")

        # Passages
        sf.write("\n-- --------------------------------------------------------------------\n")
        sf.write("-- 2. ToeicPassages\n")
        sf.write("-- --------------------------------------------------------------------\n")
        batch_size = 500
        for i in range(0, len(cleaned_passages), batch_size):
            batch = cleaned_passages[i:i+batch_size]
            sf.write("INSERT INTO `ToeicPassages` (`Id`, `TestId`, `Part`, `PassageType`, `Title`, `Content`, `AudioUrl`, `ImageUrl`, `Transcript`, `OrderIndex`, `ExternalId`, `CreatedAt`, `UpdatedAt`) VALUES\n")
            row_strs = []
            for p in batch:
                row_strs.append(f"({escape_sql(p['id'])}, {escape_sql(p['test_id'])}, {p['part']}, {escape_sql(p['passage_type'])}, {escape_sql(p['title'])}, {escape_sql(p['content'])}, {escape_sql(p['audio_url'])}, {escape_sql(p['image_url'])}, {escape_sql(p['transcript'])}, {p['order_index']}, {escape_sql(p['external_id'])}, {escape_sql(p['created_at'])}, {escape_sql(p['updated_at'])})")
            sf.write(",\n".join(row_strs) + ";\n\n")

        # Questions
        sf.write("-- --------------------------------------------------------------------\n")
        sf.write("-- 3. ToeicQuestions\n")
        sf.write("-- --------------------------------------------------------------------\n")
        for i in range(0, len(cleaned_questions), batch_size):
            batch = cleaned_questions[i:i+batch_size]
            sf.write("INSERT INTO `ToeicQuestions` (`Id`, `TestId`, `PassageId`, `Part`, `Section`, `QuestionNumber`, `QuestionText`, `AudioUrl`, `ImageUrl`, `OptionA`, `OptionB`, `OptionC`, `OptionD`, `CorrectAnswer`, `Explanation`, `Transcript`, `DifficultyLevel`, `Topic`, `Status`, `Version`, `OrderIndex`, `PreferAiExplanation`, `CreatedByUserId`, `ExternalId`, `DeletedAt`, `CreatedAt`, `UpdatedAt`) VALUES\n")
            row_strs = []
            for q in batch:
                row_strs.append(f"({escape_sql(q['id'])}, {escape_sql(q['test_id'])}, {escape_sql(q['passage_id'])}, {q['part']}, {escape_sql(q['section'])}, {escape_sql(q['question_number'])}, {escape_sql(q['question_text'])}, {escape_sql(q['audio_url'])}, {escape_sql(q['image_url'])}, {escape_sql(q['option_a'])}, {escape_sql(q['option_b'])}, {escape_sql(q['option_c'])}, {escape_sql(q['option_d'])}, {escape_sql(q['correct_answer'])}, {escape_sql(q['explanation'])}, {escape_sql(q['transcript'])}, {q['difficulty_level']}, {escape_sql(q['topic'])}, {escape_sql(q['status'])}, {q['version']}, {q['order_index']}, {escape_sql(q['prefer_ai_explanation'])}, {escape_sql(q['created_by_user_id'])}, {escape_sql(q['external_id'])}, {escape_sql(q['deleted_at'])}, {escape_sql(q['created_at'])}, {escape_sql(q['updated_at'])})")
            sf.write(",\n".join(row_strs) + ";\n\n")

        sf.write("SET FOREIGN_KEY_CHECKS = 1;\n")
        sf.write("-- SEED COMPLETE\n")

    print(f"  * Generated SQL Seed script -> {sql_out_gz} ({os.path.getsize(sql_out_gz) / (1024*1024):.2f} MB)")
    print("\nSUCCESS: Data transformation pipeline completed successfully!")

if __name__ == '__main__':
    main()
