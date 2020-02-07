using Microsoft.EntityFrameworkCore;
using OpsSecProject.Models;

namespace OpsSecProject.Data
{
    public class AccountContext : DbContext
    {
        public AccountContext(DbContextOptions<AccountContext> options) : base(options)
        {
        }
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<NotificationToken> NotificationTokens { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<Settings> Settings { get; set; }
        public DbSet<Alert> Alerts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<Role>().HasAlternateKey(r => r.RoleName).HasName("AlternateKey_RoleName");
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<User>().HasAlternateKey(u => u.Username).HasName("AlternateKey_Username");
            modelBuilder.Entity<User>().HasOne(u => u.LinkedSettings).WithOne(s => s.LinkedUser).HasForeignKey<Settings>(s => s.LinkedUserID);
            modelBuilder.Entity<NotificationToken>().ToTable("NotificationTokens");
            modelBuilder.Entity<Settings>().ToTable("Activities");
            modelBuilder.Entity<Settings>().ToTable("Settings");
            modelBuilder.Entity<Settings>().Property(s => s.Always2FA).HasDefaultValue(false);
            modelBuilder.Entity<Settings>().Property(s => s.AutoDeploy).HasDefaultValue(true);
            modelBuilder.Entity<Alert>().ToTable("Alerts");
            modelBuilder.Entity<Alert>().Property(a => a.Read).HasDefaultValue(false);
            modelBuilder.Entity<Alert>().Property(a => a.LinkedObjectID).HasDefaultValue(0);
        }
    }
}
