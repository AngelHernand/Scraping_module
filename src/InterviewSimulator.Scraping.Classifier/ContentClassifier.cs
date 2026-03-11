using System.Text.RegularExpressions;
using InterviewSimulator.Scraping.Core.Interfaces;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace InterviewSimulator.Scraping.Classifier;

/// <summary>
/// Clasificador de documentos de conocimiento técnico para el corpus RAG.
/// Usa keywords, patrones regex y heurísticas para determinar categoría,
/// tipo de contenido, dificultad, tecnología y tags.
/// </summary>
public class ContentClassifier : IContentClassifier
{
    private readonly ILogger<ContentClassifier> _logger;

    // ══════════════════════════════════════════════════════════════
    //  Mapeo de keywords → ContentCategory
    // ══════════════════════════════════════════════════════════════
    private static readonly Dictionary<ContentCategory, string[]> CategoryKeywords = new()
    {
        // Lenguajes
        [ContentCategory.Java] = ["java", "jvm", "jdk", "javac", "maven", "gradle", "openjdk", "java se", "jakarta"],
        [ContentCategory.Python] = ["python", "pip", "virtualenv", "cpython", "pypi", "pep8", "pep 8", "jupyter", "conda"],
        [ContentCategory.JavaScript] = ["javascript", "ecmascript", "es6", "es2015", "es2020", "es2023", "npm", "yarn", "v8 engine"],
        [ContentCategory.TypeScript] = ["typescript", "tsc", "tsconfig", "ts-node", ".ts file"],
        [ContentCategory.CSharp] = ["c#", "csharp", "c sharp", "roslyn", "nuget", ".net", "dotnet"],
        [ContentCategory.Go] = ["golang", "go language", "goroutine", "goroutines", "go mod", "go build"],
        [ContentCategory.Rust] = ["rust lang", "rustc", "cargo", "rust programming", "borrow checker", "ownership rust"],
        [ContentCategory.PHP] = ["php", "composer", "laravel", "symfony", "wordpress", "php-fpm"],
        [ContentCategory.Ruby] = ["ruby", "rails", "sinatra", "ruby on rails", "rubygems", "bundler"],
        [ContentCategory.Swift] = ["swift", "swiftui", "xcode", "swift programming", "ios development"],
        [ContentCategory.Kotlin] = ["kotlin", "jetbrains kotlin", "kotlin coroutines", "android kotlin"],
        [ContentCategory.C] = ["c programming", "c language", "gcc", "clang", "ansi c", "c standard"],
        [ContentCategory.Cpp] = ["c++", "cpp", "stl", "boost library", "c++ standard"],

        // Frameworks
        [ContentCategory.React] = ["react", "reactjs", "react.js", "jsx", "react hooks", "react router", "redux", "next.js", "nextjs"],
        [ContentCategory.Angular] = ["angular", "angularjs", "angular cli", "rxjs", "ngrx"],
        [ContentCategory.VueJs] = ["vue", "vuejs", "vue.js", "vuex", "nuxt", "nuxtjs", "pinia"],
        [ContentCategory.NextJs] = ["next.js", "nextjs", "next js", "vercel"],
        [ContentCategory.NodeJs] = ["node.js", "nodejs", "node js", "express.js", "expressjs", "deno", "bun runtime"],
        [ContentCategory.ExpressJs] = ["express.js", "expressjs", "express js", "express middleware"],
        [ContentCategory.Django] = ["django", "django rest", "django orm", "djangorestframework"],
        [ContentCategory.Flask] = ["flask", "flask api", "jinja2", "werkzeug"],
        [ContentCategory.SpringBoot] = ["spring boot", "spring framework", "spring mvc", "spring security", "spring data", "spring cloud"],
        [ContentCategory.AspNetCore] = ["asp.net core", "aspnetcore", "asp.net", "blazor", "razor pages", "signalr"],
        [ContentCategory.DotNet] = [".net core", ".net framework", "dotnet", ".net 8", ".net 7", ".net 6", "maui", "wpf", "winforms"],
        [ContentCategory.EntityFramework] = ["entity framework", "ef core", "dbcontext", "entityframework", "migrations ef"],
        [ContentCategory.Laravel] = ["laravel", "artisan", "eloquent", "blade template"],
        [ContentCategory.Rails] = ["rails", "ruby on rails", "activerecord", "actioncontroller"],
        [ContentCategory.FastApi] = ["fastapi", "fast api", "starlette", "uvicorn"],

        // Bases de datos
        [ContentCategory.Sql] = ["sql", "structured query", "relational database", "join", "select from", "group by", "stored procedure"],
        [ContentCategory.MySql] = ["mysql", "mariadb", "innodb", "mysql workbench"],
        [ContentCategory.PostgreSql] = ["postgresql", "postgres", "psql", "pgadmin", "postgis"],
        [ContentCategory.MongoDb] = ["mongodb", "mongoose", "nosql", "mongo shell", "mongosh", "bson"],
        [ContentCategory.Redis] = ["redis", "redis cache", "redis pub/sub", "redis sentinel", "redis cluster"],
        [ContentCategory.ElasticSearch] = ["elasticsearch", "elastic search", "kibana", "logstash", "elk stack", "opensearch"],
        [ContentCategory.SqlServer] = ["sql server", "mssql", "t-sql", "ssms", "sqlserver"],
        [ContentCategory.SQLite] = ["sqlite", "sqlite3"],

        // DevOps
        [ContentCategory.Docker] = ["docker", "dockerfile", "docker-compose", "docker compose", "container", "docker hub"],
        [ContentCategory.Kubernetes] = ["kubernetes", "k8s", "kubectl", "helm", "pod", "deployment yaml", "minikube"],
        [ContentCategory.Aws] = ["aws", "amazon web services", "s3", "ec2", "lambda", "dynamodb", "cloudformation", "sqs", "sns"],
        [ContentCategory.Azure] = ["azure", "azure devops", "azure functions", "azure blob", "azure sql", "azure kubernetes"],
        [ContentCategory.Gcp] = ["gcp", "google cloud", "cloud run", "bigquery", "firebase", "cloud functions"],
        [ContentCategory.CiCd] = ["ci/cd", "ci cd", "continuous integration", "continuous delivery", "continuous deployment", "pipeline"],
        [ContentCategory.Terraform] = ["terraform", "infrastructure as code", "iac", "hashicorp", "terragrunt"],
        [ContentCategory.Linux] = ["linux", "bash", "shell script", "ubuntu", "centos", "debian", "systemd", "cron"],
        [ContentCategory.Git] = ["git", "github", "gitlab", "bitbucket", "git branch", "git merge", "git rebase", "version control"],
        [ContentCategory.Nginx] = ["nginx", "reverse proxy", "load balancer", "apache http"],
        [ContentCategory.GitHubActions] = ["github actions", "github workflow", "github ci", ".github/workflows"],

        // Conceptos
        [ContentCategory.Oop] = ["object oriented", "oop", "encapsulation", "inheritance", "polymorphism", "abstraction", "class", "interface"],
        [ContentCategory.FunctionalProgramming] = ["functional programming", "lambda", "higher order function", "pure function", "immutability", "monad"],
        [ContentCategory.Solid] = ["solid principles", "single responsibility", "open closed", "liskov substitution", "interface segregation", "dependency inversion"],
        [ContentCategory.DesignPatterns] = ["design pattern", "singleton", "factory pattern", "observer pattern", "strategy pattern", "decorator pattern", "adapter pattern", "builder pattern", "gang of four"],
        [ContentCategory.CleanCode] = ["clean code", "code quality", "code smell", "naming conventions", "readable code", "maintainable code"],
        [ContentCategory.Refactoring] = ["refactoring", "refactor", "code refactoring", "technical debt", "legacy code"],
        [ContentCategory.Testing] = ["unit test", "integration test", "testing", "test driven", "mock", "stub", "xunit", "nunit", "jest", "pytest", "junit"],
        [ContentCategory.Tdd] = ["tdd", "test driven development", "red green refactor", "test first"],

        // Arquitectura
        [ContentCategory.Microservices] = ["microservices", "microservice", "service mesh", "api gateway", "saga pattern", "service discovery"],
        [ContentCategory.RestApi] = ["rest api", "restful", "http methods", "api design", "swagger", "openapi", "endpoint"],
        [ContentCategory.GraphQL] = ["graphql", "graph ql", "apollo", "query language api", "mutation graphql"],
        [ContentCategory.SystemDesign] = ["system design", "scalability", "high availability", "distributed system", "load balancing", "caching strategy"],
        [ContentCategory.CleanArchitecture] = ["clean architecture", "hexagonal architecture", "onion architecture", "ports and adapters", "layered architecture"],
        [ContentCategory.EventDriven] = ["event driven", "event sourcing", "message queue", "rabbitmq", "kafka", "pub sub"],
        [ContentCategory.Cqrs] = ["cqrs", "command query", "command query responsibility", "mediatr"],
        [ContentCategory.Ddd] = ["domain driven design", "ddd", "bounded context", "aggregate", "value object", "domain model", "ubiquitous language"],

        // Estructuras de datos
        [ContentCategory.Arrays] = ["array", "arrays", "matrix", "two-dimensional array", "dynamic array", "arraylist"],
        [ContentCategory.LinkedLists] = ["linked list", "linkedlist", "singly linked", "doubly linked", "circular linked"],
        [ContentCategory.Trees] = ["tree", "binary tree", "bst", "binary search tree", "avl tree", "red-black tree", "b-tree", "trie"],
        [ContentCategory.Graphs] = ["graph", "directed graph", "undirected graph", "bfs", "dfs", "adjacency list", "adjacency matrix", "shortest path"],
        [ContentCategory.HashTables] = ["hash table", "hashtable", "hashmap", "hash map", "dictionary", "hash function", "collision resolution"],
        [ContentCategory.Stacks] = ["stack", "lifo", "push pop", "call stack"],
        [ContentCategory.Queues] = ["queue", "fifo", "priority queue", "deque", "circular queue"],
        [ContentCategory.Heaps] = ["heap", "min-heap", "max-heap", "priority queue", "heapify"],

        // Algoritmos
        [ContentCategory.Sorting] = ["sorting algorithm", "quicksort", "mergesort", "bubblesort", "heapsort", "insertion sort", "selection sort", "radix sort"],
        [ContentCategory.Searching] = ["search algorithm", "binary search", "linear search", "breadth first search", "depth first search"],
        [ContentCategory.DynamicProgramming] = ["dynamic programming", "memoization", "tabulation", "knapsack", "longest common subsequence"],
        [ContentCategory.Greedy] = ["greedy algorithm", "greedy approach", "huffman", "activity selection"],
        [ContentCategory.Recursion] = ["recursion", "recursive", "base case", "recursive call", "backtracking"],
        [ContentCategory.ComplexityAnalysis] = ["big o", "time complexity", "space complexity", "asymptotic", "o(n)", "o(log n)", "computational complexity"],

        // Fundamentos CS
        [ContentCategory.OperatingSystems] = ["operating system", "process", "thread", "scheduling", "deadlock", "semaphore", "mutex", "virtual memory"],
        [ContentCategory.ComputerNetworks] = ["computer network", "tcp/ip", "udp", "http", "https", "dns", "osi model", "socket", "websocket"],
        [ContentCategory.Dbms] = ["dbms", "database management", "normalization", "acid", "transaction", "indexing", "b+ tree"],
        [ContentCategory.Concurrency] = ["concurrency", "parallelism", "multithreading", "async", "await", "race condition", "thread safety", "lock"],
        [ContentCategory.MemoryManagement] = ["memory management", "garbage collection", "memory leak", "heap memory", "stack memory", "pointer"],

        // Áreas
        [ContentCategory.Backend] = ["backend", "back-end", "server side", "api development"],
        [ContentCategory.Frontend] = ["frontend", "front-end", "client side", "css", "html", "ui development", "responsive design", "accessibility"],
        [ContentCategory.FullStack] = ["full stack", "fullstack", "full-stack"],
        [ContentCategory.DevOps] = ["devops", "dev ops", "site reliability", "sre", "infrastructure"],
        [ContentCategory.Database] = ["database design", "data modeling", "schema design", "data warehouse", "etl"],
        [ContentCategory.Cloud] = ["cloud computing", "cloud native", "serverless", "saas", "paas", "iaas"],
        [ContentCategory.Mobile] = ["mobile development", "android", "ios", "react native", "flutter", "xamarin"],
        [ContentCategory.AiMl] = ["machine learning", "deep learning", "artificial intelligence", "neural network", "nlp", "computer vision", "tensorflow", "pytorch"],
        [ContentCategory.Security] = ["cybersecurity", "security", "oauth", "jwt", "authentication", "authorization", "xss", "sql injection", "owasp"],
        [ContentCategory.QaTesting] = ["qa", "quality assurance", "selenium", "cypress", "playwright", "e2e testing", "load testing", "performance testing"],
    };

