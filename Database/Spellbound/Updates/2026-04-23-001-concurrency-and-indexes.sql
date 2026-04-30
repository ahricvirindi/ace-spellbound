-- ============================================================================
-- Spellbound DB migration: concurrency tokens + unique indexes
-- Apply against the `ace_mod_spellbound` database (or whatever
-- Settings.json::MySql.Database is set to).
--
-- Idempotent where MySQL syntax allows; otherwise commented inline.
-- ============================================================================

-- 1) Optimistic-concurrency Version columns ---------------------------------
ALTER TABLE `AccountAchievements`
    ADD COLUMN IF NOT EXISTS `Version` INT NOT NULL DEFAULT 0;

ALTER TABLE `Towns`
    ADD COLUMN IF NOT EXISTS `Version` INT NOT NULL DEFAULT 0;

-- 2) Unique constraint preventing double-grants -----------------------------
-- The model also enforces this in code, but the DB constraint is the
-- last line of defense against racy award paths.
-- IF NOT EXISTS not supported on CREATE INDEX in older MySQL; safe to drop+recreate
-- if you know what you're doing. Otherwise just run once.
CREATE UNIQUE INDEX `IX_AccountAchievements_AccountId_AchievementId`
    ON `AccountAchievements` (`AccountId`, `AchievementId`);

CREATE INDEX `IX_AccountAchievements_AccountId`
    ON `AccountAchievements` (`AccountId`);

CREATE UNIQUE INDEX `IX_Towns_Landblock`
    ON `Towns` (`Landblock`);

CREATE UNIQUE INDEX `IX_AccountVerifications_AccountId`
    ON `AccountVerifications` (`AccountId`);

-- 3) Normalize timestamps to UTC --------------------------------------------
-- All DateTime defaults in models are now DateTime.UtcNow. Existing rows
-- written with DateTime.Now will be in server-local time. If your server
-- runs in UTC already this is a no-op. Otherwise, decide whether to
-- back-correct existing rows manually before relying on these timestamps
-- for cross-host comparisons.
