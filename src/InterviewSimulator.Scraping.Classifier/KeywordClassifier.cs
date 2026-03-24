using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using InterviewSimulator.Scraping.Core.Interfaces;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace InterviewSimulator.Scraping.Classifier;

// Clasificador de preguntas de entrevista basado en keywords y patrones regex.
public class KeywordClassifier : IQuestionClassifier
{
    private readonly ILogger<KeywordClassifier> _logger;
    private readonly ClassificationRules _rules;

    public KeywordClassifier(ILogger<KeywordClassifier> logger)
    {
        _logger = logger;
        _rules = LoadRules();
    }

    // Clasifica una pregunta basándose en su texto usando keywords y patrones regex.
    public ClassificationResult Classify(string questionText)
    {
        if (string.IsNullOrWhiteSpace(questionText))
        {
            return new ClassificationResult
            {
                Category = QuestionCategory.Unknown,
                Difficulty = DifficultyLevel.Unknown,
                ConfidenceScore = 0
            };
        }

        var normalizedText = NormalizeText(questionText);
        var scores = new Dictionary<string, double>();

        // Calcular score por categoría
        foreach (var (categoryName, categoryRules) in _rules.Categories)
        {
            double score = 0;

            // Contar keywords encontrados
            foreach (var keyword in categoryRules.Keywords)
            {
                if (normalizedText.Contains(keyword.ToLowerInvariant()))
                {
                    score += 1;
                }
            }

            // Verificar patrones regex (valen 3 puntos cada uno)
            foreach (var pattern in categoryRules.Patterns)
            {
                try
                {
                    if (Regex.IsMatch(questionText, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                    {
                        score += 3;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    _logger.LogWarning("Timeout en regex pattern: {Pattern}", pattern);
                }
            }

            scores[categoryName] = score;
        }

        // La categoría con mayor score gana
        var bestCategory = scores.OrderByDescending(s => s.Value).First();
        var category = QuestionCategory.Unknown;
        double confidence = 0;

        if (bestCategory.Value >= 2) // Umbral mínimo
        {
            category = Enum.TryParse<QuestionCategory>(bestCategory.Key, out var parsed)
                ? parsed
                : QuestionCategory.Unknown;

            // Normalizar confianza (0-1)
            var totalScore = scores.Values.Sum();
            confidence = totalScore > 0 ? bestCategory.Value / totalScore : 0;
        }

        // Determinar subcategoría (solo para Technical)
        string? subcategory = null;
        var tags = new List<string>();

        if (category == QuestionCategory.Technical && _rules.Subcategories.TryGetValue("Technical", out var techSubcategories))
        {
            var subcategoryScores = new Dictionary<string, int>();

            foreach (var (subName, subKeywords) in techSubcategories)
            {
                int subScore = subKeywords.Count(kw => normalizedText.Contains(kw.ToLowerInvariant()));
                if (subScore > 0)
                {
                    subcategoryScores[subName] = subScore;
                    tags.AddRange(subKeywords.Where(kw => normalizedText.Contains(kw.ToLowerInvariant())));
                }
            }

            if (subcategoryScores.Count > 0)
            {
                subcategory = subcategoryScores.OrderByDescending(s => s.Value).First().Key;
            }
        }

        // Determinar dificultad
        var difficulty = DetermineDifficulty(normalizedText);

        // Determinar tecnología principal
        var technology = DetermineTechnology(normalizedText);

        return new ClassificationResult
        {
            Category = category,
            Subcategory = subcategory,
            Difficulty = difficulty,
            Tags = tags.Distinct().Take(10).ToList(),
            Technology = technology,
            ConfidenceScore = Math.Round(confidence, 2)
        };
    }

    private DifficultyLevel DetermineDifficulty(string normalizedText)
    {
        var difficultyScores = new Dictionary<DifficultyLevel, int>
        {
            { DifficultyLevel.Junior, 0 },
            { DifficultyLevel.Mid, 0 },
            { DifficultyLevel.Senior, 0 }
        };

        foreach (var (levelName, indicators) in _rules.DifficultyIndicators)
        {
            if (Enum.TryParse<DifficultyLevel>(levelName, out var level))
            {
                difficultyScores[level] = indicators.Count(ind => normalizedText.Contains(ind.ToLowerInvariant()));
            }
        }

        var best = difficultyScores.OrderByDescending(s => s.Value).First();
        return best.Value > 0 ? best.Key : DifficultyLevel.Unknown;
    }

    // ======================== FILTRO DE RELEVANCIA IT ========================

    /// <summary>
    /// Determina si una pregunta + respuesta es relevante para entrevistas de IT/desarrollo de software.
    /// Usa keywords de IT y una blacklist de temas no relacionados.
    /// </summary>
    public bool IsITRelevant(string questionText, string? answerText)
    {
        var combined = (" " + questionText + " " + (answerText ?? "") + " ").ToLowerInvariant();
        var questionLower = (" " + questionText + " ").ToLowerInvariant();

        // Primero: blacklist check — un solo hit fuerte es suficiente para rechazar
        int blacklistHits = _nonITBlacklist.Count(term => combined.Contains(term));
        int itHits = _itKeywords.Count(term => combined.Contains(term));

        // Si hay blacklist hits y no hay amplia mayoría IT, rechazar
        if (blacklistHits >= 1 && itHits < blacklistHits * 3)
            return false;

        // La pregunta misma (sin la respuesta) debe tener al menos 1 keyword IT
        int questionITHits = _itKeywords.Count(term => questionLower.Contains(term));
        if (questionITHits == 0)
            return false;

        // Necesita al menos 2 hits IT en el texto combinado
        if (itHits >= 2)
            return true;

        return false;
    }

    // Keywords que indican contenido de IT/desarrollo/programación/entrevista técnica
    private static readonly string[] _itKeywords = new[]
    {
        // Lenguajes de programación
        " python ", " java ", " javascript ", " typescript ", " c# ", " csharp ", " .net ",
        " golang ", " go ", " rust ", " php ", " ruby ", " swift ", " kotlin ", " scala ",
        " perl ", " lua ", " dart ", " elixir ", " haskell ", " cobol ",
        " html ", " css ", " sass ", " scss ",
        // Frameworks y librerías
        " react ", " angular ", " vue ", " svelte ", " next.js ", " nextjs ", " nuxt ",
        " django ", " flask ", " fastapi ", " spring ", " spring boot ", " springboot ",
        " express ", " node.js ", " nodejs ", " laravel ", " symfony ",
        " asp.net ", " blazor ", " rails ", " flutter ", " swiftui ",
        " langchain ", " tensorflow ", " pytorch ", " pandas ", " numpy ",
        " entity framework ", " hibernate ", " sequelize ", " prisma ",
        " bootstrap ", " tailwind ", " material ui ", " chakra ",
        " jquery ", " webpack ", " vite ", " babel ",
        // Conceptos de CS/programación
        " api ", " rest ", " restful ", " graphql ", " grpc ", " websocket ",
        " microservicio ", " microservice ", " microservicios ", " microservices ",
        " base de datos ", " database ", " bases de datos ",
        " sql ", " nosql ", " mongodb ", " postgresql ", " mysql ", " redis ",
        " elasticsearch ", " sqlite ", " oracle ", " mariadb ", " dynamodb ",
        " docker ", " kubernetes ", " k8s ", " contenedor ", " container ",
        " devops ", " ci/cd ", " ci cd ", " pipeline ",
        " cloud ", " aws ", " azure ", " gcp ", " serverless ", " lambda ",
        " git ", " github ", " gitlab ", " repositorio ",
        " algoritmo ", " algorithm ", " estructura de datos ", " data structure ",
        " programación ", " programming ", " código ", " code ", " software ",
        " frontend ", " backend ", " fullstack ", " full stack ", " full-stack ",
        " testing ", " unit test ", " tdd ", " qa ", " test unitario ",
        " orm ", " mvc ", " mvvm ", " solid ", " design pattern ", " patrón de diseño ",
        " herencia ", " inheritance ", " polimorfismo ", " polymorphism ",
        " abstracción ", " encapsulamiento ", " encapsulación ",
        " interfaz ", " interface ", " genérico ", " generic ",
        " array ", " arreglo ", " lista enlazada ", " linked list ",
        " pila ", " stack ", " cola ", " queue ", " hash ", " hashmap ",
        " árbol ", " tree ", " grafo ", " graph ", " heap ",
        " recursión ", " recursion ", " recursividad ",
        " compilador ", " compiler ", " intérprete ", " interpreter ",
        " máquina virtual ", " virtual machine ", " jvm ", " clr ",
        " sistema operativo ", " operating system ", " linux ", " unix ",
        " kernel ", " proceso ", " thread ", " hilo ", " deadlock ",
        " mutex ", " semáforo ", " semaphore ", " concurrencia ", " concurrency ",
        " asíncrono ", " async ", " await ", " promise ", " observable ",
        " red ", " network ", " tcp ", " http ", " https ", " dns ", " ip ",
        " socket ", " protocolo ", " protocol ",
        " seguridad ", " security ", " autenticación ", " authentication ",
        " autorización ", " authorization ", " oauth ", " jwt ", " token ",
        " cifrado ", " encryption ", " ssl ", " tls ",
        " machine learning ", " deep learning ", " inteligencia artificial ",
        " llm ", " chatgpt ", " gpt ", " modelo de lenguaje ", " nlp ",
        " neural network ", " red neuronal ", " data science ", " ciencia de datos ",
        " agile ", " scrum ", " kanban ",
        " deploy ", " despliegue ", " deployment ",
        " debug ", " depurar ", " debugging ", " depuración ",
        " refactoring ", " refactorizar ", " refactorización ",
        " arquitectura ", " architecture ", " diseño de sistemas ", " system design ",
        " servidor ", " server ", " cliente ", " client ",
        " desarrollo ", " developer ", " desarrollador ", " programador ",
        " framework ", " librería ", " library ", " paquete ", " package ",
        " terminal ", " consola ", " línea de comandos ", " command line ", " cli ",
        " json ", " xml ", " yaml ", " csv ", " protobuf ",
        " regex ", " expresión regular ",
        " ide ", " vscode ", " visual studio ", " intellij ", " eclipse ",
        " npm ", " pip ", " maven ", " gradle ", " nuget ", " cargo ", " composer ",
        " caché ", " cache ", " caching ",
        " hook ", " hooks ", " componente ", " component ", " módulo ", " module ",
        " endpoint ", " ruta ", " route ", " middleware ", " controlador ", " controller ",
        " sesión ", " session ", " cookie ",
        " responsive ", " spa ", " ssr ", " ssg ",
        " terraform ", " ansible ", " jenkins ", " github actions ",
        " monitoreo ", " monitoring ", " logging ", " observabilidad ",
        " rendimiento ", " performance ", " optimización ", " optimize ",
        " escalabilidad ", " scalability ", " balanceo de carga ", " load balancing ",
        " reverse proxy ", " nginx proxy ",
        " variable ", " función ", " function ", " método ", " method ",
        " clase ", " class ", " objeto ", " object ", " instancia ", " instance ",
        " constructor ", " destructor ", " getter ", " setter ", " propiedad ", " property ",
        " scope ", " closure ", " callback ", " evento ", " event ",
        " inyección de dependencias ", " dependency injection ",
        " singleton ", " factory ", " observer ", " strategy ", " adapter ",
        " modelo ", " model ", " vista ", " view ", " controlador ",
        " entrevista técnica ", " entrevista de programación ", " coding interview ",
        " leetcode ", " hackerrank ", " codewars ",
        " big o ", " complejidad ", " complexity ",
        " web scraping ", " scraping ", " crawler ",
        " blockchain ", " smart contract ", " solidity ",
        " iot ", " internet of things ", " embedded ",
        " astro ", " deno ", " bun ", " remix ", " gatsby ",
        " podman ", " containerd ", " helm ",
        " kibana ", " grafana ", " prometheus ",
        " rabbitmq ", " kafka ", " sqs ", " sns ",
        " s3 ", " ec2 ", " ecs ", " eks ", " fargate ",
        " cosmos db ", " firestore ", " firebase ",
        " xamarin ", " maui ", " wpf ", " winforms ",
        " swing ", " javafx ", " android ", " ios ",
        " react native ", " expo ", " ionic ", " capacitor ",
        " openai ", " api de openai ", " hugging face ",
        " pyqt ", " kivy ", " tkinter ",
        " pip install ", " npm install ", " dotnet ", " cargo build ",
        " pandas ", " scikit ", " matplotlib ", " seaborn ",
        " jupyter ", " notebook ", " colab ",
        " apache ", " nginx ", " iis ", " tomcat ",
        " clean code ", " código limpio ", " buenas prácticas ", " best practices ",
        " versión ", " version ", " versionado ", " versioning ",
        " branch ", " merge ", " pull request ", " commit ",
        " ciencia de la computación ", " computer science ",
        " ingeniería de software ", " software engineering ",
        " tipo de dato ", " data type ", " tipado ", " typing ",
        " compilación ", " compilation ", " runtime ", " tiempo de ejecución ",
        " binario ", " binary ", " bytecode ", " assembly ",
        " garbage collector ", " recolector de basura ", " memoria ", " memory ",
        " puntero ", " pointer ", " referencia ", " reference ",
        " crud ", " operaciones crud ",
        " tabla ", " table ", " columna ", " column ", " fila ", " row ",
        " índice ", " index ", " join ", " foreign key ", " primary key ",
        " normalización ", " normalization ", " transacción ", " transaction ",
        " stored procedure ", " trigger ", " view ",
        " backup ", " respaldo ", " restore ", " restaurar ",
        " script ", " automatización ", " automation "
    };

    // Términos que indican contenido NO relacionado con IT/desarrollo
    private static readonly string[] _nonITBlacklist = new[]
    {
        // Medicina / Salud
        " paciente ", " pacientes ", " clínico ", " clínica ", " clínicos ",
        " enfermería ", " enfermero ", " enfermera ", " enfermeros ",
        " nclex ", " presión arterial ", " diagnóstico médico ",
        " síntomas ", " síntoma ", " medicamento ", " medicación ",
        " farmacología ", " dosis ", " cirugía ", " quirúrgico ",
        " hospital ", " urgencias ", " emergencias ", " ambulancia ",
        " patología ", " fisiopatología ", " anatomía ",
        " electrocardiograma ", " ekg ", " ecg ", " mmhg ",
        " laboratorio clínico ", " hemoglobina ", " glucosa en sangre ",
        " insulina ", " diabetes ", " hipertensión ",
        " insuficiencia cardíaca ", " infarto ", " arritmia ",
        " cuidados de enfermería ", " cuidados paliativos ",
        " caso clínico ", " casos clínicos ",
        " furosemida ", " aspirina ", " ibuprofeno ",
        // Psicología / AutoAyuda
        " perdón ", " autocuidado ", " autoestima ",
        " meditación ", " mindfulness ", " terapia psicológica ",
        " psicología ", " psicológico ", " terapeuta ",
        " ansiedad ", " depresión ", " trauma ",
        // E-commerce / Shopping / Marketplaces
        " revendedor ", " revendedores ", " gangas ",
        " comprar barato ", " precio entre países ",
        " análisis de competidores ", " monitorear competidores ",
        " análisis competitivo ", " investigación de mercado ",
        " app store ", " google play ", " localización de apps ",
        " reseñas de apps ", " ratings de apps ",
        " anuncios ", " precios de productos ",
        // Física / Ciencia / Ingeniería no-IT
        " newton ", " m/s² ", " aceleración ", " fuerza ",
        " termodinámica ", " energía cinética ", " energía potencial ",
        " presión atmosférica ", " celsius ", " fahrenheit ",
        " biología ", " química ", " molécula ", " átomo ",
        // Nutrición / Cocina
        " receta de cocina ", " ingredientes ", " calorías ",
        " nutrición ", " dieta ", " alimentación ",
        // Deportes / Fitness
        " ejercicio físico ", " gimnasio ", " rutina de ejercicio ",
        // Legal / Contabilidad
        " abogado ", " jurídico ", " legislación ",
        " contabilidad ", " impuesto ", " factura ",
        // Embarazo / Bebés
        " embarazo ", " lactancia ", " pediátrico ",
        // Marketing / SEO (no-tech)
        " app store optimization ", " aso ",
        " 75% del mercado ", " 90% de las apps ",
        " lanzar una app ", " investiga el mercado ",
        " detectar a/b testing en screenshots "
    };

    // ======================== FIN FILTRO IT ========================

    private string? DetermineTechnology(string normalizedText)
    {
        if (_rules.TechnologyMappings.Count == 0)
            return null;

        var techScores = new Dictionary<string, int>();

        foreach (var (techName, keywords) in _rules.TechnologyMappings)
        {
            int score = keywords.Count(kw => normalizedText.Contains(kw.ToLowerInvariant()));
            if (score > 0)
            {
                techScores[techName] = score;
            }
        }

        if (techScores.Count == 0)
            return null;

        return techScores.OrderByDescending(s => s.Value).First().Key;
    }

    private static string NormalizeText(string text)
    {
        // Lowercase
        var normalized = text.ToLowerInvariant();

        // Remover acentos
        normalized = RemoveDiacritics(normalized);

        // Remover puntuación extra (mantener letras, numeros, espacios)
        normalized = Regex.Replace(normalized, @"[^\w\s\.\#\+]", " ");

        // Normalizar espacios
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private ClassificationRules LoadRules()
    {
        try
        {
            var assembly = typeof(KeywordClassifier).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("classification_rules.json"));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    return DeserializeRules(json);
                }
            }

            // Fallback: buscar el archivo en disco relativo al assembly
            var assemblyDir = Path.GetDirectoryName(assembly.Location) ?? ".";
            var filePath = Path.Combine(assemblyDir, "Resources", "classification_rules.json");

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return DeserializeRules(json);
            }

            _logger.LogWarning("No se encontró classification_rules.json, usando reglas vacías");
            return new ClassificationRules();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando classification_rules.json");
            return new ClassificationRules();
        }
    }

