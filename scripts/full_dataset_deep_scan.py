import json
import gzip
import os
import sys
import re
from collections import defaultdict, Counter

sys.stdout.reconfigure(encoding='utf-8')
SEEDS_DIR = os.path.abspath('data/seeds')

with open(os.path.join(SEEDS_DIR, 'toeic_tests.json'), 'r', encoding='utf-8') as f:
    tests = json.load(f)
with gzip.open(os.path.join(SEEDS_DIR, 'toeic_passages.json.gz'), 'rt', encoding='utf-8') as f:
    passages = json.load(f)
with gzip.open(os.path.join(SEEDS_DIR, 'toeic_questions.json.gz'), 'rt', encoding='utf-8') as f:
    questions = json.load(f)

passages_map = {p['id']: p for p in passages}
q_by_test = defaultdict(list)
for q in questions:
    q_by_test[q['test_id']].append(q)
p_by_test = defaultdict(list)
for p in passages:
    p_by_test[p['test_id']].append(p)

print("=" * 80)
print("BÁO CÁO QUÉT DIỆN RỘNG TOÀN BỘ CƠ SỞ DỮ LIỆU STUDYCHILL (97 ĐỀ, 30,572 CÂU)")
print("=" * 80)

# ==============================================================================
# PHẦN 1: QUÉT TOÀN BỘ ĐƯỜNG DẪN MEDIA (AUDIO & IMAGE)
# ==============================================================================
print("\n" + "=" * 80)
print("PHẦN 1: PHÂN TÍCH ĐƯỜNG DẪN AUDIO VÀ IMAGE (TÌNH TRẠNG STORAGE / CDN)")
print("=" * 80)

q_audio_http = sum(1 for q in questions if q['audio_url'] and q['audio_url'].startswith('http'))
q_audio_rel = sum(1 for q in questions if q['audio_url'] and not q['audio_url'].startswith('http'))
q_audio_null = sum(1 for q in questions if not q['audio_url'])

q_img_http = sum(1 for q in questions if q['image_url'] and q['image_url'].startswith('http'))
q_img_rel = sum(1 for q in questions if q['image_url'] and not q['image_url'].startswith('http'))
q_img_null = sum(1 for q in questions if not q['image_url'])

p_audio_http = sum(1 for p in passages if p['audio_url'] and p['audio_url'].startswith('http'))
p_audio_rel = sum(1 for p in passages if p['audio_url'] and not p['audio_url'].startswith('http'))
p_audio_null = sum(1 for p in passages if not p['audio_url'])

p_img_http = sum(1 for p in passages if p['image_url'] and p['image_url'].startswith('http'))
p_img_rel = sum(1 for p in passages if p['image_url'] and not p['image_url'].startswith('http'))
p_img_null = sum(1 for p in passages if not p['image_url'])

print(f"1. Audio câu hỏi (ToeicQuestions - {len(questions):,} câu):")
print(f"   • Đã có link CDN Cloudflare R2 (HTTP/HTTPS) : {q_audio_http:,} câu ({q_audio_http/len(questions)*100:.1f}%)")
print(f"   • Link cục bộ (Chỉ có tên file, thiếu domain): {q_audio_rel:,} câu ({q_audio_rel/len(questions)*100:.1f}%)")
print(f"   • Không có audio (chủ yếu là Reading P5,6,7)  : {q_audio_null:,} câu ({q_audio_null/len(questions)*100:.1f}%)")

print(f"\n2. Hình ảnh câu hỏi (ToeicQuestions):")
print(f"   • Đã có link CDN Cloudflare R2 (HTTP/HTTPS) : {q_img_http:,} câu")
print(f"   • Link cục bộ (Chỉ có tên file, thiếu domain): {q_img_rel:,} câu")
print(f"   • Không có hình ảnh                          : {q_img_null:,} câu")

print(f"\n3. Audio đoạn văn (ToeicPassages - {len(passages):,} bài đọc/hội thoại):")
print(f"   • Đã có link CDN Cloudflare R2 (HTTP/HTTPS) : {p_audio_http:,} đoạn")
print(f"   • Link cục bộ (Chỉ có tên file, thiếu domain): {p_audio_rel:,} đoạn")
print(f"   • Không có audio                             : {p_audio_null:,} đoạn")

