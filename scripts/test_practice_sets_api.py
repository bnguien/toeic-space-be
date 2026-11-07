#!/usr/bin/env python3
"""PBL-26 gateway smoke tests. Only creates/soft-deletes PBL26-SMOKE-* sets.

Uses existing Active questions read-only. No SQL imports, migrations, question writes,
attempt writes, or modifications to imported sets. Requires requests and cryptography,
as does test_assessment_api.py. Run only against the local development stack:
  python scripts/test_practice_sets_api.py
JWT_PRIVATE_KEY / JWT_ISSUER / JWT_AUDIENCE may be supplied via the environment;
otherwise they are read privately from the local backend .env file.
"""

import base64
import concurrent.futures
import json
import os
import time
import uuid
from pathlib import Path

import requests
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature


def settings():
    result = {}
    file = Path(__file__).resolve().parents[1] / ".env"
    if file.exists():
        for line in file.read_text(encoding="utf-8-sig").splitlines():
            if "=" in line and not line.lstrip().startswith("#"):
                key, value = line.split("=", 1)
                result[key.strip()] = value.strip().strip('"').strip("'")
    return {**result, **os.environ}


CONFIG = settings()
BASE = CONFIG.get("PBL26_API_URL", "http://localhost:5050/assessment/api/v1").rstrip("/")
value = CONFIG["JWT_PRIVATE_KEY"]
KEY = (serialization.load_pem_private_key(value.encode(), None) if "BEGIN" in value
       else serialization.load_der_private_key(base64.b64decode(value), None))


def b64(raw):
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode()


def token(role, user_id):
    now = int(time.time())
    header = b64(json.dumps({"alg": "ES256", "typ": "at+jwt"}).encode())
    payload = b64(json.dumps({"sub": user_id, "role": role,
        "iss": CONFIG.get("JWT_ISSUER", "toeicspace-identity"),
        "aud": CONFIG.get("JWT_AUDIENCE", "toeicspace-api"),
        "iat": now - 1, "nbf": now - 1, "exp": now + 1800}).encode())
    r, s = decode_dss_signature(KEY.sign(f"{header}.{payload}".encode(), ec.ECDSA(hashes.SHA256())))
    return f"{header}.{payload}.{b64(r.to_bytes(32, 'big') + s.to_bytes(32, 'big'))}"


def session(role):
    user_id = str(uuid.uuid4())
    client = requests.Session()
    client.headers["Authorization"] = f"Bearer {token(role, user_id)}"
    return client, user_id


ADMIN, ADMIN_ID = session("Admin")
TEACHER, TEACHER_ID = session("Teacher")
OTHER, _ = session("Teacher")
STUDENT, _ = session("User")
ANONYMOUS = requests.Session()
CREATED = []
PREFIX = "PBL26-SMOKE-" + uuid.uuid4().hex[:10]
PASSED = 0


def check(condition, label):
    global PASSED
    assert condition, label
    PASSED += 1
    print("PASS", label, flush=True)


def call(client, method, path, expected=200, **kwargs):
    response = client.request(method, BASE + path, timeout=30, **kwargs)
    assert response.status_code == expected, f"{method} {path}: expected {expected}, got {response.status_code}: {response.text[:500]}"
    return response.json() if response.content else None


def create(client, suffix, **overrides):
    data = {"code": f"{PREFIX}-{suffix}", "title": f"PBL26 smoke {suffix}", "description": "Local integration verification",
            "kind": "Topic", "part": "Part5", "level": None, "targetScore": None, "durationMinutes": 30, "orderIndex": 0, **overrides}
    result = call(client, "POST", "/practice-sets", 201, json=data)
    CREATED.append(result["id"])
    return result, data


