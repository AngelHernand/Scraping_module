using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Interfaces;
using InterviewSimulator.RAG.Data;
using InterviewSimulator.RAG.Data.Repositories;
using InterviewSimulator.RAG.Embedding;
using InterviewSimulator.RAG.Processing.Chunking;
using InterviewSimulator.RAG.Processing.Cleaning;
using InterviewSimulator.RAG.Processing.Enrichment;
using InterviewSimulator.RAG.VectorStore;
using InterviewSimulator.RAG.Worker;
using InterviewSimulator.Scraping.Data;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Qdrant.Client;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting RAG Pipeline Worker");

    var builder = Host.CreateDefaultBuilder(args);

    builder.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                "logs/rag-pipeline-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30));

    builder.ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Configuración
        services.Configure<RagPipelineSettings>(configuration.GetSection(RagPipelineSettings.SectionName));
        services.Configure<OpenAISettings>(configuration.GetSection(OpenAISettings.SectionName));
        services.Configure<QdrantSettings>(configuration.GetSection(QdrantSettings.SectionName));

        // DbContexts
        services.AddDbContext<ProcessingDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddDbContext<ScrapingDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Repositorios
        services.AddScoped<IProcessingStatusRepository, ProcessingStatusRepository>();

        // Processing
        services.AddScoped<ITextCleaner, HtmlTextCleaner>();
        services.AddScoped<IChunkingService, ChunkingService>();
        services.AddScoped<InterviewQuestionChunker>();
        services.AddScoped<RecursiveTextChunker>();
        services.AddScoped<ChunkMetadataEnricher>();

        // Embedding con Polly retry
        services.AddHttpClient<OpenAIEmbeddingService>(client =>
        {
            var openAiSettings = configuration.GetSection(OpenAISettings.SectionName).Get<OpenAISettings>()!;
            client.BaseAddress = new Uri(openAiSettings.BaseUrl);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiSettings.ApiKey}");
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .AddPolicyHandler(GetRetryPolicy());
        services.AddScoped<IEmbeddingService, OpenAIEmbeddingService>();
        services.AddSingleton<EmbeddingCache>();

        // Qdrant
        services.AddSingleton(sp =>
        {
            var qdrantSettings = configuration.GetSection(QdrantSettings.SectionName).Get<QdrantSettings>()!;
            return new QdrantClient(qdrantSettings.Host, qdrantSettings.Port);
        });
        services.AddScoped<QdrantCollectionManager>();
        services.AddScoped<IVectorStoreService, QdrantVectorStoreService>();

        // Retrieval
        services.AddScoped<IRetrievalService, InterviewSimulator.RAG.Retrieval.RetrievalService>();

        // Orchestrator y Worker
        services.AddScoped<IRagPipelineOrchestrator, RagPipelineOrchestrator>();
        services.AddHostedService<RagPipelineWorker>();

        // Health checks
        services.AddHealthChecks()
            .AddCheck<QdrantHealthCheck>("qdrant");
    });

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RAG Pipeline Worker terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Log.Warning("Retry {RetryAttempt} after {Delay}s for OpenAI API: {Reason}",
                    retryAttempt, timespan.TotalSeconds,
                    outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
            });
}
