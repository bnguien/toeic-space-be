#!/usr/bin/env python3
"""
Smoke test for the Assessment API (tests, practice sets, question bank).

Environment variables:
  ASSESSMENT_API_URL   http://localhost:5237
  JWT_ISSUER           toeicspace-identity
  JWT_AUDIENCE         toeicspace-api
  JWT_PRIVATE_KEY      base64 PKCS#8 P-256 key of the Identity service. Defaults to the local
                       Identity user-secrets created by `dotnet run scripts/GenerateJwtKeys.cs -- --user-secrets`.

Tokens are ES256-signed like the Identity service does, so the script only works where the
Identity private key is available (a developer machine), never against production.

The script creates a few records (codes prefixed with SMOKE-) and soft deletes them again.
Run it against a development database only.
"""

import base64
import hashlib
import hmac
import json
import os
import sys
import time
import uuid
from pathlib import Path

import requests
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

BASE_URL = os.getenv("ASSESSMENT_API_URL", "http://localhost:5237").rstrip("/") + "/api/v1"
ISSUER = os.getenv("JWT_ISSUER", "toeicspace-identity")
AUDIENCE = os.getenv("JWT_AUDIENCE", "toeicspace-api")
passed = 0


def load_private_key() -> ec.EllipticCurvePrivateKey:
    value = os.getenv("JWT_PRIVATE_KEY")
    if not value:
        root = Path(os.getenv("APPDATA", "")) / "Microsoft" if os.name == "nt" else Path.home() / ".microsoft"
        secrets = root / "UserSecrets" / "toeicspace-identity-local" / "secrets.json"
        if not secrets.exists():
            sys.exit("Set JWT_PRIVATE_KEY or run: dotnet run scripts/GenerateJwtKeys.cs -- --user-secrets")
        value = json.loads(secrets.read_text(encoding="utf-8-sig")).get("Jwt:PrivateKey", "")
    if "BEGIN" in value:
        return serialization.load_pem_private_key(value.encode(), password=None)
    return serialization.load_der_private_key(base64.b64decode(value), password=None)


PRIVATE_KEY = load_private_key()


def b64url(raw: bytes) -> str:
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode()


def encode(part: dict) -> str:
    return b64url(json.dumps(part, separators=(",", ":")).encode())


def create_token(role: str, *, typ: str = "at+jwt", lifetime: int = 600, audience: str = AUDIENCE) -> str:
    """ES256 access token shaped like the ones the Identity service issues."""
    now = int(time.time())
    header = encode({"alg": "ES256", "typ": typ})
    payload = encode({"sub": str(uuid.uuid4()), "role": role, "iss": ISSUER, "aud": audience,
                      "iat": now - 1, "nbf": now - 1, "exp": now + lifetime})
    der = PRIVATE_KEY.sign(f"{header}.{payload}".encode(), ec.ECDSA(hashes.SHA256()))
    r, s = decode_dss_signature(der)
    return f"{header}.{payload}.{b64url(r.to_bytes(32, 'big') + s.to_bytes(32, 'big'))}"


def forged_hs256_token(role: str) -> str:
    """Algorithm-confusion attempt: HS256 keyed with the public key."""
    public_der = PRIVATE_KEY.public_key().public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)
    now = int(time.time())
    header = encode({"alg": "HS256", "typ": "at+jwt"})
    payload = encode({"sub": str(uuid.uuid4()), "role": role, "iss": ISSUER, "aud": AUDIENCE, "iat": now, "nbf": now, "exp": now + 600})
    signature = hmac.new(base64.b64encode(public_der), f"{header}.{payload}".encode(), hashlib.sha256).digest()
    return f"{header}.{payload}.{b64url(signature)}"


def session_with(token: str) -> requests.Session:
    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {token}"
    return session


ANONYMOUS = requests.Session()
LEARNER = session_with(create_token("User"))
ADMIN = session_with(create_token("Admin"))


def check(title: str, response: requests.Response, expected_status: int):
    global passed
    ok = response.status_code == expected_status
    print(f"{'PASS' if ok else 'FAIL'}  {response.request.method:<6} {response.request.path_url[:70]:<70} -> {response.status_code}  {title}")
    if not ok:
        print(f"      expected {expected_status}, body: {response.text[:500]}")
        sys.exit(1)
    passed += 1
    return response.json() if response.content else None


def expect(condition: bool, message: str):
    global passed
    print(f"{'PASS' if condition else 'FAIL'}  {'':<6} {message}")
    if not condition:
        sys.exit(1)
    passed += 1


def all_questions(content: dict) -> list:
    return [q for part in content["parts"] for q in part["standaloneQuestions"] + [q for p in part["passages"] for q in p["questions"]]]


