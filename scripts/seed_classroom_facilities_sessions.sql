-- DEMO DATA ONLY. Run against a disposable MySQL 8.4 database whose name ends in _test.
-- IMPORTANT: always connect with --default-character-set=utf8mb4, or Vietnamese text is mangled.
-- Requires Classroom migrations through 20261114032949_AddClassScheduleAudit.
-- Does not create/alter real branches or overwrite existing room assignments.
-- One active class with a currently assigned, eligible teacher and >= 14 days remaining
-- receives two weekly schedules, three regular sessions, one makeup session and audit rows.
-- Dates are calculated in Asia/Ho_Chi_Minh (+07:00); stored session times are UTC.
-- Safe to rerun: fixed IDs and NOT EXISTS checks; cancelled sessions stay cancelled.

-- Use the mysql client option --init-command="SET @allow_non_test_seed=1" to opt in to a
-- non-_test database deliberately. Without it the script writes nothing outside *_test.
SET @seed_allowed = (RIGHT(DATABASE(), 5) = '_test'
        OR COALESCE(@allow_non_test_seed, 0) = 1)
    AND @@character_set_client = 'utf8mb4'
    AND (SELECT COUNT(*) FROM `__EFMigrationsHistory` WHERE `MigrationId` IN (
        '20261113180417_AddCentersRoomsAndClassDefaultRoom',
        '20261114025837_AddClassSchedulesAndSessions',
        '20261114032949_AddClassScheduleAudit')) = 3;

SET @center_id = 'f2000000-0000-4000-8000-000000000001';
SET @room_id = 'f2000000-0000-4000-8000-000000000002';
SET @now = UTC_TIMESTAMP(6);
SET @local_today = DATE(@now + INTERVAL 7 HOUR);
-- Always the *next* Monday, not an already-started Monday morning.
SET @monday = DATE_ADD(@local_today, INTERVAL
    IF(MOD(9 - DAYOFWEEK(@local_today), 7) = 0, 7, MOD(9 - DAYOFWEEK(@local_today), 7)) DAY);
SET @last_day = DATE_ADD(@monday, INTERVAL 13 DAY);

SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
START TRANSACTION;

INSERT INTO `Centers` (`Id`, `Name`, `Address`, `TimeZoneId`, `Status`, `CreatedAt`, `UpdatedAt`)
SELECT @center_id, 'DEMO - Chi nhánh TOEIC', 'DỮ LIỆU MẪU - không dùng làm địa chỉ thật',
       'Asia/Ho_Chi_Minh', 'Active', @now, @now
WHERE @seed_allowed AND NOT EXISTS (SELECT 1 FROM `Centers` WHERE `Id` = @center_id);

INSERT INTO `Rooms` (`Id`, `CenterId`, `Name`, `Capacity`, `Status`, `CreatedAt`, `UpdatedAt`)
SELECT @room_id, @center_id, 'DEMO - Phòng A101', 200, 'Active', @now, @now
WHERE @seed_allowed AND EXISTS (SELECT 1 FROM `Centers` WHERE `Id` = @center_id AND `Name` = 'DEMO - Chi nhánh TOEIC')
  AND NOT EXISTS (SELECT 1 FROM `Rooms` WHERE `Id` = @room_id);

-- Only an eligible class with no default room is assigned the demonstration room.
SET @class_id = (SELECT c.`Id` FROM `Classes` c
    JOIN `ClassInstructors` i ON i.`ClassId` = c.`Id` AND i.`UnassignedAt` IS NULL
    JOIN `TeacherReadModels` t ON t.`TeacherId` = i.`TeacherId` AND t.`IsEligible` = 1
    WHERE @seed_allowed AND c.`Status` = 'Active'
      AND (c.`DefaultRoomId` IS NULL OR c.`DefaultRoomId` = @room_id)
      AND c.`StartDate` <= @monday AND c.`EndDate` >= @last_day
      AND (SELECT COUNT(*) FROM `ClassEnrollments` e WHERE e.`ClassId` = c.`Id`
           AND e.`Status` = 'Active' AND e.`WithdrawnAt` IS NULL) <= 200
    ORDER BY CASE WHEN c.`DefaultRoomId` = @room_id THEN 0 ELSE 1 END, c.`Id` LIMIT 1);
