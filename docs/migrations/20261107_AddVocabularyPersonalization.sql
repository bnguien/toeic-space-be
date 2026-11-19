START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `SavedWords` MODIFY COLUMN `Note` varchar(1000) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `SavedWords` ADD `MyExample` varchar(300) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `SavedWords` ADD `MyExampleVi` varchar(300) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `SavedWords` ADD `MyMeaningVi` varchar(200) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `SavedWords` ADD `MyPartOfSpeech` varchar(24) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `SavedWords` ADD `MyPhonetic` varchar(100) CHARACTER SET utf8mb4 NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `SavedWords` ADD `PersonalizedAt` datetime(6) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `LearnerWordProgress` ADD `LastMistakeAt` datetime(6) NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `LearnerWordProgress` ADD `MistakeCount` int NOT NULL DEFAULT 0;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `LearnerWordProgress` ADD `MistakePending` tinyint(1) NOT NULL DEFAULT FALSE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    ALTER TABLE `LearnerVocabularyPreferences` ADD `StudySettings` json NULL;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    CREATE INDEX `IX_LearnerWordProgress_LearnerId_MistakePending_LastMistakeAt` ON `LearnerWordProgress` (`LearnerId`, `MistakePending`, `LastMistakeAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    UPDATE `LearnerWordProgress` AS p
    JOIN (
        SELECT `LearnerId`, `WordId`, COUNT(*) AS `Mistakes`, MAX(`ReviewedAt`) AS `LastAt`
        FROM `WordReviewLogs`
        WHERE `Grade` = 'Forget'
        GROUP BY `LearnerId`, `WordId`
    ) AS f ON f.`LearnerId` = p.`LearnerId` AND f.`WordId` = p.`WordId`
    SET p.`MistakeCount` = f.`Mistakes`,
        p.`LastMistakeAt` = f.`LastAt`,
        p.`MistakePending` = NOT EXISTS (
            SELECT 1 FROM `WordReviewLogs` AS g
            WHERE g.`LearnerId` = p.`LearnerId` AND g.`WordId` = p.`WordId`
              AND g.`Grade` IN ('Good', 'Easy') AND g.`ReviewedAt` > f.`LastAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261107102903_AddVocabularyPersonalization') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20261107102903_AddVocabularyPersonalization', '9.0.11');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

