using ACE.Mods.Spellbound.Config;
using ACE.Mods.Spellbound.Data;
using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Base
{
    [HarmonyPatch]
    public class SpellboundPatchBase : BasicPatch<Settings>
    {
        // Lazy<T> with the default thread-safety mode (ExecutionAndPublication) guarantees
        // exactly one ServiceProvider/factory is constructed even under concurrent first-touch.
        private static readonly Lazy<IDbContextFactory<SpellboundContext>> _dbContextFactory =
            new(BuildDbContextFactory, LazyThreadSafetyMode.ExecutionAndPublication);

        public SpellboundPatchBase(Mod mod, string settingsName) : base(mod, settingsName)
        {

        }

        public override Task OnStartSuccess()
        {
            Settings = SettingsContainer.Settings;

            return Task.CompletedTask;
        }

        private static IDbContextFactory<SpellboundContext> BuildDbContextFactory()
        {
            if (Settings?.MySql == null)
            {
                throw new InvalidOperationException("Settings.MySql has not been initialized. Cannot build Db context factory.");
            }

            var services = new ServiceCollection();
            string connectionString = $"server={Settings.MySql.Host};database={Settings.MySql.Database};user={Settings.MySql.Username};password={Settings.MySql.Password}";
            services.AddPooledDbContextFactory<SpellboundContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                    builder => builder.EnableRetryOnFailure(10)));

            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IDbContextFactory<SpellboundContext>>();
        }

        public static bool IsDbReady => Settings?.MySql != null;

        public static SpellboundContext CreateDbContext()
        {
            if (!IsDbReady)
                throw new InvalidOperationException(
                    "Spellbound DB not ready: Settings.MySql has not been initialized. " +
                    "CreateDbContext() must not be called before OnStartSuccess.");

            return _dbContextFactory.Value.CreateDbContext();
        }

        public static Task RunDbWork(Action<SpellboundContext> work, [CallerMemberName] string caller = "")
        {
            if (!IsDbReady)
            {
                SpellboundLog.Warn($"DB work from {caller} dropped: Spellbound mod not yet started (Settings.MySql is null).");
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                try
                {
                    using var db = CreateDbContext();
                    work(db);
                }
                catch (Exception ex)
                {
                    SpellboundLog.Error($"DB work from {caller} failed: {ex}");
                }
            });
        }

        public static Task RunDbWork<T>(Func<SpellboundContext, T> work, Action<T> onResult, [CallerMemberName] string caller = "")
        {
            if (!IsDbReady)
            {
                SpellboundLog.Warn($"DB work from {caller} dropped: Spellbound mod not yet started (Settings.MySql is null).");
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                try
                {
                    T result;
                    using (var db = CreateDbContext())
                    {
                        result = work(db);
                    }
                    onResult(result);
                }
                catch (Exception ex)
                {
                    SpellboundLog.Error($"DB work from {caller} failed: {ex}");
                }
            });
        }
    }
}
