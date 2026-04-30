namespace ACE.Mods.Spellbound.Helpers
{
    /// <summary>
    /// Single source of truth for the <c>[Spellbound]</c> log prefix. Use these
    /// helpers instead of calling <see cref="ModManager.Log"/> directly so the prefix
    /// can never drift between files and so log-level intent is obvious at the call site.
    /// </summary>
    public static class SpellboundLog
    {
        public static void Info(string msg) => ModManager.Log($"[Spellbound] {msg}");
        public static void Warn(string msg) => ModManager.Log($"[Spellbound] {msg}", ModManager.LogLevel.Warn);
        public static void Error(string msg) => ModManager.Log($"[Spellbound] {msg}", ModManager.LogLevel.Error);
    }
}
