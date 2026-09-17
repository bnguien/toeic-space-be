import json
import gzip
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

with gzip.open('data/seeds/toeic_questions.json.gz', 'rt', encoding='utf-8') as f:
    questions = json.load(f)

with gzip.open('data/seeds/toeic_passages.json.gz', 'rt', encoding='utf-8') as f:
    passages = json.load(f)

with open('data/seeds/toeic_tests.json', 'r', encoding='utf-8') as f:
    tests = json.load(f)

print('=== VERIFICATION OF HEALED DATASETS ===')
print(f'Total Tests: {len(tests):,}')
print(f'Total Passages: {len(passages):,}')
print(f'Total Questions: {len(questions):,}')

rel_q_aud = sum(1 for q in questions if q.get('AudioUrl') and not q['AudioUrl'].startswith('http'))
rel_q_img = sum(1 for q in questions if q.get('ImageUrl') and not q['ImageUrl'].startswith('http'))
rel_p_aud = sum(1 for p in passages if p.get('AudioUrl') and not p['AudioUrl'].startswith('http'))
rel_p_img = sum(1 for p in passages if p.get('ImageUrl') and not p['ImageUrl'].startswith('http'))

print(f'\n[Media URLs Audit]')
print(f'- Relative Question Audio remaining: {rel_q_aud}')
print(f'- Relative Question Image remaining: {rel_q_img}')
print(f'- Relative Passage Audio remaining: {rel_p_aud}')
print(f'- Relative Passage Image remaining: {rel_p_img}')

full_tests = [t for t in tests if t['Category'] == 'FULL_TEST']
print(f'\n[Full Tests Audit] Total: {len(full_tests)}')
for t in full_tests:
    print(f"  ✓ [{t['Code']}] {t['Title']} ({t['TotalQuestions']} questions)")

inactive_tests = [t for t in tests if not t['IsActive']]
print(f'\n[Quarantined Tests] Total: {len(inactive_tests)}')
for t in inactive_tests:
    print(f"  ⚠️ [{t['Code']}] {t['Title']} (Audit: {t['AuditNotes']})")
