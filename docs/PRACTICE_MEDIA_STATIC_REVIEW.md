# Practice media failure and Parts 1–7 static review

Reviewed on 2026-11-17. This review used source files and the supplied screenshots/HTTP response only. No compiler, build, test runner, application, database query, upstream media request, or container command was run.

## Confirmed failure boundary

The failing request is `POST /api/v1/practice-sets/{id}/attempts`, with HTTP 409 and `code: media_preservation_failed`. The screenshot shows a Part 4 collection with 480 questions. This is an attempt-start failure during media preparation, before an attempt is persisted; it is not an answer submission or scoring failure.

The path is:

1. `PracticeStartAction` looks up an active attempt and invokes the start API.
2. `PracticeAttemptHandlers` loads the published source and calls `IAttemptMediaStore.PreserveAsync` outside database locks.
3. `RetainedAttemptMediaStore` downloads the question/passage media and retains immutable, content-addressed copies in R2.
4. Previously, every failure in this stage became the same 409 and generic message, without a diagnostic reason in the response.

The latest supplied logs identify the primary failure: source fingerprint `40d4e8fa6d7b` returns `source_http_404`; preparation of 179 references fails after 8703 ms. This is a not-found response from the external source GET, not R2's normal metadata-miss/upload path. The earlier hypothesis of missing R2 configuration is superseded for this request. The logs do not prove whether the source file was removed or the stored URL is wrong; resolving that distinction requires locating the stored reference and verifying the intended asset.

The following `media_timeout` warnings are explained by a confirmed cancellation-classification bug: `Parallel.ForEachAsync` cancels sibling workers after the first fault, but the old catch filter recognized only cancellation of the original request token. Sibling cancellations therefore fell through to the generic timeout mapper. The filter now recognizes the worker's cancellation token. A genuine 30-second transfer deadline still maps to `media_timeout`; request and sibling cancellations do not produce independent source-failure warnings. The primary exception and its source fingerprint are preserved through the outer handler and into the API's additive `sourceReference` field.

The import pipeline's `resolve_media` accepts any absolute source URL, while its "unplayable" check detects missing URL values rather than live HTTP availability. The publishing validators likewise check syntax and required fields, not remote existence. A nonempty absolute URL returning 404 can therefore remain in an Active set in any Part. No import snapshot or seed containing the affected reference is present in this workspace; only the manual explanation data is available. No live database or source URL was queried.

## Repairing the reported source

The actual media-data repair remains pending: restore the intended upstream file or replace the broken field with a verified URL for the same asset through the existing versioned question/passage editing workflow. Retrying a permanent 404, increasing timeouts, substituting unrelated audio, or omitting the required asset does not repair the exercise.

[`scripts/find_practice_media_reference.sql`](../scripts/find_practice_media_reference.sql) is a read-only MySQL 8 lookup, prefilled with the reported set ID and fingerprint. It follows the set's question and passage references, splits multi-page image fields, and matches the same first 12 hex characters of SHA-256 over trimmed UTF-8 URLs as the source logger. It returns entity type, entity ID, field and fingerprint only; it neither outputs signed URLs nor changes records. It works for Parts 1–7 by changing the two requested values. Execute it separately only when database inspection is authorized, then inspect/correct the identified field privately. The query was reviewed statically and was not executed.

The Compose service runs published DLLs from its Docker image without a source bind mount. Applying C# changes requires a new deployed image; recreating an existing image only reloads environment configuration. No image build, deployment or container command was performed by the agent.

## Defects resolved

| Finding | Correction |
| --- | --- |
| Every HTTP redirect was rejected, including redirects within a configured trusted origin. Origin comparison also rejected otherwise equivalent origins with a trailing slash. | Added a source policy that canonicalizes configured origins, follows at most five redirects, validates every target, and rejects credentials, untrusted origins and HTTPS downgrades. Automatic redirects and cookies remain disabled. |
| Unsupported HTTP MIME labels caused valid media to be rejected before examining its bytes. Conversely, a recognized label bypassed content detection entirely. | Determine the canonical type from recognizable image/audio signatures. HTML/error bodies do not qualify merely because the response claims to be an MP3 or image. |
| Formats accepted with explicit MIME types were rejected with generic labels because their signatures were missing: Ogg audio, WebM, common MP4 containers, compatible-brand AVIF and ADIF AAC. | Extended the shared detector, preserving PNG/JPEG/GIF/WebP/WAV/FLAC/MP3/ADTS support. Bounded prefix reads, nonempty bodies and the existing 25 MiB limit remain enforced. |
| Partial HTTP responses could become an apparently successful retained asset. | Reject unsolicited 206 responses instead of retaining incomplete media. |
| Every retry redownloaded every source before checking whether its bytes already existed in storage. A failure in a large collection repeated completed work. | Cache retained-source validators through the existing `IContentCache`. Verify the immutable object exists, then revalidate the final upstream resource with a strong ETag or Last-Modified. Only a conditional 304 permits reuse; a changed source is downloaded and hashed again. Sources without validators still receive full downloads. Cache keys contain hashes, not source URLs. |
| Transient upstream network/408/429/5xx failures had no bounded source retry. | Retry once per redirect hop within the existing transfer deadline; respect a long Retry-After by returning the failure rather than retrying prematurely. |
| Blank/malformed storage configuration and permanent source failures appeared as a generic instruction to retry later. | Added `PracticeMediaException` with a safe `reason` Problem Details extension. The frontend distinguishes configuration/access, missing media, invalid content and timeouts, while retaining the legacy fallback. |
| A primary media failure cancelled sibling workers, which were then incorrectly reported as separate timeouts; the outer exception also lost the source fingerprint. | Preserve worker cancellation semantics and the primary exception. Expose the safe source fingerprint in Problem Details and provide a read-only record lookup. |
| Invalid or stale starts could perform external media work before failing source/generation validation. | Validate the generation and publishability before preservation, retaining the authoritative checks inside the transaction. |
| Part 6 attempt blank labels searched all questions in the attempt. Collections spanning multiple passages/tests can repeat source numbers such as 131. | Limit the lookup to the current passage and identify the current source question for highlighting. Partial selections retain the helper's conservative fallback. |