    // ══════════════════════════════════════════════════════════════
    //  Patrones de tipo de contenido
    // ══════════════════════════════════════════════════════════════
    private static readonly (ContentType Type, string[] Patterns)[] ContentTypePatterns =
    [
        (ContentType.Tutorial, [
            "tutorial", "step by step", "paso a paso", "how to", "cómo", "getting started",
            "guía de inicio", "learn", "aprende", "beginner guide", "from scratch", "desde cero",
            "hands-on", "walkthrough", "example project"
        ]),
        (ContentType.Documentation, [
            "documentation", "documentación", "docs", "official docs", "api reference", "specification",
            "release notes", "changelog", "migration guide", "guía de migración"
        ]),
        (ContentType.Reference, [
            "reference", "api docs", "cheat sheet", "quick reference", "syntax reference",
            "command reference", "referencia", "manual"
        ]),
        (ContentType.Guide, [
            "guide", "guía", "best practice", "buenas prácticas", "architecture guide",
            "production ready", "in production", "enterprise", "principles"
        ]),
        (ContentType.Cheatsheet, [
            "cheatsheet", "cheat sheet", "quick reference", "one-pager", "resumen",
            "flashcards", "summary", "overview"
        ]),
        (ContentType.Pattern, [
            "design pattern", "patrón de diseño", "pattern implementation", "pattern example",
            "refactoring to pattern", "architectural pattern"
        ]),
        (ContentType.Comparison, [
            " vs ", " versus ", "comparison", "comparación", "compared to",
            "differences between", "diferencias entre", "which is better",
            "pros and cons", "ventajas y desventajas"
        ]),
        (ContentType.InterviewQA, [
            "interview question", "pregunta de entrevista", "interview preparation",
            "coding interview", "technical interview", "top questions"
        ]),
        (ContentType.Article, [
            "article", "blog", "artículo", "post", "introduction to", "introducción a",
            "understanding", "deep dive", "explained", "explicado", "what is", "qué es"
        ]),
        (ContentType.GitHubRepo, [
            "github.com", "repository", "open source project", "awesome-", "readme"
        ]),
    ];

