namespace ACE.Mods.Spellbound.Helpers
{
    /// <summary>
    /// Semantic shortcuts for sending system-chat messages to players. Replaces the
    /// raw <c>player.Session.Network.EnqueueSend(new GameMessageSystemChat(...))</c>
    /// ceremony scattered through publishers and gameplay rules.
    ///
    /// All entry points are null-safe and offline-safe: passing a null player or one
    /// without a session is a no-op, so call sites don't need to guard themselves.
    /// </summary>
    public static class PlayerMessaging
    {
        /// <summary>
        /// Send a system-chat line to one player. No-op if the player is offline
        /// (no session) or null. Intended to be called from any thread — message is
        /// enqueued onto the network out queue, not sent inline.
        /// </summary>
        public static void Tell(this Player? player, string message, ChatMessageType type = ChatMessageType.System)
        {
            if (player?.Session == null) return;
            player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, type));
        }

        /// <summary>
        /// Send a broadcast line to every online player on the world. Use sparingly
        /// — this fans out to N sessions on the calling thread. For announcements
        /// triggered from gameplay events, prefer firing from a slash command or a
        /// season-rollover hook rather than from a hot per-tick path.
        /// </summary>
        public static void BroadcastWorld(string message, ChatMessageType type = ChatMessageType.Broadcast)
        {
            foreach (var p in PlayerManager.GetAllOnline())
                p.Tell(message, type);
        }
    }
}
