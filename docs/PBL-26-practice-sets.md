# PBL-26: Practice Set management and browsing

Approved scope: manual Question Bank selection and browsing, with no PDF import or
Attempt execution. Existing schema, enum storage, migrations and Gateway routes are reused.

## API and ownership

Use the existing `/assessment/api/v1/practice-sets` Gateway prefix:

| Operation | Endpoint |
| --- | --- |
| List | `GET /` with `page`, `pageSize`, `search`, `part`, `kind`, `level`, `status` |
| Owned management list | Same endpoint with `ownedOnly=true` (Teacher/Admin) |
| Metadata and ordered IDs | `GET /{id}` |
| Questions, passages, answers, explanations | `GET /{id}/content` (authenticated) |
| Create Draft | `POST /` |
| Edit metadata | `PUT /{id}` |
| Replace entire ordered question list | `PUT /{id}/items` with `questionIds` and `expectedQuestionIds` |
| Publish/unpublish | `PATCH /{id}/status` with `Active`/`Archived` |
| Soft delete | `DELETE /{id}` |

`createdByUserId` is a nullable manager-only field on list/detail responses. It is
omitted from anonymous and learner JSON responses. Existing
request fields, pagination, ordering and status names remain compatible. Ownership is
assigned on creation only. Teacher mutations require their own non-null owner ID;
Admin can mutate all sets without taking ownership, including imported ownerless sets.
Authenticated Users are read-only. Anonymous metadata browsing and existing manager
draft visibility remain supported. `ownedOnly` filters on the server before pagination.
Forbidden mutations return 403; unavailable/deleted content returns 404. Content access
checks visibility before looking in the cache, including cache hits after unpublishing.

`Level` sets require Level 1–5. Topic and PartDrill require null Level. UI labels Active
as “Đã xuất bản” and Archived as “Đã ngừng xuất bản”; stored enums are unchanged.

## Publishing and editing

Publishing, republishing and Active membership replacements validate every selected question:
nonempty collection; existing, nondeleted, Active questions of the set's Part; existing
question and passage validators (options, correct answer, safe content/media, placement);
matching nondeleted passages when required; audio on the question or passage for listening;
question text for Parts 3/4/5/7; reading passage content or image for Parts 6/7.
Part 1/2 prompts may be null and Part 6 may use the passage's blanks.
Deleting or archiving an existing bank question remains governed by the existing bank guards.

Metadata, ordered-list and status writes lock the set row with MySQL `FOR UPDATE` inside
a transaction. The EF execution strategy retries the whole transaction on transient errors.
This serializes writes to one set and prevents publishing/replacement races. No revision
column was added. Metadata PUT accepts optional `expectedUpdatedAt` (the last detail
`updatedAt`, or `createdAt` before the first update). It is checked inside the set lock
and returns 409 `concurrency_conflict` for stale writes. Clients omitting it retain
last-write-wins behavior for compatibility. Compare at MySQL datetime(6) precision.
Publishing and Active membership edits also lock referenced questions, then passages, in sorted ID
order. Ordered-list replacement locks both stored and requested questions before editing
links. Question Bank create/update/status/delete and passage update/delete use the same
READ COMMITTED transaction helper and lock protocol. Bank writers never lock set rows:
their guards read committed set state after acquiring the content locks. Content edits
save and validate only changed questions and their passages against affected Active
set memberships before commit. Unrelated invalid legacy siblings do not block a valid
edit. Metadata edits use their metadata validator, without revalidating legacy bank content.
Full-set strict validation remains required for publishing and membership replacement.
Invalid edits roll back content,
versions and transactional outbox events. Valid bank edits remain allowed. Cache eviction
runs after the transaction commits.

Full-test deletion participates too: it first locks its Test row, then questions and passages, and only
mutates those locked records. If another question was added/moved into the test before
the passage locks were acquired, deletion returns the existing concurrency-conflict code
so the caller can reload; it never acquires question locks after passage locks. Existing
reused-question/passage detachment behavior is preserved. CreateQuestion/CreatePassage
and moves into a Test acquire that Test lock before content locks and recheck its visibility.
Both question and passage membership are rechecked within deletion’s transaction.
Multi-row content locks use parameterized `WHERE Id IN (...) ORDER BY Id FOR UPDATE`;
shared passage content is validated once per publishing operation. No global content lock is used;
independent sets with independent content can make progress concurrently.

Reordering updates retained composite-key links and adds/removes only changed links.
Soft deletion touches the set's DeletedAt; questions and retained relationships remain.
The API does not write Attempt, AttemptAnswer or question-history data.

## PBL-28 immutable Attempt snapshot contract

PBL-28 must capture a complete immutable snapshot **when a student starts practice**,
before returning runnable content or accepting answers. It must:

1. Authenticate the student and recheck that the set is Active and available.
2. Read metadata, ordered membership, all question versions, and referenced passages
   in one consistent database transaction. Coordinate with the set write lock and bank
   writes or use an appropriate repeatable-read snapshot; a sequence of cached HTTP reads
   is insufficient to guarantee consistency.
3. Persist source Set/Question/Passage IDs for provenance, snapshot schema version and
   captured timestamp; metadata (title, description, Part, Kind, applicable Level,
   duration/target score), exact item positions, question text/options/correct answers/
   explanation/transcripts/translations, and passage content/media needed for review.
