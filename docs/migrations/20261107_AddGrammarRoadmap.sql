START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE TABLE `GrammarAttempts` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Topic` varchar(2) CHARACTER SET utf8mb4 NULL,
        `Stage` varchar(2) CHARACTER SET utf8mb4 NULL,
        `Block` varchar(8) CHARACTER SET utf8mb4 NOT NULL,
        `Answers` json NOT NULL,
        `Results` json NOT NULL,
        `FixedQuestionIds` json NOT NULL,
        `Score` int NOT NULL,
        `Total` int NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_GrammarAttempts` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE TABLE `GrammarComments` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `Target` varchar(24) CHARACTER SET utf8mb4 NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `AuthorName` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
        `ParentId` bigint NULL,
        `Content` text CHARACTER SET utf8mb4 NOT NULL,
        `LikeCount` int NOT NULL,
        `ReplyCount` int NOT NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_GrammarComments` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE TABLE `GrammarDocuments` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Kind` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `Key` varchar(8) CHARACTER SET utf8mb4 NOT NULL,
        `Content` longtext CHARACTER SET utf8mb4 NOT NULL,
        `ContentHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `ImportedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_GrammarDocuments` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE TABLE `GrammarLearnerStates` (
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `PositionTopic` varchar(2) CHARACTER SET utf8mb4 NULL,
        `PositionPart` varchar(12) CHARACTER SET utf8mb4 NULL,
        `PositionAnchor` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
        `DarkMode` tinyint(1) NULL,
        `RoadmapWidth` int NULL,
        `RoadmapCollapsed` tinyint(1) NOT NULL,
        `KnownWords` json NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        `Version` int NOT NULL,
        CONSTRAINT `PK_GrammarLearnerStates` PRIMARY KEY (`LearnerId`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE TABLE `GrammarLessonProgress` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Topic` varchar(2) CHARACTER SET utf8mb4 NOT NULL,
        `TheoryConfirmedAt` datetime(6) NULL,
        `VocabConfirmedAt` datetime(6) NULL,
        `QuickCheckBest` json NOT NULL,
        `MiniTestBest` int NULL,
        `AttemptCount` int NOT NULL,
        `CompletedAt` datetime(6) NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        `Version` int NOT NULL,
        CONSTRAINT `PK_GrammarLessonProgress` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE TABLE `GrammarQuestionResults` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `QuestionId` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
        `Topic` varchar(2) CHARACTER SET utf8mb4 NOT NULL,
        `Block` varchar(8) CHARACTER SET utf8mb4 NOT NULL,
        `UnitCode` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
        `TrapCode` varchar(24) CHARACTER SET utf8mb4 NOT NULL,
        `FirstCorrect` tinyint(1) NULL,
        `LastCorrect` tinyint(1) NOT NULL,
        `Fixed` tinyint(1) NOT NULL,
        `Revealed` tinyint(1) NOT NULL,
        `WrongCount` int NOT NULL,
        `AnsweredAt` datetime(6) NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_GrammarQuestionResults` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE TABLE `GrammarCommentLikes` (
        `CommentId` bigint NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_GrammarCommentLikes` PRIMARY KEY (`CommentId`, `LearnerId`),
        CONSTRAINT `FK_GrammarCommentLikes_GrammarComments_CommentId` FOREIGN KEY (`CommentId`) REFERENCES `GrammarComments` (`Id`) ON DELETE CASCADE
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE INDEX `IX_GrammarAttempts_LearnerId_Stage` ON `GrammarAttempts` (`LearnerId`, `Stage`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE INDEX `IX_GrammarComments_LearnerId_Id` ON `GrammarComments` (`LearnerId`, `Id`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE INDEX `IX_GrammarComments_Target_ParentId_Id` ON `GrammarComments` (`Target`, `ParentId`, `Id`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE UNIQUE INDEX `IX_GrammarDocuments_Kind_Key` ON `GrammarDocuments` (`Kind`, `Key`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE UNIQUE INDEX `IX_GrammarLessonProgress_LearnerId_Topic` ON `GrammarLessonProgress` (`LearnerId`, `Topic`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE INDEX `IX_GrammarQuestionResults_LearnerId_LastCorrect_WrongCount` ON `GrammarQuestionResults` (`LearnerId`, `LastCorrect`, `WrongCount`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE UNIQUE INDEX `IX_GrammarQuestionResults_LearnerId_QuestionId` ON `GrammarQuestionResults` (`LearnerId`, `QuestionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    CREATE INDEX `IX_GrammarQuestionResults_LearnerId_TrapCode_WrongCount` ON `GrammarQuestionResults` (`LearnerId`, `TrapCode`, `WrongCount`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162858_AddGrammarRoadmap') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20261107162858_AddGrammarRoadmap', '9.0.11');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