    // ══════════════════════════════════════════════════════════════
    //  Patrones de dificultad
    // ══════════════════════════════════════════════════════════════
    private static readonly string[] BeginnerKeywords =
    [
        "beginner", "principiante", "basic", "básico", "introduction", "introducción",
        "getting started", "101", "for dummies", "easy", "fácil", "simple",
        "starter", "first steps", "primeros pasos", "fundamentals", "fundamentos"
    ];

    private static readonly string[] IntermediateKeywords =
    [
        "intermediate", "intermedio", "practical", "práctico", "real world",
        "hands-on", "project", "implementation", "implementación", "applied",
        "patterns", "patrones", "middleware", "integration", "integración"
    ];

    private static readonly string[] AdvancedKeywords =
    [
        "advanced", "avanzado", "expert", "experto", "deep dive", "profundización",
        "internals", "under the hood", "low level", "bajo nivel", "optimization",
        "optimización", "performance tuning", "scalability", "distributed",
        "production-grade", "enterprise", "architecture decision"
    ];

    // ══════════════════════════════════════════════════════════════
    //  Keywords de relevancia IT
    // ══════════════════════════════════════════════════════════════
    private static readonly string[] ITRelevanceKeywords =
    [
        "programming", "programación", "software", "developer", "desarrollo",
        "code", "código", "algorithm", "algoritmo", "database", "api",
        "framework", "library", "librería", "deploy", "server", "cloud",
        "devops", "testing", "debug", "compile", "runtime", "function",
        "class", "method", "interface", "architecture", "arquitectura",
        "pattern", "patrón", "repository", "container", "security",
        "authentication", "frontend", "backend", "fullstack", "web",
        "mobile", "data structure", "estructura de datos", "network",
        "protocol", "encryption", "ci/cd", "version control", "git",
        "open source", "machine learning", "artificial intelligence"
    ];

