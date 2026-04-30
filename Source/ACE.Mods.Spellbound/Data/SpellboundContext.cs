using ACE.Mods.Spellbound.Model;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Data
{
    public class SpellboundContext : DbContext
    {
        public SpellboundContext(DbContextOptions<SpellboundContext> options) : base(options)
        {

        }

        public DbSet<AccountAchievement> AccountAchievements { get; set; }
        public DbSet<AwardedCharacterAchievement> AwardedCharacterAchievements { get; set; }
        public DbSet<Achievement> Achievement { get; set; }
        public DbSet<Zone> Zones { get; set; }
        public DbSet<AccountVerification> AccountVerifications { get; set; }
        public DbSet<WorldStateRule> WorldStateRules { get; set; }
        public DbSet<ReservedName> ReservedNames { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccountAchievement>(b =>
            {
                b.HasKey(x => x.Id);
                // makes sure we dont double-award
                b.HasIndex(x => new { x.AccountId, x.AchievementId }).IsUnique();
                b.HasIndex(x => x.AccountId);
            });

            modelBuilder.Entity<Achievement>(b =>
            {
                b.HasKey(x => x.Id);
            });

            modelBuilder.Entity<AwardedCharacterAchievement>(b =>
            {
                b.HasKey(x => x.Id);
                // makes sure we don't double-award
                b.HasIndex(x => new { x.CharacterId, x.AchievementId }).IsUnique();
                b.HasIndex(x => x.CharacterId);
            });

            modelBuilder.Entity<Zone>(b =>
            {
                b.HasKey(x => x.Id);
                b.HasIndex(x => x.Landblock).IsUnique();
                // Case-insensitive uniqueness via the table's utf8mb4_general_ci
                // collation. Required so /zone tele <name> resolves a single zone.
                b.HasIndex(x => x.Name).IsUnique();
            });

            modelBuilder.Entity<AccountVerification>(b =>
            {
                b.HasKey(x => x.Id);
                b.HasIndex(x => x.AccountId).IsUnique();
            });

            modelBuilder.Entity<WorldStateRule>(b =>
            {
                b.HasKey(x => x.Id);
                // we're reading this....on every event listener so definitely needs this index
                b.HasIndex(x => x.EventTrigger);
                b.HasOne(x => x.Zone)
                    .WithMany()
                    .HasForeignKey(x => x.ZoneId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ReservedName>(b =>
            {
                b.HasKey(x => x.Id);
                // Unique on Name relies on the column collation (utf8mb4_general_ci) for
                // case-insensitive matching, mirroring how shard.character.name behaves.
                b.HasIndex(x => x.Name).IsUnique();
                b.HasIndex(x => x.AccountId);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
