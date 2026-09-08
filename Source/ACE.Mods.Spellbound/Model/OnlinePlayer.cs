namespace ACE.Mods.Spellbound.Model
{
    // Snapshot row for the /who page on the web app. One row per online character,
    // primary key is CharacterId (game-side Player.Guid.Full / shard.character.id).
    // Login/logout patches insert and delete; a 5-minute timer rebuilds the table
    // from the live PlayerManager roster as the truth source.
    public class OnlinePlayer
    {
        public uint CharacterId { get; set; }
        public int AccountId { get; set; }
        public string CharacterName { get; set; } = string.Empty;
        public int Level { get; set; }
        public uint? Landblock { get; set; }
        public string? LandblockName { get; set; }
        public DateTime SeenAt { get; set; }
    }
}
