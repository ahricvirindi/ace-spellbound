using ACE.Mods.Spellbound.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Spellbound.Data
{
    public class SpellboundContext : DbContext
    {
        public SpellboundContext(DbContextOptions<SpellboundContext> options) : base(options)
        {

        }

        public DbSet<AccountAchievement> AccountAchievements { get; set; }
        public DbSet<Achievement> Achievement { get; set; }
        public DbSet<Town> Towns { get; set; }
        public DbSet<AccountVerification> AccountVerifications { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccountAchievement>().HasKey(x => x.Id);
            modelBuilder.Entity<Achievement>().HasKey(x => x.Id);
            modelBuilder.Entity<Town>().HasKey(x => x.Id);
            modelBuilder.Entity<AccountVerification>().HasKey(x => x.Id);

            base.OnModelCreating(modelBuilder);
        }
    }
}
