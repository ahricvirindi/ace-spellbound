namespace ACE.Mods.Spellbound.Model
{
    // Equipped-gear snapshot for the character profile page. Captured in
    // lockstep with CharacterProfileSnapshot — same writers (logout patch +
    // 30-min timer), same key (CharacterId), same logic for "no row yet means
    // never logged out since the snapshot system landed."
    //
    // EquipmentJson is owned by SnapshotService — list of EquippedItemEntry
    // objects. TEXT blob so the shape can evolve without an EF migration.
    public class CharacterEquipmentSnapshot
    {
        public uint CharacterId { get; set; }
        public int AccountId { get; set; }
        public string CharacterName { get; set; } = string.Empty;
        public string EquipmentJson { get; set; } = "[]";
        public DateTime SnapshottedAt { get; set; }
    }
}
