using InterviewSimulator.RAG.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace InterviewSimulator.RAG.Data;

public class ProcessingDbContext : DbContext
{
    public ProcessingDbContext(DbContextOptions<ProcessingDbContext> options) : base(options) { }

    public DbSet<ProcessingStatus> ProcessingStatuses => Set<ProcessingStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcessingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
