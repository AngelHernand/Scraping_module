using InterviewSimulator.Scraping.Classifier;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Interfaces;
using InterviewSimulator.Scraping.Data;
using InterviewSimulator.Scraping.Data.Repositories;
using InterviewSimulator.Scraping.Scrapers;
using InterviewSimulator.Scraping.Scrapers.Extensions;
using InterviewSimulator.Scraping.Worker;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/scraping-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando InterviewSimulator Scraping Worker...");

    var builder = Host.CreateApplicationBuilder(args);

    // Serilog
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

    // Configuración
    builder.Services.Configure<ScrapingSettings>(
        builder.Configuration.GetSection(ScrapingSettings.SectionName));

    // Entity Framework Core
    builder.Services.AddDbContext<ScrapingDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Repositorio
    builder.Services.AddScoped<IScrapedDataRepository, ScrapedDataRepository>();

    // Clasificadores
    builder.Services.AddSingleton<IQuestionClassifier, KeywordClassifier>();
    builder.Services.AddSingleton<IContentClassifier, ContentClassifier>();

    // Scrapers (incluye HttpClients con retry policies)
    builder.Services.AddScrapers();

    // Orquestador
    builder.Services.AddScoped<IScrapingOrchestrator, ScrapingOrchestrator>();

    // Worker Service
    builder.Services.AddHostedService<ScrapingWorker>();

    var host = builder.Build();

    // Asegurar que la BD existe y aplicar migraciones
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ScrapingDbContext>();
        await db.Database.EnsureCreatedAsync();
        Log.Information("Base de datos verificada/creada correctamente");
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Error fatal al iniciar el Worker");
}
finally
{
    Log.CloseAndFlush();
}
