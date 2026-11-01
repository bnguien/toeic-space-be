# Manage Classes (Classroom Service)

## Responsibility and code map

Classroom owns `Class`, `ClassInstructor`, lifecycle rules, audit and its own MySQL schema. Identity owns user profiles, roles and account status. `TeacherId` is an Identity user ID, **not** a database foreign key. Classroom never reads the Identity database.

```text
Classroom.API (HTTP, JWT, authorization, ProblemDetails)
    -> Classroom.Application (CQRS handlers, local Teacher projection abstraction)
        -> Classroom.Domain (Class, ClassInstructor, ClassStatus)
    -> Classroom.Infrastructure (EF Core + MassTransit consumers)
        -> Classroom MySQL / RabbitMQ
```

The projects and references mirror the Identity complex-service structure. API endpoints are in `Classroom.API/Controllers`, CQRS use cases in `Classroom.Application/Features/Classes`, entity lifecycle behavior in `Classroom.Domain/Entities`, and EF persistence plus the Teacher projection in `Classroom.Infrastructure`.

Classroom currently includes class creation, editing, search, lifecycle changes, archive, teacher assignment history, teacher self-read and Admin-managed enrollment. Scheduling, attendance, assignments, announcements and content belong to other features.

## Data and invariants

`Class` stores `Id`, immutable unique `Code`, `Name`, `Description`, `Status`, optional `StartDate`/`EndDate`, audit timestamps/actor IDs and an internal `Version` concurrency token. `ClassInstructor` stores `Id`, `ClassId`, `TeacherId`, `AssignedAt`/`AssignedBy` and nullable `UnassignedAt`/`UnassignedBy`. A null `UnassignedAt` means current; a non-null value means historical.

The initial migration creates a unique index on `Classes.Code`. MySQL cannot enforce “one current assignment” with a normal unique index containing nullable `UnassignedAt`. The mapping therefore adds a generated `CurrentClassId` column: it equals `ClassId` for current rows and NULL for history. A unique index on that column enforces at most one current assignment per class. A normal foreign key connects `ClassInstructor.ClassId` to the Classroom `Classes` table. No foreign key points to Identity.

Core invariants:

1. Code is unique and never updated. Permanent deletion is a separate Admin action restricted to soft-deleted classes without related instructor or enrollment history (database foreign keys also protect other dependents).
2. Dates must satisfy `StartDate <= EndDate` when both are present.
3. Draft may have no teacher; activation needs both dates and a current teacher whose local projection is eligible.
4. A class has at most one current teacher. Reassignment closes the old row and inserts a new row without erasing history.
5. New assignments validate the local Identity-owned Teacher projection. Missing or ineligible data leaves the current assignment unchanged.
6. Teacher self-access uses authenticated JWT `sub` plus a current assignment. The client never selects the authorization identity.

## Lifecycle and permissions

Allowed normal transitions: `Draft -> Active -> Completed -> Archived`, plus `Draft -> Archived` and `Active -> Archived`. Archive records the immediately preceding status (`Draft`, `Active`, or `Completed`) in `ArchivedFromStatus`. `POST /restore` restores an Archived class exactly to that status. Soft delete changes Archived to Deleted; `POST /admin-restore` restores a Deleted class **directly to that same original status**, without requiring a second restore. Both restore actions clear `ArchivedFromStatus`; restoring to Active still requires valid dates and an eligible current teacher. Status PATCH cannot restore or delete. Legacy Archived/Deleted classes with no recorded prior status return `restore_state_unknown` (409) rather than guessing; resolve their previous status manually before restoring. Draft permits metadata edits and assign, reassign or unassign. Active permits Name, Description and EndDate edits, and reassign only; Code and StartDate are immutable. Completed and Archived permit no normal metadata or assignment edits.

Activation checks Code, Name, both dates, date order, current assignment and the latest consumed Teacher eligibility. If a teacher becomes inactive or loses the role, only the projection changes; class status and all `ClassInstructor` records remain intact.

Draft classes with valid start/end dates automatically transition to Active when the local start date arrives (provided the end date has not passed and the current teacher is eligible). Classroom checks on service startup and every five minutes using `Classroom:TimeZoneId` (default `Asia/Ho_Chi_Minh`). The job reuses the normal activation command and its validations; an ineligible/missing teacher leaves the class in Draft until a later check, without bypassing eligibility. Automatic updates set `UpdatedBy` to `Guid.Empty` to identify the system actor. This job does not automatically complete or delete classes.