    private static readonly string[] NonITKeywords =
    [
        "cooking recipe", "fashion", "celebrity gossip", "horoscope",
        "weight loss", "dating tips", "real estate", "mortgage",
        "political opinion", "sports score", "movie review"
    ];

    // ══════════════════════════════════════════════════════════════
    //  Mapeo tecnología principal (display name)
    // ══════════════════════════════════════════════════════════════
    private static readonly Dictionary<ContentCategory, string> TechnologyNames = new()
    {
        [ContentCategory.Java] = "Java",
        [ContentCategory.Python] = "Python",
        [ContentCategory.JavaScript] = "JavaScript",
        [ContentCategory.TypeScript] = "TypeScript",
        [ContentCategory.CSharp] = "C#",
        [ContentCategory.Go] = "Go",
        [ContentCategory.Rust] = "Rust",
        [ContentCategory.PHP] = "PHP",
        [ContentCategory.Ruby] = "Ruby",
        [ContentCategory.Swift] = "Swift",
        [ContentCategory.Kotlin] = "Kotlin",
        [ContentCategory.C] = "C",
        [ContentCategory.Cpp] = "C++",
        [ContentCategory.React] = "React",
        [ContentCategory.Angular] = "Angular",
        [ContentCategory.VueJs] = "Vue.js",
        [ContentCategory.NextJs] = "Next.js",
        [ContentCategory.NodeJs] = "Node.js",
        [ContentCategory.ExpressJs] = "Express.js",
        [ContentCategory.Django] = "Django",
        [ContentCategory.Flask] = "Flask",
        [ContentCategory.SpringBoot] = "Spring Boot",
        [ContentCategory.AspNetCore] = "ASP.NET Core",
        [ContentCategory.DotNet] = ".NET",
        [ContentCategory.EntityFramework] = "Entity Framework",
        [ContentCategory.Laravel] = "Laravel",
        [ContentCategory.Rails] = "Ruby on Rails",
        [ContentCategory.FastApi] = "FastAPI",
        [ContentCategory.Sql] = "SQL",
        [ContentCategory.MySql] = "MySQL",
        [ContentCategory.PostgreSql] = "PostgreSQL",
        [ContentCategory.MongoDb] = "MongoDB",
        [ContentCategory.Redis] = "Redis",
        [ContentCategory.ElasticSearch] = "Elasticsearch",
        [ContentCategory.SqlServer] = "SQL Server",
        [ContentCategory.SQLite] = "SQLite",
        [ContentCategory.Docker] = "Docker",
        [ContentCategory.Kubernetes] = "Kubernetes",
        [ContentCategory.Aws] = "AWS",
        [ContentCategory.Azure] = "Azure",
        [ContentCategory.Gcp] = "GCP",
        [ContentCategory.CiCd] = "CI/CD",
        [ContentCategory.Terraform] = "Terraform",
        [ContentCategory.Linux] = "Linux",
        [ContentCategory.Git] = "Git",
        [ContentCategory.Nginx] = "Nginx",
        [ContentCategory.GitHubActions] = "GitHub Actions",
        [ContentCategory.Oop] = "OOP",
        [ContentCategory.FunctionalProgramming] = "Functional Programming",
        [ContentCategory.Solid] = "SOLID",
        [ContentCategory.DesignPatterns] = "Design Patterns",
        [ContentCategory.CleanCode] = "Clean Code",
        [ContentCategory.Refactoring] = "Refactoring",
        [ContentCategory.Testing] = "Testing",
        [ContentCategory.Tdd] = "TDD",
        [ContentCategory.Microservices] = "Microservices",
        [ContentCategory.RestApi] = "REST API",
        [ContentCategory.GraphQL] = "GraphQL",
        [ContentCategory.SystemDesign] = "System Design",
        [ContentCategory.CleanArchitecture] = "Clean Architecture",
        [ContentCategory.EventDriven] = "Event-Driven",
        [ContentCategory.Cqrs] = "CQRS",
        [ContentCategory.Ddd] = "DDD",
        [ContentCategory.Arrays] = "Arrays",
        [ContentCategory.LinkedLists] = "Linked Lists",
        [ContentCategory.Trees] = "Trees",
        [ContentCategory.Graphs] = "Graphs",
        [ContentCategory.HashTables] = "Hash Tables",
        [ContentCategory.Stacks] = "Stacks",
        [ContentCategory.Queues] = "Queues",
        [ContentCategory.Heaps] = "Heaps",
        [ContentCategory.Sorting] = "Sorting",
        [ContentCategory.Searching] = "Searching",
        [ContentCategory.DynamicProgramming] = "Dynamic Programming",
        [ContentCategory.Greedy] = "Greedy",
        [ContentCategory.Recursion] = "Recursion",
        [ContentCategory.ComplexityAnalysis] = "Complexity Analysis",
        [ContentCategory.OperatingSystems] = "Operating Systems",
        [ContentCategory.ComputerNetworks] = "Computer Networks",
        [ContentCategory.Dbms] = "DBMS",
        [ContentCategory.Concurrency] = "Concurrency",
        [ContentCategory.MemoryManagement] = "Memory Management",
        [ContentCategory.Backend] = "Backend",
        [ContentCategory.Frontend] = "Frontend",
        [ContentCategory.FullStack] = "Full Stack",
        [ContentCategory.DevOps] = "DevOps",
        [ContentCategory.Database] = "Databases",
        [ContentCategory.Cloud] = "Cloud",
        [ContentCategory.Mobile] = "Mobile",
        [ContentCategory.AiMl] = "AI/ML",
        [ContentCategory.Security] = "Security",
        [ContentCategory.QaTesting] = "QA/Testing",
    };

