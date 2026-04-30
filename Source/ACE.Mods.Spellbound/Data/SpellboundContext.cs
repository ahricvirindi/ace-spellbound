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
        public DbSet<Town> Towns { get; set; }
        public DbSet<AccountVerification> AccountVerifications { get; set; }
        public DbSet<WorldStateRule> WorldStateRules { get; set; }
        public DbSet<LandblockAlias> LandblockAliases { get; set; }

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

            modelBuilder.Entity<Town>(b =>
            {
                b.HasKey(x => x.Id);
                b.HasIndex(x => x.Landblock).IsUnique();
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
                b.HasOne(x => x.Town)
                    .WithMany()
                    .HasForeignKey(x => x.TownId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LandblockAlias>(b =>
            {
                b.HasKey(x => x.Id);
                b.HasIndex(x => x.Landblock).IsUnique();
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