## Per-Part review

| Part | Static review result |
| --- | --- |
| 1 | Required photograph and question audio use the shared preservation path. Image URLs, audio signatures, retry/error handling and retained routes were reviewed. Four answer choices remain intact. |
| 2 | Question audio uses the same corrected path. Frontend options and backend `ToeicPartRules.IsValidAnswer` both retain A–C only. |
| 3 | Reviewed passage/question audio fallback, duplicate shared-media suppression, separate question audio, transcripts and four choices. The shared copier fixes apply to both question and passage assets. |
| 4 | Same grouped listening path as Part 3; the start failure originates in media preservation. Reviewed the large-collection deadline/retry path and snapshot creation after preservation. |
| 5 | Text-only sets still skip storage/configuration work when there are no media references. Optional media uses the shared copier. Four-choice validation remains intact. |
| 6 | Reviewed passage rendering, ordered snapshot question IDs, source/local blank labels and four-choice validation. Fixed the cross-passage label lookup. |
| 7 | Reviewed passage/question images, `<image_split>` multi-page references, media deduplication and source-order rendering. Every split image remains preserved and replaced; reading images retain their unrestricted height. |

## Modified-file review and invariants

- Reviewed `PracticeAttemptHandlers.cs`, `PracticeMediaException.cs`, `RetainedAttemptMediaStore.cs`, `AttemptMediaSourcePolicy.cs`, `AttemptMediaContentTypes.cs`, `GlobalExceptionHandler.cs`, frontend `PracticeStartAction.tsx` and `PracticeAttemptPage.tsx`, and `find_practice_media_reference.sql`.
- Reviewed the existing media URL splitters, validators, publishing guard, cache implementation/DI registration, API enum serialization, frontend answer schema, shared question renderer and autosave path for compatibility.
- Storage/source I/O still occurs before database locks. The source comparison, publishing validation and generation fence are checked again before snapshot persistence. A preservation failure does not create a partial attempt.
- No migration, schema change or new package is required. `IContentCache` is already registered; its Redis/memory fallback and best-effort failure behavior are reused.
- The media error retains HTTP 409 and `code: media_preservation_failed`; `reason` is additive. Legacy frontend/backend combinations retain a generic fallback.
- Source diagnostics log a URL hash and bounded reason rather than signed URLs, upstream bodies, tokens, credentials or raw exceptions. Storage failures remain distinguishable from source failures.
- Conditional headers are scoped to the exact final-resource hash. Redirect changes do not reuse validators from a different resource. Unsolicited 304 responses are rejected, and missing retained objects trigger a full copy.
- Failed/cancelled transfer resources are disposed. The existing R2-compatible upload options remain in place. In the retained-media read path, a failed copy now also disposes its allocated stream.
- Existing answer saving, ownership, expiry, conflict resolution, Part 2 option restrictions and snapshot privacy are preserved.

## Limits and deferred verification

The latest user-supplied logs establish the upstream 404 as the reported primary failure; this review does not claim to have reproduced it, restored the missing asset or passed runtime verification. Invalid credentials, missing source objects, new unlisted redirect origins and genuine upstream outages still require correction at their source; they are not bypassed. The 30-second per-transfer and 75-second total budgets remain bounded. A sufficiently slow cold collection can still exceed those limits; validator reuse reduces repeat work but is not an asynchronous preparation job or an unlimited-time guarantee.

When execution is authorized separately, verify the reported Part 4 collection and representative Parts 1–7; include trusted/untrusted redirects, alternate MIME labels, malformed bodies, all supported signatures, 206/304 responses, changed ETags, missing retained objects, cache loss, cancellation, storage permission errors and multiple Part 6 passages with repeated source question numbers. These scenarios were reviewed statically, not executed.
