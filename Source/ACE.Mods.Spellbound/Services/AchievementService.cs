using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.Model;
using ACE.Mods.Spellbound.Model.Enumerations;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Services
{
    public static class AchievementService
    {
        public static bool Award(SpellboundContext db, uint accountIdU, Achievement ach)
        {
            var awarded = TryAwardAtomic(db, (int)accountIdU, ach);
            if (!awarded)
                return false;

            SpellboundLog.Info($"Awarded '{ach.Name}' to account {accountIdU}.");
            ApplyToAllAccountCharacters(accountIdU, new[] { ach });
            return true;
        }

        public static bool AwardById(SpellboundContext db, uint accountIdU, int achievementId)
        {
            var ach = db.Achievements.AsNoTracking().FirstOrDefault(a => a.Id == achievementId);
            if (ach == null)
            {
                SpellboundLog.Warn($"AwardById: no Achievement with id {achievementId}.");
                return false;
            }

            return Award(db, accountIdU, ach);
        }

        public static bool TryAwardAtomic(SpellboundContext db, int accountId, Achievement ach)
        {
            var amountRequired = Math.Max(1, ach.AmountRequired ?? 1);

            using var tx = db.Database.BeginTransaction();
            try
            {
                // Reason for Inline SQL: EF's read-modify-write pattern (FirstOrDefault → mutate → SaveChanges)
                // opens a TOCTOU window between the SELECT and the UPDATE. Two threads can both read
                // AwardedAt == null and both flip it; the unique index doesn't help because the second
                // writer is updating an existing row, not inserting a new one. INSERT...ON DUPLICATE
                // KEY UPDATE is one DB roundtrip with InnoDB row-level X-locking — the only way to
                // guarantee progress increments serialize correctly under concurrent fires.
                db.Database.ExecuteSqlInterpolated($@"
                    INSERT INTO `AccountAchievements`
                        (`AccountId`, `AchievementId`, `Progress`, `AwardedAt`, `Version`)
                    VALUES
                        ({accountId}, {ach.Id}, 1, NULL, 1)
                    ON DUPLICATE KEY UPDATE
                        `Progress` = IF(`AwardedAt` IS NULL, `Progress` + 1, `Progress`),
                        `Version`  = IF(`AwardedAt` IS NULL, `Version`  + 1, `Version`);");

                // Reason for inline SQL: the claim. WHERE AwardedAt IS NULL is the atomic flip — exactly one
                // transaction sees claimed == 1 even if N threads race here simultaneously. EF can't
                // express this; SaveChanges() on a tracked entity would just do a blind UPDATE that
                // overwrites a concurrent flip.
                var claimed = db.Database.ExecuteSqlInterpolated($@"
                    UPDATE `AccountAchievements`
                       SET `AwardedAt` = UTC_TIMESTAMP(),
                           `Version`   = `Version` + 1
                     WHERE `AccountId`     = {accountId}
                       AND `AchievementId` = {ach.Id}
                       AND `AwardedAt` IS NULL
                       AND `Progress`     >= {amountRequired};");

                tx.Commit();
                return claimed == 1;
            }
            catch (Exception ex)
            {
                SpellboundLog.Error($"TryAwardAtomic(account={accountId}, achievement={ach.Id}) failed: {ex}");
                try { tx.Rollback(); } catch { /* connection may already be aborted */ }
                return false;
            }
        }

        public static void ApplyToAllAccountCharacters(uint accountId, IReadOnlyList<Achievement> awards)
        {
            var characters = PlayerManager.GetAccountPlayers(accountId);
            if (characters == null || characters.Count == 0)
            {
                SpellboundLog.Warn($"No characters on account {accountId} to apply {awards.Count} new award(s) to.");
                return;
            }

            foreach (var kvp in characters)
            {
                var character = kvp.Value;
                foreach (var ach in awards)
                    ApplyToCharacter(character, ach);
            }
        }

        public static void ApplyToCharacter(IPlayer character, Achievement ach)
        {
            if (character == null) return;

            var characterId = character.Guid.Full;
            var awardValue = ach.AwardValue ?? 0;
            if (awardValue <= 0)
            {
                SpellboundLog.Warn(
                    $"ApplyToCharacter: '{ach.Name}' has no positive AwardValue ({awardValue}); skipping for {character.Name}.");
                return;
            }

            if (!IsBaseStatAward(ach.AwardType))
            {
                SpellboundLog.Info(
                    $"ApplyToCharacter: '{ach.Name}' AwardType {ach.AwardType} is multiplier/runtime-style; deferred. Skipping {character.Name}.");
                return;
            }

            SpellboundPatchBase.RunDbWork<int>(
                db =>
                {
                    // Reason for inline SQL: INSERT IGNORE makes the unique (CharacterId, AchievementId) index
                    // the source of truth for "already applied" — the DB returns 0 rows-affected on
                    // duplicate without raising. EF's db.Add + SaveChanges would throw
                    // DbUpdateException on the same race and force the caller to inspect the
                    // exception to distinguish "duplicate, no-op" from a real failure. INSERT IGNORE
                    // keeps the success path branch-free.
                    return db.Database.ExecuteSqlInterpolated($@"
                        INSERT IGNORE INTO `AwardedCharacterAchievements`
                            (`CharacterId`, `AchievementId`, `AppliedAt`)
                        VALUES
                            ({characterId}, {ach.Id}, UTC_TIMESTAMP(6));");
                },
                inserted =>
                {
                    if (inserted == 0) return;

                    try
                    {
                        if (character is Player onlinePlayer)
                        {
                            var chain = new ActionChain();
                            chain.AddAction(onlinePlayer, () => ApplyToOnlinePlayer(onlinePlayer, ach, awardValue));
                            chain.EnqueueChain();
                        }
                        else if (character is OfflinePlayer offline)
                        {
                            ApplyToOfflinePlayer(offline, ach, awardValue);
                        }
                        else
                        {
                            SpellboundLog.Warn(
                                $"ApplyToCharacter: unknown IPlayer subtype {character.GetType().Name} for {character.Name}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        SpellboundLog.Error(
                            $"ApplyToCharacter: bonus mutation FAILED for {character.Name} ({characterId:X8}) on '{ach.Name}'. " +
                            $"AwardedCharacterAchievements row is now a phantom — delete it manually to retry. {ex}");
                    }
                });
        }

        private static bool IsBaseStatAward(AchievementAwardTypes type) => type switch
        {
            AchievementAwardTypes.Strength
            or AchievementAwardTypes.Endurance
            or AchievementAwardTypes.Coordination
            or AchievementAwardTypes.Quickness
            or AchievementAwardTypes.Focus
            or AchievementAwardTypes.Self
            or AchievementAwardTypes.Health
            or AchievementAwardTypes.Stamina
            or AchievementAwardTypes.Mana
            or AchievementAwardTypes.MeleeDefense
            or AchievementAwardTypes.MissileDefense
            or AchievementAwardTypes.MagicDefense => true,
            _ => false,
        };

        private static void ApplyToOnlinePlayer(Player player, Achievement ach, int delta)
        {
            var udelta = (uint)delta;

            switch (ach.AwardType)
            {
                case AchievementAwardTypes.Strength:
                case AchievementAwardTypes.Endurance:
                case AchievementAwardTypes.Coordination:
                case AchievementAwardTypes.Quickness:
                case AchievementAwardTypes.Focus:
                case AchievementAwardTypes.Self:
                {
                    var attr = MapAttribute(ach.AwardType);
                    var playerAttr = player.Attributes[attr];
                    playerAttr.StartingValue += udelta;
                    player.Session?.Network?.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, playerAttr));
                    break;
                }
                case AchievementAwardTypes.Health:
                case AchievementAwardTypes.Stamina:
                case AchievementAwardTypes.Mana:
                {
                    var vital = MapVital(ach.AwardType);
                    var playerVital = player.Vitals[vital];
                    playerVital.StartingValue += udelta;
                    player.Session?.Network?.EnqueueSend(new GameMessagePrivateUpdateVital(player, playerVital));
                    break;
                }
                case AchievementAwardTypes.MeleeDefense:
                case AchievementAwardTypes.MissileDefense:
                case AchievementAwardTypes.MagicDefense:
                {
                    var skill = MapDefenseSkill(ach.AwardType);
                    var playerSkill = player.GetCreatureSkill(skill);
                    playerSkill.InitLevel += udelta;
                    player.Session?.Network?.EnqueueSend(new GameMessagePrivateUpdateSkill(player, playerSkill));
                    break;
                }
            }

            player.SaveBiotaToDatabase();
            SpellboundLog.Info($"Applied {ach.AwardType} +{delta} from '{ach.Name}' to {player.Name} ({player.Guid.Full:X8}).");
        }

        private static void ApplyToOfflinePlayer(OfflinePlayer offline, Achievement ach, int delta)
        {
            var udelta = (uint)delta;
            var biota = offline.Biota;
            var rwLock = offline.BiotaDatabaseLock;

            switch (ach.AwardType)
            {
                case AchievementAwardTypes.Strength:
                case AchievementAwardTypes.Endurance:
                case AchievementAwardTypes.Coordination:
                case AchievementAwardTypes.Quickness:
                case AchievementAwardTypes.Focus:
                case AchievementAwardTypes.Self:
                {
                    var attr = MapAttribute(ach.AwardType);
                    rwLock.EnterWriteLock();
                    try
                    {
                        if (biota.PropertiesAttribute == null || !biota.PropertiesAttribute.TryGetValue(attr, out var props))
                        {
                            SpellboundLog.Warn(
                                $"ApplyToOfflinePlayer: {offline.Name} has no PropertiesAttribute[{attr}] entry. Skipping.");
                            return;
                        }
                        props.InitLevel += udelta;
                    }
                    finally { rwLock.ExitWriteLock(); }
                    break;
                }
                case AchievementAwardTypes.Health:
                case AchievementAwardTypes.Stamina:
                case AchievementAwardTypes.Mana:
                {
                    var vital = MapVital(ach.AwardType);
                    rwLock.EnterWriteLock();
                    try
                    {
                        if (biota.PropertiesAttribute2nd == null || !biota.PropertiesAttribute2nd.TryGetValue(vital, out var props))
                        {
                            SpellboundLog.Warn(
                                $"ApplyToOfflinePlayer: {offline.Name} has no PropertiesAttribute2nd[{vital}] entry. Skipping.");
                            return;
                        }
                        props.InitLevel += udelta;
                    }
                    finally { rwLock.ExitWriteLock(); }
                    break;
                }
                case AchievementAwardTypes.MeleeDefense:
                case AchievementAwardTypes.MissileDefense:
                case AchievementAwardTypes.MagicDefense:
                {
                    var skill = MapDefenseSkill(ach.AwardType);
                    var props = biota.GetOrAddSkill(skill, rwLock, out _);
                    rwLock.EnterWriteLock();
                    try
                    {
                        props.InitLevel += udelta;
                    }
                    finally { rwLock.ExitWriteLock(); }
                    break;
                }
            }

            offline.ChangesDetected = true;
            offline.SaveBiotaToDatabase();
            SpellboundLog.Info(
                $"Applied {ach.AwardType} +{delta} from '{ach.Name}' to offline {offline.Name} ({offline.Guid.Full:X8}).");
        }

        private static PropertyAttribute MapAttribute(AchievementAwardTypes t) => t switch
        {
            AchievementAwardTypes.Strength => PropertyAttribute.Strength,
            AchievementAwardTypes.Endurance => PropertyAttribute.Endurance,
            AchievementAwardTypes.Coordination => PropertyAttribute.Coordination,
            AchievementAwardTypes.Quickness => PropertyAttribute.Quickness,
            AchievementAwardTypes.Focus => PropertyAttribute.Focus,
            AchievementAwardTypes.Self => PropertyAttribute.Self,
            _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Not an attribute award type."),
        };

        private static PropertyAttribute2nd MapVital(AchievementAwardTypes t) => t switch
        {
            AchievementAwardTypes.Health => PropertyAttribute2nd.MaxHealth,
            AchievementAwardTypes.Stamina => PropertyAttribute2nd.MaxStamina,
            AchievementAwardTypes.Mana => PropertyAttribute2nd.MaxMana,
            _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Not a vital award type."),
        };

        private static Skill MapDefenseSkill(AchievementAwardTypes t) => t switch
        {
            AchievementAwardTypes.MeleeDefense => Skill.MeleeDefense,
            AchievementAwardTypes.MissileDefense => Skill.MissileDefense,
            AchievementAwardTypes.MagicDefense => Skill.MagicDefense,
            _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Not a defense skill award type."),
        };
    }
}
