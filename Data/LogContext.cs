using Microsoft.EntityFrameworkCore;
using OpsSecProject.Models;

namespace OpsSecProject.Data
{
    public class LogContext : DbContext
    {
        public LogContext(DbContextOptions<LogContext> options) : base(options)
        {
        }
        public DbSet<LogInput> LogInputs { get; set; }
        public DbSet<S3Bucket> S3Buckets { get; set; }
        public DbSet<GlueDatabase> GlueDatabases { get; set; }
        public DbSet<GlueDatabaseTable> GlueDatabaseTables { get; set; }
        public DbSet<GlueConsolidatedInputEntity> GlueConsolidatedInputEntities { get; set; }
        public DbSet<SagemakerConsolidatedEntity> SagemakerConsolidatedEntities { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogInput>().ToTable("LogInputs");
            modelBuilder.Entity<S3Bucket>().ToTable("S3Buckets");
            modelBuilder.Entity<GlueDatabase>().ToTable("GlueDatabases");
            modelBuilder.Entity<GlueDatabaseTable>().ToTable("GlueDatabaseTables");
            modelBuilder.Entity<GlueConsolidatedInputEntity>().ToTable("GlueConsolidatedInputEntities");
            modelBuilder.Entity<SagemakerConsolidatedEntity>().ToTable("SagemakerConsolidatedEntities");
        }
    }
}