print(f"\n4. Hình ảnh đoạn văn (ToeicPassages):")
print(f"   • Đã có link CDN Cloudflare R2 (HTTP/HTTPS) : {p_img_http:,} đoạn")
print(f"   • Link cục bộ (Chỉ có tên file, thiếu domain): {p_img_rel:,} đoạn")
print(f"   • Không có hình ảnh                          : {p_img_null:,} đoạn")

# ==============================================================================
# PHẦN 2: DANH SÁCH CÁC ĐỀ BỊ THIẾU DOMAIN STORAGE (LINK CỤC BỘ)
# ==============================================================================
print("\n" + "=" * 80)
print("PHẦN 2: DANH SÁCH ĐỀ BỊ DÍNH LINK CỤC BỘ (CẦN BÁO STUDYCHILL BỔ SUNG PREFIX)")
print("=" * 80)

tests_with_rel_audio = []
for t in tests:
    t_qs = q_by_test[t['id']]
    rel_q_aud = sum(1 for q in t_qs if q['audio_url'] and not q['audio_url'].startswith('http'))
    rel_q_img = sum(1 for q in t_qs if q['image_url'] and not q['image_url'].startswith('http'))
    t_ps = p_by_test[t['id']]
    rel_p_aud = sum(1 for p in t_ps if p['audio_url'] and not p['audio_url'].startswith('http'))
    rel_p_img = sum(1 for p in t_ps if p['image_url'] and not p['image_url'].startswith('http'))

    if rel_q_aud > 0 or rel_q_img > 0 or rel_p_aud > 0 or rel_p_img > 0:
        tests_with_rel_audio.append({
            "code": t['code'],
            "title": t['title'],
            "category": t['category'],
            "total_q": len(t_qs),
            "rel_q_audio": rel_q_aud,
            "rel_q_img": rel_q_img,
            "rel_p_audio": rel_p_aud,
            "rel_p_img": rel_p_img,
            "sample_audio": [q['audio_url'] for q in t_qs if q['audio_url'] and not q['audio_url'].startswith('http')][:2],
            "sample_img": [q['image_url'] for q in t_qs if q['image_url'] and not q['image_url'].startswith('http')][:2],
        })

print(f"Tổng số bộ đề bị dính link cục bộ: {len(tests_with_rel_audio)} / 97 đề.")
print(f"{'Mã Đề':<22} | {'Tổng câu':<8} | {'Audio thiếu domain':<18} | {'Ảnh thiếu domain':<16} | Tên Bộ Đề")
print("-" * 95)
for item in tests_with_rel_audio:
    print(f"{item['code']:<22} | {item['total_q']:<8} | {item['rel_q_audio']:<18} | {item['rel_q_img']:<16} | {item['title'][:40]}")

# ==============================================================================
# PHẦN 3: KIỂM TRA LỖI NGHIỆP VỤ BÀI THI (CRITICAL DEFECTS)
# ==============================================================================
print("\n" + "=" * 80)
print("PHẦN 3: CÁC LỖI DỮ LIỆU NGHIÊM TRỌNG (CRITICAL DEFECTS)")
print("=" * 80)

# 1. Đề rỗng (0 câu hỏi)
empty_tests = [t for t in tests if len(q_by_test[t['id']]) == 0]
print(f"1. Đề thi hoàn toàn rỗng (0 câu hỏi): {len(empty_tests)} đề")
for t in empty_tests:
    print(f"   [!] Mã: {t['code']} | Tiêu đề: '{t['title']}' | Khai báo: {t['total_questions']} câu nhưng có {len(p_by_test[t['id']])} đoạn văn, 0 câu hỏi!")

# 2. Câu hỏi Listening bị câm (Không có audio câu hỏi, cũng không có audio bài đọc)
silent_listening_questions = []
for q in questions:
    if q['part'] <= 4:
        has_q_audio = bool(q['audio_url'])
        p = passages_map.get(q.get('passage_id'))
        has_p_audio = bool(p and p.get('audio_url'))
        if not has_q_audio and not has_p_audio:
            silent_listening_questions.append(q)

print(f"\n2. Câu hỏi Listening bị 'câm' (Part 1-4 nhưng KHÔNG có Audio): {len(silent_listening_questions)} câu")
if silent_listening_questions:
    # Group by test
    silent_by_test = Counter(q['test_id'] for q in silent_listening_questions)
    print("   Các đề bị ảnh hưởng:")
    for t_id, cnt in silent_by_test.most_common(5):
        t = next((t for t in tests if t['id'] == t_id), None)
        print(f"   • [{t['code'] if t else '?'}] {t['title'] if t else '?'}: {cnt} câu listening không có audio!")

