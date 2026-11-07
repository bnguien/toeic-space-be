START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107111547_AddBilingualNews') THEN

    CREATE TABLE `NewsArticles` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Slug` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
        `SourceName` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
        `SourceUrl` varchar(400) CHARACTER SET utf8mb4 NOT NULL,
        `SourceId` varchar(40) CHARACTER SET utf8mb4 NULL,
        `SourceSeries` varchar(80) CHARACTER SET utf8mb4 NULL,
        `SourceLevel` varchar(40) CHARACTER SET utf8mb4 NULL,
        `Author` varchar(160) CHARACTER SET utf8mb4 NULL,
        `PublishedAt` datetime(6) NULL,
        `License` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
        `Attribution` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
        `TitleEn` varchar(240) CHARACTER SET utf8mb4 NOT NULL,
        `TitleVi` varchar(240) CHARACTER SET utf8mb4 NULL,
        `SummaryVi` varchar(400) CHARACTER SET utf8mb4 NULL,
        `Paragraphs` json NOT NULL,
        `Vocabulary` json NOT NULL,
        `WordCount` int NOT NULL,
        `Minutes` int NOT NULL,
        `Stage` varchar(4) CHARACTER SET utf8mb4 NOT NULL,
        `Topic` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
        `CoverFile` varchar(80) CHARACTER SET utf8mb4 NULL,
        `CoverAlt` varchar(200) CHARACTER SET utf8mb4 NULL,
        `CoverCredit` varchar(300) CHARACTER SET utf8mb4 NULL,
        `CoverLicense` varchar(20) CHARACTER SET utf8mb4 NULL,
        `CoverSourceUrl` varchar(500) CHARACTER SET utf8mb4 NULL,
        `AudioUrl` varchar(500) CHARACTER SET utf8mb4 NULL,
        `Status` varchar(12) CHARACTER SET utf8mb4 NOT NULL,
        `Featured` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_NewsArticles` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107111547_AddBilingualNews') THEN

    CREATE TABLE `NewsReadings` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `ArticleId` char(36) COLLATE ascii_general_ci NOT NULL,
        `OpenedAt` datetime(6) NOT NULL,
        `CompletedAt` datetime(6) NULL,
        `ReadingSeconds` int NOT NULL,
        `KnownWords` json NOT NULL,
        `Notes` varchar(4000) CHARACTER SET utf8mb4 NULL,
        `Dictation` json NOT NULL,
        `Settings` json NULL,
        `SettingsUpdatedAt` datetime(6) NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_NewsReadings` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_NewsReadings_NewsArticles_ArticleId` FOREIGN KEY (`ArticleId`) REFERENCES `NewsArticles` (`Id`) ON DELETE CASCADE
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107111547_AddBilingualNews') THEN

    CREATE UNIQUE INDEX `IX_NewsArticles_Slug` ON `NewsArticles` (`Slug`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107111547_AddBilingualNews') THEN

    CREATE INDEX `IX_NewsArticles_Status_Stage_PublishedAt` ON `NewsArticles` (`Status`, `Stage`, `PublishedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107111547_AddBilingualNews') THEN

    CREATE INDEX `IX_NewsArticles_Status_Topic_PublishedAt` ON `NewsArticles` (`Status`, `Topic`, `PublishedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107111547_AddBilingualNews') THEN

    CREATE INDEX `IX_NewsReadings_ArticleId` ON `NewsReadings` (`ArticleId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107111547_AddBilingualNews') THEN

    CREATE UNIQUE INDEX `IX_NewsReadings_LearnerId_ArticleId` ON `NewsReadings` (`LearnerId`, `ArticleId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107111547_AddBilingualNews') THEN

    CREATE INDEX `IX_NewsReadings_LearnerId_CompletedAt` ON `NewsReadings` (`LearnerId`, `CompletedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107111547_AddBilingualNews') THEN

    CREATE INDEX `IX_NewsReadings_LearnerId_SettingsUpdatedAt` ON `NewsReadings` (`LearnerId`, `SettingsUpdatedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107111547_AddBilingualNews') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20261107111547_AddBilingualNews', '9.0.11');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

