START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE TABLE `VideoCatalogItems` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `VideoId` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `TopicSlug` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
        `TopicTitle` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
        `TopicPosition` int NOT NULL,
        `Title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
        `ChannelHandle` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
        `ChannelTitle` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
        `Duration` int NOT NULL,
        `Level` varchar(4) CHARACTER SET utf8mb4 NOT NULL,
        `Thumbnail` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
        `ViewCount` bigint NOT NULL,
        `Position` int NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `SubtitleStatus` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `SubtitleCheckedAt` datetime(6) NULL,
        `Difficulty` int NOT NULL,
        `Cefr` varchar(4) CHARACTER SET utf8mb4 NOT NULL,
        `Track` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `LevelSource` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `LevelMetrics` json NULL,
        `LevelScoredAt` datetime(6) NULL,
        `EmbedStatus` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `EmbedReason` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `EmbedCheckedAt` datetime(6) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_VideoCatalogItems` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE TABLE `VideoLearnerStates` (
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `ReaderSettings` json NULL,
        `ListenMode` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `PathTrack` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_VideoLearnerStates` PRIMARY KEY (`LearnerId`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE TABLE `VideoSubtitles` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `VideoId` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `Title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
        `Duration` int NOT NULL,
        `OriginalLanguage` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `Languages` longtext CHARACTER SET utf8mb4 NULL,
        `LanguagesFetchedAt` datetime(6) NULL,
        `Tracks` longtext CHARACTER SET utf8mb4 NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_VideoSubtitles` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE TABLE `VideoWatches` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `VideoId` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `Title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
        `Duration` int NOT NULL,
        `Language` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `IsPublic` tinyint(1) NOT NULL,
        `SharerName` varchar(80) CHARACTER SET utf8mb4 NULL,
        `SharerAvatar` varchar(300) CHARACTER SET utf8mb4 NULL,
        `OpenCount` int NOT NULL,
        `LastOpenedAt` datetime(6) NULL,
        `LastPosition` int NOT NULL,
        `WatchedPercent` int NOT NULL,
        `CompletedAt` datetime(6) NULL,
        `ProgressAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_VideoWatches` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE INDEX `IX_VideoCatalogItems_TopicSlug_IsActive_SubtitleStatus_Position` ON `VideoCatalogItems` (`TopicSlug`, `IsActive`, `SubtitleStatus`, `Position`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE INDEX `IX_VideoCatalogItems_Track_IsActive_Difficulty` ON `VideoCatalogItems` (`Track`, `IsActive`, `Difficulty`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE UNIQUE INDEX `IX_VideoCatalogItems_VideoId` ON `VideoCatalogItems` (`VideoId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE UNIQUE INDEX `IX_VideoSubtitles_VideoId` ON `VideoSubtitles` (`VideoId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE INDEX `IX_VideoWatches_IsPublic_LastOpenedAt` ON `VideoWatches` (`IsPublic`, `LastOpenedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE INDEX `IX_VideoWatches_LearnerId_LastOpenedAt` ON `VideoWatches` (`LearnerId`, `LastOpenedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE INDEX `IX_VideoWatches_LearnerId_ProgressAt` ON `VideoWatches` (`LearnerId`, `ProgressAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    CREATE UNIQUE INDEX `IX_VideoWatches_LearnerId_VideoId` ON `VideoWatches` (`LearnerId`, `VideoId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107162351_AddVideoLearning') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20261107162351_AddVideoLearning', '9.0.11');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

