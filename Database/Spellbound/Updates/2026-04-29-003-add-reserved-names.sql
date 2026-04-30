-- ============================================================================
-- Spellbound DB migration: reserved character names
-- Apply against the `ace_mod_spellbound` database (or whatever
-- Settings.json::MySql.Database is set to).
--
-- Why: season-wipe.sql truncates `ace_shard.character`, which destroys upstream's
-- only record of who owned which character name. Without this table, a
-- previous-season griefer could re-create a freed handle on their own account.
-- The season-wipe procedure snapshots every (name, account_Id) pair into this
-- table BEFORE the truncate, and CharacterCreateEx is gated by a Harmony prefix
-- that rejects creates of any name reserved by a different account.
--
-- Reservations are permanent. A name reserved to account A in season 1 stays
-- reserved to account A forever, even if A never logs in again. (If the account
-- is itself purged from auth, an admin can DELETE the row manually.)
--
-- Name comparison is case-insensitive via the column's utf8mb4_general_ci
-- collation, mirroring how shard.character.name is matched today.
-- ============================================================================

CREATE TABLE IF NOT EXISTS `ReservedNames` (
    `Id`         INT          NOT NULL AUTO_INCREMENT,
    `Name`       VARCHAR(255) NOT NULL,
    `AccountId`  INT UNSIGNED NOT NULL,
    `ReservedAt` DATETIME(6)  NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE UNIQUE INDEX `IX_ReservedNames_Name`
    ON `ReservedNames` (`Name`);

CREATE INDEX `IX_ReservedNames_AccountId`
    ON `ReservedNames` (`AccountId`);
