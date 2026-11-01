# Content modules and future lesson access

Admin manages ordered modules under `/content/api/v1/courses/{courseId}/modules` and lessons under
`/content/api/v1/courses/{courseId}/modules/{moduleId}/lessons`.
`sortOrder` is 1-based and contiguous per course; reordering submits **every** module ID exactly once.
Modules belong to one course. Archived courses remain readable but their modules cannot be changed.
Lessons reference `CourseModule.Id`; module deletion cannot cascade-delete Lessons. Lessons are
ordered 1-based within each module, support Markdown text, one video and multiple attachments.
Archived courses remain readable by Admin but lessons and resources cannot be changed.
Lesson media supports batch presign/confirm for up to 20 mixed video/attachment files. The `videoKeys`
array on Create/Update Lesson replaces the complete ordered video collection; attachments are added
through the resources bulk endpoint and remain independent of video replacement.

Only Admin can access `/content/api/v1/courses/{courseId}/modules/{moduleId}/lessons/**`.
Admin Preview returns draft or published text plus 5-minute signed GET URLs for private R2 media.
Direct uploads use Admin-only `/content/api/v1/uploads/lessons/presign/bulk` and `/confirm/bulk` before
associating confirmed owner-controlled object keys with a lesson. Unreferenced uploads/media
are cleaned after a grace period; stored lesson data contains keys, not permanent media URLs.

`IsFreePreview` defaults to `false` and is editable independently from lesson content.
Future student-facing APIs may let users view **published** lessons marked free preview without
Course enrollment or purchase; non-preview lessons require valid Course access.
The **backend** must check this on every
content request; hiding a Lesson in the frontend is not authorization. No student-facing lesson
API, access grant or payment logic is implemented here.
