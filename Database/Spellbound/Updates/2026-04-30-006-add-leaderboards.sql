-- ============================================================================
-- Spellbound DB migration: add Leaderboards for Phase 3 of the web app.
-- Apply against the `ace_mod_spellbound` database.
--
-- Why: per-character event-driven counters. The first user is "kills by
-- creature type" (e.g. Tusker leaderboard). The Category column is an enum
-- (LeaderboardCategory.cs), Target is a free-form sub-key (creature type
-- name when Category=KillsByCreature; "" for category-wide leaderboards).
-- Count is BIGINT so xp-style values fit later.
--
-- Target NOT NULL DEFAULT '' rather than NULL — MySQL treats NULL as distinct
-- in unique keys, which would silently allow duplicate (Category, Char) rows
-- for category-wide leaderboards. Empty-string sentinel keeps the unique
-- constraint honest.
--
-- Two indexes:
--   * UQ on (Category, Target, CharacterId) is the "increment my row"
--     lookup used by INSERT ... ON DUPLICATE KEY UPDATE.
--   * IX on (Category, Target, Count) is the "top N" range scan used by
--     the web app's leaderboard page.
--
-- TotalXp is NOT stored here — derived from CharacterProfileSnapshots.TotalXp
-- via a sort-order query. The Leaderboards table only carries event-driven
-- counters where there's no natural snapshot to read from.
--
-- Season behavior: TRUNCATE on season wipe (mirrored into season-wipe.sql).
-- ============================================================================

CREATE TABLE IF NOT EXISTS `Leaderboards` (
    `Id`            INT          NOT NULL AUTO_INCREMENT,
    `Category`      INT          NOT NULL,
    `Target`        VARCHAR(100) NOT NULL DEFAULT '',
    `CharacterId`   INT UNSIGNED NOT NULL,
    `AccountId`     INT          NOT NULL,
    `CharacterName` VARCHAR(64)  NOT NULL,
    `Count`         BIGINT       NOT NULL,
    `UpdatedAt`     DATETIME(6)  NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE UNIQUE INDEX `IX_Leaderboards_Cat_Target_Char`
    ON `Leaderboards` (`Category`, `Target`, `CharacterId`);

CREATE INDEX `IX_Leaderboards_Rank`
    ON `Leaderboards` (`Category`, `Target`, `Count`);
