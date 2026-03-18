using System.Diagnostics;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace InterviewSimulator.Scraping.Scrapers.Glassdoor;

// Scraper para Glassdoor usando Playwright con configuración anti-detección.
// Glassdoor tiene anti-bot agresivo, este scraper es trata de sacar lo mas que pueda
// Incluye circuit breaker: si falla 3 veces consecutivas, se desactiva por 24 horas.
public class GlassdoorScraper : BaseScraper
{
    private static readonly string[] SearchUrls =
    {
        // Glassdoor en español (México/España)
        "https://www.glassdoor.com.mx/Entrevista/ingeniero-de-software-preguntas-de-entrevista-SRCH_KO0,22.htm",
        "https://www.glassdoor.com.mx/Entrevista/desarrollador-backend-preguntas-de-entrevista-SRCH_KO0,21.htm",
        "https://www.glassdoor.com.mx/Entrevista/desarrollador-full-stack-preguntas-de-entrevista-SRCH_KO0,24.htm",
        // Glassdoor en inglés
        "https://www.glassdoor.com/Interview/software-engineer-interview-questions-SRCH_KO0,17.htm",
        "https://www.glassdoor.com/Interview/backend-developer-interview-questions-SRCH_KO0,17.htm",
        "https://www.glassdoor.com/Interview/full-stack-developer-interview-questions-SRCH_KO0,20.htm",
        "https://www.glassdoor.com/Interview/devops-engineer-interview-questions-SRCH_KO0,15.htm",
        "https://www.glassdoor.com/Interview/data-engineer-interview-questions-SRCH_KO0,13.htm",
        "https://www.glassdoor.com/Interview/systems-engineer-interview-questions-SRCH_KO0,16.htm",
        "https://www.glassdoor.com/Interview/frontend-developer-interview-questions-SRCH_KO0,18.htm",
        "https://www.glassdoor.com/Interview/database-administrator-interview-questions-SRCH_KO0,22.htm",
        "https://www.glassdoor.com/Interview/cloud-engineer-interview-questions-SRCH_KO0,14.htm"
    };

    private static int _consecutiveFailures;
    private static DateTime? _disabledUntil;

    public override string SourceName => "Glassdoor";
    public override SourceType SourceType => SourceType.JobBoard;

    public GlassdoorScraper(
        ILogger<GlassdoorScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        // Circuit breaker: verificar si está desactivado
        if (_disabledUntil.HasValue && DateTime.UtcNow < _disabledUntil.Value)
        {
            Logger.LogWarning("[Glassdoor] Scraper desactivado por circuit breaker hasta {Until}",
                _disabledUntil.Value);
            result.Success = false;
            result.Errors.Add($"Circuit breaker activo hasta {_disabledUntil.Value:u}");
            result.Duration = sw.Elapsed;
            return result;
        }

        Logger.LogInformation("[Glassdoor] Iniciando scraping con Playwright (best effort)...");

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

            foreach (var url in SearchUrls)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    Logger.LogInformation("[Glassdoor] Navegando a: {Url}", url);
                    var page = await context.NewPageAsync();

                    await page.GotoAsync(url, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = Settings.RequestTimeoutSeconds * 1000
                    });

                    // Verificar si hay CAPTCHA o login requerido
                    var blocked = await page.EvaluateAsync<bool>(
                        @"() => {
                            const text = document.body.innerText.toLowerCase();
                            return text.includes('captcha') || 
                                   text.includes('sign in to continue') ||
                                   text.includes('unusual activity') ||
                                   text.includes('verify you are a human');
                        }");

                    if (blocked)
                    {
                        Logger.LogWarning("[Glassdoor] Bloqueado por CAPTCHA/login en: {Url}", url);
                        result.Errors.Add($"Bloqueado en {url}: CAPTCHA o login requerido");
                        await page.CloseAsync();
                        _consecutiveFailures++;
                        CheckCircuitBreaker();
                        continue;
                    }

                    // Extraer preguntas de entrevista
                    var questionsHtml = await page.EvaluateAsync<string[]>(
                        @"() => {
                            const questions = [];
                            // Intentar múltiples selectores
                            const selectors = [
                                '.interview-question',
                                'span[data-test=""interview-question""]',
                                'li.interview-question-text',
                                '.interviewQuestions span',
                                '[data-test=""InterviewQuestionText""]'
                            ];
                            for (const sel of selectors) {
                                document.querySelectorAll(sel).forEach(el => {
                                    const text = el.innerText?.trim();
                                    if (text && text.length > 15 && text.includes('?')) {
                                        questions.push(text);
                                    }
                                });
                            }
                            // Fallback: buscar en todo el contenido elementos con '?'
                            if (questions.length === 0) {
                                document.querySelectorAll('p, li, span, div').forEach(el => {
                                    const text = el.innerText?.trim();
                                    if (text && text.length > 20 && text.length < 500 && text.includes('?') && !text.includes('\n')) {
                                        questions.push(text);
                                    }
                                });
                            }
                            return [...new Set(questions)];
                        }");

                    if (questionsHtml != null && questionsHtml.Length > 0)
                    {
                        foreach (var questionText in questionsHtml)
                        {
                            if (IsValidQuestion(questionText))
                            {
                                var scrapedQuestion = CreateScrapedQuestion(
                                    questionText, url, null, sourceId: 0);
                                result.Questions.Add(scrapedQuestion);
                            }
                        }

                        Logger.LogInformation("[Glassdoor] {Count} preguntas extraídas de {Url}",
                            questionsHtml.Length, url);
                        _consecutiveFailures = 0; // Reset tras éxito
                    }

                    await page.CloseAsync();
                    await ApplyRateLimitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[Glassdoor] Error en URL: {Url}", url);
                    result.Errors.Add($"Error en {url}: {ex.Message}");
                    _consecutiveFailures++;
                    CheckCircuitBreaker();
                }
            }

            result.Success = true;
            result.TotalQuestionsFound = result.Questions.Count;
            Logger.LogInformation("[Glassdoor] Scraping completado. {Count} preguntas", result.TotalQuestionsFound);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Glassdoor] Error fatal");
            result.Success = false;
            result.Errors.Add($"Error fatal: {ex.Message}");
            _consecutiveFailures++;
            CheckCircuitBreaker();
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

    private void CheckCircuitBreaker()
    {
        if (_consecutiveFailures >= 3)
        {
            _disabledUntil = DateTime.UtcNow.AddHours(24);
            Logger.LogWarning("[Glassdoor] CIRCUIT BREAKER ACTIVADO. Desactivado hasta {Until}",
                _disabledUntil.Value);
        }
    }
}
