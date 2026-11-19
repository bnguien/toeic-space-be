-- Read-only MySQL 8 query. Run against the Assessment database only when authorized.
-- Finds the question/passage fields matching a logged source fingerprint without
-- outputting source URLs, signed query strings or credentials. No records are changed.
-- Change these two values for a different practice set/reference (any TOEIC Part).
WITH RECURSIVE
requested AS (
    SELECT 'c7a9c10e-661f-4242-af3d-c475fd80a494' AS PracticeSetId,
           '40d4e8fa6d7b' AS SourceReference
),
selected_questions AS (
    SELECT q.Id, q.PassageId, q.AudioUrl, q.ImageUrl
    FROM ToeicPracticeSetItems AS i
    INNER JOIN ToeicQuestions AS q ON q.Id = i.QuestionId
    INNER JOIN requested AS r ON r.PracticeSetId = i.PracticeSetId
),
selected_passages AS (
    SELECT p.Id, p.AudioUrl, p.ImageUrl
    FROM ToeicPassages AS p
    INNER JOIN (
        SELECT DISTINCT PassageId
        FROM selected_questions
        WHERE PassageId IS NOT NULL
    ) AS included ON included.PassageId = p.Id
),
raw_media AS (
    SELECT 'Question' AS EntityType, Id AS EntityId, 'AudioUrl' AS Field, AudioUrl AS Value
    FROM selected_questions
    UNION ALL
    SELECT 'Question', Id, 'ImageUrl', ImageUrl FROM selected_questions
    UNION ALL
    SELECT 'Passage', Id, 'AudioUrl', AudioUrl FROM selected_passages
    UNION ALL
    SELECT 'Passage', Id, 'ImageUrl', ImageUrl FROM selected_passages
),
normalized_media AS (
    SELECT EntityType, EntityId, Field,
           REGEXP_REPLACE(Value, '<[[:space:]]*image_split[[:space:]]*/?[[:space:]]*>',
                          '<image_split>', 1, 0, 'i') AS Value
    FROM raw_media
    WHERE Value IS NOT NULL AND TRIM(Value) <> ''
),
media_tokens AS (
    SELECT EntityType, EntityId, Field,
           CAST(REGEXP_REPLACE(SUBSTRING_INDEX(Value, '<image_split>', 1),
                               '^[[:space:]]+|[[:space:]]+$', '') AS CHAR(1000)) AS Url,
           CAST(CASE WHEN LOCATE('<image_split>', Value) > 0
                THEN SUBSTRING(Value, LOCATE('<image_split>', Value) + CHAR_LENGTH('<image_split>'))
                ELSE '' END AS CHAR(1000)) AS Remaining
    FROM normalized_media
    UNION ALL
    SELECT EntityType, EntityId, Field,
           CAST(REGEXP_REPLACE(SUBSTRING_INDEX(Remaining, '<image_split>', 1),
                               '^[[:space:]]+|[[:space:]]+$', '') AS CHAR(1000)),
           CAST(CASE WHEN LOCATE('<image_split>', Remaining) > 0
                THEN SUBSTRING(Remaining, LOCATE('<image_split>', Remaining) + CHAR_LENGTH('<image_split>'))
                ELSE '' END AS CHAR(1000))
    FROM media_tokens
    WHERE Remaining <> ''
)
SELECT DISTINCT m.EntityType, m.EntityId, m.Field,
       LOWER(LEFT(SHA2(CONVERT(m.Url USING utf8mb4), 256), 12)) AS SourceReference
FROM media_tokens AS m
INNER JOIN requested AS r
    ON LOWER(LEFT(SHA2(CONVERT(m.Url USING utf8mb4), 256), 12)) = LOWER(r.SourceReference)
WHERE m.Url <> ''
ORDER BY m.EntityType, m.EntityId, m.Field;
