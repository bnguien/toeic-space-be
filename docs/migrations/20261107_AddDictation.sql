START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    CREATE TABLE `DictationCourses` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Code` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
        `Pack` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
        `Level` int NOT NULL,
        `Order` int NOT NULL,
        `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `ItemCount` int NOT NULL,
        `WordsPerSentence` double NOT NULL,
        `VocabCount` int NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_DictationCourses` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    CREATE TABLE `DictationLearnerStates` (
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Stars` json NOT NULL,
        `DailySeconds` json NOT NULL,
        `BasicSettings` json NULL,
        `ProSettings` json NULL,
        `SettingsUpdatedAt` bigint NOT NULL,
        `Voice` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
        `VoiceUpdatedAt` bigint NOT NULL,
        `Sound` json NULL,
        `SoundUpdatedAt` bigint NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        `Version` int NOT NULL,
        CONSTRAINT `PK_DictationLearnerStates` PRIMARY KEY (`LearnerId`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    CREATE TABLE `DictationItems` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `CourseId` int NOT NULL,
        `SourceCode` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
        `Order` int NOT NULL,
        `Level` int NOT NULL,
        `Topic` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
        `Text` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
        `Translation` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
        `Ipa` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
        `VoiceKey` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
        `Rate` int NOT NULL,
        `MaxPlays` int NULL,
        `Keywords` json NOT NULL,
        `Notes` json NOT NULL,
        `Vocab` json NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_DictationItems` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_DictationItems_DictationCourses_CourseId` FOREIGN KEY (`CourseId`) REFERENCES `DictationCourses` (`Id`) ON DELETE RESTRICT
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    CREATE TABLE `DictationProgress` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `ItemId` int NOT NULL,
        `BestAccuracy` double NOT NULL,
        `BestScore` int NOT NULL,
        `Passed` tinyint(1) NOT NULL,
        `PassedClean` tinyint(1) NOT NULL,
        `AttemptCount` int NOT NULL,
        `LastAttemptAt` datetime(6) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        `Version` int NOT NULL,
        CONSTRAINT `PK_DictationProgress` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_DictationProgress_DictationItems_ItemId` FOREIGN KEY (`ItemId`) REFERENCES `DictationItems` (`Id`) ON DELETE CASCADE
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    CREATE UNIQUE INDEX `IX_DictationCourses_Code` ON `DictationCourses` (`Code`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    CREATE INDEX `IX_DictationCourses_Pack_IsActive_Order` ON `DictationCourses` (`Pack`, `IsActive`, `Order`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    CREATE INDEX `IX_DictationItems_CourseId_IsActive_Order` ON `DictationItems` (`CourseId`, `IsActive`, `Order`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    CREATE UNIQUE INDEX `IX_DictationItems_SourceCode` ON `DictationItems` (`SourceCode`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    CREATE INDEX `IX_DictationProgress_ItemId` ON `DictationProgress` (`ItemId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    CREATE UNIQUE INDEX `IX_DictationProgress_LearnerId_ItemId` ON `DictationProgress` (`LearnerId`, `ItemId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162016_AddDictation') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20261107162016_AddDictation', '9.0.11');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

