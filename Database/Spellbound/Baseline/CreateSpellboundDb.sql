-- ============================================================================
-- Spellbound DB baseline schema.
-- Run once against an empty `ace_custom_spellbound` (or whatever
-- Settings.json::MySql.Database is set to) to bootstrap a fresh dev box or
-- a new deployment.
--
-- This file is the single source of truth for the schema as of the most
-- recent dated script under Database/Spellbound/Updates/. After running this,
-- you do NOT need to apply any Updates/*.sql whose date is on or before the
-- header date below — they're already folded in. Future deltas land as new
-- Updates/*.sql scripts.
--
-- Maintenance rule: when an Updates/*.sql lands, mirror the resulting schema
-- shape into this file in the same PR so a fresh-box bootstrap stays
-- equivalent to "blank DB + every Updates/*.sql in chronological order."
--
-- Baseline as of: 2026-04-29 (covers Updates through
--                              2026-04-29-001-awarded-character-achievements.sql).
-- ============================================================================

SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------------------------------------------------------
-- Achievement: catalog row referenced by AccountAchievements + the on-grant
-- bonus walk. EventTrigger / FilterType / Target form the data-driven match
-- shape (see Services/RuleMatcher.cs); AwardType / AwardValue / AmountRequired
-- describe what the player gets and how many sub-events are needed to flip
-- AwardedAt.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `Achievement` (
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
-- Towns: one row per landblock that participates in the seasonal town-stage
-- system. Stage is mutated by WorldStateService (event-driven advance,
-- admin-driven force-set); Version is the [ConcurrencyCheck] token so racing
-- updates trigger DbUpdateConcurrencyException instead of last-write-wins.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `Towns` (
    `Id`        INT          NOT NULL AUTO_INCREMENT,
    `Name`      VARCHAR(200) NOT NULL,
    `Landblock` VARCHAR(50)  NOT NULL,
    `Stage`     INT          NOT NULL DEFAULT 0,
    `UpdatedAt` DATETIME(6)  NOT NULL,
    `Version`   INT          NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE UNIQUE INDEX `IX_Towns_Landblock`
    ON `Towns` (`Landblock`);

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
        FOREIGN KEY (`AchievementId`) REFERENCES `Achievement` (`Id`)
        ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE UNIQUE INDEX `IX_AccountAchievements_AccountId_AchievementId`
    ON `AccountAchievements` (`AccountId`, `AchievementId`);

CREATE INDEX `IX_AccountAchievements_AccountId`
    ON `AccountAchievements` (`AccountId`);

-- ----------------------------------------------------------------------------
-- AwardedCharacterAchievements: per-character idempotency guard for the
-- bonus-application walk. The unique (CharacterId, AchievementId) index lets
-- the on-grant walk and the on-character-create walk both attempt the insert
-- and treat a duplicate-key as "already applied; no-op" — see
-- AchievementService.ApplyToCharacter.
-- ----------------------------------------------------------------------------
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
-- WorldStateRules: declarative trigger -> town-stage rules consumed by
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
    `TownId`       INT          NOT NULL,
    `TargetStage`  INT          NOT NULL,
    `Version`      INT          NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_WorldStateRules_Towns_TownId`
        FOREIGN KEY (`TownId`) REFERENCES `Towns` (`Id`) ON DELETE RESTRICT
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_general_ci;

CREATE INDEX `IX_WorldStateRules_EventTrigger`
    ON `WorldStateRules` (`EventTrigger`);

SET FOREIGN_KEY_CHECKS = 1;
