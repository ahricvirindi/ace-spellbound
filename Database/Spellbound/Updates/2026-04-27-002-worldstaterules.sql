-- ============================================================================
-- Spellbound DB migration: WorldStateRules table
-- Apply against `ace_mod_spellbound`.
--
-- WorldStateRules drive `WorldStateService` — a row says "when this trigger
-- fires and the event matches my filters, advance Town X to stage N." Used
-- to wire narrative beats off in-game events (e.g., killing a boss flips a
-- town stage) without writing new code.
-- ============================================================================

CREATE TABLE IF NOT EXISTS `WorldStateRules` (
    `Id`              INT          NOT NULL AUTO_INCREMENT,
    `Name`            VARCHAR(200) NOT NULL,
    `EventTrigger`    INT          NOT NULL,
    `WeenieClassId`   INT UNSIGNED NULL,
    `CreatureType`    INT          NULL,
    `TownId`          INT          NOT NULL,
    `TargetStage`     INT          NOT NULL,
    `Version`         INT          NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    INDEX `IX_WorldStateRules_EventTrigger` (`EventTrigger`),
    CONSTRAINT `FK_WorldStateRules_Towns_TownId`
        FOREIGN KEY (`TownId`) REFERENCES `Towns` (`Id`) ON DELETE RESTRICT
);

-- ----------------------------------------------------------------------------
-- Sample seed (commented). Uncomment + adjust after you have a Town row to
-- attach to. WeenieClassId 12345 is the canonical "Aerbax" example from the
-- design conversation; replace with the actual boss wcid for your content.
-- ----------------------------------------------------------------------------
-- INSERT INTO `WorldStateRules`
--     (`Name`, `EventTrigger`, `WeenieClassId`, `CreatureType`, `TownId`, `TargetStage`)
-- VALUES
--     ('Aerbax falls -> Stage 5', 117, 12345, NULL, /* TownId */ 1, 5);
-- Note: 117 == SpellboundEventTrigger.Player_OnKill (see
-- Source/ACE.Mods.Spellbound/Model/Enumerations/SpellboundEventTrigger.cs).
