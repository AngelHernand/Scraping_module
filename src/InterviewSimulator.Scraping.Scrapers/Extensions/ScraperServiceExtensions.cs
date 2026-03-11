using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Interfaces;
using InterviewSimulator.Scraping.Scrapers.Baeldung;
using InterviewSimulator.Scraping.Scrapers.CSharpCorner;
using InterviewSimulator.Scraping.Scrapers.DevTo;
using InterviewSimulator.Scraping.Scrapers.DigitalOcean;
using InterviewSimulator.Scraping.Scrapers.DotNetTricks;
using InterviewSimulator.Scraping.Scrapers.Edureka;
using InterviewSimulator.Scraping.Scrapers.FreeCodeCamp;
using InterviewSimulator.Scraping.Scrapers.FullStackCafe;
using InterviewSimulator.Scraping.Scrapers.GeeksForGeeks;
using InterviewSimulator.Scraping.Scrapers.Glassdoor;
using InterviewSimulator.Scraping.Scrapers.Indeed;
using InterviewSimulator.Scraping.Scrapers.InterviewBit;
using InterviewSimulator.Scraping.Scrapers.JavaTPoint;
using InterviewSimulator.Scraping.Scrapers.KnowledgeHut;
using InterviewSimulator.Scraping.Scrapers.LeetCode;
using InterviewSimulator.Scraping.Scrapers.MdnWebDocs;
using InterviewSimulator.Scraping.Scrapers.Medium;
using InterviewSimulator.Scraping.Scrapers.MicrosoftLearn;
using InterviewSimulator.Scraping.Scrapers.RefactoringGuru;
using InterviewSimulator.Scraping.Scrapers.Simplilearn;
using InterviewSimulator.Scraping.Scrapers.StackOverflow;
using InterviewSimulator.Scraping.Scrapers.TealHQ;
using InterviewSimulator.Scraping.Scrapers.Turing;
using InterviewSimulator.Scraping.Scrapers.W3Schools;
using InterviewSimulator.Scraping.Scrapers.KeepCoding;
using InterviewSimulator.Scraping.Scrapers.Platzi;
using InterviewSimulator.Scraping.Scrapers.OpenWebinars;
using InterviewSimulator.Scraping.Scrapers.Talently;
using InterviewSimulator.Scraping.Scrapers.Epitech;
using InterviewSimulator.Scraping.Scrapers.TheBridge;
using InterviewSimulator.Scraping.Scrapers.EPAMAnywhere;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace InterviewSimulator.Scraping.Scrapers.Extensions;

// Extensiones para registrar todos los scrapers en el contenedor de DI
public static class ScraperServiceExtensions
{
    // Registra todos los scrapers y sus HttpClients con políticas de resiliencia.
    public static IServiceCollection AddScrapers(this IServiceCollection services)
    {
        // Política de reintento con backoff exponencial
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        // ── Scrapers originales ──────────────────────────────────────────────
        services.AddHttpClient("DevTo", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("Medium", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("LeetCode", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("Glassdoor", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("Indeed", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("FreeCodeCamp", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        // ── Tier 1 — Alta densidad ───────────────────────────────────────────
        services.AddHttpClient("GeeksForGeeks", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("InterviewBit", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("FullStackCafe", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
            // GitHub API requiere User-Agent
            client.DefaultRequestHeaders.Add("User-Agent", "InterviewSimulator-Scraper/1.0");
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("JavaTPoint", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        // ── Tier 2 — Buena estructura ────────────────────────────────────────
        services.AddHttpClient("TealHQ", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("KnowledgeHut", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("Simplilearn", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("Edureka", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        // ── Tier 3 — Complementarias ─────────────────────────────────────────
        services.AddHttpClient("CSharpCorner", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("Baeldung", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("DotNetTricks", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        // ── Fuentes en español ───────────────────────────────────────────────
        services.AddHttpClient("Turing", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("KeepCoding", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("Platzi", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("OpenWebinars", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("Talently", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("Epitech", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("TheBridge", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("EPAMAnywhere", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        // ── Fuentes RAG — Documentación y conocimiento técnico ───────────────
        services.AddHttpClient("MicrosoftLearn", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("MdnWebDocs", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("W3Schools", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("RefactoringGuru", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("DigitalOcean", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        services.AddHttpClient("StackOverflow", (sp, client) =>
        {
            var s = sp.GetRequiredService<IOptions<ScrapingSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(s.RequestTimeoutSeconds);
        }).AddPolicyHandler(retryPolicy);

        // ── Registro de scrapers como IScraper ───────────────────────────────
        // Originales
        services.AddTransient<IScraper, DevToScraper>();
        services.AddTransient<IScraper, MediumScraper>();
        services.AddTransient<IScraper, LeetCodeScraper>();
        services.AddTransient<IScraper, GlassdoorScraper>();
        services.AddTransient<IScraper, IndeedScraper>();
        services.AddTransient<IScraper, FreeCodeCampScraper>();
        // Tier 1
        services.AddTransient<IScraper, GeeksForGeeksScraper>();
        services.AddTransient<IScraper, InterviewBitScraper>();
        services.AddTransient<IScraper, FullStackCafeScraper>();
        services.AddTransient<IScraper, JavaTPointScraper>();
        // Tier 2
        services.AddTransient<IScraper, TealHQScraper>();
        services.AddTransient<IScraper, KnowledgeHutScraper>();
        services.AddTransient<IScraper, SimplilearnScraper>();
        services.AddTransient<IScraper, EdurekaScraper>();
        // Tier 3
        services.AddTransient<IScraper, CSharpCornerScraper>();
        services.AddTransient<IScraper, BaeldungScraper>();
        services.AddTransient<IScraper, DotNetTricksScraper>();
        // Fuentes en español
        services.AddTransient<IScraper, TuringScraper>();
        services.AddTransient<IScraper, KeepCodingScraper>();
        services.AddTransient<IScraper, PlatziScraper>();
        services.AddTransient<IScraper, OpenWebinarsScraper>();
        services.AddTransient<IScraper, TalentlyScraper>();
        services.AddTransient<IScraper, EpitechScraper>();
        services.AddTransient<IScraper, TheBridgeScraper>();
        services.AddTransient<IScraper, EPAMAnywhereScraper>();
        // Fuentes RAG — Documentación y conocimiento técnico
        services.AddTransient<IScraper, MicrosoftLearnScraper>();
        services.AddTransient<IScraper, MdnWebDocsScraper>();
        services.AddTransient<IScraper, W3SchoolsScraper>();
        services.AddTransient<IScraper, RefactoringGuruScraper>();
        services.AddTransient<IScraper, DigitalOceanScraper>();
        services.AddTransient<IScraper, StackOverflowScraper>();

        return services;
    }
}