SET @teacher_id = (SELECT i.`TeacherId` FROM `ClassInstructors` i
    WHERE i.`ClassId` = @class_id AND i.`UnassignedAt` IS NULL LIMIT 1);
SET @actor_id = (SELECT c.`CreatedBy` FROM `Classes` c WHERE c.`Id` = @class_id);

SET @mon_hash = MD5(CONCAT('toeic-demo-mon-', @class_id));
SET @thu_hash = MD5(CONCAT('toeic-demo-thu-', @class_id));
SET @makeup_hash = MD5(CONCAT('toeic-demo-makeup-', @class_id));
SET @mon_id = LOWER(CONCAT(SUBSTR(@mon_hash,1,8),'-',SUBSTR(@mon_hash,9,4),'-',
    SUBSTR(@mon_hash,13,4),'-',SUBSTR(@mon_hash,17,4),'-',SUBSTR(@mon_hash,21,12)));
SET @thu_id = LOWER(CONCAT(SUBSTR(@thu_hash,1,8),'-',SUBSTR(@thu_hash,9,4),'-',
    SUBSTR(@thu_hash,13,4),'-',SUBSTR(@thu_hash,17,4),'-',SUBSTR(@thu_hash,21,12)));
SET @makeup_id = LOWER(CONCAT(SUBSTR(@makeup_hash,1,8),'-',SUBSTR(@makeup_hash,9,4),'-',
    SUBSTR(@makeup_hash,13,4),'-',SUBSTR(@makeup_hash,17,4),'-',SUBSTR(@makeup_hash,21,12)));

-- Same protocol as the app: lock Class, then Room and Teacher resource rows.
SELECT `Id` INTO @locked_class FROM `Classes` WHERE `Id` = @class_id FOR UPDATE;
INSERT IGNORE INTO `SchedulingResources` (`Key`)
SELECT CONCAT('R:', LOWER(REPLACE(@room_id,'-',''))) WHERE @class_id IS NOT NULL;
INSERT IGNORE INTO `SchedulingResources` (`Key`)
SELECT CONCAT('T:', LOWER(REPLACE(@teacher_id,'-',''))) WHERE @class_id IS NOT NULL;
SELECT `Key` FROM `SchedulingResources`
WHERE `Key` IN (CONCAT('R:', LOWER(REPLACE(@room_id,'-',''))),
                CONCAT('T:', LOWER(REPLACE(@teacher_id,'-',''))))
ORDER BY `Key` FOR UPDATE;

-- Abort *scheduling* if any intended occurrence conflicts with an existing,
-- non-cancelled lesson for this teacher or room. Existing demo IDs are ignored on reruns.
SET @conflicts = (SELECT COUNT(*) FROM
    (SELECT 0 AS `DayOffset` UNION ALL SELECT 3 UNION ALL SELECT 7 UNION ALL SELECT 4) d
    JOIN `ClassSessions` s ON s.`Status` <> 'Cancelled'
      AND s.`Id` <> @makeup_id
      AND (s.`ClassScheduleId` IS NULL OR s.`ClassScheduleId` NOT IN (@mon_id, @thu_id))
      AND s.`StartAt` < TIMESTAMP(DATE_ADD(@monday, INTERVAL d.`DayOffset` DAY),
          CASE WHEN d.`DayOffset` IN (0,7) THEN '03:00:00' WHEN d.`DayOffset` = 3 THEN '08:00:00' ELSE '10:00:00' END)
      AND s.`EndAt` > TIMESTAMP(DATE_ADD(@monday, INTERVAL d.`DayOffset` DAY),
          CASE WHEN d.`DayOffset` IN (0,7) THEN '02:00:00' WHEN d.`DayOffset` = 3 THEN '07:00:00' ELSE '09:00:00' END)
      AND (s.`TeacherId` = @teacher_id OR s.`Mode` = 'Offline' AND s.`RoomId` = @room_id));
