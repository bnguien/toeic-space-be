#!/usr/bin/env python3
"""Seed realistic Classroom demo data through the existing Identity/Classroom APIs."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from zoneinfo import ZoneInfo

PREFIX = "DEMO-TOEIC-"
ZONE = ZoneInfo("Asia/Ho_Chi_Minh")


class ApiError(RuntimeError):
    pass


class Api:
    def __init__(self, base_url: str, token: str | None = None):
        self.base_url = base_url.rstrip("/")
        self.token = token

    def request(self, method: str, path: str, body=None, expected=(200, 201)):
        data = None if body is None else json.dumps(body).encode()
        headers = {"Accept": "application/json"}
        if data is not None:
            headers["Content-Type"] = "application/json"
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        req = urllib.request.Request(self.base_url + path, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=30) as response:
                raw = response.read()
                if response.status not in expected:
                    raise ApiError(f"{method} {path}: unexpected HTTP {response.status}")
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode(errors="replace")
            raise ApiError(f"{method} {path}: HTTP {exc.code}: {detail}") from exc
        except urllib.error.URLError as exc:
            raise ApiError(f"{method} {path}: connection failed: {exc.reason}") from exc

    def list_all(self, path: str):
        result = self.request("GET", path)
        if isinstance(result, list):
            return result
        if isinstance(result, dict):
            return result.get("items", [])
        raise ApiError(f"GET {path}: expected an array or paged result")


def login(identity: Api, email: str, password: str) -> str:
    result = identity.request("POST", "/api/auth/login", {"email": email, "password": password})
    token = result.get("accessToken") or result.get("token")
    if not token:
        raise ApiError("Identity login response did not contain accessToken")
    return token


def find_by(items, key, value):
    return next((item for item in items if item.get(key) == value), None)


def wait_for_sync(classroom: Api, teacher_ids: list[str], student_ids: list[str], timeout: int):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        # Requesters run on startup and every 15 minutes. Restarting is not required;
        # this loop allows the normal event synchronization to catch up.
        try:
            teachers = classroom.list_all("/api/v1/teachers?status=Active&page=1&pageSize=500")
            students = classroom.list_all("/api/v1/students?status=Active&page=1&pageSize=1000")
            known_teachers = {x.get("teacherId") or x.get("id") for x in teachers}
            known_students = {x.get("studentId") or x.get("id") for x in students}
            if set(teacher_ids) <= known_teachers and set(student_ids) <= known_students:
                return
        except ApiError:
            # These projection list routes are intentionally not assumed to exist.
            # Enrollment/assignment APIs below are the verified check of projection readiness.
            break
        time.sleep(2)
    # Eligibility is verified through normal domain operations, with explicit guidance on failure.


def ensure_user(identity: Api, users: list[dict], role: str, number: int):
    email = f"demo.toeic.{role.lower()}.{number}@example.com"
    existing = next((u for u in users if u.get("email", "").lower() == email), None)
    if existing:
        return existing["id"], existing
    result = identity.request("POST", "/api/v1/admin/users", {
        "fullName": ("Demo TOEIC Teacher" if role == "Teacher" else "Demo TOEIC Student") + f" {number}",
        "email": email,
        "phone": None,
        "role": role,
    })
    user = result.get("user", result)
    user_id = user.get("id")
    if not user_id:
        raise ApiError(f"Creating {role} {email} returned no user ID")
    print(f"Created {role}: {email} ({user_id}). Temporary password is returned by Identity; set it manually for demo login if needed.")
    return user_id, user


def ensure_center(classroom: Api, name: str, address: str):
    centers = classroom.list_all("/api/v1/centers?page=1&pageSize=100")
    center = find_by(centers, "name", name)
    if center:
        return center
    center = classroom.request("POST", "/api/v1/centers", {"name": name, "address": address, "timeZoneId": "Asia/Ho_Chi_Minh"})
    return classroom.request("POST", f"/api/v1/centers/{center['id']}/activate")


def ensure_room(classroom: Api, center_id: str, name: str, capacity: int):
    rooms = classroom.list_all("/api/v1/rooms?page=1&pageSize=200")
    room = find_by(rooms, "name", name)
    if room:
        return room
    room = classroom.request("POST", "/api/v1/rooms", {"centerId": center_id, "name": name, "capacity": capacity})
    return classroom.request("POST", f"/api/v1/rooms/{room['id']}/activate")


def next_monday(today: dt.date) -> dt.date:
    return today + dt.timedelta(days=(7 - today.weekday()) % 7 or 7)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--identity-url", default=os.getenv("IDENTITY_API_URL", "http://localhost:6001"))
    parser.add_argument("--classroom-url", default=os.getenv("CLASSROOM_API_URL", "http://localhost:6003"))
    parser.add_argument("--email", default=os.getenv("DEMO_ADMIN_EMAIL"))
    parser.add_argument("--password", default=os.getenv("DEMO_ADMIN_PASSWORD"))
    parser.add_argument("--token", default=os.getenv("DEMO_ADMIN_TOKEN"), help="Optional Admin JWT; otherwise login is performed")
    parser.add_argument("--sync-timeout", type=int, default=120)
    args = parser.parse_args()

    if not args.token and (not args.email or not args.password):
        parser.error("set DEMO_ADMIN_TOKEN or both DEMO_ADMIN_EMAIL and DEMO_ADMIN_PASSWORD")

    identity = Api(args.identity_url)
    if args.token:
        token = args.token
    else:
        token = login(identity, args.email, args.password)
    identity.token = token
    classroom = Api(args.classroom_url, token)

    # Ensure event-driven projections are requested through the established bus contract.
    # Normal consumers produce TeacherReadModel and StudentReadModel in Classroom.
    identity.request("GET", "/api/v1/users/teachers?search=demo.toeic.teacher&page=1&pageSize=20")
    identity.request("GET", "/api/v1/users/students?search=demo.toeic.student&page=1&pageSize=100")

    teachers, students = [], []
    for i in range(1, 4):
        uid, _ = ensure_user(identity, [], "Teacher", i)
        teachers.append(uid)
    for i in range(1, 9):
        uid, _ = ensure_user(identity, [], "User", i)
        students.append(uid)

    # Classroom API triggers an Identity snapshot synchronization through RabbitMQ.
    sync = classroom.request("POST", "/api/v1/teacher-synchronization", expected=(200, 202, 204))
    _ = sync
    wait_for_sync(classroom, teachers, students, args.sync_timeout)

    center_a = ensure_center(classroom, PREFIX + "HCMC-DISTRICT-1", "District 1, Ho Chi Minh City")
    center_b = ensure_center(classroom, PREFIX + "HCMC-THU-DUC", "Thu Duc City, Ho Chi Minh City")
    rooms = [
        ensure_room(classroom, center_a["id"], PREFIX + "D1-ROOM-01", 30),
        ensure_room(classroom, center_a["id"], PREFIX + "D1-ROOM-02", 25),
        ensure_room(classroom, center_a["id"], PREFIX + "D1-ROOM-03", 20),
        ensure_room(classroom, center_b["id"], PREFIX + "TD-ROOM-01", 30),
        ensure_room(classroom, center_b["id"], PREFIX + "TD-ROOM-02", 25),
    ]

    monday = next_monday(dt.datetime.now(ZONE).date())
    start_date = monday
    end_date = monday + dt.timedelta(days=83)
    class_specs = [
        ("MASTER-800", "TOEIC Master 800+", "Nâng cao, mục tiêu 800+", 0, "18:30", "20:00", [0, 2]),
        ("LISTENING-700", "TOEIC Listening 700+", "Nghe chuyên sâu, mục tiêu 700+", 1, "18:30", "20:00", [1, 3]),
        ("FOUNDATION-600", "TOEIC Foundation 600+", "Củng cố nền tảng và chiến lược làm bài", 2, "09:00", "10:30", [5]),
        ("INTENSIVE-750", "TOEIC Intensive 750+", "Luyện đề chuyên sâu mục tiêu 750+", 0, "09:00", "10:30", [6]),
    ]
    classes, schedules = [], []
    for idx, (code, title, description, teacher_idx, start, end, days) in enumerate(class_specs):
        code = PREFIX + code
        existing = next((c for c in classroom.list_all("/api/v1/classes?page=1&pageSize=500") if c.get("code") == code), None)
        if existing:
            cls = classroom.request("GET", f"/api/v1/classes/{existing['id']}")
        else:
            cls = classroom.request("POST", "/api/v1/classes", {
                "code": code, "name": title, "description": description,
                "startDate": start_date.isoformat(), "endDate": end_date.isoformat(),
            })
        cls_data = cls.get("class", cls)
        cid = cls_data["id"]
        teacher_id = teachers[teacher_idx]
        current_teacher = cls_data.get("teacherId")
        if current_teacher != teacher_id:
            classroom.request("PUT", f"/api/v1/classes/{cid}/teacher", {"teacherId": teacher_id})
        room_id = rooms[idx]["id"]
        if not cls_data.get("defaultRoomId"):
            classroom.request("PUT", f"/api/v1/classes/{cid}/default-room", {"roomId": room_id, "reason": "Initial demo room assignment"})

        # Existing test-enrollments are checked through the class enrollment list before posting.
        enrolled = classroom.list_all(f"/api/v1/classes/{cid}/enrollments?page=1&pageSize=100")
        active_ids = {e.get("studentId") for e in enrolled if e.get("status") == "Active"}
        new_ids = [sid for sid in students[idx * 2:idx * 2 + 3] if sid not in active_ids]
        if new_ids:
            classroom.request("POST", f"/api/v1/classes/{cid}/enrollments/bulk", {"studentIds": new_ids})

        latest = classroom.request("GET", f"/api/v1/classes/{cid}")
        if latest.get("class", latest).get("status") == "Draft":
            classroom.request("PATCH", f"/api/v1/classes/{cid}/status", {"status": "Active"})

        schedule_list = classroom.list_all(f"/api/v1/classes/{cid}/schedules")
        schedule = next((s for s in schedule_list if s.get("timeZoneId") == "Asia/Ho_Chi_Minh" and s.get("teacherId") == teacher_id), None)
        if not schedule:
            schedule = classroom.request("POST", f"/api/v1/classes/{cid}/schedules", {
                "classId": cid,
                "daysOfWeek": days,
                "startTime": start,
                "endTime": end,
                "teacherId": teacher_id,
                "mode": "Offline",
                "timeZoneId": "Asia/Ho_Chi_Minh",
                "effectiveFrom": start_date.isoformat(),
                "effectiveTo": end_date.isoformat(),
                "excludeDates": [],
            })
        schedules.append(schedule)
        classes.append({"id": cid, "code": code, "name": title, "teacherId": teacher_id, "roomId": room_id})

    generated = classroom.request("POST", f"/api/v1/classes/{classes[0]['id']}/schedules/generate", {
        "from": start_date.isoformat(), "to": end_date.isoformat(), "excludeDates": [],
    })
    # Generate remaining schedules via existing API. Each schedule uses conflict-free times/resources.
    for cls in classes[1:]:
        result = classroom.request("POST", f"/api/v1/classes/{cls['id']}/schedules/generate", {
            "from": start_date.isoformat(), "to": end_date.isoformat(), "excludeDates": [],
        })
        generated["affected"].extend(result.get("affected", []))

    # Verify persisted records using existing list endpoints.
    for cls in classes:
        schedules = classroom.list_all(f"/api/v1/classes/{cls['id']}/schedules")
        sessions = classroom.list_all(f"/api/v1/class-sessions?from={urllib.parse.quote(start_date.isoformat())}&to={urllib.parse.quote(end_date.isoformat())}&classId={cls['id']}&page=1&pageSize=200")
        if not schedules:
            raise ApiError(f"Verification failed: no schedule for {cls['code']}")
        if not sessions:
            raise ApiError(f"Verification failed: no sessions for {cls['code']}")

    print(json.dumps({
        "timezone": "Asia/Ho_Chi_Minh",
        "period": {"from": start_date.isoformat(), "to": end_date.isoformat()},
        "centers": [center_a, center_b],
        "rooms": rooms,
        "teachers": teachers,
        "students": students,
        "classes": classes,
        "sessionsGenerated": len(generated.get("affected", [])),
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (ApiError, KeyError, TypeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)