# 3. Câu hỏi Part 7 (Đọc hiểu) bị mất đoạn văn liên kết (No passage)
p7_no_passage = [q for q in questions if q['part'] == 7 and (not q.get('passage_id') or not passages_map.get(q['passage_id']) or not (passages_map[q['passage_id']].get('content') or '').strip())]
print(f"\n3. Câu hỏi Part 7 không có bài đọc / bài đọc rỗng: {len(p7_no_passage)} câu")

# 4. Câu hỏi Part 1 (Mô tả tranh) bị mất hình ảnh (No image)
p1_no_image = [q for q in questions if q['part'] == 1 and not q.get('image_url')]
print(f"\n4. Câu hỏi Part 1 không có hình ảnh: {len(p1_no_image)} câu")

# 5. Các đề bị lệch đáp án bất thường (Answer bias > 55%)
biased_tests = []
for t in tests:
    t_qs = q_by_test[t['id']]
    if len(t_qs) >= 20:
        c = Counter(q['correct_answer'] for q in t_qs)
        top_ans, top_cnt = c.most_common(1)[0]
        ratio = top_cnt / len(t_qs)
        if ratio > 0.50: # Chiếm hơn 50%
            biased_tests.append((t, top_ans, top_cnt, len(t_qs), ratio))

print(f"\n5. Các đề có đáp án bị lệch bất thường (> 50% cùng 1 đáp án): {len(biased_tests)} đề")
for t, ans, cnt, tot, r in biased_tests:
    print(f"   • [{t['code']}] {t['title']}: Đáp án '{ans}' chiếm {cnt}/{tot} câu ({r*100:.1f}%)")

# ==============================================================================
# PHẦN 4: BẢNG PHÂN HẠNG 97 BỘ ĐỀ THEO ĐỘ SẠCH DỮ LIỆU
# ==============================================================================
print("\n" + "=" * 80)
print("PHẦN 4: TỔNG HỢP PHÂN HẠNG CHẤT LƯỢNG 97 BỘ ĐỀ (DATA QUALITY TIERS)")
print("=" * 80)

tier1_perfect = [] # 200 câu, 100% full CDN link, chuẩn ETS
tier2_good_but_relative_audio = [] # Có câu hỏi tốt, nhưng dính relative link
tier3_broken = [] # Rỗng hoặc dính lỗi nghiêm trọng

for t in tests:
    t_qs = q_by_test[t['id']]
    if len(t_qs) == 0:
        tier3_broken.append((t, "Đề rỗng (0 câu)"))
        continue
    
    # Check if biased
    c = Counter(q['correct_answer'] for q in t_qs)
    top_ans, top_cnt = c.most_common(1)[0]
    if len(t_qs) >= 20 and (top_cnt / len(t_qs)) > 0.60:
        tier3_broken.append((t, f"Lệch đáp án nghiêm trọng ({top_ans} chiếm {top_cnt/len(t_qs)*100:.0f}%)"))
        continue

    # Check relative audio
    rel_cnt = sum(1 for q in t_qs if q['audio_url'] and not q['audio_url'].startswith('http'))
    if rel_cnt > 0:
        tier2_good_but_relative_audio.append((t, f"{rel_cnt} câu dính link audio cục bộ"))
    else:
        tier1_perfect.append((t, f"{len(t_qs)} câu hỏi 100% link CDN chuẩn"))

print(f"🟢 HẠNG 1: HOÀN HẢO 100% (PRODUCTION READY)    : {len(tier1_perfect)} đề")
print(f"🟡 HẠNG 2: NỘI DUNG TỐT NHƯNG THIẾU DOMAIN AUDIO: {len(tier2_good_but_relative_audio)} đề")
print(f"🔴 HẠNG 3: DỮ LIỆU HỎNG / DUMMY CẦN STUDYCHILL SỬA: {len(tier3_broken)} đề")

print("\nChi tiết Hạng 1 (Hoàn hảo - 100% Sẵn sàng chạy Production):")
for t, desc in tier1_perfect[:27]:
    print(f"   ✓ [{t['code']}] {t['title']} ({desc})")

print("\nChi tiết Hạng 3 (Hỏng / Dummy - Cần Studychill xử lý):")
for t, desc in tier3_broken:
    print(f"   ✗ [{t['code']}] {t['title']}: {desc}")

print("\n" + "=" * 80)
print("BÁO CÁO HOÀN TẤT")
print("=" * 80)
