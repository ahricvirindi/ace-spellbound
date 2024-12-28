using ACE.Mods.Spellbound.Config;
using ACE.Mods.Spellbound.Data;
using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Base
{
    [HarmonyPatch]
    public class SpellboundPatchBase : BasicPatch<Settings>
    {
        private static IDbContextFactory<SpellboundContext>? _dbContextFactory;
        private static IServiceProvider? ServiceProvider;

        public SpellboundPatchBase(Mod mod, string settingsName) : base(mod, settingsName)
        {
   
        }

        public override Task OnStartSuccess()
        {
            Settings = SettingsContainer.Settings;

            return Task.CompletedTask;
        }

        public static SpellboundContext CreateDbContext()
        {
            if (Settings == null)
            {
                throw new Exception("Settings has not been initialized. Cannot generate Db context.");
            }

            if (ServiceProvider == null)
            {
                var services = new ServiceCollection();
                string connectionString = $"server={Settings?.MySql?.Host};database={Settings?.MySql?.Database};user={Settings?.MySql?.Username};password={Settings?.MySql?.Password}";
                services.AddPooledDbContextFactory<SpellboundContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), builder => builder.EnableRetryOnFailure(10)));
                ServiceProvider = services.BuildServiceProvider();
                _dbContextFactory = ServiceProvider.GetRequiredService<IDbContextFactory<SpellboundContext>>();
            }

            if (_dbContextFactory == null)
            {
                throw new Exception("Database context factory has not been initialized. Cannot generate Db context.");
            }

            return _dbContextFactory.CreateDbContext();
        }
    }
}
