using Microsoft.EntityFrameworkCore;
using OpsSecProject.Models;

namespace OpsSecProject.Data
{
    public class LogDataContext : DbContext
    {
        public LogDataContext(DbContextOptions options) : base(options)
        {
        }
        public DbSet<S3Bucket> S3Buckets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<S3Bucket>().ToTable("S3Buckets");
        }
    }
}
