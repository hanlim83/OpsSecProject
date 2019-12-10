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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>().ToTable("Roles").HasAlternateKey(r => r.RoleName).HasName("AlternateKey_RoleName");
            modelBuilder.Entity<User>().ToTable("Users").HasAlternateKey(u => u.Username).HasName("AlternateKey_Username");
            modelBuilder.Entity<NotificationToken>().ToTable("NotificationTokens");
        }
    }
}
