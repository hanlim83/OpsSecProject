using Microsoft.EntityFrameworkCore;
using OpsSecProject.Models;

namespace OpsSecProject.Data
{
    public class SecurityContext : DbContext
    {
        public SecurityContext(DbContextOptions<SecurityContext> options) : base(options)
        {
        }
        public DbSet<Activity> Activities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Activity>().ToTable("Activities");
        }
    }
}