def run():
    baseline = call(ADMIN, "GET", "/practice-sets", params={"pageSize": 100})
    imported = [item for item in baseline["items"] if not item.get("createdByUserId")][0]
    original = call(ADMIN, "GET", f'/practice-sets/{imported["id"]}')
    for method, suffix, data in [("PUT", "", original), ("PUT", "/items", {"questionIds": [], "expectedQuestionIds": []}),
                                 ("PATCH", "/status", {"status": "Archived"}), ("DELETE", "", None)]:
        call(TEACHER, method, f'/practice-sets/{imported["id"]}{suffix}', 403, json=data)
    check(call(ADMIN, "GET", f'/practice-sets/{imported["id"]}') == original, "imported ownerless set unchanged after forbidden mutations")

    questions = call(ADMIN, "GET", "/questions", params={"part": "Part5", "status": "Active", "pageSize": 3})["items"]
    ids = [question["id"] for question in questions]
    check(len(ids) == 3, "existing Active bank questions available read-only")
    own, metadata = create(TEACHER, "owned")
    sid = own["id"]
    path = f"/practice-sets/{sid}"
    check(own["createdByUserId"] == TEACHER_ID and own["status"] == "Draft", "creation assigns Teacher ownership and Draft")
    call(TEACHER, "PATCH", path + "/status", 400, json={"status": "Draft"})
    check(True, "status contract rejects Draft as a transition target")
    call(STUDENT, "POST", "/practice-sets", 403, json={**metadata, "code": PREFIX + "-denied"})
    for client in [OTHER, STUDENT]:
        for method, suffix, data in [("PUT", "", metadata), ("PUT", "/items", {"questionIds": ids, "expectedQuestionIds": []}), ("PATCH", "/status", {"status": "Active"}), ("DELETE", "", None)]:
            call(client, method, path + suffix, 403, json=data)
    check(True, "all mutations reject other Teacher and Student")
    call(STUDENT, "GET", path, 404)
    call(STUDENT, "GET", path + "/content", 404)
    call(ANONYMOUS, "GET", path + "/content", 401)
    call(TEACHER, "PATCH", path + "/status", 409, json={"status": "Active"})
    call(TEACHER, "PUT", path + "/items", 400, json={"questionIds": ids[:2]})
    call(TEACHER, "PUT", path + "/items", json={"questionIds": ids[:2], "expectedQuestionIds": []})
    call(TEACHER, "PATCH", path + "/status", json={"status": "Active"})
    result = call(TEACHER, "PUT", path + "/items", json={"questionIds": list(reversed(ids)), "expectedQuestionIds": ids[:2]})
    check([i["questionId"] for i in result["items"]] == list(reversed(ids)), "Active set add/reorder retains exact requested order")
    call(TEACHER, "PUT", path + "/items", 409, json={"questionIds": ids[:1], "expectedQuestionIds": ids[:2]})
    check([item["questionId"] for item in call(TEACHER, "GET", path)["items"]] == list(reversed(ids)), "stale question baseline cannot overwrite a newer save")
    call(TEACHER, "PUT", path + "/items", 409, json={"questionIds": [], "expectedQuestionIds": list(reversed(ids))})
    call(TEACHER, "PUT", path + "/items", 400, json={"questionIds": [ids[0], ids[0]], "expectedQuestionIds": list(reversed(ids))})
    updated = call(ADMIN, "PUT", path, json={**metadata, "title": "Admin updated Teacher set"})
    check(updated["createdByUserId"] == TEACHER_ID, "Admin override preserves Teacher ownership")
    baseline_stamp = updated.get("updatedAt") or updated["createdAt"]
    call(TEACHER, "PUT", path, json={**metadata, "title": "Fresh metadata", "expectedUpdatedAt": baseline_stamp})
    call(TEACHER, "PUT", path, 409, json={**metadata, "title": "Stale overwrite", "expectedUpdatedAt": baseline_stamp})
    check(call(TEACHER, "GET", path)["title"] == "Fresh metadata", "metadata precondition rejects stale writes without overwriting")
    check("createdByUserId" not in call(ANONYMOUS, "GET", path)
          and "createdByUserId" not in call(STUDENT, "GET", path), "public metadata omits internal owner ID")
    public_list = call(ANONYMOUS, "GET", "/practice-sets", params={"pageSize": 100})
    check(all("createdByUserId" not in item for item in public_list["items"]), "anonymous list omits all owner IDs")
    content = call(STUDENT, "GET", path + "/content")
    preview = content["parts"][0]["standaloneQuestions"]
    check(content["includesAnswers"] and all(q.get("correctAnswer") and q.get("explanation") for q in preview), "Student receives answers and explanations")
    check(all(q.get("status") == "Active" for q in preview), "content question payload retains status")
    call(TEACHER, "PATCH", path + "/status", json={"status": "Archived"})
    call(STUDENT, "GET", path, 404)
    call(STUDENT, "GET", path + "/content", 404)
    call(TEACHER, "PATCH", path + "/status", json={"status": "Active"})
    check(call(STUDENT, "GET", path)["status"] == "Active", "Archived set can be revalidated and republished")

    # A concurrent unpublish followed by empty replacement, or empty replacement followed
    # by unpublish, must never leave an Active set empty. Both operations share a row lock.
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        statuses = list(executor.map(lambda request: request(), [
            lambda: TEACHER.patch(BASE + path + "/status", json={"status": "Archived"}, timeout=30),
            lambda: ADMIN.put(BASE + path + "/items", json={"questionIds": [], "expectedQuestionIds": list(reversed(ids))}, timeout=30)]))
    check(statuses[0].status_code == 200 and statuses[1].status_code in (200, 409), "concurrent status/items updates serialize successfully")
    final = call(ADMIN, "GET", path)
    check(final["status"] != "Active" or len(final["items"]) > 0, "concurrency preserves published content invariant")

    for kind, level in [("Topic", 1), ("PartDrill", 2), ("Level", None), ("Level", 6)]:
        call(TEACHER, "POST", "/practice-sets", 400, json={**metadata, "code": PREFIX + "-invalid", "kind": kind, "level": level})
    level_set, _ = create(TEACHER, "level", kind="Level", level=3)
    other_set, other_metadata = create(ADMIN, "admin")
    own_list = call(TEACHER, "GET", "/practice-sets", params={"ownedOnly": True, "search": PREFIX, "pageSize": 1})
    check(own_list["totalCount"] == 2 and len(own_list["items"]) == 1 and own_list["totalPages"] == 2, "owned search and pagination exclude Admin records")
    levels = call(TEACHER, "GET", "/practice-sets", params={"ownedOnly": True, "part": "Part5", "kind": "Level", "level": 3, "status": "Draft", "search": PREFIX})
    check([item["id"] for item in levels["items"]] == [level_set["id"]], "combined Part/Kind/Level/status filters")
    call(TEACHER, "PUT", f'/practice-sets/{other_set["id"]}', 403, json=other_metadata)
    call(ADMIN, "DELETE", f'/practice-sets/{other_set["id"]}', 204)
    call(ADMIN, "GET", f'/practice-sets/{other_set["id"]}', 404)
    check(True, "soft-deleted set is hidden")
    student_list = call(STUDENT, "GET", "/practice-sets", params={"status": "Draft", "pageSize": 100})
    check(all(item["status"] == "Active" for item in student_list["items"]), "Student cannot override Active-only visibility")


if __name__ == "__main__":
    try:
        run()
        print(f"{PASSED} PBL-26 gateway checks passed", flush=True)
    finally:
        for sid in CREATED:
            response = ADMIN.delete(BASE + "/practice-sets/" + sid, timeout=30)
            assert response.status_code in (204, 404), f"Cleanup failed for {sid}"
        print(f"Soft-delete cleanup completed for {len(CREATED)} new test sets", flush=True)