SET @create_sessions = @class_id IS NOT NULL AND @conflicts = 0
    AND EXISTS (SELECT 1 FROM `Rooms` r JOIN `Centers` c ON c.`Id` = r.`CenterId`
                WHERE r.`Id` = @room_id AND r.`CenterId` = @center_id
                  AND r.`Name` = 'DEMO - Phòng A101' AND r.`Status` = 'Active'
                  AND c.`Name` = 'DEMO - Chi nhánh TOEIC' AND c.`Status` = 'Active'
                  AND r.`Capacity` >= (SELECT COUNT(*) FROM `ClassEnrollments` e
                       WHERE e.`ClassId` = @class_id AND e.`Status` = 'Active' AND e.`WithdrawnAt` IS NULL))
    -- Do not append occurrences to an old demo schedule if the script is rerun in a later week.
    AND NOT EXISTS (SELECT 1 FROM `ClassSchedules` s WHERE s.`Id` IN (@mon_id, @thu_id)
         AND (s.`ClassId` <> @class_id OR s.`TeacherId` <> @teacher_id
              OR s.`EffectiveFrom` <> @monday OR s.`EffectiveTo` <> @last_day
              OR s.`Mode` <> 'Offline' OR s.`TimeZoneId` <> 'Asia/Ho_Chi_Minh'
              OR s.`Id` = @mon_id AND (s.`StartTime` <> '09:00:00' OR s.`EndTime` <> '10:00:00'
                  OR JSON_LENGTH(s.`DaysOfWeek`) <> 1
                  OR JSON_UNQUOTE(JSON_EXTRACT(s.`DaysOfWeek`, '$[0]')) <> '1'
                  OR JSON_LENGTH(s.`ExcludeDates`) <> 0)
              OR s.`Id` = @thu_id AND (s.`StartTime` <> '14:00:00' OR s.`EndTime` <> '15:00:00'
                  OR JSON_LENGTH(s.`DaysOfWeek`) <> 1
                  OR JSON_UNQUOTE(JSON_EXTRACT(s.`DaysOfWeek`, '$[0]')) <> '4'
                  OR JSON_LENGTH(s.`ExcludeDates`) <> 1
                  OR JSON_UNQUOTE(JSON_EXTRACT(s.`ExcludeDates`, '$[0]')) <>
                      DATE_FORMAT(DATE_ADD(@monday, INTERVAL 10 DAY), '%Y-%m-%d'))));

UPDATE `Classes` SET `DefaultRoomId` = @room_id,
    `Version` = `Version` + 1, `UpdatedAt` = @now, `UpdatedBy` = @actor_id
WHERE `Id` = @class_id AND `DefaultRoomId` IS NULL AND @create_sessions;
SET @assigned_room = ROW_COUNT();

SET @room_change_hash = MD5(CONCAT('toeic-demo-room-change-', @class_id));
SET @room_change_id = LOWER(CONCAT(SUBSTR(@room_change_hash,1,8),'-',SUBSTR(@room_change_hash,9,4),'-',
    SUBSTR(@room_change_hash,13,4),'-',SUBSTR(@room_change_hash,17,4),'-',SUBSTR(@room_change_hash,21,12)));
INSERT INTO `ClassRoomChanges` (`Id`, `ClassId`, `PreviousRoomId`, `NewRoomId`, `ChangedBy`, `ChangedAt`, `Reason`)
SELECT @room_change_id, @class_id, NULL, @room_id, @actor_id, @now,
       'DEMO: gán phòng mẫu cho dữ liệu kiểm thử'
WHERE @assigned_room = 1 AND NOT EXISTS (SELECT 1 FROM `ClassRoomChanges` WHERE `Id` = @room_change_id);

-- C# DayOfWeek: Monday=1, Thursday=4. Exclude second Thursday from the Thursday slot.
INSERT INTO `ClassSchedules` (`Id`, `ClassId`, `DaysOfWeek`, `StartTime`, `EndTime`, `TeacherId`,
    `Mode`, `TimeZoneId`, `EffectiveFrom`, `EffectiveTo`, `ExcludeDates`, `CreatedAt`, `UpdatedAt`, `Version`)
SELECT @mon_id, @class_id, JSON_ARRAY(1), '09:00:00', '10:00:00', @teacher_id,
       'Offline', 'Asia/Ho_Chi_Minh', @monday, @last_day, JSON_ARRAY(), @now, @now, 0
WHERE @create_sessions AND NOT EXISTS (SELECT 1 FROM `ClassSchedules` WHERE `Id` = @mon_id);
INSERT INTO `ClassSchedules` (`Id`, `ClassId`, `DaysOfWeek`, `StartTime`, `EndTime`, `TeacherId`,
    `Mode`, `TimeZoneId`, `EffectiveFrom`, `EffectiveTo`, `ExcludeDates`, `CreatedAt`, `UpdatedAt`, `Version`)