4. Use snapshotted answers and content for execution, grading and history, independent
   of later set edits, unpublishing, deletion, bank updates and cache eviction.
5. Preserve private answer data on the server until the attempt's answer visibility
   policy permits disclosure. PBL-26's authenticated read-only preview deliberately
   includes answers and is not the future attempt-execution endpoint.
6. Preserve source media availability (versioned keys or retained objects). Snapshotting
   a URL alone does not preserve a mutable/deleted remote file.

Existing Attempts only hold the current legacy data, including submitted answer keys;
they do not contain a complete start-time set/question/passage snapshot. PBL-26 never
rewrites or backfills them. Historical presentation reconstructed from live references
can still drift with permitted bank edits (e.g. explanation/media), and original content
cannot be recovered retroactively without a trustworthy prior source. PBL-28 must define
an explicit legacy-history policy. There are no Attempt rows in the supplied dump/local
baseline, while unit tests exercise preservation with a synthetic historical answer.

## Verification

`PracticeSetManagementTests` covers ownership, ownerless/Admin rules, validation,
status transitions, Active edits/order and unchanged historical answers.
`scripts/test_practice_sets_api.py` exercises the local YARP path and real MySQL,
including concurrent status/item writes. It reads existing questions and creates only
uniquely named `PBL26-SMOKE-*` sets, soft-deleting those records in cleanup. Run it only
against a local development stack with the matching signing key; tokens/keys are never printed.
Record before/after hashes of the original sets/items, questions, passages, Attempts and
migration history to verify preservation. No SQL dump is executed by these checks.

`PracticeSetMySqlConcurrencyTests` is an opt-in real-MySQL suite. Set
`PBL26_MYSQL_CONNECTION` to a connection on **localhost/127.0.0.1 only**, with rights to
create/drop disposable schemas, and run `dotnet test` with filter
`FullyQualifiedName~PracticeSetMySqlConcurrencyTests`. Each case creates a unique
`pbl26_review_<guid>` database using the current EF model, uses separate DbContexts and
controlled overlapping saves, then drops only that schema. It never imports SQL or
touches the configured Assessment database. Without the environment variable these tests
are explicitly skipped. The suite covers both operation orders, publish/archive/delete,
membership/unpublish, Active edits, invalid question/passage edits, question creation,
bulk test deletion, independent-set progress, repeated competing writes and rollback
including outbox events. MySQL FK constraints alone cannot prevent the original race:
status changes and soft deletes do not remove the referenced row.

## Review contract decisions (3 November 2026)

Status PATCH accepts only `Active` and `Archived`; `Draft` is the creation state, not
a publish/unpublish target. The frontend no longer offers an Archived-to-Draft action.
Authenticated Preview deliberately includes answers/explanations for an available set,
as specified by this existing PBL-26 contract. The PBL-26 and PBL-28 Jira descriptions
were checked: neither adds an enrollment/answer-key restriction to this Preview.
This is not an Attempt API; future Attempt answer disclosure remains server-controlled.
Content question payloads now also include `status` and `topic` so managers can identify
archived selected questions. The changes are additive; no migration or imported-data repair runs.

## Creation persistence fix (7 November 2026)

POST accepts optional ordered `questionIds` (default empty) and initial `status`
(`Draft` by default, or `Active`). Metadata, items and status commit together after
selection validation. Active creation uses the same publishing guard and content locks
as existing-set publishing. Missing/wrong-Part questions or failed publication leave
no set or items behind. The selection limits match the replace-items endpoint.

The Teacher creation page holds changes locally until Save/Publish. Discard sends no
POST or DELETE. Deploy the frontend and backend together; legacy metadata-only callers
continue to create an empty Draft explicitly. Existing saved drafts are not removed.
`PracticeSetCreationTests` covers ordered Draft/Active creation, invalid selections
without orphan records, duplicates, limits, empty IDs and invalid initial status.

## Concurrent question saves and Preview revocation (7 November 2026)

The item-replacement contract requires `expectedQuestionIds`: the exact saved ordered
list read by the client. Under the existing set write lock, the handler reads current
items in order and compares them before modifying any item. Mismatches return 409
`concurrency_conflict`; missing baselines fail request validation. This detects reorder,
addition and removal even though item changes do not update the parent's metadata audit
timestamp. No migration or new revision field is required. Clients must submit their
editing baseline, not a background refetch that arrived after local editing began.

Detail and content GET responses are non-cacheable (`no-store`). Content still checks
the live set's visibility before reading the server cache. The frontend revalidates
Preview every five seconds while visible and on focus/reconnect, clears its snapshot on
failed access checks, and does not display an old snapshot before re-entry verification.
This bounds normal visible-page revocation detection to the next poll and HTTP response;
it does not add server-pushed revocation events.

`PracticeSetAccessAndConflictTests` covers competing editor baselines, reorder/add/remove,
fresh-baseline retry, missing preconditions, and cached Preview access after unpublish/delete.
The opt-in `ConcurrentQuestionLists_WithSameBaseline_RejectTheStaleWriter` MySQL test
overlaps two independent contexts using the same baseline and verifies only the first
writer's list commits. The local API smoke script also checks missing/stale baselines.