Only Admin can create, list all, edit, change status, archive and change assignments. Teacher can list only current classes and read details only while currently assigned. Other authenticated roles have no class endpoints. Audit actor IDs are taken from JWT `sub`.

## Teacher event flow and consistency

The Admin UI searches `GET /identity/api/v1/users/teachers?search=&page=&pageSize=` with an Admin JWT. Identity returns only active Teacher users, with ID, full name, email, avatar URL and status. The result helps the UI select an ID but does not authorize assignment.

Identity writes a `TeacherChangedIntegrationEvent` to its transactional `OutboxMessages` table whenever a User is created/deleted or Name, Email, AvatarUrl, Role, Status or DeletedAt changes. The outbox dispatcher publishes through the shared MassTransit + RabbitMQ infrastructure with at-least-once delivery. The service-independent contract contains a complete projection snapshot and monotonic User `Version`.

Classroom's durable `classroom-teacher-changed` consumer upserts `TeacherReadModels` with TeacherId, Name, Email, AvatarUrl, Role, Status, IsEligible, Version, LastEventId and SynchronizedAt. Equal or lower versions are duplicate/out-of-order events and are ignored. Consumer retry handles transient failures.

For initial synchronization and repair, Classroom publishes `TeacherSynchronizationRequestedIntegrationEvent` at startup and every 15 minutes. Identity consumes the request and stages current snapshots through its outbox. Missing local data fails closed with `teacher_not_synchronized` (503). No service reads another service's database and no distributed transaction is introduced.

## Transaction and concurrency

Reassign validates the new teacher before opening a database transaction. Inside the transaction it loads the class and current assignment, marks the old row ended, increments the class `Version`, saves, inserts the new current row, saves and commits. Any failure rolls back both saves. The first save releases the generated-column unique slot before the insert. EF's concurrency token on `Class.Version` makes stale competing edits fail; the unique index is the database's final defense. The API returns 409 for concurrency or unique-index conflicts. Concurrent creation of the same Code is likewise stopped by the unique index even if both requests pass the preliminary existence check. Status and update operations increment Version and fail on stale writes.

## Enrollment (PBL-61)

Identity `UserRole.User` represents a student. Admin searches eligible students via `GET /identity/api/v1/users/students?search=&page=1&pageSize=20`; the result lists active, verified, non-deleted `User` accounts with ID, full name, email, avatar URL and status. Identity stages `StudentChangedIntegrationEvent` through its outbox on user creation or changes to role, status, email verification, deletion, name or email. A startup/periodic `StudentSynchronizationRequestedIntegrationEvent` repairs missed snapshots. Classroom stores a versioned `StudentReadModel`; duplicate/out-of-order events are ignored, and a missing projection fails closed with `student_not_synchronized` (503). Classroom never accesses the Identity database. Deploy Identity and Classroom consumers together before enrolling students.

An enrollment records `ClassId`, `StudentId`, `EnrolledAt/By`, `Status`, and optional `WithdrawnAt/By`. A student is currently enrolled only if `Status == Active && WithdrawnAt == null`. Withdrawal retains the record; re-enrollment inserts another row. The generated nullable `CurrentStudentId` column and unique `(ClassId, CurrentStudentId)` index prevent two current enrollments even under concurrent requests. Enrollment writes also claim Class `Version` to prevent races with archive/delete. Archived, Completed and Deleted classes cannot be modified. Admin may inspect history after soft deletion, but Hard Delete is blocked when any history exists.

| Method | Route (through `/classroom`) | Role | Description |
|---|---|---|---|
| GET | `/api/v1/classes/{classId}/enrollments?status=Active&page=1&pageSize=20` | Admin | List all/current/withdrawn enrollments; optional `studentId` filter; omit filters for full history, including soft-deleted classes |
| POST | `/api/v1/classes/{classId}/enrollments` | Admin | Body `{ "studentId": "..." }`; create current enrollment, return 201 + `ClassEnrollmentDto` |
| POST | `/api/v1/classes/{classId}/enrollments/bulk` | Admin | Body `{ "studentIds": ["...", "..."] }` (1–1,000 IDs); returns 200 with success/error per student |
| POST | `/api/v1/classes/{classId}/enrollments/import` | Admin | `multipart/form-data` field `file` (.csv or .xlsx); returns 200 with successes and per-row failures |
| POST | `/api/v1/classes/{classId}/enrollments/{enrollmentId}/withdraw` | Admin | Withdraw a current enrollment, return updated `ClassEnrollmentDto` |

