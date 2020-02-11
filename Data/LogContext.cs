using Microsoft.EntityFrameworkCore;
using OpsSecProject.Models;
using System;

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
        public DbSet<GlueConsolidatedEntity> GlueConsolidatedEntities { get; set; }
        public DbSet<Trigger> AlertTriggers { get; set; }
        public DbSet<QuestionableEvent> QuestionableEvents { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogInput>().ToTable("LogInputs");
            modelBuilder.Entity<LogInput>().Property(l => l.InitialIngest).HasDefaultValue(false);
            modelBuilder.Entity<LogInput>().HasAlternateKey(l => l.Name).HasName("AlternateKey_LogInputName");
            modelBuilder.Entity<S3Bucket>().ToTable("S3Buckets");
            modelBuilder.Entity<S3Bucket>().HasAlternateKey(b => b.Name).HasName("AlternateKey_BucketName");
            modelBuilder.Entity<GlueDatabase>().ToTable("GlueDatabases");
            modelBuilder.Entity<GlueDatabaseTable>().ToTable("GlueDatabaseTables");
            modelBuilder.Entity<GlueConsolidatedEntity>().ToTable("GlueConsolidatedEntities");
            modelBuilder.Entity<Trigger>().Property(s => s.SagemakerStatus).HasDefaultValue(SagemakerStatus.None);
            modelBuilder.Entity<Trigger>().Property(s => s.SagemakerErrorStage).HasDefaultValue(SagemakerErrorStage.None);
            modelBuilder.Entity<Trigger>().Property(s => s.DeprecatedInputDataKeys).HasConversion(v => string.Join(',', v), v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
            modelBuilder.Entity<Trigger>().Property(s => s.DeprecatedModelNames).HasConversion(v => string.Join(',', v), v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
            modelBuilder.Entity<Trigger>().Property(s => s.IgnoredEvents).HasConversion(v => string.Join(',', v), v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
            modelBuilder.Entity<Trigger>().Property(s => s.InferenceBookmark).HasDefaultValue(0);
            modelBuilder.Entity<Trigger>().ToTable("AlertTriggers");
            modelBuilder.Entity<QuestionableEvent>().ToTable("QuestionableEvents");
            modelBuilder.Entity<QuestionableEvent>().Property(q => q.status).HasDefaultValue(QuestionableEventStatus.PendingReview);
        }
    }
}
