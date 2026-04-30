using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Config;
using ACE.Mods.Spellbound.Model.Events;
using ACE.Mods.Spellbound.Services;

namespace ACE.Mods.Spellbound
{
    public class Mod : BasicMod
    {
        private const string SETTINGS_NAME = "Settings.json";

        public Mod() : base() {
            // Harmony.DEBUG must be set BEFORE Setup() applies patches — by the time
            // SpellboundPatchBase.OnStartSuccess populates Settings, patch IL has
            // already been written. We therefore read Settings.json directly here
            // rather than going through SettingsContainer. Default off; flip to true
            // in Settings.json to diagnose a specific patch failure.
            Harmony.DEBUG = TryReadHarmonyDebugFlag();

            List<IPatch> patches = LoadPatches();

            // Discover [SpellboundEvent] subscribers and [CustomAchievement]
            // evaluators in the mod assembly. Done before Setup() so any
            // registration error fails the mod boot rather than going live with
            // broken event wiring.
            var asm = Assembly.GetExecutingAssembly();
            EventBus.DiscoverAndRegister(asm);
            CustomAchievementRegistry.DiscoverAndRegister(asm);

            Setup("ACE.Mods.Spellbound", patches.ToArray());
        }

        private List<IPatch> LoadPatches()
        {
            List<IPatch> patches = [];

            Assembly assembly = Assembly.GetExecutingAssembly();

            List<Type> patchTypes = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(SpellboundPatchBase))).ToList();

            foreach (Type patchType in patchTypes)
            {
                try
                {
                    IPatch? patchInstance = Activator.CreateInstance(patchType, this, SETTINGS_NAME) as SpellboundPatchBase ?? null;
                    if (patchInstance == null) { continue; }
                    patches.Add(patchInstance);
                }
                catch (Exception ex)
                {
                    SpellboundLog.Error($"Failed to create instance of {patchType.Name}: {ex.Message}");
                }
            }

            return patches;
        }

        // Read the HarmonyDebug flag straight from Settings.json. Failures fall back to
        // false — verbose Harmony logging is opt-in, never default-on.
        private static bool TryReadHarmonyDebugFlag()
        {
            try
            {
                var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(asmDir)) return false;

                var path = Path.Combine(asmDir, SETTINGS_NAME);
                if (!File.Exists(path)) return false;

                using var stream = File.OpenRead(path);
                var settings = JsonSerializer.Deserialize<Settings>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                return settings?.HarmonyDebug ?? false;
            }
            catch
            {
                return false;
            }
        }
    }
}
