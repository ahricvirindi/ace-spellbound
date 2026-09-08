-- ============================================================================
-- Spellbound DB migration: add CharacterEquipmentSnapshots for the web app.
-- Apply against the `ace_mod_spellbound` database.
--
-- Why: Phase 2 of the web app shows equipped gear on the character profile
-- page. Like SkillsJson on CharacterProfileSnapshots, the equipment payload
-- is a TEXT JSON blob owned by SnapshotService — a list of
-- {Name, Slot, IconId, Workmanship, Damage, ArmorLevel, Spellcraft, MaxMana,
-- Value, Spells} objects. Stored as a string, not a typed JSON column, so
-- the shape can evolve without an EF migration.
--
-- Cadence matches profile snapshots: written on logout (FinalizeLogout
-- patch) and by the existing 30-minute timer for online characters. The
-- writers live alongside the profile writers in EventHandlers/SnapshotRules/.
-- ============================================================================

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
