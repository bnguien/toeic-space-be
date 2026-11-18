# Classroom development demo data

Run the Classroom API in seeding mode to migrate the Classroom database and create a repeatable TOEIC demo dataset. The seeder is available only in `Development` and `Testing`; normal API startup does not seed data.

```sh
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__ClassroomDatabase='Server=127.0.0.1;Port=3307;Database=classroom;User=classroom;Password=YOUR_LOCAL_PASSWORD;' \
dotnet run --project src/Services/Classroom/ToeicSpace.Classroom.API -- --seed-classroom-demo
```

Use the local Classroom connection string from your own environment or user secrets. Do not commit credentials. In Docker, the database host and port may differ; use the existing `ConnectionStrings:ClassroomDatabase` configuration for that environment.

Before running the command:

1. Start MySQL and RabbitMQ, then start Identity and Classroom normally in Development.
2. Ensure Identity contains at least 3 active Teacher accounts and at least 10 active, verified student accounts.
3. Wait until the Classroom `TeacherReadModels` and `StudentReadModels` projections contain those accounts. Classroom requests synchronization when it starts. The seeder checks the local projections and stops before writing centers/classes if prerequisites are missing.

The Classroom `Class` model has no Course reference, so this seed does not create or fabricate Content Service course IDs. Identity and Content databases are never accessed by the seeder.

## Dataset

The seed creates or reuses:

- 2 TOEIC MASTER centers in Ho Chi Minh City, using `Asia/Ho_Chi_Minh`.
- 5 rooms distributed across the centers, with capacities from 18 to 24.
- 4 active classes: TOEIC MASTER 800+, TOEIC BOOST 750+, TOEIC SPEAKING & LISTENING, and TOEIC INTENSIVE 600+.
- 3 distinct eligible Identity teachers assigned to the classes and 10–12 active student enrollments, depending on available synchronized students.
- 6 weekly schedules and approximately 36 sessions over six weeks. Classes 800+ and 750+ run simultaneously on Mondays and Wednesdays with different teachers and rooms. The same room is reused by the Intensive class on Thursdays. Sessions include online and offline modes, an online session override, a room override, and one cancelled session.

Stable center/room names and class codes prefixed with `DEMO` identify the records. Re-running the command reuses these records, skips existing schedule occurrences, and resumes missing operations without deleting unrelated rows. Application commands create classes, assign teachers, enroll students, set default rooms, generate/edit/cancel sessions, and preserve their validation and audit behavior.

On success, the command prints the created primary demo record total (centers, rooms, classes, enrollments, schedules, and sessions), reused records, skipped recurring occurrences, and resulting counts. It also verifies the seeded records, teacher/session assignments, local date ranges, UTC timestamps, FK references, enrollment capacity, recurring occurrence uniqueness, and scheduled teacher/room conflicts. It exercises the same class, schedule, and session query handlers used by the REST API.

To inspect the result in the running Classroom API, use the existing Admin endpoints:

```text
GET /api/v1/centers?page=1&pageSize=20
GET /api/v1/rooms?page=1&pageSize=20
GET /api/v1/classes?search=DEMO-TOEIC&page=1&pageSize=20
GET /api/v1/classes/{classId}/schedules
GET /api/v1/classes/{classId}/enrollments?page=1&pageSize=20
GET /api/v1/class-sessions?from={utcStart}&to={utcEnd}&page=1&pageSize=100
```

The APIs require the existing Admin bearer token. Session `from` and `to` values must be UTC and span no more than 366 days.
