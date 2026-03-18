using System.Diagnostics;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace InterviewSimulator.Scraping.Scrapers.Medium;

/// Scraper para Medium usando Playwright (headless browser).
/// Medium no tiene API pública gratuita.
public class MediumScraper : BaseScraper
{
    private static readonly string[] TagUrls =
    {
        // Tags en español
        "https://medium.com/tag/entrevista-tecnica",
        "https://medium.com/tag/preguntas-entrevista",
        "https://medium.com/tag/programacion",
        "https://medium.com/tag/desarrollo-web",
        "https://medium.com/tag/desarrollo-de-software",
        // Tags en inglés
        "https://medium.com/tag/interview-questions",
        "https://medium.com/tag/coding-interviews",
        "https://medium.com/tag/software-engineering",
        "https://medium.com/tag/programming",
        "https://medium.com/tag/software-development",
        "https://medium.com/tag/web-development"
    };

    public override string SourceName => "Medium";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public MediumScraper(
        ILogger<MediumScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();
        var scraperSettings = Settings.Scrapers.GetValueOrDefault("Medium");
        var maxPages = scraperSettings?.MaxPages ?? 5;

        Logger.LogInformation("[Medium] Iniciando scraping con Playwright...");

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
                TimezoneId = "America/New_York",
                JavaScriptEnabled = true
            });

            foreach (var tagUrl in TagUrls)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    Logger.LogInformation("[Medium] Navegando a: {Url}", tagUrl);
                    var page = await context.NewPageAsync();

                    await page.GotoAsync(tagUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = Settings.RequestTimeoutSeconds * 1000
                    });

                    // Scroll para cargar más artículos (infinite scroll)
                    for (int i = 0; i < maxPages; i++)
                    {
                        await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
                        await Task.Delay(2000, cancellationToken);
                    }

                    // Extraer links de artículos
                    var articleLinks = await page.EvaluateAsync<string[]>(
                        @"() => {
                            const links = new Set();
                            document.querySelectorAll('article a[href], a[data-testid=""postPreview-title""]').forEach(a => {
                                const href = a.href;
                                if (href && href.includes('medium.com') && !href.includes('/tag/')) {
                                    links.add(href);
                                }
                            });
                            return [...links];
                        }");

                    Logger.LogInformation("[Medium] Encontrados {Count} links de artículos en {Url}",
                        articleLinks?.Length ?? 0, tagUrl);

                    if (articleLinks == null) continue;

                    foreach (var articleUrl in articleLinks.Take(maxPages * 5))
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        try
                        {
                            await ApplyRateLimitAsync(cancellationToken);
                            var articlePage = await context.NewPageAsync();

                            await articlePage.GotoAsync(articleUrl, new PageGotoOptions
                            {
                                WaitUntil = WaitUntilState.DOMContentLoaded,
                                Timeout = Settings.RequestTimeoutSeconds * 1000
                            });

                            // Detectar paywall
                            var hasPaywall = await articlePage.EvaluateAsync<bool>(
                                @"() => {
                                    const paywallDiv = document.querySelector('div[data-testid=""paywall""]');
                                    const memberOnly = document.body.innerText.includes('Member-only story');
                                    const shortContent = (document.querySelector('article')?.innerText?.length || 0) < 500;
                                    return !!(paywallDiv || memberOnly || shortContent);
                                }");

                            if (hasPaywall)
                            {
                                Logger.LogDebug("[Medium] Artículo bloqueado por paywall: {Url}", articleUrl);
                                await articlePage.CloseAsync();
                                continue;
                            }

                            // Extraer contenido del artículo
                            var content = await articlePage.EvaluateAsync<string>(
                                @"() => {
                                    const article = document.querySelector('article');
                                    return article ? article.innerHTML : document.body.innerHTML;
                                }");

                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                // Extraer solo Q+A (preguntas con respuesta)
                                var qaPairs = ExtractQuestionsWithAnswersFromText(content);

                                foreach (var (questionText, answerText) in qaPairs)
                                {
                                    var scrapedQuestion = CreateScrapedQuestion(
                                        questionText, articleUrl, content,
                                        sourceId: 0, answerText: answerText);
                                    result.Questions.Add(scrapedQuestion);
                                }

                                Logger.LogDebug("[Medium] Artículo '{Url}': {QACount} preguntas con respuesta",
                                    articleUrl, qaPairs.Count);
                            }

                            await articlePage.CloseAsync();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning(ex, "[Medium] Error procesando artículo: {Url}", articleUrl);
                            result.Errors.Add($"Error en artículo {articleUrl}: {ex.Message}");
                        }
                    }

                    await page.CloseAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[Medium] Error procesando tag: {Url}", tagUrl);
                    result.Errors.Add($"Error en tag {tagUrl}: {ex.Message}");
                }
            }

            result.Success = true;
            result.TotalQuestionsFound = result.Questions.Count;
            Logger.LogInformation("[Medium] Scraping completado. {Count} preguntas encontradas", result.TotalQuestionsFound);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Medium] Error fatal durante scraping");
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
