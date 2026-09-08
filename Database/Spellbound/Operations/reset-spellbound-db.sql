-- ============================================================================
-- Spellbound DB hard reset.
-- Single-file drop + create + seed for the Spellbound private database
-- (whatever Settings.json::MySql.Database is set to, default
-- `ace_mod_spellbound`).
--
-- DESTRUCTIVE. Drops every Spellbound-owned table and rebuilds it from
-- scratch, then re-applies canonical seeds. Use during dev to nuke a
-- corrupted local schema or to reset between feature branches that have
-- diverged on schema. NEVER run against production unless that's exactly
-- what you mean.
--
-- Usage (server stopped recommended; mod's Lazy<T> context factory reads
-- connection settings on first use, so a running server with cached
-- connections will be confused by the table churn):
--   mysql -u <user> -p <db_name> < reset-spellbound-db.sql
--
-- Maintenance rule: this file is a concatenation of
--   Baseline/CreateSpellboundDb.sql + Seeds/achievements.sql + Seeds/zones.sql
-- preceded by DROP statements. When any of those source files change, mirror
-- the change here in the same commit. The DROP TABLE list must mention every
-- table that Baseline creates.
--
-- Reset as of: 2026-04-30 (covers Updates through
--                          2026-04-30-006-add-leaderboards.sql).
-- ============================================================================

SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------------------------------------------------------
-- DROP all Spellbound tables. Order doesn't matter with FK checks off.
-- Keep this list in sync with the CREATE TABLE statements below.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS `AccountAchievements`;
DROP TABLE IF EXISTS `CharacterAchievements`;
-- Legacy name from before commit 622b59a4. Dropped explicitly so dev DBs
-- that bootstrapped pre-rename get cleaned up on reset.
DROP TABLE IF EXISTS `AwardedCharacterAchievements`;
DROP TABLE IF EXISTS `AccountVerifications`;
DROP TABLE IF EXISTS `WorldStateRules`;
DROP TABLE IF EXISTS `ReservedNames`;
DROP TABLE IF EXISTS `OnlinePlayers`;
DROP TABLE IF EXISTS `CharacterProfileSnapshots`;
DROP TABLE IF EXISTS `CharacterEquipmentSnapshots`;
DROP TABLE IF EXISTS `Leaderboards`;
DROP TABLE IF EXISTS `Achievements`;
DROP TABLE IF EXISTS `Zones`;

