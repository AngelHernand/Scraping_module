using System.Diagnostics;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace InterviewSimulator.Scraping.Scrapers.Indeed;

// Scraper para Indeed career-advice usando Playwright.
// Indeed career-advice es contenido estático editorial.
public class IndeedScraper : BaseScraper
{
    private static readonly string[] ArticleUrls =
    {
        // Solo URLs en español — sitios de Indeed México y España
        "https://mx.indeed.com/orientacion-profesional/entrevistas/preguntas-entrevista-ingeniero-software",
        "https://mx.indeed.com/orientacion-profesional/entrevistas/preguntas-entrevista-desarrollador",
        "https://mx.indeed.com/orientacion-profesional/entrevistas/preguntas-frecuentes-entrevista-trabajo",
        "https://es.indeed.com/orientacion-profesional/entrevistas/preguntas-frecuentes-entrevista-trabajo",
        "https://es.indeed.com/orientacion-profesional/entrevistas/preguntas-entrevista-programador",
        "https://mx.indeed.com/orientacion-profesional/entrevistas/preguntas-entrevista-tecnica"
    };

    private const bool SpanishOnly = true;

    public override string SourceName => "Indeed";
    public override SourceType SourceType => SourceType.JobBoard;

    public IndeedScraper(
        ILogger<IndeedScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[Indeed] Iniciando scraping con Playwright...");

        IPlaywright? playwright = null;
        IBrowser? browser = null;

        try
        {
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = GetRandomUserAgent(),
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                Locale = "en-US",
                JavaScriptEnabled = true
            });

            foreach (var articleUrl in ArticleUrls)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    Logger.LogInformation("[Indeed] Navegando a: {Url}", articleUrl);
                    var page = await context.NewPageAsync();

                    await page.GotoAsync(articleUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = Settings.RequestTimeoutSeconds * 1000
                    });

                    // Extraer el contenido del artículo
                    var articleContent = await page.EvaluateAsync<string>(
                        @"() => {
                            const article = document.querySelector('article .article-content') ||
                                           document.querySelector('article') ||
                                           document.querySelector('.caas-body') ||
                                           document.querySelector('main');
                            return article ? article.innerHTML : document.body.innerHTML;
                        }");

                    if (string.IsNullOrWhiteSpace(articleContent))
                    {
                        Logger.LogWarning("[Indeed] No se encontró contenido en: {Url}", articleUrl);
                        await page.CloseAsync();
                        continue;
                    }

                    // Filtrar por idioma: solo español
                    if (SpanishOnly && !IsSpanishText(articleContent))
                    {
                        Logger.LogDebug("[Indeed] Artículo descartado (no es español): {Url}", articleUrl);
                        await page.CloseAsync();
                        continue;
                    }

                    // Extraer preguntas usando patrones HTML
                    var questionsFromJs = await page.EvaluateAsync<string[]>(
                        @"() => {
                            const questions = new Set();
                            const article = document.querySelector('article') || document.querySelector('main') || document.body;
                            
                            // H3 que son preguntas
                            article.querySelectorAll('h3, h2').forEach(h => {
                                const text = h.innerText?.trim();
                                if (text && text.includes('?') && text.length > 15) {
                                    questions.add(text);
                                }
                            });

                            // Lista ordenadas con preguntas
                            article.querySelectorAll('ol > li').forEach(li => {
                                const text = li.innerText?.trim();
                                if (text && text.length > 15 && text.length < 500) {
                                    // Tomar solo la primera línea si es muy largo
                                    const firstLine = text.split('\n')[0].trim();
                                    if (firstLine.includes('?') || firstLine.match(/^\d+[\.\)]/)) {
                                        questions.add(firstLine);
                                    }
                                }
                            });

                            // Strong/bold que son preguntas
                            article.querySelectorAll('strong, b').forEach(el => {
                                const text = el.innerText?.trim();
                                if (text && text.includes('?') && text.length > 15 && text.length < 500) {
                                    questions.add(text);
                                }
                            });

                            return [...questions];
                        }");

                    // Extraer Q+A con patrones del BaseScraper (solo con respuesta)
                    var qaPairs = ExtractQuestionsWithAnswersFromText(articleContent);

                    foreach (var (q, a) in qaPairs)
                    {
                        var existing = result.Questions.Any(x =>
                            x.QuestionTextNormalized == NormalizeQuestionText(q));
                        if (!existing)
                        {
                            var scrapedQuestion = CreateScrapedQuestion(q, articleUrl, articleContent, sourceId: 0, answerText: a);
                            result.Questions.Add(scrapedQuestion);
                        }
                    }

                    Logger.LogInformation("[Indeed] {Count} preguntas con respuesta extraídas de {Url}",
                        qaPairs.Count, articleUrl);

                    await page.CloseAsync();
                    await ApplyRateLimitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[Indeed] Error procesando artículo: {Url}", articleUrl);
                    result.Errors.Add($"Error en {articleUrl}: {ex.Message}");
                }
            }

            result.Success = true;
            result.TotalQuestionsFound = result.Questions.Count;
            Logger.LogInformation("[Indeed] Scraping completado. {Count} preguntas encontradas", result.TotalQuestionsFound);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Indeed] Error fatal durante scraping");
            result.Success = false;
            result.Errors.Add($"Error fatal: {ex.Message}");
        }
        finally
        {
            if (browser != null) await browser.CloseAsync();
            playwright?.Dispose();
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }
}