    private static ClassificationRules DeserializeRules(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var raw = JsonSerializer.Deserialize<RawClassificationRules>(json, options);

        if (raw == null) return new ClassificationRules();

        var rules = new ClassificationRules();

        if (raw.Categories != null)
        {
            foreach (var (name, cat) in raw.Categories)
            {
                rules.Categories[name] = new CategoryRule
                {
                    Keywords = cat.Keywords ?? new List<string>(),
                    Patterns = cat.Patterns ?? new List<string>()
                };
            }
        }

        if (raw.Subcategories != null)
        {
            foreach (var (name, subs) in raw.Subcategories)
            {
                rules.Subcategories[name] = new Dictionary<string, List<string>>();
                foreach (var (subName, keywords) in subs)
                {
                    rules.Subcategories[name][subName] = keywords ?? new List<string>();
                }
            }
        }

        if (raw.DifficultyIndicators != null)
        {
            foreach (var (level, indicators) in raw.DifficultyIndicators)
            {
                rules.DifficultyIndicators[level] = indicators ?? new List<string>();
            }
        }

        if (raw.TechnologyMappings != null)
        {
            foreach (var (tech, keywords) in raw.TechnologyMappings)
            {
                rules.TechnologyMappings[tech] = keywords ?? new List<string>();
            }
        }

        return rules;
    }

    #region Internal Models

    private class ClassificationRules
    {
        public Dictionary<string, CategoryRule> Categories { get; set; } = new();
        public Dictionary<string, Dictionary<string, List<string>>> Subcategories { get; set; } = new();
        public Dictionary<string, List<string>> DifficultyIndicators { get; set; } = new();
        public Dictionary<string, List<string>> TechnologyMappings { get; set; } = new();
    }

    private class CategoryRule
    {
        public List<string> Keywords { get; set; } = new();
        public List<string> Patterns { get; set; } = new();
    }

    // Raw deserialization models
    private class RawClassificationRules
    {
        public Dictionary<string, RawCategoryRule>? Categories { get; set; }
        public Dictionary<string, Dictionary<string, List<string>>>? Subcategories { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("difficulty_indicators")]
        public Dictionary<string, List<string>>? DifficultyIndicators { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("technology_mappings")]
        public Dictionary<string, List<string>>? TechnologyMappings { get; set; }
    }

    private class RawCategoryRule
    {
        public List<string>? Keywords { get; set; }
        public List<string>? Patterns { get; set; }
    }

    #endregion
}