-- ============================================================================
-- BEGIN mirror of Baseline/CreateSpellboundDb.sql
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Achievement: catalog row referenced by AccountAchievements + the on-grant
-- bonus walk. EventTrigger / FilterType / Target form the data-driven match
-- shape (see Services/RuleMatcher.cs); AwardType / AwardValue / AmountRequired
-- describe what the player gets and how many sub-events are needed to flip
-- AwardedAt.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `Achievements` (
    `Id`               INT            NOT NULL AUTO_INCREMENT,
    `Name`             VARCHAR(200)   NOT NULL,
    `EventTrigger`     INT            NOT NULL,
    `AwardDescription` VARCHAR(1000)  NOT NULL DEFAULT '',
    `FilterType`       INT            NOT NULL,
    `Target`           VARCHAR(200)   NULL,
    `AwardType`        INT            NOT NULL,
    `AwardValue`       INT            NULL,
    `AmountRequired`   INT            NULL,
    PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

-- ----------------------------------------------------------------------------
-- Zones: one row per named landblock. Pure aliases stay at Stage = 0; staged
-- zones (towns, dungeons, wilderness — anywhere narrative beats live) carry a
-- non-zero Stage that WorldStateService advances by importing
-- Content/zone-stages/<Name>/<stage>.sql and reloading the landblock. Replaces
-- the older Towns + LandblockAliases pair. SetByAccountId is audit-only —
-- which admin last set the name via /zone name. The optional Tele* columns
-- mirror the upstream Position constructor (cell, posX/Y/Z, rotX/Y/Z/W); all
-- 8 are nullable together — TeleCell IS NULL means "no tele point set" and
-- /zone tele will refuse to teleport.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `Zones` (
    `Id`             INT          NOT NULL AUTO_INCREMENT,
    `Name`           VARCHAR(200) NOT NULL,
    `Landblock`      VARCHAR(50)  NOT NULL,
    `Stage`          INT          NOT NULL DEFAULT 0,
    `UpdatedAt`      DATETIME(6)  NOT NULL,
    `SetByAccountId` INT UNSIGNED NULL,
    `TeleCell`       INT UNSIGNED NULL,
    `TelePosX`       FLOAT        NULL,
    `TelePosY`       FLOAT        NULL,
    `TelePosZ`       FLOAT        NULL,
    `TeleRotX`       FLOAT        NULL,
    `TeleRotY`       FLOAT        NULL,
    `TeleRotZ`       FLOAT        NULL,
    `TeleRotW`       FLOAT        NULL,
    `Version`        INT          NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE UNIQUE INDEX `IX_Zones_Landblock`
    ON `Zones` (`Landblock`);

CREATE UNIQUE INDEX `IX_Zones_Name`
    ON `Zones` (`Name`);

-- ----------------------------------------------------------------------------
-- AccountAchievements: per-account achievement progress + grant timestamp.
-- Unique (AccountId, AchievementId) is the last line of defense for
-- AchievementService.TryAwardAtomic against double-grants under racing
-- triggers; AwardedAt IS NOT NULL is the "fully granted" signal that the
-- on-grant walk and bonus-application code key on. Version is the
-- [ConcurrencyCheck] token, manually bumped inside TryAwardAtomic.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `AccountAchievements` (
    `Id`            INT         NOT NULL AUTO_INCREMENT,
    `AccountId`     INT         NOT NULL,
    `AchievementId` INT         NOT NULL,
    `Progress`      INT         NOT NULL DEFAULT 0,
    `AwardedAt`     DATETIME(6) NULL,
    `Version`       INT         NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AccountAchievements_Achievement_AchievementId`
        FOREIGN KEY (`AchievementId`) REFERENCES `Achievements` (`Id`)
        ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE UNIQUE INDEX `IX_AccountAchievements_AccountId_AchievementId`
    ON `AccountAchievements` (`AccountId`, `AchievementId`);

CREATE INDEX `IX_AccountAchievements_AccountId`
    ON `AccountAchievements` (`AccountId`);

-- ----------------------------------------------------------------------------
-- CharacterAchievements: per-character idempotency guard for the
-- bonus-application walk. The unique (CharacterId, AchievementId) index lets
-- the on-grant walk and the on-character-create walk both attempt the insert
-- and treat a duplicate-key as "already applied; no-op" — see
-- AchievementService.ApplyToCharacter. (Renamed from AwardedCharacterAchievements
-- in commit 622b59a4; the legacy name is dropped explicitly above.)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `CharacterAchievements` (
    `Id`            INT          NOT NULL AUTO_INCREMENT,
    `CharacterId`   INT UNSIGNED NOT NULL,
    `AchievementId` INT          NOT NULL,
    `AppliedAt`     DATETIME(6)  NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE UNIQUE INDEX `IX_CharacterAchievements_CharacterId_AchievementId`
    ON `CharacterAchievements` (`CharacterId`, `AchievementId`);

CREATE INDEX `IX_CharacterAchievements_CharacterId`
    ON `CharacterAchievements` (`CharacterId`);

-- ----------------------------------------------------------------------------
-- AccountVerifications: tracks accounts that have completed external
-- verification (e.g., Discord linking). One row per account.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `AccountVerifications` (
    `Id`         INT         NOT NULL AUTO_INCREMENT,
    `AccountId`  INT         NOT NULL,
    `VerifiedAt` DATETIME(6) NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE UNIQUE INDEX `IX_AccountVerifications_AccountId`
    ON `AccountVerifications` (`AccountId`);

-- ----------------------------------------------------------------------------
-- WorldStateRules: declarative trigger -> zone-stage rules consumed by
-- WorldStateService. Filter shape (FilterType, Target) mirrors Achievement
-- so RuleMatcher evaluates both kinds. EventTrigger index is hot-path —
-- looked up on every published event.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `WorldStateRules` (
    `Id`           INT          NOT NULL AUTO_INCREMENT,
    `Name`         VARCHAR(200) NOT NULL,
    `EventTrigger` INT          NOT NULL,
    `FilterType`   INT          NOT NULL DEFAULT 1,
    `Target`       VARCHAR(200) NULL,
    `ZoneId`       INT          NOT NULL,
    `TargetStage`  INT          NOT NULL,
    `Version`      INT          NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_WorldStateRules_Zones_ZoneId`
        FOREIGN KEY (`ZoneId`) REFERENCES `Zones` (`Id`) ON DELETE RESTRICT
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE INDEX `IX_WorldStateRules_EventTrigger`
    ON `WorldStateRules` (`EventTrigger`);

-- ----------------------------------------------------------------------------
-- ReservedNames: permanent (name -> account) reservations carried across
-- season wipes so handles can't be sniped after season-wipe.sql truncates
-- shard.character. Populated by season-wipe.sql pre-truncate; read by the
-- Harmony prefix on CharacterHandler.CharacterCreateEx (see
-- EventHandlers/AccountRules/PlayerOnCreateReservedNameHandler.cs).
-- Case-insensitive uniqueness via utf8mb4_general_ci on the Name column.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- OnlinePlayers: /who roster snapshot. One row per online character; the mod
-- writes on login (insert) / logout (delete) and a 5-minute timer rebuilds
-- the table from the live PlayerManager roster as the truth source. Read-only
-- from the web app.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `OnlinePlayers` (
    `CharacterId`   INT UNSIGNED NOT NULL,
    `AccountId`     INT          NOT NULL,
    `CharacterName` VARCHAR(64)  NOT NULL,
    `Level`         INT          NOT NULL,
    `Landblock`     INT UNSIGNED NULL,
    `LandblockName` VARCHAR(128) NULL,
    `SeenAt`        DATETIME(6)  NOT NULL,
    PRIMARY KEY (`CharacterId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE INDEX `IX_OnlinePlayers_SeenAt`
    ON `OnlinePlayers` (`SeenAt`);

CREATE INDEX `IX_OnlinePlayers_AccountId`
    ON `OnlinePlayers` (`AccountId`);

-- ----------------------------------------------------------------------------
-- CharacterProfileSnapshots: profile-page snapshot. Written on logout and by
-- a 30-minute timer for online characters. SkillsJson is a free-form blob
-- owned by SnapshotService — list of {Skill, State, Ranks, Base, Current}
-- objects. Stored as TEXT so the shape can evolve without an EF migration.
-- Read-only from the web app.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `CharacterProfileSnapshots` (
    `CharacterId`   INT UNSIGNED NOT NULL,
    `AccountId`     INT          NOT NULL,
    `CharacterName` VARCHAR(64)  NOT NULL,
    `Level`         INT          NOT NULL,
    `TotalXp`       BIGINT       NOT NULL,
    `UnassignedXp`  BIGINT       NOT NULL DEFAULT 0,
    `Strength`      INT          NOT NULL,
    `Endurance`     INT          NOT NULL,
    `Coordination`  INT          NOT NULL,
    `Quickness`     INT          NOT NULL,
    `Focus`         INT          NOT NULL,
    `Self`          INT          NOT NULL,
    `HealthMax`     INT          NOT NULL,
    `StaminaMax`    INT          NOT NULL,
    `ManaMax`       INT          NOT NULL,
    `SkillsJson`    TEXT         NOT NULL,
    `SnapshottedAt` DATETIME(6)  NOT NULL,
    PRIMARY KEY (`CharacterId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE INDEX `IX_CharacterProfileSnapshots_AccountId`
    ON `CharacterProfileSnapshots` (`AccountId`);

CREATE INDEX `IX_CharacterProfileSnapshots_Name`
    ON `CharacterProfileSnapshots` (`CharacterName`);

-- ----------------------------------------------------------------------------
-- CharacterEquipmentSnapshots: equipped-gear snapshot for the profile page.
-- Written on logout and by the 30-minute timer for online characters, in
-- lockstep with CharacterProfileSnapshots. EquipmentJson is owned by
-- SnapshotService — list of {Name, Slot, IconId, Workmanship, Damage,
-- ArmorLevel, Spellcraft, MaxMana, Value, Spells} entries.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `CharacterEquipmentSnapshots` (
    `CharacterId`   INT UNSIGNED NOT NULL,
    `AccountId`     INT          NOT NULL,
    `CharacterName` VARCHAR(64)  NOT NULL,
    `EquipmentJson` TEXT         NOT NULL,
    `SnapshottedAt` DATETIME(6)  NOT NULL,
    PRIMARY KEY (`CharacterId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE INDEX `IX_CharacterEquipmentSnapshots_AccountId`
    ON `CharacterEquipmentSnapshots` (`AccountId`);

CREATE INDEX `IX_CharacterEquipmentSnapshots_Name`
    ON `CharacterEquipmentSnapshots` (`CharacterName`);

-- ----------------------------------------------------------------------------
-- Leaderboards: per-character event-driven counters (e.g. KillsByCreature
-- with Target=creature type name). Category is the LeaderboardCategory enum;
-- Target is a free-form sub-key, '' for category-wide leaderboards. Truncated
-- on season wipe (see Operations/season-wipe.sql).
-- ----------------------------------------------------------------------------
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

SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================================
-- END mirror of Baseline/CreateSpellboundDb.sql
-- ============================================================================


-- ============================================================================
-- BEGIN mirror of Seeds/achievements.sql
-- Stable Ids matter: code-driven achievement evaluators (see
-- EventHandlers/CustomAchievementRules/) reference rows by Id. Renumbering
-- breaks those references silently.
-- ============================================================================

-- 9001 — First Critical Kill (code-driven via [CustomAchievement(9001, ...)]).
INSERT IGNORE INTO `Achievements`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9001, 'First Critical Kill', 117, 'Reward for landing your first critical-hit killing blow.', 1, NULL, 1, 10, 1);

-- 9002 — First Blood (data-driven wildcard kill).
INSERT IGNORE INTO `Achievements`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9002, 'First Blood', 117, 'Awarded the first time you defeat any creature.', 1, NULL, 7, 1, 1);

-- 9003 — Hunter (kill 100 of any creature).
INSERT IGNORE INTO `Achievements`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9003, 'Hunter', 117, 'Defeat 100 creatures.', 1, NULL, 8, 1, 100);

-- 9004 — Apprentice (reach level 5).
INSERT IGNORE INTO `Achievements`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9004, 'Apprentice', 106, 'Reach level 5.', 5, '5', 2, 5, 1);

-- 9005 — Inevitable (any non-PvP death).
INSERT IGNORE INTO `Achievements`
    (`Id`, `Name`, `EventTrigger`, `AwardDescription`, `FilterType`, `Target`, `AwardType`, `AwardValue`, `AmountRequired`)
VALUES
    (9005, 'Inevitable', 102, 'You died. It happens.', 1, NULL, 3, 5, 1);

-- ============================================================================
-- END mirror of Seeds/achievements.sql
-- ============================================================================


-- ============================================================================
-- BEGIN mirror of Seeds/zones.sql
-- ============================================================================

-- 1 — Holtburg (placeholder landblock — replace with the campaign's actual
-- staging landblock before going live).
INSERT IGNORE INTO `Zones`
    (`Id`, `Name`, `Landblock`, `Stage`, `UpdatedAt`, `Version`)
VALUES
    (1, 'Holtburg', '0xA9B4FFFF', 0, UTC_TIMESTAMP(6), 0);

-- ============================================================================
-- END mirror of Seeds/zones.sql
-- ============================================================================
