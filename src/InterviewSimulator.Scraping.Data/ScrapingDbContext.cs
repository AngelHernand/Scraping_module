using InterviewSimulator.Scraping.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace InterviewSimulator.Scraping.Data;

/// <summary>
/// DbContext para las entidades del módulo de scraping.
/// Incluye tanto preguntas Q&A como documentos RAG del corpus.
/// </summary>
public class ScrapingDbContext : DbContext
{
    public ScrapingDbContext(DbContextOptions<ScrapingDbContext> options) : base(options) { }

    public DbSet<ScrapedQuestion> ScrapedQuestions => Set<ScrapedQuestion>();
    public DbSet<ScrapedDocument> ScrapedDocuments => Set<ScrapedDocument>();
    public DbSet<ScrapedSource> ScrapedSources => Set<ScrapedSource>();
    public DbSet<ScrapingJob> ScrapingJobs => Set<ScrapingJob>();

    public DbSet<BehavioralQuestion> BehavioralQuestions => Set<BehavioralQuestion>();
    public DbSet<EvaluationCriteria> EvaluationCriteria => Set<EvaluationCriteria>();
    public DbSet<RedFlag> RedFlags => Set<RedFlag>();
    public DbSet<ExampleAnswer> ExampleAnswers => Set<ExampleAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScrapingDbContext).Assembly);
    }
}
