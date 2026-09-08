using System.Text.Json;

using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.Services;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Web.Services;

// Profile-page composer. Three reads:
//   1. shard.character (via CharacterLookupService) — name -> CharacterId / AccountId.
//   2. ace_mod_spellbound.CharacterProfileSnapshots — stats / skills.
//   3. ace_mod_spellbound.AccountAchievements join Achievements — badge wall
//      (account-scoped per CLAUDE.md, not per-character).
//
// HasSnapshot=false means the character exists on shard but has never had a
// profile snapshot written. The page surfaces this distinctly from "character
// not found" — a fresh character that hasn't logged out (or hit the 30-min
// timer) has no snapshot yet.
public sealed class CharacterProfileService(
    CharacterLookupService lookup,
    SpellboundContext spellboundDb,
    ILogger<CharacterProfileService> logger)
{
    public async Task<CharacterProfileViewModel?> GetByNameAsync(string name)
    {
        var ident = await lookup.GetByNameAsync(name);
        if (ident is null) return null;

        var vm = new CharacterProfileViewModel
        {
            Name = ident.Name,
            CharacterId = ident.Id,
            AccountId = ident.AccountId
        };

        try
        {
            var snap = await spellboundDb.CharacterProfileSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.CharacterId == ident.Id);

            if (snap != null)
            {
                vm.HasSnapshot = true;
                vm.Level = snap.Level;
                vm.TotalXp = snap.TotalXp;
                vm.UnassignedXp = snap.UnassignedXp;
                vm.Strength = snap.Strength;
                vm.Endurance = snap.Endurance;
                vm.Coordination = snap.Coordination;
                vm.Quickness = snap.Quickness;
                vm.Focus = snap.Focus;
                vm.Self = snap.Self;
                vm.HealthMax = snap.HealthMax;
                vm.StaminaMax = snap.StaminaMax;
                vm.ManaMax = snap.ManaMax;
                vm.SnapshottedAt = snap.SnapshottedAt;
                vm.Skills = DeserializeSkills(snap.SkillsJson);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load profile snapshot for character={Name}", name);
        }

        try
        {
            var eq = await spellboundDb.CharacterEquipmentSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.CharacterId == ident.Id);

            if (eq != null)
                vm.Equipment = DeserializeEquipment(eq.EquipmentJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load equipment snapshot for character={Name}", name);
        }

        try
        {
            vm.Badges = await spellboundDb.AccountAchievements
                .AsNoTracking()
                .Where(aa => aa.AccountId == (int)ident.AccountId && aa.AwardedAt != null)
                .Join(spellboundDb.Achievements,
                    aa => aa.AchievementId,
                    ach => ach.Id,
                    (aa, ach) => new BadgeViewModel(
                        ach.Name,
                        ach.AwardDescription,
                        aa.AwardedAt!.Value))
                .OrderByDescending(b => b.AwardedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load achievements for account={AccountId}", ident.AccountId);
            vm.Badges = Array.Empty<BadgeViewModel>();
        }

        return vm;
    }

    private static IReadOnlyList<SnapshotService.SkillSnapshotEntry> DeserializeSkills(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SnapshotService.SkillSnapshotEntry>();
        try
        {
            return JsonSerializer.Deserialize<List<SnapshotService.SkillSnapshotEntry>>(json)
                ?? new List<SnapshotService.SkillSnapshotEntry>();
        }
        catch
        {
            return Array.Empty<SnapshotService.SkillSnapshotEntry>();
        }
    }

    private static IReadOnlyList<SnapshotService.EquippedItemEntry> DeserializeEquipment(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SnapshotService.EquippedItemEntry>();
        try
        {
            return JsonSerializer.Deserialize<List<SnapshotService.EquippedItemEntry>>(json)
                ?? new List<SnapshotService.EquippedItemEntry>();
        }
        catch
        {
            return Array.Empty<SnapshotService.EquippedItemEntry>();
        }
    }
}

public sealed class CharacterProfileViewModel
{
    public string Name { get; set; } = "";
    public uint CharacterId { get; set; }
    public uint AccountId { get; set; }

    public bool HasSnapshot { get; set; }
    public int Level { get; set; }
    public long TotalXp { get; set; }
    public long UnassignedXp { get; set; }
    public int Strength { get; set; }
    public int Endurance { get; set; }
    public int Coordination { get; set; }
    public int Quickness { get; set; }
    public int Focus { get; set; }
    public int Self { get; set; }
    public int HealthMax { get; set; }
    public int StaminaMax { get; set; }
    public int ManaMax { get; set; }
    public DateTime SnapshottedAt { get; set; }
    public IReadOnlyList<SnapshotService.SkillSnapshotEntry> Skills { get; set; } =
        Array.Empty<SnapshotService.SkillSnapshotEntry>();

    public IReadOnlyList<SnapshotService.EquippedItemEntry> Equipment { get; set; } =
        Array.Empty<SnapshotService.EquippedItemEntry>();

    public IReadOnlyList<BadgeViewModel> Badges { get; set; } = Array.Empty<BadgeViewModel>();
}

public sealed record BadgeViewModel(string Name, string Description, DateTime AwardedAt);
