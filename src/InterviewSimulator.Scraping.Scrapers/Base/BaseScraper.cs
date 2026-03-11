using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Interfaces;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.Base;

// Clase base abstracta para todos los scrapers. Provee funcionalidad común:
// rate limiting, user-agent rotation, normalización, hashing y parsing de preguntas.
public abstract class BaseScraper : IScraper
{
    protected readonly ILogger Logger;
    protected readonly ScrapingSettings Settings;
    protected readonly HttpClient HttpClient;
    private readonly Random _random = new();

    public abstract string SourceName { get; }
    public abstract SourceType SourceType { get; }

    protected BaseScraper(ILogger logger, IOptions<ScrapingSettings> settings, IHttpClientFactory httpClientFactory)
    {
        Logger = logger;
        Settings = settings.Value;
        HttpClient = httpClientFactory.CreateClient(SourceName);
    }

    public abstract Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default);

    // Aplica un delay aleatorio entre requests (jitter) para parecer tráfico humano.
    protected async Task ApplyRateLimitAsync(CancellationToken ct)
    {
        var delay = _random.Next(Settings.MinDelayBetweenRequestsMs, Settings.MaxDelayBetweenRequestsMs + 1);
        await Task.Delay(delay, ct);
    }

    // Retorna un User-Agent aleatorio de la lista configurada.
    protected string GetRandomUserAgent()
    {
        if (Settings.UserAgents.Count == 0)
            return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        return Settings.UserAgents[_random.Next(Settings.UserAgents.Count)];
    }

    // Normaliza un texto de pregunta: lowercase, sin puntuación extra, sin artículos.
    protected static string NormalizeQuestionText(string text)
    {
        var normalized = text.ToLowerInvariant().Trim();

        // Remover artículos comunes
        var articles = new[] { " the ", " a ", " an ", " el ", " la ", " un ", " una ", " los ", " las ", " unos ", " unas " };
        foreach (var article in articles)
        {
            normalized = normalized.Replace(article, " ");
        }

        // Remover puntuación
        normalized = Regex.Replace(normalized, @"[^\w\s]", "");

        // Normalizar espacios
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    // Calcula SHA-256 hash del texto normalizado.
    protected static string ComputeHash(string normalizedText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // Extrae preguntas de un texto en formato HTML o Markdown.
    // Busca patrones como listas numeradas, encabezados con interrogación, etc.
    protected List<string> ExtractQuestionsFromText(string text)
    {
        var questions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
            return questions.ToList();

        // Patron 1: Líneas que empiezan con número + punto
        var numberedPattern = new Regex(@"^\s*\d+[\.\)]\s*(.+\?)\s*$", RegexOptions.Multiline);
        foreach (Match match in numberedPattern.Matches(text))
        {
            var q = CleanQuestion(match.Groups[1].Value);
            if (IsValidQuestion(q)) questions.Add(q);
        }

        // Patro 2: Headers que son preguntas (Markdown)
        var headerPattern = new Regex(@"^#+\s*(.+\?)\s*$", RegexOptions.Multiline);
        foreach (Match match in headerPattern.Matches(text))
        {
            var q = CleanQuestion(match.Groups[1].Value);
            if (IsValidQuestion(q)) questions.Add(q);
        }

        // Patron 3: Líneas con signos de interrogación que contienen keywords técnicos
        var questionMarkPattern = new Regex(@"^(.{20,}?\?)\s*$", RegexOptions.Multiline);
        foreach (Match match in questionMarkPattern.Matches(text))
        {
            var q = CleanQuestion(match.Groups[1].Value);
            if (IsValidQuestion(q)) questions.Add(q);
        }

        // Patron 4: HTML list items con preguntas
        var liPattern = new Regex(@"<li[^>]*>\s*(.*?\?)\s*</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match match in liPattern.Matches(text))
        {
            var q = CleanQuestion(StripHtml(match.Groups[1].Value));
            if (IsValidQuestion(q)) questions.Add(q);
        }

        // Patron 5: Strong/Bold dentro del texto que sea pregunta
        var boldPattern = new Regex(@"<(?:strong|b)>(.*?\?)</(?:strong|b)>", RegexOptions.IgnoreCase);
        foreach (Match match in boldPattern.Matches(text))
        {
            var q = CleanQuestion(StripHtml(match.Groups[1].Value));
            if (IsValidQuestion(q)) questions.Add(q);
        }

        return questions.ToList();
    }

    /// <summary>
    /// Extrae pares pregunta-respuesta de un texto HTML o Markdown.
    /// Busca preguntas y captura el contenido que les sigue como respuesta.
    /// </summary>
    protected List<(string Question, string Answer)> ExtractQuestionsWithAnswersFromText(string text)
    {
        var results = new List<(string Question, string Answer)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
            return results;

        // Estrategia 1: Headers (H2/H3) con pregunta → contenido hasta el siguiente header
        var headerQaPattern = new Regex(
            @"<h[2-4][^>]*>\s*(.*?\?)\s*</h[2-4]>\s*(.*?)(?=<h[2-4][^>]*>|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match match in headerQaPattern.Matches(text))
        {
            var q = CleanQuestion(StripHtml(match.Groups[1].Value));
            var a = CleanAnswer(match.Groups[2].Value);
            if (IsValidQuestion(q) && a.Length >= 20 && seen.Add(q.ToLowerInvariant()))
            {
                results.Add((q, a));
            }
        }

        // Estrategia 2: Markdown headers con pregunta → contenido hasta el siguiente header
        var mdHeaderQaPattern = new Regex(
            @"^#{1,4}\s*(.+\?)\s*\n(.*?)(?=^#{1,4}\s|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        foreach (Match match in mdHeaderQaPattern.Matches(text))
        {
            var q = CleanQuestion(match.Groups[1].Value);
            var a = CleanAnswer(match.Groups[2].Value);
            if (IsValidQuestion(q) && a.Length >= 20 && seen.Add(q.ToLowerInvariant()))
            {
                results.Add((q, a));
            }
        }

        // Estrategia 3: <strong>/<b> pregunta seguida de párrafo/contenido
        var boldQaPattern = new Regex(
            @"<(?:strong|b)>\s*(.*?\?)\s*</(?:strong|b)>\s*(.*?)(?=<(?:strong|b)>|<h[2-4]|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match match in boldQaPattern.Matches(text))
        {
            var q = CleanQuestion(StripHtml(match.Groups[1].Value));
            var a = CleanAnswer(match.Groups[2].Value);
            if (IsValidQuestion(q) && a.Length >= 20 && seen.Add(q.ToLowerInvariant()))
            {
                results.Add((q, a));
            }
        }

        // Estrategia 4: Numbered list "1. pregunta?" seguido de contenido
        var numberedQaPattern = new Regex(
            @"^\s*\d+[\.\)]\s*(.+\?)\s*\n(.*?)(?=^\s*\d+[\.\)]\s|^#{1,4}\s|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        foreach (Match match in numberedQaPattern.Matches(text))
        {
            var q = CleanQuestion(match.Groups[1].Value);
            var a = CleanAnswer(match.Groups[2].Value);
            if (IsValidQuestion(q) && a.Length >= 20 && seen.Add(q.ToLowerInvariant()))
            {
                results.Add((q, a));
            }
        }

        return results;
    }

    /// <summary>
    /// Limpia el texto de una respuesta: remueve HTML, normaliza espacios, trunca a 4000 chars.
    /// Rechaza respuestas que contengan rúbricas de evaluación o estén en portugués.
    /// </summary>
    protected static string CleanAnswer(string rawAnswer)
    {
        if (string.IsNullOrWhiteSpace(rawAnswer)) return string.Empty;

        var cleaned = StripHtml(rawAnswer);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        // Rechazar respuestas con rúbricas de evaluación (Nível/Resposta/etc.)
        if (ContainsEvaluationRubric(cleaned)) return string.Empty;

        // Rechazar respuestas en portugués
        if (cleaned.Length > 30 && DetectLanguage(cleaned) == "pt") return string.Empty;

        // Truncar respuestas muy largas
        if (cleaned.Length > 4000)
            cleaned = cleaned[..4000];

        return cleaned;
    }

    // Determina si un texto extraído es una pregunta válida de entrevista.
    protected static bool IsValidQuestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.Length < 20) return false;   // Muy corta (era 15, ahora más estricto)
        if (text.Length > 500) return false;  // Probablemente es un párrafo, no una pregunta
        if (!text.Contains('?')) return false; // No es pregunta

        // Rechazar preguntas en portugués o con rúbricas de evaluación
        if (ContainsEvaluationRubric(text)) return false;
        if (DetectLanguage(text) == "pt") return false;

        // Rechazar si el signo ? aparece después del carácter 400 (párrafo con ? al final)
        int questionMarkPos = text.IndexOf('?');
        if (questionMarkPos > 400) return false;

        // Rechazar preguntas demasiado vagas / sin contexto (menos de 25 chars sin keywords IT)
        if (text.Length < 30)
        {
            // Preguntas muy cortas deben al menos tener algo específico
            var vaguePhrases = new[]
            {
                "¿por qué importa?", "¿y por qué importa?", "¿qué delegarías?",
                "¿cuándo reevaluarías?", "¿por qué?", "¿cómo funciona?",
                "¿y si ya todo", "¿qué versión", "¿por qué es",
                "¿qué me acerca", "¿qué quieres"
            };
            var lower = text.ToLowerInvariant();
            if (vaguePhrases.Any(v => lower.Contains(v))) return false;
        }

        // Rechazar textos que empiezan como párrafos narrativos (no son preguntas)
        var narrativeStarts = new[]
        {
            "paso 1:", "paso 2:", "paso 3:", "componentes del", "la solución:",
            "el problema:", "descubrimiento avanzado", "navegación bidireccional",
            "filosofía:", "bonus:", "pruébalo", "preguntas frecuentes",
            "mi contexto", "acerca de mí", "introducción"
        };
        // Remover emojis comunes del inicio antes de evaluar
        var textLower = Regex.Replace(text.ToLowerInvariant(), @"^[\s\p{So}\p{Cs}]+", "");
        if (narrativeStarts.Any(ns => textLower.StartsWith(ns))) return false;

        return true;
    }

    // Limpia una pregunta removiendo HTML, espacios extra, etc.
    protected static string CleanQuestion(string text)
    {
        var cleaned = StripHtml(text);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        // Remover números al inicio (e.g., "1. ", "42) ")
        cleaned = Regex.Replace(cleaned, @"^\d+[\.\)]\s*", "");
        return cleaned;
    }

    // Remueve todas las etiquetas HTML de un texto
    protected static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        return Regex.Replace(html, @"<[^>]+>", "").Trim();
    }

    // Detecta el idioma predominante del texto (en, es, o pt).
    protected static string DetectLanguage(string text)
    {
        var lower = " " + text.ToLowerInvariant() + " ";

        // --- Portugués: palabras exclusivas que NO existen en español ---
        var portugueseWords = new[]
        {
            " não ", " são ", " você ", " vocês ", " isso ", " isto ",
            " aqui ", " aquilo ", " esse ", " essa ", " esses ", " essas ",
            " nível ", " resposta ", " indicadores ", " dedução ",
            " inadequada ", " intermediária ", " avançada ",
            " também ", " então ", " além ", " através ",
            " será ", " foram ", " seria ", " está ",
            " ainda ", " onde ", " quando ", " como ",
            " muito ", " mais ", " bem ", " bom ",
            " fazer ", " pode ", " deve ", " tem ", " há ",
            " já ", " sem ", " nos ", " das ", " dos ",
            " uma ", " umas ", " uns ", " pela ", " pelo ",
            " qual ", " quais ", " como ", " são ",
            " porque ", " porém ", " contudo ", " todavia ",
            " explicar ", " implementar ", " utilizar ",
            " goroutine ", " goroutines ",
            " compreender ", " desenvolvimento ", " aplicação ",
            " configuração ", " implementação ", " manutenção ",
            " funcionalidade ", " desempenho ", " segurança ",
            " gerenciamento ", " conhecimento ", " experiência ",
            " ção ", " ções ", " ência ", " ância "
        };

        // Palabras/patrones exclusivamente en portugués (NO en español)
        var portugueseExclusive = new[]
        {
            " não ", " você ", " vocês ", " isso ",
            " esse ", " essa ", " esses ", " foram ",
            " nível ", " resposta ", " dedução ",
            " inadequada ", " avançada ", " intermediária ",
            " também ", " então ", " além ", " através ",
            " ainda ", " muito ", " fazer ", " bem ",
            " já ", " pelo ", " pela ", " dos ", " das ",
            " porém ", " contudo ", " todavia ",
            " compreender ", " implementação ", " manutenção ",
            " gerenciamento ", " conhecimento "
        };

        int portugueseScore = portugueseExclusive.Count(w => lower.Contains(w));

        // Caracteres exclusivos del portugués (ã, õ) que NO existen en español
        int ptCharCount = lower.Count(c => c == 'ã' || c == 'õ');
        portugueseScore += ptCharCount * 3;

        // Terminaciones propias del portugués
        if (Regex.IsMatch(lower, @"\bção\b|ções\b|ência\b|ância\b")) portugueseScore += 3;

        // Si el score de portugués es significativo, es portugués
        if (portugueseScore >= 3)
            return "pt";

        // --- Español vs Inglés ---
        var spanishWords = new[]
        {
            " que ", " qué ", " cómo ", " cuál ", " cuáles ", " por qué ",
            " dónde ", " háblame ", " explica ", " menciona ", " también ",
            " pero ", " porque ", " cuando ", " como ", " para ", " entre ",
            " sobre ", " desde ", " hasta ", " según ", " puede ", " pueden ",
            " tiene ", " tienen ", " está ", " están ", " este ", " esta ",
            " estos ", " estas ", " cada ", " más ", " sin ", " todo ",
            " cual ", " hay ", " ser ", " muy ", " con el ", " con la ",
            " del ", " las ", " los ", " una ", " uno ", " usted ",
            " ejemplo ", " función ", " método ", " clase ", " variable ",
            " diferencia ", " ventaja ", " desventaja ", " siguiente ",
            " resultado ", " código ", " desarrollo ", " aplicación ",
            " respuesta ", " pregunta ", " entrevista ", " programación ",
            " es ", " en ", " al ", " o ", " y ", " la ", " el ",
            " con ", " se ", " tu ", " tus ", " su ", " sus "
        };

        var englishWords = new[]
        {
            " the ", " is ", " are ", " was ", " were ", " have ", " has ",
            " been ", " being ", " will ", " would ", " could ", " should ",
            " this ", " that ", " these ", " those ", " which ", " what ",
            " when ", " where ", " how ", " does ", " do ", " did ",
            " not ", " with ", " from ", " they ", " them ", " their ",
            " you ", " your ", " we ", " our ", " an ", " a ",
            " for ", " and ", " or ", " of ", " to ", " in ", " on "
        };

        int spanishScore = spanishWords.Count(w => lower.Contains(w));
        int englishScore = englishWords.Count(w => lower.Contains(w));

        // Si hay acentos típicos del español (á,é,í,ó,ú,ñ,¿,¡) bonus
        var spanishChars = new[] { 'á', 'é', 'í', 'ó', 'ú', 'ñ', '¿', '¡' };
        int accentCount = spanishChars.Sum(c => lower.Count(ch => ch == c));
        spanishScore += accentCount * 2;

        return spanishScore > englishScore ? "es" : "en";
    }

    /// <summary>
    /// Determina si un texto está predominantemente en español (y NO portugués).
    /// </summary>
    protected static bool IsSpanishText(string text)
    {
        var lang = DetectLanguage(text);
        return lang == "es";
    }

    /// <summary>
    /// Detecta si el texto contiene rúbricas de evaluación (Nível/Resposta/Indicadores/Dedução)
    /// que no son contenido útil de preguntas de entrevista.
    /// </summary>
    private static bool ContainsEvaluationRubric(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();

        var rubricMarkers = new[]
        {
            "nível", "resposta", "indicadores", "dedução",
            "inadequada", "intermediária", "avançada",
            "júnior confirmado", "pleno potencial", "pleno+",
            "❌", "✓✓", "→ p7", "→ p8", "→ p9"
        };

        int matchCount = rubricMarkers.Count(m => lower.Contains(m));
        return matchCount >= 2; // Si tiene 2+ marcadores, es rúbrica
    }

    /// Crea un ScrapedQuestion con los campos base pre-poblados.
    protected ScrapedQuestion CreateScrapedQuestion(string questionText, string? sourceUrl, string? rawContent, int sourceId, string? answerText = null)
    {
        var normalized = NormalizeQuestionText(questionText);
        var hash = ComputeHash(normalized);
        var language = DetectLanguage(questionText);

        return new ScrapedQuestion
        {
            SourceId = sourceId,
            QuestionText = questionText.Length > 2000 ? questionText[..2000] : questionText,
            QuestionTextNormalized = normalized.Length > 2000 ? normalized[..2000] : normalized,
            HashFingerprint = hash,
            OriginalLanguage = language,
            SourceUrl = sourceUrl,
            RawContent = rawContent,
            AnswerText = answerText,
            ScrapedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  SOPORTE RAG: Extracción de documentos y chunking
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Extrae contenido limpio de un nodo HTML, conservando bloques de código como ```.
    /// Elimina navegación, ads, footers, scripts, etc.
    /// </summary>
    protected static string ExtractCleanContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        // Preservar bloques <pre><code> como ``` markdown
        var content = Regex.Replace(html,
            @"<pre[^>]*>\s*<code[^>]*>(.*?)</code>\s*</pre>",
            m => "\n```\n" + StripHtml(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).Trim() + "\n```\n",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Preservar <code> inline
        content = Regex.Replace(content, @"<code[^>]*>(.*?)</code>",
            m => "`" + StripHtml(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).Trim() + "`",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Convertir headers a markdown
        content = Regex.Replace(content, @"<h1[^>]*>(.*?)</h1>",
            m => "\n# " + StripHtml(m.Groups[1].Value).Trim() + "\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        content = Regex.Replace(content, @"<h2[^>]*>(.*?)</h2>",
            m => "\n## " + StripHtml(m.Groups[1].Value).Trim() + "\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        content = Regex.Replace(content, @"<h3[^>]*>(.*?)</h3>",
            m => "\n### " + StripHtml(m.Groups[1].Value).Trim() + "\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        content = Regex.Replace(content, @"<h[4-6][^>]*>(.*?)</h[4-6]>",
            m => "\n#### " + StripHtml(m.Groups[1].Value).Trim() + "\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Convertir listas
        content = Regex.Replace(content, @"<li[^>]*>(.*?)</li>",
            m => "\n- " + StripHtml(m.Groups[1].Value).Trim(), RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Convertir párrafos
        content = Regex.Replace(content, @"<p[^>]*>(.*?)</p>",
            m => "\n" + StripHtml(m.Groups[1].Value).Trim() + "\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Convertir <br> a newline
        content = Regex.Replace(content, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

        // Convertir strong/b a markdown bold
        content = Regex.Replace(content, @"<(?:strong|b)[^>]*>(.*?)</(?:strong|b)>",
            m => "**" + StripHtml(m.Groups[1].Value).Trim() + "**", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Convertir em/i a markdown italic
        content = Regex.Replace(content, @"<(?:em|i)[^>]*>(.*?)</(?:em|i)>",
            m => "*" + StripHtml(m.Groups[1].Value).Trim() + "*", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Eliminar todas las etiquetas HTML restantes
        content = Regex.Replace(content, @"<[^>]+>", "");

        // HTML decode
        content = System.Net.WebUtility.HtmlDecode(content);

        // Normalizar líneas en blanco (máximo 2 seguidas)
        content = Regex.Replace(content, @"\n{3,}", "\n\n");

        // Normalizar espacios en cada línea
        var lines = content.Split('\n').Select(l => l.TrimEnd());
        content = string.Join("\n", lines).Trim();

        return content;
    }

    /// <summary>
    /// Divide un texto largo en chunks de 500-1500 palabras, cada uno sobre un concepto coherente.
    /// Intenta dividir en headers (## o ###) primero, luego por párrafos si es necesario.
    /// </summary>
    protected static List<(string Title, string Content)> ChunkContent(string content, string defaultTitle, int minWords = 500, int maxWords = 1500)
    {
        var chunks = new List<(string Title, string Content)>();

        if (string.IsNullOrWhiteSpace(content))
            return chunks;

        var wordCount = CountWords(content);

        // Si el contenido es lo suficientemente pequeño, devolverlo completo
        if (wordCount <= maxWords)
        {
            chunks.Add((defaultTitle, content.Trim()));
            return chunks;
        }

        // Intentar dividir por headers (## o ###)
        var headerPattern = new Regex(@"^(#{1,3})\s+(.+)$", RegexOptions.Multiline);
        var matches = headerPattern.Matches(content);

        if (matches.Count >= 2)
        {
            // Dividir por secciones de headers
            var sections = new List<(string Title, string Content)>();

            for (int i = 0; i < matches.Count; i++)
            {
                var sectionTitle = matches[i].Groups[2].Value.Trim();
                var start = matches[i].Index;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
                var sectionContent = content[start..end].Trim();

                sections.Add((sectionTitle, sectionContent));
            }

            // El texto antes del primer header
            if (matches[0].Index > 0)
            {
                var preamble = content[..matches[0].Index].Trim();
                if (CountWords(preamble) >= 50)
                {
                    sections.Insert(0, (defaultTitle + " - Introducción", preamble));
                }
            }

            // Merge secciones pequeñas y split secciones grandes
            var currentChunk = "";
            var currentTitle = defaultTitle;

            foreach (var (sTitle, sContent) in sections)
            {
                var sWords = CountWords(sContent);

                if (sWords > maxWords)
                {
                    // Flush lo que tengamos
                    if (!string.IsNullOrWhiteSpace(currentChunk) && CountWords(currentChunk) >= minWords / 2)
                    {
                        chunks.Add((currentTitle, currentChunk.Trim()));
                        currentChunk = "";
                    }

                    // Dividir sección grande por párrafos
                    var subChunks = SplitByParagraphs(sContent, sTitle, minWords, maxWords);
                    chunks.AddRange(subChunks);
                    currentTitle = sTitle;
                }
                else if (CountWords(currentChunk) + sWords > maxWords)
                {
                    // Flush y empezar nuevo
                    if (!string.IsNullOrWhiteSpace(currentChunk) && CountWords(currentChunk) >= minWords / 2)
                    {
                        chunks.Add((currentTitle, currentChunk.Trim()));
                    }
                    currentChunk = sContent;
                    currentTitle = sTitle;
                }
                else
                {
                    // Acumular
                    if (string.IsNullOrWhiteSpace(currentChunk))
                        currentTitle = sTitle;
                    currentChunk += "\n\n" + sContent;
                }
            }

            // Flush el último chunk
            if (!string.IsNullOrWhiteSpace(currentChunk) && CountWords(currentChunk) >= minWords / 3)
            {
                chunks.Add((currentTitle, currentChunk.Trim()));
            }
        }
        else
        {
            // Sin headers: dividir por párrafos
            chunks.AddRange(SplitByParagraphs(content, defaultTitle, minWords, maxWords));
        }

        // Si no hubo chunks, devolver completo
        if (chunks.Count == 0)
        {
            chunks.Add((defaultTitle, content.Trim()));
        }

        return chunks;
    }

    private static List<(string Title, string Content)> SplitByParagraphs(string content, string title, int minWords, int maxWords)
    {
        var chunks = new List<(string Title, string Content)>();
        var paragraphs = Regex.Split(content, @"\n\n+");
        var currentChunk = "";
        int chunkIdx = 0;

        foreach (var para in paragraphs)
        {
            var paraWords = CountWords(para);

            if (CountWords(currentChunk) + paraWords > maxWords && CountWords(currentChunk) >= minWords)
            {
                chunkIdx++;
                chunks.Add(($"{title} (parte {chunkIdx})", currentChunk.Trim()));
                currentChunk = para;
            }
            else
            {
                currentChunk += "\n\n" + para;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentChunk) && CountWords(currentChunk) >= minWords / 3)
        {
            chunkIdx++;
            var chunkTitle = chunkIdx > 1 ? $"{title} (parte {chunkIdx})" : title;
            chunks.Add((chunkTitle, currentChunk.Trim()));
        }

        return chunks;
    }

    /// <summary>
    /// Cuenta palabras en un texto.
    /// </summary>
    protected static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Normaliza el contenido de un documento (para deduplicación).
    /// Más permisivo que la normalización de preguntas: conserva más contexto.
    /// </summary>
    protected static string NormalizeDocumentContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;

        var normalized = content.ToLowerInvariant().Trim();

        // Remover bloques de código (no son relevantes para deduplicación)
        normalized = Regex.Replace(normalized, @"```[\s\S]*?```", "[code]");
        normalized = Regex.Replace(normalized, @"`[^`]+`", "[code]");

        // Remover puntuación
        normalized = Regex.Replace(normalized, @"[^\w\s]", "");

        // Normalizar espacios
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        // Tomar solo los primeros 500 chars para el hash (eficiencia)
        if (normalized.Length > 500)
            normalized = normalized[..500];

        return normalized;
    }

    /// <summary>
    /// Crea un ScrapedDocument con campos base pre-poblados.
    /// </summary>
    protected ScrapedDocument CreateScrapedDocument(
        string title, string content, string sourceUrl, string sourceSite,
        int sourceId, string? technology = null, ContentType contentType = ContentType.Article)
    {
        var normalized = NormalizeDocumentContent(content);
        var hash = ComputeHash(normalized);
        var language = DetectLanguage(content.Length > 300 ? content[..300] : content);

        return new ScrapedDocument
        {
            SourceId = sourceId,
            Title = title.Length > 500 ? title[..500] : title,
            Content = content,
            ContentNormalized = normalized,
            HashFingerprint = hash,
            Language = language,
            SourceUrl = sourceUrl,
            SourceSite = sourceSite,
            ContentType = contentType,
            Technology = technology,
            WordCount = CountWords(content),
            ScrapedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    /// <summary>
    /// Extrae y chunkea contenido HTML en documentos RAG listos para persistir.
    /// </summary>
    protected List<ScrapedDocument> ExtractDocumentsFromHtml(
        string html, string pageTitle, string sourceUrl, string sourceSite,
        int sourceId, string? technology = null, ContentType contentType = ContentType.Article)
    {
        var documents = new List<ScrapedDocument>();

        var cleanContent = ExtractCleanContent(html);
        if (string.IsNullOrWhiteSpace(cleanContent) || CountWords(cleanContent) < 50)
            return documents;

        var chunks = ChunkContent(cleanContent, pageTitle);

        for (int i = 0; i < chunks.Count; i++)
        {
            var (chunkTitle, chunkContent) = chunks[i];
            var doc = CreateScrapedDocument(chunkTitle, chunkContent, sourceUrl, sourceSite, sourceId, technology, contentType);
            doc.ChunkIndex = i;

            documents.Add(doc);
        }

        return documents;
    }
}
