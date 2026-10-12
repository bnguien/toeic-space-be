CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    ALTER DATABASE CHARACTER SET utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE TABLE `OutboxMessages` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Type` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
        `Content` longtext CHARACTER SET utf8mb4 NOT NULL,
        `OccurredOn` datetime(6) NOT NULL,
        `ProcessedOn` datetime(6) NULL,
        `AttemptCount` int NOT NULL,
        `LastError` varchar(2000) CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_OutboxMessages` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE TABLE `ToeicPracticeSets` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Code` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `Title` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(2000) CHARACTER SET utf8mb4 NULL,
        `Kind` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `Part` int NOT NULL,
        `Level` int NULL,
        `TargetScore` int NULL,
        `DurationMinutes` int NOT NULL,
        `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `OrderIndex` int NOT NULL,
        `CreatedByUserId` char(36) COLLATE ascii_general_ci NULL,
        `ExternalId` char(36) COLLATE ascii_general_ci NULL,
        `Source` varchar(100) CHARACTER SET utf8mb4 NULL,
        `DeletedAt` datetime(6) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        CONSTRAINT `PK_ToeicPracticeSets` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE TABLE `ToeicTests` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Title` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `Code` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(2000) CHARACTER SET utf8mb4 NULL,
        `Category` varchar(100) CHARACTER SET utf8mb4 NULL,
        `Year` int NULL,
        `TotalQuestions` int NOT NULL,
        `DurationMinutes` int NOT NULL,
        `TotalListeningQuestions` int NOT NULL,
        `TotalReadingQuestions` int NOT NULL,
        `AudioUrl` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `IsActive` tinyint(1) NOT NULL,
        `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `CreatedByUserId` char(36) COLLATE ascii_general_ci NULL,
        `ExternalId` char(36) COLLATE ascii_general_ci NULL,
        `Source` varchar(100) CHARACTER SET utf8mb4 NULL,
        `Metadata` longtext CHARACTER SET utf8mb4 NULL,
        `DeletedAt` datetime(6) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        CONSTRAINT `PK_ToeicTests` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE TABLE `ToeicAttempts` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
        `TestId` char(36) COLLATE ascii_general_ci NOT NULL,
        `StartTime` datetime(6) NOT NULL,
        `EndTime` datetime(6) NULL,
        `DurationSeconds` int NOT NULL,
        `TotalScore` int NOT NULL,
        `ListeningScore` int NOT NULL,
        `ReadingScore` int NOT NULL,
        `TotalCorrect` int NOT NULL,
        `TotalQuestions` int NOT NULL DEFAULT 200,
        `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'InProgress',
        `Mode` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'FullTest',
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        CONSTRAINT `PK_ToeicAttempts` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ToeicAttempts_ToeicTests_TestId` FOREIGN KEY (`TestId`) REFERENCES `ToeicTests` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE TABLE `ToeicPassages` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `TestId` char(36) COLLATE ascii_general_ci NULL,
        `Part` int NOT NULL,
        `PassageType` varchar(100) CHARACTER SET utf8mb4 NULL,
        `Title` varchar(500) CHARACTER SET utf8mb4 NULL,
        `Content` longtext CHARACTER SET utf8mb4 NULL,
        `AudioUrl` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `ImageUrl` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `Transcript` longtext CHARACTER SET utf8mb4 NULL,
        `OrderIndex` int NOT NULL,
        `ExternalId` char(36) COLLATE ascii_general_ci NULL,
        `DeletedAt` datetime(6) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        CONSTRAINT `PK_ToeicPassages` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ToeicPassages_ToeicTests_TestId` FOREIGN KEY (`TestId`) REFERENCES `ToeicTests` (`Id`) ON DELETE SET NULL
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE TABLE `ToeicQuestions` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `TestId` char(36) COLLATE ascii_general_ci NULL,
        `PassageId` char(36) COLLATE ascii_general_ci NULL,
        `Part` int NOT NULL,
        `Section` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `QuestionNumber` int NULL,
        `QuestionText` text CHARACTER SET utf8mb4 NULL,
        `AudioUrl` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `ImageUrl` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `OptionA` text CHARACTER SET utf8mb4 NOT NULL,
        `OptionB` text CHARACTER SET utf8mb4 NOT NULL,
        `OptionC` text CHARACTER SET utf8mb4 NOT NULL,
        `OptionD` text CHARACTER SET utf8mb4 NULL,
        `CorrectAnswer` varchar(1) CHARACTER SET utf8mb4 NOT NULL,
        `Explanation` longtext CHARACTER SET utf8mb4 NULL,
        `Transcript` longtext CHARACTER SET utf8mb4 NULL,
        `DifficultyLevel` int NOT NULL,
        `Topic` varchar(200) CHARACTER SET utf8mb4 NULL,
        `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `Version` int NOT NULL,
        `OrderIndex` int NOT NULL,
        `PreferAiExplanation` tinyint(1) NOT NULL,
        `CreatedByUserId` char(36) COLLATE ascii_general_ci NULL,
        `ExternalId` char(36) COLLATE ascii_general_ci NULL,
        `DeletedAt` datetime(6) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        CONSTRAINT `PK_ToeicQuestions` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ToeicQuestions_ToeicPassages_PassageId` FOREIGN KEY (`PassageId`) REFERENCES `ToeicPassages` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_ToeicQuestions_ToeicTests_TestId` FOREIGN KEY (`TestId`) REFERENCES `ToeicTests` (`Id`) ON DELETE SET NULL
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE TABLE `ToeicAttemptAnswers` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `AttemptId` char(36) COLLATE ascii_general_ci NOT NULL,
        `QuestionId` char(36) COLLATE ascii_general_ci NOT NULL,
        `UserAnswer` varchar(1) CHARACTER SET utf8mb4 NULL,
        `CorrectAnswer` varchar(1) CHARACTER SET utf8mb4 NOT NULL,
        `IsCorrect` tinyint(1) NOT NULL,
        `TimeSpentSeconds` int NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        CONSTRAINT `PK_ToeicAttemptAnswers` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ToeicAttemptAnswers_ToeicAttempts_AttemptId` FOREIGN KEY (`AttemptId`) REFERENCES `ToeicAttempts` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_ToeicAttemptAnswers_ToeicQuestions_QuestionId` FOREIGN KEY (`QuestionId`) REFERENCES `ToeicQuestions` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE TABLE `ToeicPracticeSetItems` (
        `PracticeSetId` char(36) COLLATE ascii_general_ci NOT NULL,
        `QuestionId` char(36) COLLATE ascii_general_ci NOT NULL,
        `OrderIndex` int NOT NULL,
        CONSTRAINT `PK_ToeicPracticeSetItems` PRIMARY KEY (`PracticeSetId`, `QuestionId`),
        CONSTRAINT `FK_ToeicPracticeSetItems_ToeicPracticeSets_PracticeSetId` FOREIGN KEY (`PracticeSetId`) REFERENCES `ToeicPracticeSets` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_ToeicPracticeSetItems_ToeicQuestions_QuestionId` FOREIGN KEY (`QuestionId`) REFERENCES `ToeicQuestions` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_OutboxMessages_ProcessedOn_OccurredOn` ON `OutboxMessages` (`ProcessedOn`, `OccurredOn`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicAttemptAnswers_AttemptId_QuestionId` ON `ToeicAttemptAnswers` (`AttemptId`, `QuestionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicAttemptAnswers_QuestionId` ON `ToeicAttemptAnswers` (`QuestionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicAttempts_TestId` ON `ToeicAttempts` (`TestId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicAttempts_UserId` ON `ToeicAttempts` (`UserId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicAttempts_UserId_TestId` ON `ToeicAttempts` (`UserId`, `TestId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicPassages_ExternalId` ON `ToeicPassages` (`ExternalId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicPassages_Part` ON `ToeicPassages` (`Part`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicPassages_TestId_Part_OrderIndex` ON `ToeicPassages` (`TestId`, `Part`, `OrderIndex`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicPracticeSetItems_PracticeSetId_OrderIndex` ON `ToeicPracticeSetItems` (`PracticeSetId`, `OrderIndex`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicPracticeSetItems_QuestionId` ON `ToeicPracticeSetItems` (`QuestionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE UNIQUE INDEX `IX_ToeicPracticeSets_Code` ON `ToeicPracticeSets` (`Code`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicPracticeSets_ExternalId` ON `ToeicPracticeSets` (`ExternalId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicPracticeSets_Part_Kind_Status` ON `ToeicPracticeSets` (`Part`, `Kind`, `Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE FULLTEXT INDEX `FT_ToeicQuestions_Content` ON `ToeicQuestions` (`QuestionText`, `Explanation`, `Transcript`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicQuestions_ExternalId` ON `ToeicQuestions` (`ExternalId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicQuestions_Part_DifficultyLevel` ON `ToeicQuestions` (`Part`, `DifficultyLevel`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicQuestions_PassageId` ON `ToeicQuestions` (`PassageId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicQuestions_TestId_QuestionNumber` ON `ToeicQuestions` (`TestId`, `QuestionNumber`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicQuestions_Topic` ON `ToeicQuestions` (`Topic`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicTests_Category` ON `ToeicTests` (`Category`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE UNIQUE INDEX `IX_ToeicTests_Code` ON `ToeicTests` (`Code`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicTests_ExternalId` ON `ToeicTests` (`ExternalId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    CREATE INDEX `IX_ToeicTests_Status_IsActive` ON `ToeicTests` (`Status`, `IsActive`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260914082511_InitialAssessmentSchema') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260914082511_InitialAssessmentSchema', '9.0.11');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

