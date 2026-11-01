START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `LearnerVocabularyPreferences` (
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `FontScale` double NOT NULL,
        `SidebarCollapsed` tinyint(1) NOT NULL,
        `SidebarWidth` int NULL,
        `Snowfall` tinyint(1) NOT NULL,
        `AutoPlayAudio` tinyint(1) NOT NULL,
        `DefaultMode` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `DailyGoal` int NOT NULL,
        `LastStageId` char(36) COLLATE ascii_general_ci NULL,
        `LastView` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `LastFolderId` char(36) COLLATE ascii_general_ci NULL,
        `LastFolderPickerId` char(36) COLLATE ascii_general_ci NULL,
        `UpdatedAt` datetime(6) NULL,
        CONSTRAINT `PK_LearnerVocabularyPreferences` PRIMARY KEY (`LearnerId`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `ReelBackgrounds` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Provider` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `ProviderId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `VideoUrl` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
        `PosterUrl` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
        `PageUrl` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
        `Author` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `DurationSeconds` int NOT NULL,
        `Width` int NOT NULL,
        `Height` int NOT NULL,
        `Tags` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
        `LastSeenAt` datetime(6) NOT NULL,
        `MissCount` int NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_ReelBackgrounds` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `StudySessions` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Mode` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `SourceKind` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `SourceId` char(36) COLLATE ascii_general_ci NULL,
        `SourceTitle` varchar(200) CHARACTER SET utf8mb4 NULL,
        `Queue` json NOT NULL,
        `Position` int NOT NULL,
        `Requeued` json NOT NULL,
        `Results` json NOT NULL,
        `CorrectCount` int NOT NULL,
        `ReviewCount` int NOT NULL,
        `Status` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `ActiveSlot` varchar(80) CHARACTER SET utf8mb4 NULL,
        `StartedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        `CompletedAt` datetime(6) NULL,
        `Version` int NOT NULL,
        CONSTRAINT `PK_StudySessions` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `VocabularyFolders` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Name` varchar(60) CHARACTER SET utf8mb4 NOT NULL,
        `NormalizedName` varchar(60) CHARACTER SET utf8mb4 NOT NULL,
        `Order` int NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_VocabularyFolders` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `VocabularyStages` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Slug` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
        `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `Order` int NOT NULL,
        `ToeicTarget` int NULL,
        `IsActive` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_VocabularyStages` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `VocabularyWords` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `OwnerKey` char(36) COLLATE ascii_general_ci NOT NULL,
        `Headword` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `NormalizedHeadword` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `PartOfSpeech` varchar(24) CHARACTER SET utf8mb4 NOT NULL,
        `Phonetic` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `MeaningVi` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
        `DefinitionEn` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `Example` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `ExampleVi` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `Cefr` varchar(8) CHARACTER SET utf8mb4 NULL,
        `Synonyms` json NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_VocabularyWords` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `WordGlossCache` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `NormalizedText` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `Kind` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `Headword` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `Phonetic` varchar(100) CHARACTER SET utf8mb4 NULL,
        `PartOfSpeech` varchar(24) CHARACTER SET utf8mb4 NULL,
        `MeaningVi` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
        `DefinitionEn` varchar(400) CHARACTER SET utf8mb4 NULL,
        `Example` varchar(400) CHARACTER SET utf8mb4 NULL,
        `ExampleVi` varchar(400) CHARACTER SET utf8mb4 NULL,
        `Cefr` varchar(8) CHARACTER SET utf8mb4 NULL,
        `PhraseType` varchar(32) CHARACTER SET utf8mb4 NULL,
        `Usage` varchar(500) CHARACTER SET utf8mb4 NULL,
        `Explanation` varchar(500) CHARACTER SET utf8mb4 NULL,
        `LiteralMeaning` varchar(500) CHARACTER SET utf8mb4 NULL,
        `Sources` json NOT NULL,
        `Version` int NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `ExpiresAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_WordGlossCache` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `VocabularySets` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `StageId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Slug` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
        `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Topic` varchar(200) CHARACTER SET utf8mb4 NULL,
        `Order` int NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_VocabularySets` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_VocabularySets_VocabularyStages_StageId` FOREIGN KEY (`StageId`) REFERENCES `VocabularyStages` (`Id`) ON DELETE RESTRICT
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `LearnerWordProgress` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `WordId` char(36) COLLATE ascii_general_ci NOT NULL,
        `EaseFactor` double NOT NULL,
        `IntervalDays` int NOT NULL,
        `Repetitions` int NOT NULL,
        `Lapses` int NOT NULL,
        `DueOn` date NOT NULL,
        `LastReviewedAt` datetime(6) NULL,
        `LastGradeDay` date NULL,
        `LastGrade` varchar(16) CHARACTER SET utf8mb4 NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        `Version` int NOT NULL,
        CONSTRAINT `PK_LearnerWordProgress` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_LearnerWordProgress_VocabularyWords_WordId` FOREIGN KEY (`WordId`) REFERENCES `VocabularyWords` (`Id`) ON DELETE RESTRICT
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `SavedWords` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `WordId` char(36) COLLATE ascii_general_ci NOT NULL,
        `FolderId` char(36) COLLATE ascii_general_ci NULL,
        `Note` varchar(300) CHARACTER SET utf8mb4 NULL,
        `SavedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NOT NULL,
        CONSTRAINT `PK_SavedWords` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_SavedWords_VocabularyFolders_FolderId` FOREIGN KEY (`FolderId`) REFERENCES `VocabularyFolders` (`Id`) ON DELETE SET NULL,
        CONSTRAINT `FK_SavedWords_VocabularyWords_WordId` FOREIGN KEY (`WordId`) REFERENCES `VocabularyWords` (`Id`) ON DELETE CASCADE
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `WordReviewLogs` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `LearnerId` char(36) COLLATE ascii_general_ci NOT NULL,
        `WordId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Mode` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `Grade` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
        `ClientEventId` char(36) COLLATE ascii_general_ci NOT NULL,
        `ReviewedAt` datetime(6) NOT NULL,
        `LocalDay` date NOT NULL,
        `IntervalBefore` int NOT NULL,
        `IntervalAfter` int NOT NULL,
        `EaseFactorBefore` double NOT NULL,
        `EaseFactorAfter` double NOT NULL,
        `ScheduleChanged` tinyint(1) NOT NULL,
        CONSTRAINT `PK_WordReviewLogs` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_WordReviewLogs_VocabularyWords_WordId` FOREIGN KEY (`WordId`) REFERENCES `VocabularyWords` (`Id`) ON DELETE RESTRICT
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE TABLE `VocabularySetWords` (
        `SetId` char(36) COLLATE ascii_general_ci NOT NULL,
        `WordId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Order` int NOT NULL,
        CONSTRAINT `PK_VocabularySetWords` PRIMARY KEY (`SetId`, `WordId`),
        CONSTRAINT `FK_VocabularySetWords_VocabularySets_SetId` FOREIGN KEY (`SetId`) REFERENCES `VocabularySets` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_VocabularySetWords_VocabularyWords_WordId` FOREIGN KEY (`WordId`) REFERENCES `VocabularyWords` (`Id`) ON DELETE RESTRICT
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_LearnerWordProgress_LearnerId_DueOn` ON `LearnerWordProgress` (`LearnerId`, `DueOn`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_LearnerWordProgress_LearnerId_WordId` ON `LearnerWordProgress` (`LearnerId`, `WordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_LearnerWordProgress_WordId` ON `LearnerWordProgress` (`WordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_ReelBackgrounds_IsActive` ON `ReelBackgrounds` (`IsActive`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_ReelBackgrounds_Provider_ProviderId` ON `ReelBackgrounds` (`Provider`, `ProviderId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_SavedWords_FolderId` ON `SavedWords` (`FolderId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_SavedWords_LearnerId_FolderId_SavedAt` ON `SavedWords` (`LearnerId`, `FolderId`, `SavedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_SavedWords_LearnerId_WordId` ON `SavedWords` (`LearnerId`, `WordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_SavedWords_WordId` ON `SavedWords` (`WordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_StudySessions_LearnerId_ActiveSlot` ON `StudySessions` (`LearnerId`, `ActiveSlot`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_StudySessions_LearnerId_Status_UpdatedAt` ON `StudySessions` (`LearnerId`, `Status`, `UpdatedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_VocabularyFolders_LearnerId_NormalizedName` ON `VocabularyFolders` (`LearnerId`, `NormalizedName`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_VocabularySets_StageId_Order` ON `VocabularySets` (`StageId`, `Order`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_VocabularySets_StageId_Slug` ON `VocabularySets` (`StageId`, `Slug`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_VocabularySetWords_SetId_Order` ON `VocabularySetWords` (`SetId`, `Order`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_VocabularySetWords_WordId` ON `VocabularySetWords` (`WordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_VocabularyStages_Order` ON `VocabularyStages` (`Order`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_VocabularyStages_Slug` ON `VocabularyStages` (`Slug`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_VocabularyWords_OwnerKey_NormalizedHeadword_PartOfSpeech` ON `VocabularyWords` (`OwnerKey`, `NormalizedHeadword`, `PartOfSpeech`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_WordGlossCache_ExpiresAt` ON `WordGlossCache` (`ExpiresAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_WordGlossCache_NormalizedText_Kind` ON `WordGlossCache` (`NormalizedText`, `Kind`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE UNIQUE INDEX `IX_WordReviewLogs_LearnerId_ClientEventId` ON `WordReviewLogs` (`LearnerId`, `ClientEventId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_WordReviewLogs_LearnerId_LocalDay` ON `WordReviewLogs` (`LearnerId`, `LocalDay`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    CREATE INDEX `IX_WordReviewLogs_WordId` ON `WordReviewLogs` (`WordId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261101113940_AddVocabularyLearning') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20261101113940_AddVocabularyLearning', '9.0.11');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

