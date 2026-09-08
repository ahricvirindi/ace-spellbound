-- ============================================================================
-- Spellbound DB migration: add snapshot tables for the web app.
-- Apply against the `ace_mod_spellbound` database.
--
-- Why: the web app (Asheron's Eye) is read-only against the game shard. Rather
-- than have it query live shard tables on every page load, the mod owns
-- snapshot writers (login/logout patches + periodic timers) that capture the
-- relevant slices of player state into these tables. The web app then reads
-- exclusively from snapshots.
--
--   * OnlinePlayers          -- /who roster.   Cadence: login/logout + 5min sweep.
--   * CharacterProfileSnapshots -- profile page. Cadence: logout + 30min sweep.
--
-- SkillsJson: free-form JSON blob (TEXT). Shape is owned by SnapshotService —
-- a list of {Skill, State, Ranks, Base, Current} objects. Stored as a string,
-- not a typed JSON column, so the mod and web app can evolve the shape
-- without an EF migration on every tweak.
-- ============================================================================

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