    public ContentClassifier(ILogger<ContentClassifier> logger)
    {
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════
    //  Interfaz pública
    // ══════════════════════════════════════════════════════════════

    public DocumentClassificationResult ClassifyDocument(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content))
        {
            return new DocumentClassificationResult
            {
                Category = ContentCategory.Unknown,
                ContentType = ContentType.Unknown,
                Difficulty = DifficultyLevel.Unknown,
                ConfidenceScore = 0
            };
        }

        var combinedText = $"{title} {content}".ToLowerInvariant();
        // Para categoría usamos title con mayor peso + primeros 2000 chars del contenido
        var classificationText = $"{title} {title} {title} {(content.Length > 2000 ? content[..2000] : content)}".ToLowerInvariant();

        var category = ClassifyCategory(classificationText, out double categoryConfidence);
        var contentType = ClassifyContentType(combinedText);
        var difficulty = ClassifyDifficulty(combinedText);
        var tags = ExtractTags(combinedText, category);
        var technology = TechnologyNames.TryGetValue(category, out var techName) ? techName : null;
        var subcategory = DetermineSubcategory(combinedText, category);

        return new DocumentClassificationResult
        {
            Category = category,
            Subcategory = subcategory,
            ContentType = contentType,
            Difficulty = difficulty,
            Tags = tags,
            Technology = technology,
            ConfidenceScore = categoryConfidence
        };
    }

    public bool IsITRelevantContent(string title, string content)
    {
        var text = $"{title} {content}".ToLowerInvariant();

        // Rechazar contenido claramente no-IT
        foreach (var nk in NonITKeywords)
        {
            if (text.Contains(nk))
                return false;
        }

        // Contar keywords IT encontrados
        int itScore = 0;
        foreach (var kw in ITRelevanceKeywords)
        {
            if (text.Contains(kw))
                itScore++;
        }

        // Al menos 2 keywords IT encontrados
        if (itScore >= 2)
            return true;

        // Verificar si alguna categoría tecnológica tiene score positivo
        foreach (var (_, keywords) in CategoryKeywords)
        {
            int hits = 0;
            foreach (var kw in keywords)
            {
                if (text.Contains(kw))
                {
                    hits++;
                    if (hits >= 2) return true;
                }
            }
        }

        return false;
    }

    // ══════════════════════════════════════════════════════════════
    //  Métodos privados
    // ══════════════════════════════════════════════════════════════

    private ContentCategory ClassifyCategory(string text, out double confidence)
    {
        var scores = new Dictionary<ContentCategory, double>();

        foreach (var (category, keywords) in CategoryKeywords)
        {
            double score = 0;
            foreach (var kw in keywords)
            {
                if (text.Contains(kw))
                {
                    // Keywords más largos (más específicos) valen más
                    score += kw.Length > 8 ? 3.0 : kw.Length > 5 ? 2.0 : 1.0;

                    // Bonus si aparece en la primera parte (probablemente título)
                    var firstQuarter = text[..(text.Length / 4)];
                    if (firstQuarter.Contains(kw))
                        score += 2.0;
                }
            }

            if (score > 0)
                scores[category] = score;
        }

        if (scores.Count == 0)
        {
            confidence = 0;
            return ContentCategory.General;
        }

        var ranked = scores.OrderByDescending(s => s.Value).ToList();
        var best = ranked[0];

        // Calcular confianza basada en la separación del segundo mejor
        double secondBest = ranked.Count > 1 ? ranked[1].Value : 0;
        confidence = Math.Min(1.0, best.Value / (best.Value + secondBest + 1.0));

        // Si la confianza es muy baja, puede ser General
        if (confidence < 0.2 && best.Value < 3)
        {
            confidence = 0.1;
            return ContentCategory.General;
        }

        return best.Key;
    }

    private static ContentType ClassifyContentType(string text)
    {
        var scores = new Dictionary<ContentType, int>();

        foreach (var (type, patterns) in ContentTypePatterns)
        {
            int score = 0;
            foreach (var p in patterns)
            {
                if (text.Contains(p))
                    score++;
            }
            if (score > 0)
                scores[type] = score;
        }

        if (scores.Count == 0)
            return ContentType.Article; // Default: artículo

        return scores.OrderByDescending(s => s.Value).First().Key;
    }

    private static DifficultyLevel ClassifyDifficulty(string text)
    {
        int beginnerScore = 0, intermediateScore = 0, advancedScore = 0;

        foreach (var kw in BeginnerKeywords)
            if (text.Contains(kw)) beginnerScore++;

        foreach (var kw in IntermediateKeywords)
            if (text.Contains(kw)) intermediateScore++;

        foreach (var kw in AdvancedKeywords)
            if (text.Contains(kw)) advancedScore++;

        if (advancedScore > intermediateScore && advancedScore > beginnerScore)
            return DifficultyLevel.Senior;
        if (intermediateScore > beginnerScore)
            return DifficultyLevel.Mid;
        if (beginnerScore > 0)
            return DifficultyLevel.Junior;

        return DifficultyLevel.Mid; // Default: intermedio
    }

    private List<string> ExtractTags(string text, ContentCategory category)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Añadir technology name como tag
        if (TechnologyNames.TryGetValue(category, out var techName))
            tags.Add(techName);

        // Buscar otras tecnologías mencionadas (cross-technology tags)
        foreach (var (cat, keywords) in CategoryKeywords)
        {
            if (cat == category) continue;

            int hits = 0;
            foreach (var kw in keywords)
            {
                if (text.Contains(kw))
                {
                    hits++;
                    if (hits >= 2) // Solo si tiene menciones significativas
                    {
                        if (TechnologyNames.TryGetValue(cat, out var otherTech))
                            tags.Add(otherTech);
                        break;
                    }
                }
            }
        }

        // Detectar tags transversales
        if (text.Contains("api")) tags.Add("API");
        if (text.Contains("security") || text.Contains("seguridad")) tags.Add("Security");
        if (text.Contains("performance") || text.Contains("rendimiento")) tags.Add("Performance");
        if (text.Contains("scalab") || text.Contains("escalab")) tags.Add("Scalability");
        if (text.Contains("deploy") || text.Contains("despliegue")) tags.Add("Deployment");
        if (text.Contains("migrat") || text.Contains("migración")) tags.Add("Migration");
        if (text.Contains("best practice") || text.Contains("buenas prácticas")) tags.Add("Best Practices");
        if (text.Contains("debug")) tags.Add("Debugging");
        if (text.Contains("monitor")) tags.Add("Monitoring");
        if (text.Contains("logging") || text.Contains("log")) tags.Add("Logging");

        return tags.Take(10).ToList(); // Máximo 10 tags
    }

    private static string? DetermineSubcategory(string text, ContentCategory category)
    {
        // Subcategorías específicas según la categoría principal
        return category switch
        {
            ContentCategory.CSharp => DetectFirst(text,
                ("LINQ", ["linq", "language integrated query"]),
                ("Async/Await", ["async", "await", "task"]),
                ("Generics", ["generic", "generics"]),
                ("Collections", ["collection", "list<", "dictionary<"]),
                ("Delegates/Events", ["delegate", "event", "action<", "func<"])),

            ContentCategory.Java => DetectFirst(text,
                ("Streams", ["stream api", "java stream"]),
                ("Collections", ["collection framework", "arraylist", "hashmap"]),
                ("Concurrency", ["thread", "executor", "concurrent"]),
                ("Spring", ["spring", "bean", "autowired"])),

            ContentCategory.Python => DetectFirst(text,
                ("Data Science", ["pandas", "numpy", "matplotlib", "data analysis"]),
                ("Web", ["flask", "django", "fastapi"]),
                ("Async", ["asyncio", "async def"]),
                ("Scripting", ["script", "automation"])),

            ContentCategory.React => DetectFirst(text,
                ("Hooks", ["useffect", "usestate", "usecontext", "custom hook"]),
                ("State Management", ["redux", "zustand", "context api"]),
                ("Routing", ["react router", "routing"]),
                ("SSR", ["server side rendering", "next.js", "ssr"])),

            ContentCategory.Docker => DetectFirst(text,
                ("Compose", ["docker-compose", "docker compose"]),
                ("Networking", ["docker network", "bridge", "overlay"]),
                ("Security", ["docker security", "rootless"]),
                ("Multi-stage", ["multi-stage", "multistage"])),

            ContentCategory.Kubernetes => DetectFirst(text,
                ("Networking", ["ingress", "service mesh", "istio"]),
                ("Storage", ["persistent volume", "pvc", "storage class"]),
                ("Monitoring", ["prometheus", "grafana", "monitoring"]),
                ("Helm", ["helm chart", "helm"])),

            ContentCategory.Sql => DetectFirst(text,
                ("Optimization", ["query optimization", "execution plan", "index"]),
                ("Joins", ["join", "inner join", "left join"]),
                ("Functions", ["stored procedure", "function", "trigger"]),
                ("Design", ["normalization", "schema design"])),

            _ => null
        };
    }

    private static string? DetectFirst(string text, params (string Name, string[] Keywords)[] options)
    {
        foreach (var (name, keywords) in options)
        {
            foreach (var kw in keywords)
            {
                if (text.Contains(kw))
                    return name;
            }
        }
        return null;
    }
}