List responses use `PagedResult<ClassEnrollmentDto>` (`items`, `page`, `pageSize`, `totalCount`), ordered by newest enrollment then ID. DTO includes student ID, projected name/email, status, enrollment/withdrawal timestamps and actor IDs. Identity data may be briefly stale until the projection is synchronized.

For bulk import, upload a UTF-8 CSV or .xlsx file (first worksheet) with a header on row 1 containing **exactly one** of `studentId` or `email` (case-insensitive); other columns are ignored. Maximum file size is 2 MB and maximum is 1,000 data rows. The import uses the same enrollment rules as the single-student API, isolates each row in a separate scope, and returns `{ totalRows, importedCount, failedCount, rows: [{ rowNumber, studentId, enrollmentId, code, message }] }`. Successful rows have `enrollmentId` and null error fields; failed rows have `code` and `message`, without rolling back successful rows. Invalid file/header/size returns `400 invalid_import_file`. An invalid class state rejects the entire import before processing rows.

For UI multi-selection, use the JSON `/bulk` endpoint instead of creating a file. It returns `{ totalCount, enrolledCount, failedCount, items: [{ index, studentId, enrollmentId, code, message }] }`, where `index` is the zero-based position in the submitted `studentIds` array. One item can succeed while others fail (e.g. `already_enrolled`, `student_not_synchronized`); an invalid Class state rejects the entire request.

## API reference

Routes below are within Classroom (`/classroom` through the gateway). Responses use JSON; errors use ProblemDetails with a `code` extension.

| Method | Route | Role | Purpose |
|---|---|---|---|
| POST | `/api/v1/classes` | Admin | Create Draft (`Code`, `Name`, optional dates/description); returns detail and 201 |
| GET | `/api/v1/classes` | Admin | Search Code/Name; filter Status, TeacherId, StartDateFrom/To; page/pageSize; `UpdatedAt DESC`, then ID |
| GET | `/api/v1/classes/deleted` | Admin | Search soft-deleted classes by Code/Name; `search`, `page`, `pageSize` (1–100); `UpdatedAt DESC`, then ID |
| GET | `/api/v1/classes/me` | Teacher | Current classes of JWT subject; page/pageSize |
| GET | `/api/v1/classes/{id}` | Admin/Teacher | Detail; Admin receives assignment history, Teacher requires current assignment and does not receive history |
| PATCH | `/api/v1/classes/{id}` | Admin | Partially edit metadata. Omitted fields remain unchanged; explicit `null` clears optional Description/StartDate/EndDate. Code remains immutable. |
| PATCH | `/api/v1/classes/{id}/status` | Admin | Transition via `{ "status": "Active" }`, etc. |
| POST | `/api/v1/classes/{id}/archive` | Admin | Transition to Archived |
| POST | `/api/v1/classes/{id}/restore` | Admin | Restore Archived to its recorded `ArchivedFromStatus` |
| DELETE | `/api/v1/classes/{id}` | Admin | Soft delete Archived; returns 204 |
| POST | `/api/v1/classes/{id}/admin-restore` | Admin | Restore Deleted directly to its original Draft/Active/Completed status |
| DELETE | `/api/v1/classes/{id}/permanent` | Admin | Permanently delete eligible Deleted class; returns 204 |
| PUT | `/api/v1/classes/{id}/teacher` | Admin | Assign/reassign using `{ "teacherId": "..." }` |
| DELETE | `/api/v1/classes/{id}/teacher` | Admin | Unassign current teacher from Draft only |

Typical errors: `class_not_found` (404), `duplicate_code`/`unique_constraint` (409), `invalid_transition` (409), `restore_state_unknown` (409), `delete_blocked` (409), `invalid_date_range` (400), `immutable_code` (400), `teacher_required` (400/409), `notteacher`/`inactive` (400), `teacher_not_synchronized` (503), `forbidden_class` (403), `concurrency_conflict` (409), `invalid_state` (409).

For example, correcting only a class name uses `PATCH /api/v1/classes/{id}` with `{ "name": "TOEIC MASTER 850+" }`; description and dates are retained. Send `{ "description": null }` only when the description should be cleared. Lifecycle changes remain on the separate `/status` endpoint.

## Running and extending

Set both database connections, shared RabbitMQ settings and JWT keys/issuer/audience. Apply the new Identity and Classroom migrations before deployment. Docker Compose's `classroom` profile includes both databases, RabbitMQ, Identity and Classroom.

When changing lifecycle behavior, update the `Class` entity, use cases, tests and this document. Teacher contract changes belong in BuildingBlocks Messaging and both producer/consumer tests. Preserve assignment transactions, Class Version and unique-index behavior.
