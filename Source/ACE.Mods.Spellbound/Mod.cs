using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.EventHandlers;

namespace ACE.Mods.Spellbound
{
    public class Mod : BasicMod
    {
        private const string SETTINGS_NAME = "Settings.json";

        public Mod() : base() {
            Harmony.DEBUG = true;

            List<IPatch> patches = LoadPatches();

            Setup("ACE.Mods.Spellbound", patches.ToArray());

            //TestManualPatchApplication();
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
                    ModManager.Log($"Failed to create instance of {patchType.Name}: {ex.Message}", ModManager.LogLevel.Error);
                }
            }

            return patches;
        }

        private void TestManualPatchApplication()
        {
            var harmony = new Harmony("ACE.Mods.Spellbound.ManualTest");
            var sourceMethod = AccessTools.Method(typeof(Player), nameof(Player.GetCastingPreCheckStatus), new[] { typeof(Server.Entity.Spell), typeof(uint), typeof(bool) });

            if (sourceMethod != null)
            {
                var postfix = typeof(PlayerMagicEventHandler).GetMethod(nameof(PlayerMagicEventHandler.OnSpellFizzleCheck), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                if (postfix != null)
                {
                    harmony.Patch(sourceMethod, postfix: new HarmonyMethod(postfix));
                }
                else
                {
                    ModManager.Log("Postfix method not found.", ModManager.LogLevel.Error);
                }
            }
            else
            {
                ModManager.Log("Source method not found.", ModManager.LogLevel.Error);
            }
        }
    }
}
