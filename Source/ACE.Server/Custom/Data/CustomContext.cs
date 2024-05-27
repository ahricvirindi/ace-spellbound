using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE.Common;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using ACE.Server.Custom.Config;

namespace ACE.Server.Custom.Data
{
    internal class CustomContext : DbContext 
    {
        public DbSet<AccountAchievement> AccountAchievements { get; set; }
        public DbSet<Achievement> Achievement { get; set; }
        public DbSet<Town> Towns { get; set; }
        public DbSet<AccountVerification> AccountVerifications { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionString = $"server={CustomConfigManager.Config.Custom.MySql.Host};database={CustomConfigManager.Config.Custom.MySql.Database};user={CustomConfigManager.Config.Custom.MySql.Username};password={CustomConfigManager.Config.Custom.MySql.Password}";
            optionsBuilder.UseMySql(connectionString,  ServerVersion.AutoDetect(connectionString));
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