SELECT @thu_id, @class_id, JSON_ARRAY(4), '14:00:00', '15:00:00', @teacher_id,
       'Offline', 'Asia/Ho_Chi_Minh', @monday, @last_day,
       JSON_ARRAY(DATE_FORMAT(DATE_ADD(@monday, INTERVAL 10 DAY), '%Y-%m-%d')), @now, @now, 0
WHERE @create_sessions AND NOT EXISTS (SELECT 1 FROM `ClassSchedules` WHERE `Id` = @thu_id);

-- Stable audit IDs: rerunning will not duplicate history.
INSERT INTO `ClassScheduleAudits` (`Id`, `ClassScheduleId`, `ActorId`, `ChangedAt`, `Action`, `PreviousValue`, `NewValue`)
SELECT s.`Id`, s.`Id`, @actor_id, s.`CreatedAt`, 'Created', NULL,
       JSON_OBJECT('DaysOfWeek', s.`DaysOfWeek`, 'StartTime', s.`StartTime`, 'EndTime', s.`EndTime`,
           'TeacherId', s.`TeacherId`, 'Mode', s.`Mode`, 'TimeZoneId', s.`TimeZoneId`,
           'EffectiveFrom', s.`EffectiveFrom`, 'EffectiveTo', s.`EffectiveTo`,
           'ExcludeDates', s.`ExcludeDates`, 'Version', s.`Version`)
FROM `ClassSchedules` s WHERE @create_sessions AND s.`Id` IN (@mon_id, @thu_id)
  AND NOT EXISTS (SELECT 1 FROM `ClassScheduleAudits` a WHERE a.`Id` = s.`Id`);

-- Dates 0, 3, 7 -> Monday, Thursday, next Monday. UTC = local time minus 7h.
INSERT INTO `ClassSessions` (`Id`, `ClassId`, `ClassScheduleId`, `OriginalLocalDate`, `TeacherId`,
    `Title`, `Description`, `SessionType`, `StartAt`, `EndAt`, `Mode`, `RoomId`, `Status`,
    `Notes`, `CancellationReason`, `CompletedAt`, `TimeOverridden`, `RoomOverridden`,
    `TeacherOverridden`, `ModeOverridden`, `CreatedAt`, `UpdatedAt`, `Version`)
SELECT LOWER(CONCAT(SUBSTR(h.`Hash`,1,8),'-',SUBSTR(h.`Hash`,9,4),'-',SUBSTR(h.`Hash`,13,4),'-',
       SUBSTR(h.`Hash`,17,4),'-',SUBSTR(h.`Hash`,21,12))),
       @class_id, h.`ScheduleId`, h.`LocalDate`, @teacher_id, 'DEMO - Luyện TOEIC',
       'Buổi học mẫu; không dùng cho lớp thực tế', 'Regular',
       TIMESTAMP(h.`LocalDate`, h.`StartUtc`), TIMESTAMP(h.`LocalDate`, h.`EndUtc`),
       'Offline', @room_id, 'Scheduled', NULL, NULL, NULL, 0, 0, 0, 0, @now, @now, 0
FROM (SELECT @mon_id `ScheduleId`, @monday `LocalDate`, '02:00:00' `StartUtc`, '03:00:00' `EndUtc`,
             MD5(CONCAT('toeic-demo-session:', @mon_id, ':', @monday)) `Hash`
      UNION ALL SELECT @thu_id, DATE_ADD(@monday, INTERVAL 3 DAY), '07:00:00', '08:00:00',
             MD5(CONCAT('toeic-demo-session:', @thu_id, ':', DATE_ADD(@monday, INTERVAL 3 DAY)))
      UNION ALL SELECT @mon_id, DATE_ADD(@monday, INTERVAL 7 DAY), '02:00:00', '03:00:00',
             MD5(CONCAT('toeic-demo-session:', @mon_id, ':', DATE_ADD(@monday, INTERVAL 7 DAY)))) h
WHERE @create_sessions AND NOT EXISTS (SELECT 1 FROM `ClassSessions` s
    WHERE s.`ClassScheduleId` = h.`ScheduleId` AND s.`OriginalLocalDate` = h.`LocalDate`);

