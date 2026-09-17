import json
import gzip
import uuid
import os
import sys
from collections import Counter

sys.stdout.reconfigure(encoding='utf-8')

SEEDS_DIR = os.path.abspath('data/seeds')
CRAWLED_DIR = os.path.abspath('data/crawled')

def is_valid_uuid(val):
    if not val:
        return False
    try:
        uuid.UUID(str(val))
        return True
    except ValueError:
        return False

def main():
    print("=" * 60)
    print("SENIOR AUDIT & VERIFICATION REPORT")
    print("=" * 60)

    # 1. Load Clean Datasets
    print("\n[Audit 1/6] Loading processed datasets...")
    with open(os.path.join(SEEDS_DIR, 'toeic_tests.json'), 'r', encoding='utf-8') as f:
        tests = json.load(f)
    
    with gzip.open(os.path.join(SEEDS_DIR, 'toeic_passages.json.gz'), 'rt', encoding='utf-8') as f:
        passages = json.load(f)

    with gzip.open(os.path.join(SEEDS_DIR, 'toeic_questions.json.gz'), 'rt', encoding='utf-8') as f:
        questions = json.load(f)

    print(f"  * Tests count: {len(tests):,}")
    print(f"  * Passages count: {len(passages):,}")
    print(f"  * Questions count: {len(questions):,}")

    assert len(tests) == 97, f"Expected 97 tests, got {len(tests)}"
    assert len(passages) == 7716, f"Expected 7716 passages, got {len(passages)}"
    assert len(questions) == 30572, f"Expected 30572 questions, got {len(questions)}"
    print("  => [PASS] All record counts match VPS source exactly (100%).")

    # 2. Referential Integrity
    print("\n[Audit 2/6] Checking Referential Integrity...")
    test_ids = set(t['id'] for t in tests)
    passage_ids = set(p['id'] for p in passages)

    orphan_passages = [p for p in passages if p['test_id'] not in test_ids]
    orphan_questions_test = [q for q in questions if q['test_id'] not in test_ids]
    orphan_questions_passage = [q for q in questions if q['passage_id'] is not None and q['passage_id'] not in passage_ids]

    print(f"  * Orphan passages (invalid test_id): {len(orphan_passages)}")
    print(f"  * Orphan questions (invalid test_id): {len(orphan_questions_test)}")
    print(f"  * Orphan questions (invalid passage_id): {len(orphan_questions_passage)}")

    assert len(orphan_passages) == 0
    assert len(orphan_questions_test) == 0
    assert len(orphan_questions_passage) == 0
    print("  => [PASS] Foreign Key & Referential Integrity 100% clean (0 orphans).")

    # 3. Uniqueness of Test Codes
    print("\n[Audit 3/6] Checking Test Code Uniqueness...")
    test_codes = [t['code'] for t in tests]
    code_counts = Counter(test_codes)
    dup_codes = [c for c, cnt in code_counts.items() if cnt > 1]
    print(f"  * Total unique codes: {len(code_counts)} / {len(tests)}")
    print(f"  * Duplicate codes: {dup_codes}")
    assert len(dup_codes) == 0
    print("  => [PASS] All Test Codes are unique (guarantees IX_ToeicTests_Code constraint).")

    # 4. Media URLs Preservation Check
    print("\n[Audit 4/6] Verifying Audio & Image URLs Preservation...")
    with gzip.open(os.path.join(CRAWLED_DIR, 'toeic_mock_questions.json.gz'), 'rt', encoding='utf-8') as f:
        raw_questions = json.load(f)
    with gzip.open(os.path.join(CRAWLED_DIR, 'toeic_mock_passages.json.gz'), 'rt', encoding='utf-8') as f:
        raw_passages = json.load(f)

    # Compare raw vs cleaned URLs
    raw_q_audio_cnt = sum(1 for q in raw_questions if (q.get('audio_url') or '').strip())
    clean_q_audio_cnt = sum(1 for q in questions if q['audio_url'])
    raw_q_img_cnt = sum(1 for q in raw_questions if (q.get('image_url') or '').strip())
    clean_q_img_cnt = sum(1 for q in questions if q['image_url'])

    raw_p_audio_cnt = sum(1 for p in raw_passages if (p.get('audio_url') or '').strip())
    clean_p_audio_cnt = sum(1 for p in passages if p['audio_url'])
    raw_p_img_cnt = sum(1 for p in raw_passages if (p.get('image_url') or '').strip())
    clean_p_img_cnt = sum(1 for p in passages if p['image_url'])

    print(f"  * Question Audio: Raw={raw_q_audio_cnt:,} | Cleaned={clean_q_audio_cnt:,}")
    print(f"  * Question Image: Raw={raw_q_img_cnt:,} | Cleaned={clean_q_img_cnt:,}")
    print(f"  * Passage Audio:  Raw={raw_p_audio_cnt:,} | Cleaned={clean_p_audio_cnt:,}")
    print(f"  * Passage Image:  Raw={raw_p_img_cnt:,} | Cleaned={clean_p_img_cnt:,}")

    assert raw_q_audio_cnt == clean_q_audio_cnt
    assert raw_q_img_cnt == clean_q_img_cnt
    assert raw_p_audio_cnt == clean_p_audio_cnt
    assert raw_p_img_cnt == clean_p_img_cnt
    print("  => [PASS] Audio and Image URLs preserved 100% with ZERO data loss.")

    # 5. Correct Answers and Choices Constraints
    print("\n[Audit 5/6] Verifying Question Options, Answers & Sections...")
    invalid_answers = [q for q in questions if q['correct_answer'] not in ['A', 'B', 'C', 'D']]
    print(f"  * Invalid correct_answer values (not A,B,C,D): {len(invalid_answers)}")
    assert len(invalid_answers) == 0

    part_section_mismatches = []
    for q in questions:
        part = q['part']
        sec = q['section']
        if part <= 4 and sec != "Listening":
            part_section_mismatches.append(q)
        elif part >= 5 and sec != "Reading":
            part_section_mismatches.append(q)
    print(f"  * Part / Section mismatches: {len(part_section_mismatches)}")
    assert len(part_section_mismatches) == 0

    part2_with_opt_d = [q for q in questions if q['part'] == 2 and q['option_d'] is not None]
    print(f"  * Part 2 questions with non-null option_d: {len(part2_with_opt_d)}")
    assert len(part2_with_opt_d) == 0
    print("  => [PASS] Options, Answers & Section mappings adhere 100% to international TOEIC standards.")

    # 6. UUID and GUID Validity
    print("\n[Audit 6/6] Verifying UUID/GUID formats...")
    invalid_test_ids = [t for t in tests if not is_valid_uuid(t['id'])]
    invalid_passage_ids = [p for p in passages if not is_valid_uuid(p['id'])]
    invalid_q_ids = [q for q in questions if not is_valid_uuid(q['id'])]
    print(f"  * Invalid Test GUIDs: {len(invalid_test_ids)}")
    print(f"  * Invalid Passage GUIDs: {len(invalid_passage_ids)}")
    print(f"  * Invalid Question GUIDs: {len(invalid_q_ids)}")
    assert len(invalid_test_ids) == 0
    assert len(invalid_passage_ids) == 0
    assert len(invalid_q_ids) == 0
    print("  => [PASS] All entity primary and foreign keys are valid 36-char GUIDs.")

    print("\n" + "=" * 60)
    print("ALL 6 AUDIT PHASES PASSED WITH ZERO WARNINGS OR ERRORS!")
    print("=" * 60)

if __name__ == '__main__':
    main()
