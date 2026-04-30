-- ============================================================================
-- Spellbound DB migration: per-character achievement application tracking
-- Apply against the `ace_custom_spellbound` database (or whatever
-- Settings.json::MySql.Database is set to).
--
-- Why: account-level achievement awards must apply bonuses to every character
-- on the account, including future characters. To stay idempotent across both
-- the on-grant walk and the on-character-create walk we record per-character
-- application here. The unique (CharacterId, AchievementId) index is the race
-- guard so both walks can race the insert without double-applying.
-- ============================================================================

CREATE TABLE IF NOT EXISTS `AwardedCharacterAchievements` (
    `Id`            INT          NOT NULL AUTO_INCREMENT,
    `CharacterId`   INT UNSIGNED NOT NULL,
    `AchievementId` INT          NOT NULL,
    `AppliedAt`     DATETIME(6)  NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE UNIQUE INDEX `IX_AwardedCharacterAchievements_CharacterId_AchievementId`
    ON `AwardedCharacterAchievements` (`CharacterId`, `AchievementId`);

CREATE INDEX `IX_AwardedCharacterAchievements_CharacterId`
    ON `AwardedCharacterAchievements` (`CharacterId`);
