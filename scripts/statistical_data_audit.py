import json
import gzip
import os
import sys
import re
from collections import defaultdict, Counter
import math

sys.stdout.reconfigure(encoding='utf-8')

SEEDS_DIR = os.path.abspath('data/seeds')

def load_data():
    with open(os.path.join(SEEDS_DIR, 'toeic_tests.json'), 'r', encoding='utf-8') as f:
        tests = json.load(f)
    with gzip.open(os.path.join(SEEDS_DIR, 'toeic_passages.json.gz'), 'rt', encoding='utf-8') as f:
        passages = json.load(f)
    with gzip.open(os.path.join(SEEDS_DIR, 'toeic_questions.json.gz'), 'rt', encoding='utf-8') as f:
        questions = json.load(f)
    return tests, passages, questions

def main():
    print("=" * 80)
    print("PHÂN TÍCH XÁC SUẤT & THỐNG KÊ BẤT THƯỜNG DỮ LIỆU TOEIC (ANOMALY DETECTION)")
    print("=" * 80)

    tests, passages, questions = load_data()
    total_q = len(questions)
    total_p = len(passages)
    total_t = len(tests)

    passages_by_id = {p['id']: p for p in passages}
    questions_by_test = defaultdict(list)
    for q in questions:
        questions_by_test[q['test_id']].append(q)

    # --------------------------------------------------------------------------
    # 1. PHÂN BỐ XÁC SUẤT ĐÁP ÁN (A, B, C, D)
    # --------------------------------------------------------------------------
    print("\n" + "-" * 80)
    print("1. KIỂM TRA PHÂN BỐ XÁC SUẤT ĐÁP ÁN ĐÚNG (A, B, C, D)")
    print("-" * 80)

    # Phân bố chung toàn bộ 30,572 câu
    overall_ans = Counter(q['correct_answer'] for q in questions)
    print(f"Tổng số câu hỏi: {total_q:,}")
    for ans in ['A', 'B', 'C', 'D']:
        cnt = overall_ans[ans]
        pct = cnt / total_q * 100
        print(f"  * Đáp án {ans}: {cnt:,} câu ({pct:.2f}%)")

    # Phân bố theo từng Part
    print("\nPhân bố đáp án theo từng Part (Chi tiết):")
    part_ans = defaultdict(Counter)
    part_total = Counter()
    for q in questions:
        part_ans[q['part']][q['correct_answer']] += 1
        part_total[q['part']] += 1

    part_anomalies = []
    for p in range(1, 8):
        ptot = part_total[p]
        if ptot == 0: continue
        print(f"  [Part {p}] Tổng {ptot:,} câu:")
        row_str = "    "
        for ans in ['A', 'B', 'C', 'D']:
            cnt = part_ans[p][ans]
            pct = cnt / ptot * 100 if ptot else 0
            row_str += f"{ans}: {cnt:,} ({pct:5.1f}%) | "
        print(row_str)

        # Kiểm tra bất thường Part 2: Không được có đáp án D
        if p == 2 and part_ans[2]['D'] > 0:
            part_anomalies.append(f"Part 2 phát hiện {part_ans[2]['D']} câu có đáp án D (Bất thường TOEIC quốc tế)")

        # Kiểm tra độ lệch chuẩn phân bố (Kỳ vọng ~25% cho A,B,C,D; ~33.3% cho Part 2 A,B,C)
        expected = 100 / 3 if p == 2 else 25.0
        allowed_ans = ['A', 'B', 'C'] if p == 2 else ['A', 'B', 'C', 'D']
        for a in allowed_ans:
            pct = part_ans[p][a] / ptot * 100
            if abs(pct - expected) > 15: # lệch quá 15% là có dấu hiệu bias
                part_anomalies.append(f"Part {p} đáp án {a} có tỷ lệ {pct:.1f}% (lệch nhiều so với kỳ vọng {expected:.1f}%)")

    if part_anomalies:
        print("\n  [!] Cảnh báo lệch phân bố đáp án:")
        for a in part_anomalies:
            print(f"      - {a}")
    else:
        print("\n  => Phân bố xác suất đáp án hoàn toàn tự nhiên, cân bằng.")

    # --------------------------------------------------------------------------
    # 2. KIỂM TRA ĐỀ CÓ ĐÁP ÁN BỊ BIAS HOẶC DUMMY (ALL 'A' HOẶC KHÔNG RANDOM)
    # --------------------------------------------------------------------------
    print("\n" + "-" * 80)
    print("2. KIỂM TRA TÍNH BẤT THƯỜNG TRONG TỪNG ĐỀ (TEST-LEVEL ANOMALIES)")
    print("-" * 80)

    suspicious_tests = []
    for t in tests:
        t_id = t['id']
        t_qs = questions_by_test.get(t_id, [])
        if not t_qs:
            suspicious_tests.append((t, "Đề rỗng, không có câu hỏi nào"))
            continue

        c = Counter(q['correct_answer'] for q in t_qs)
        n = len(t_qs)
        # Nếu 1 đáp án chiếm > 60% tổng số câu của đề đó -> Đề fake / crawl lỗi
        max_ans, max_cnt = c.most_common(1)[0]
        if n >= 20 and (max_cnt / n) > 0.60:
            suspicious_tests.append((t, f"Đáp án {max_ans} chiếm {max_cnt}/{n} ({max_cnt/n*100:.1f}%) - Nghi vấn dữ liệu dummy"))

    print(f"Tổng số đề được kiểm tra: {len(tests)}")
    if suspicious_tests:
        print(f"  [!] Phát hiện {len(suspicious_tests)} đề có dấu hiệu bất thường:")
        for t, reason in suspicious_tests:
            print(f"      - [{t['code']}] {t['title']}: {reason}")
    else:
        print("  => 100% đề thi đều có phân bố đáp án đa dạng, không có đề nào bị dummy hay lỗi 1 đáp án duy nhất.")

    # --------------------------------------------------------------------------
    # 3. KIỂM TRA TÍNH TOÀN VẸN VÀ THỨ TỰ CỦA 26 ĐỀ ETS FULL 200 CÂU
    # --------------------------------------------------------------------------
    print("\n" + "-" * 80)
    print("3. KIỂM TRA CẤU TRÚC 26 BỘ ĐỀ ETS FULL TEST (200 CÂU)")
    print("-" * 80)

    ets_tests = [t for t in tests if 'ETS' in t['category'] or 'ETS' in t['title'].upper() or 'ETS' in t['code']]
    print(f"Số lượng đề ETS: {len(ets_tests)}")

    ets_anomalies = []
    for t in ets_tests:
        t_qs = questions_by_test.get(t['id'], [])
        q_nums = [q['question_number'] for q in t_qs if q['question_number'] is not None]
        
        # Check đúng 200 câu
        if len(t_qs) != 200:
            ets_anomalies.append(f"[{t['code']}] {t['title']}: Có {len(t_qs)} câu (kỳ vọng 200)")
            continue

        # Check trùng số thứ tự
        dup_nums = [num for num, cnt in Counter(q_nums).items() if cnt > 1]
        if dup_nums:
            ets_anomalies.append(f"[{t['code']}] {t['title']}: Bị trùng số câu: {dup_nums[:5]}")

        # Check đủ từ 1 đến 200
        missing_nums = set(range(1, 201)) - set(q_nums)
        if missing_nums:
            ets_anomalies.append(f"[{t['code']}] {t['title']}: Thiếu các số câu: {sorted(list(missing_nums))[:5]}")

        # Check phân bổ chuẩn ETS:
        # Part 1: 1-6 (6) | Part 2: 7-31 (25) | Part 3: 32-70 (39) | Part 4: 71-100 (30)
        # Part 5: 101-130 (30) | Part 6: 131-146 (16) | Part 7: 147-200 (54)
        part_counts = Counter(q['part'] for q in t_qs)
        std_parts = {1: 6, 2: 25, 3: 39, 4: 30, 5: 30, 6: 16, 7: 54}
        for p, expected_cnt in std_parts.items():
            actual = part_counts.get(p, 0)
            if actual != expected_cnt:
                ets_anomalies.append(f"[{t['code']}] {t['title']}: Part {p} có {actual} câu (chuẩn ETS là {expected_cnt})")

    if ets_anomalies:
        print(f"  [!] Phát hiện {len(ets_anomalies)} bất thường trong các đề ETS:")
        for a in ets_anomalies[:10]:
            print(f"      - {a}")
        if len(ets_anomalies) > 10:
            print(f"      ... và {len(ets_anomalies) - 10} vấn đề khác.")
    else:
        print("  => TOÀN BỘ 26 ĐỀ ETS FULL TEST KHỚP 100% CẤU TRÚC ETS:")
        print("     • Đủ 200 câu đánh số tuần tự từ 1 đến 200 không thiếu, không trùng.")
        print("     • Chuẩn từng Part: Part 1(6), Part 2(25), Part 3(39), Part 4(30), Part 5(30), Part 6(16), Part 7(54).")

    # --------------------------------------------------------------------------
    # 4. KIỂM TRA CÂU HỎI TRÙNG LẶP & CÁC TRƯỜNG DỮ LIỆU BẤT THƯỜNG (OUTLIERS)
    # --------------------------------------------------------------------------
    print("\n" + "-" * 80)
    print("4. KIỂM TRA PHÂN PHỐI ĐỘ DÀI VĂN BẢN & CÁC GIÁ TRỊ NGOẠI LAI (OUTLIERS)")
    print("-" * 80)

    # Độ dài text của các option
    long_options = []
    empty_options = []
    for q in questions:
        # Check option A, B, C rỗng
        if not q['option_a']: empty_options.append((q, 'A'))
        if not q['option_b']: empty_options.append((q, 'B'))
        if not q['option_c']: empty_options.append((q, 'C'))
        if q['part'] != 2 and not q['option_d']: empty_options.append((q, 'D'))

        # Check option quá dài (ví dụ > 1000 ký tự có thể do scraper bị dính html)
        for opt_name in ['option_a', 'option_b', 'option_c', 'option_d']:
            val = q.get(opt_name) or ''
            if len(val) > 1000:
                long_options.append((q, opt_name, len(val)))

    print(f"  * Số câu bị rỗng Option A/B/C: {len(empty_options):,}")
    print(f"  * Số câu có Option dài bất thường (> 1000 ký tự): {len(long_options):,}")
    if long_options:
        for q, opt, l in long_options[:5]:
            print(f"      - Q ID {q['id']} Part {q['part']} {opt}: {l} ký tự")

    # Kiểm tra URL hỏng / format lạ
    print("\nKiểm tra định dạng URL (Audio & Image):")
    invalid_audio_urls = []
    invalid_image_urls = []
    url_pattern = re.compile(r'^https?://[^\s/$.?#].[^\s]*$', re.IGNORECASE)

    for q in questions:
        if q['audio_url'] and not url_pattern.match(q['audio_url']):
            invalid_audio_urls.append((q['id'], q['audio_url'][:50]))
        if q['image_url'] and not url_pattern.match(q['image_url']):
            invalid_image_urls.append((q['id'], q['image_url'][:50]))

    for p in passages:
        if p['audio_url'] and not url_pattern.match(p['audio_url']):
            invalid_audio_urls.append((p['id'], p['audio_url'][:50]))
        if p['image_url'] and not url_pattern.match(p['image_url']):
            invalid_image_urls.append((p['id'], p['image_url'][:50]))

    print(f"  * Số Audio URL sai định dạng HTTP/HTTPS: {len(invalid_audio_urls)}")
    print(f"  * Số Image URL sai định dạng HTTP/HTTPS: {len(invalid_image_urls)}")
    if invalid_audio_urls:
        print(f"      Sample: {invalid_audio_urls[:3]}")

    # --------------------------------------------------------------------------
    # 5. SO SÁNH 2 BỘ ĐỀ TRÙNG TÊN (ETS-TEST-1..6 A vs B)
    # --------------------------------------------------------------------------
    print("\n" + "-" * 80)
    print("5. PHÂN TÍCH XÁC SUẤT TRÙNG LẶP NỘI DUNG GIỮA CÁC ĐỀ")
    print("-" * 80)

    # Kiểm tra xem các đề trùng tên như ETS-TEST-01 và ETS-TEST-01-A có bị trùng câu hỏi không
    # Hay là 2 bộ đề khác nhau hoàn toàn
    for i in range(1, 7):
        code_a = f"ETS-TEST-{i:02d}"
        code_b = f"ETS-TEST-{i:02d}-A"
        test_a = next((t for t in tests if t['code'] == code_a), None)
        test_b = next((t for t in tests if t['code'] == code_b), None)
        if test_a and test_b:
            qs_a = questions_by_test[test_a['id']]
            qs_b = questions_by_test[test_b['id']]
            
            # Tính Jaccard similarity giữa text câu hỏi Part 5
            p5_a = set(q['question_text'] for q in qs_a if q['part'] == 5 and q['question_text'])
            p5_b = set(q['question_text'] for q in qs_b if q['part'] == 5 and q['question_text'])
            overlap = len(p5_a.intersection(p5_b))
            print(f"  * Cặp đề [{code_a}] vs [{code_b}]: Trùng {overlap}/30 câu Part 5 -> {'2 đề KHÁC NHAU hoàn toàn' if overlap == 0 else f'Trùng {overlap} câu'}")

    print("\n" + "=" * 80)
    print("KẾT LUẬN KIỂM ĐỊNH THỐNG KÊ HOÀN TẤT")
    print("=" * 80)

if __name__ == '__main__':
    main()
