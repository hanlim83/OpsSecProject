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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogInput>().ToTable("LogInputs");
        }
    }
}
