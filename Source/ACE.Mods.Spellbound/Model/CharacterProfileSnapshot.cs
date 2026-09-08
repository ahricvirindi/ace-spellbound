namespace ACE.Mods.Spellbound.Model
{
    // Snapshot row for the character profile page. Captured on logout + a
    // 30-minute timer for online characters. SkillsJson is a serialized list
    // of {Skill, State, Ranks, Base, Current} objects — kept as a TEXT blob
    // so the shape can evolve without an EF migration. SnapshotService is the
    // sole writer; the web app is read-only.
    public class CharacterProfileSnapshot
    {
        public uint CharacterId { get; set; }
        public int AccountId { get; set; }
        public string CharacterName { get; set; } = string.Empty;
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

        public string SkillsJson { get; set; } = "[]";
        public DateTime SnapshottedAt { get; set; }
    }
}