INSERT INTO `ClassSessions` (`Id`, `ClassId`, `ClassScheduleId`, `OriginalLocalDate`, `TeacherId`,
    `Title`, `Description`, `SessionType`, `StartAt`, `EndAt`, `Mode`, `RoomId`, `Status`,
    `Notes`, `CancellationReason`, `CompletedAt`, `TimeOverridden`, `RoomOverridden`,
    `TeacherOverridden`, `ModeOverridden`, `CreatedAt`, `UpdatedAt`, `Version`)
SELECT @makeup_id, @class_id, NULL, NULL, @teacher_id,
       'DEMO - Buổi học bù', NULL, 'Makeup',
       TIMESTAMP(DATE_ADD(@monday, INTERVAL 4 DAY), '09:00:00'),
       TIMESTAMP(DATE_ADD(@monday, INTERVAL 4 DAY), '10:00:00'),
       'Offline', @room_id, 'Scheduled', 'Demo makeup', NULL, NULL, 0, 0, 0, 0, @now, @now, 0
WHERE @create_sessions AND NOT EXISTS (SELECT 1 FROM `ClassSessions` WHERE `Id` = @makeup_id);

INSERT INTO `ClassSessionAudits` (`Id`, `ClassSessionId`, `ActorId`, `ChangedAt`,
    `Action`, `Reason`, `PreviousValue`, `NewValue`)
SELECT s.`Id`, s.`Id`, @actor_id, s.`CreatedAt`,
       CASE WHEN s.`SessionType` = 'Makeup' THEN 'Created' ELSE 'Generated' END,
       NULL, NULL,
       JSON_OBJECT('TeacherId', s.`TeacherId`, 'StartAt', s.`StartAt`, 'EndAt', s.`EndAt`,
           'Mode', s.`Mode`, 'RoomId', s.`RoomId`, 'Status', s.`Status`,
           'Title', s.`Title`, 'SessionType', s.`SessionType`)
FROM `ClassSessions` s WHERE @create_sessions AND s.`ClassId` = @class_id
  AND (s.`ClassScheduleId` IN (@mon_id, @thu_id) OR s.`Id` = @makeup_id)
  AND NOT EXISTS (SELECT 1 FROM `ClassSessionAudits` a WHERE a.`Id` = s.`Id`);

COMMIT;

SELECT DATABASE() AS `SeedDatabase`, @seed_allowed AS `TestSchemaAndMigrationsReady`,
       @class_id AS `AssignedClassId`, @conflicts AS `PreexistingSessionConflicts`,
       @create_sessions AS `CreatedOrReusedDemoSessions`;
SELECT 'Centers' AS `TableName`, COUNT(*) AS `DemoRows` FROM `Centers` WHERE `Id` = @center_id
UNION ALL SELECT 'Rooms', COUNT(*) FROM `Rooms` WHERE `Id` = @room_id
UNION ALL SELECT 'ClassRoomChanges', COUNT(*) FROM `ClassRoomChanges` WHERE `Id` = @room_change_id
UNION ALL SELECT 'ClassSchedules', COUNT(*) FROM `ClassSchedules` WHERE `Id` IN (@mon_id, @thu_id)
UNION ALL SELECT 'ClassSessions', COUNT(*) FROM `ClassSessions`
  WHERE `ClassScheduleId` IN (@mon_id, @thu_id) OR `Id` = @makeup_id
UNION ALL SELECT 'ClassScheduleAudits', COUNT(*) FROM `ClassScheduleAudits` WHERE `ClassScheduleId` IN (@mon_id, @thu_id)
UNION ALL SELECT 'ClassSessionAudits', COUNT(*) FROM `ClassSessionAudits` WHERE `ClassSessionId` IN
  (SELECT `Id` FROM `ClassSessions` WHERE `ClassScheduleId` IN (@mon_id, @thu_id) OR `Id` = @makeup_id)
UNION ALL SELECT 'SchedulingResources', COUNT(*) FROM `SchedulingResources` WHERE `Key` IN
  (CONCAT('T:', LOWER(REPLACE(@teacher_id,'-',''))), CONCAT('R:', LOWER(REPLACE(@room_id,'-',''))));