def main():
    # ---- Token hardening ------------------------------------------------------------------------
    questions_url = f"{BASE_URL}/questions"
    check("HS256 token with the public key is rejected", session_with(forged_hs256_token("Admin")).get(questions_url), 401)
    check("token of another type is rejected", session_with(create_token("Admin", typ="JWT")).get(questions_url), 401)
    check("expired token is rejected", session_with(create_token("Admin", lifetime=-120)).get(questions_url), 401)
    check("token for another audience is rejected", session_with(create_token("Admin", audience="another-api")).get(questions_url), 401)
    headers = ADMIN.get(questions_url, params={"pageSize": 1}).headers
    expect(headers.get("Cache-Control") == "no-store", "content responses are not cacheable")
    expect(headers.get("X-Content-Type-Options") == "nosniff" and "Server" not in headers, "hardening headers are set and the server banner is hidden")

    # ---- Full tests (public catalogue) ----------------------------------------------------------
    tests = check("list published tests", ANONYMOUS.get(f"{BASE_URL}/tests", params={"pageSize": 50}), 200)
    expect(tests["totalCount"] == 26, f"26 full tests are published (got {tests['totalCount']})")
    test_id = next(t["id"] for t in tests["items"] if t["code"] == "ETS-2026-TEST-01")

    categories = check("test categories", ANONYMOUS.get(f"{BASE_URL}/tests/categories"), 200)
    expect({c["category"] for c in categories} == {"ETS 2026", "Crack TOEIC Vol 1", "Pass TOEIC"}, "categories are the three test series")

    detail = check("test overview", ANONYMOUS.get(f"{BASE_URL}/tests/{test_id}"), 200)
    expect(detail["questionCount"] == 200, "overview counts 200 questions")

    check("full test requires login", ANONYMOUS.get(f"{BASE_URL}/tests/{test_id}/full"), 401)

    learner_view = check("learner full test", LEARNER.get(f"{BASE_URL}/tests/{test_id}/full"), 200)
    learner_questions = all_questions(learner_view)
    expect(len(learner_questions) == 200 and not learner_view["includesAnswers"], "learner gets 200 questions without answer key")
    expect(all("correctAnswer" not in q and "explanation" not in q and "transcript" not in q for q in learner_questions), "no answer, explanation or transcript is exposed")
    part3 = next(p for p in learner_view["parts"] if p["part"] == "Part3")
    expect(all("content" not in passage for passage in part3["passages"]), "Part 3 conversation scripts are hidden")

    check("learner cannot request answers", LEARNER.get(f"{BASE_URL}/tests/{test_id}/full", params={"includeAnswers": "true"}), 403)
    admin_view = check("manager full test with answers", ADMIN.get(f"{BASE_URL}/tests/{test_id}/full", params={"includeAnswers": "true"}), 200)
    expect(all(q.get("correctAnswer") for q in all_questions(admin_view)), "manager sees every answer key")

    # ---- Practice sets --------------------------------------------------------------------------
    sets = check("list published Part 3 practice sets", ANONYMOUS.get(f"{BASE_URL}/practice-sets", params={"part": "Part3", "pageSize": 50}), 200)
    expect(sets["totalCount"] > 0 and all(s["status"] == "Active" for s in sets["items"]), "anonymous users only see Active sets")
    practice_set = next(s for s in sets["items"] if s["kind"] == "Level")
    content = check("practice content", LEARNER.get(f"{BASE_URL}/practice-sets/{practice_set['id']}/content"), 200)
    expect(content["includesAnswers"] and content["questionCount"] == practice_set["questionCount"], "practice content includes answers for feedback")

    # ---- Question bank (content managers) -----------------------------------------------------
    check("learner cannot open the question bank", LEARNER.get(f"{BASE_URL}/questions"), 403)
    search = check("full-text search", ADMIN.get(f"{BASE_URL}/questions", params={"search": "meeting schedule", "pageSize": 5}), 200)
    expect(search["totalCount"] > 0, f"search finds questions ({search['totalCount']})")

    invalid = {
        "part": "Part2", "optionA": "Yes.", "optionB": "No.", "optionC": "Maybe.", "optionD": "Never.", "correctAnswer": "D",
    }
    errors = check("Part 2 with option D is rejected", ADMIN.post(f"{BASE_URL}/questions", json=invalid), 400)
    expect({"OptionD", "CorrectAnswer"} <= set(errors["errors"].keys()), "validation reports OptionD and CorrectAnswer")

    question = {
        "part": "Part5",
        "questionText": "The marketing department has successfully _____ the new branding campaign.",
        "optionA": "launch", "optionB": "launched", "optionC": "launching", "optionD": "launcher",
        "correctAnswer": "B",
        "explanation": "Present perfect: has + past participle.",
        "difficultyLevel": "Medium",
        "topic": "SMOKE - Verb forms",
    }
    created = check("create bank question", ADMIN.post(f"{BASE_URL}/questions", json=question), 201)
    expect(created["section"] == "Reading" and created["version"] == 1, "section is derived from the part")

    stale = {**question, "expectedVersion": 99, "explanation": "Updated."}
    check("stale version is rejected", ADMIN.put(f"{BASE_URL}/questions/{created['id']}", json=stale), 409)
    updated = check("update question", ADMIN.put(f"{BASE_URL}/questions/{created['id']}", json={**stale, "expectedVersion": 1}), 200)
    expect(updated["version"] == 2, "version increases after update")

    archived = check("archive question", ADMIN.patch(f"{BASE_URL}/questions/{created['id']}/status", json={"status": "Archived"}), 200)
    expect(archived["status"] == "Archived", "status is Archived")
    check("delete question", ADMIN.delete(f"{BASE_URL}/questions/{created['id']}"), 204)
    check("deleted question is gone", ADMIN.get(f"{BASE_URL}/questions/{created['id']}"), 404)

    # ---- Test lifecycle -----------------------------------------------------------------------
    code = f"SMOKE-{uuid.uuid4().hex[:8].upper()}"
    new_test = check("create draft test", ADMIN.post(f"{BASE_URL}/tests", json={"code": code, "title": "Smoke test", "category": "SMOKE", "year": 2026}), 201)
    check("duplicate code is rejected", ADMIN.post(f"{BASE_URL}/tests", json={"code": code, "title": "Duplicate", "year": 2026}), 409)
    check("empty test cannot be published", ADMIN.patch(f"{BASE_URL}/tests/{new_test['id']}/status", json={"status": "Active"}), 409)
    check("draft test is hidden from learners", LEARNER.get(f"{BASE_URL}/tests/{new_test['id']}"), 404)
    check("delete draft test", ADMIN.delete(f"{BASE_URL}/tests/{new_test['id']}"), 204)

    print(f"\nAll {passed} checks passed.")


if __name__ == "__main__":
    main()
