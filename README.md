# Web Scraping Module – Interview Simulator

Módulo de scraping para el **Interview Simulator**. Extrae, clasifica y almacena preguntas de entrevista desde 5 fuentes web de forma automatizada.

## Arquitectura

```
Web-scraping_module.sln
├── src/
│   ├── InterviewSimulator.Scraping.Core        # Modelos, Enums, Interfaces, Configuración
│   ├── InterviewSimulator.Scraping.Data         # EF Core DbContext, Repositorios
│   ├── InterviewSimulator.Scraping.Classifier   # Clasificador por keywords/regex
│   ├── InterviewSimulator.Scraping.Scrapers     # 5 scrapers + Orchestrator
│   └── InterviewSimulator.Scraping.Worker       # Background Service (punto de entrada)
└── tests/
    └── InterviewSimulator.Scraping.Tests.Unit   # Tests unitarios (xUnit)
```

## Fuentes de scraping

| Fuente     | Método                | Tipo            |
|------------|-----------------------|-----------------|
| Dev.to     | REST API (Forem)      | BlogPlatform    |
| Medium     | Playwright headless   | BlogPlatform    |
| LeetCode   | GraphQL API           | CodingPlatform  |
| Glassdoor  | Playwright + anti-bot | JobBoard        |
| Indeed     | Playwright headless   | JobBoard        |

## Requisitos previos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (local o remoto)
- Playwright browsers (se instalan automáticamente en el primer uso)

## Configuración

Editar `appsettings.json` en el proyecto Worker:

```json
{
  "ConnectionStrings": {
    "ScrapingDb": "Server=localhost;Database=InterviewSimulator_Scraping;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "ScrapingSettings": {
    "EnabledScrapers": ["DevTo"],
    "CronSchedule": "0 3 * * *"
  }
}
```

### Variables importantes

| Variable                        | Descripción                                    | Default          |
|---------------------------------|------------------------------------------------|------------------|
| `EnabledScrapers`               | Lista de scrapers activos                      | `["DevTo"]`      |
| `CronSchedule`                  | Expresión cron para ejecución programada       | `0 3 * * *`      |
| `MaxConcurrentScrapers`         | Scrapers ejecutándose en paralelo              | `2`              |
| `MinDelayBetweenRequestsMs`     | Delay mínimo entre requests (ms)               | `2000`           |
| `MaxDelayBetweenRequestsMs`     | Delay máximo entre requests (ms)               | `5000`           |

## Compilar

```bash
dotnet build Web-scraping_module.sln
```

## Ejecutar

```bash
dotnet run --project src/InterviewSimulator.Scraping.Worker
```

## Tests

```bash
dotnet test
```

## Clasificación de preguntas

El clasificador basado en keywords/regex asigna:

- **Categoría**: Technical, Behavioral, Situational, General
- **Subcategoría** (solo Technical): Algorithms, DataStructures, Databases, WebDevelopment, SystemDesign, DevOps, Security, Languages, Testing
- **Dificultad**: Junior, Mid, Senior
- **Tags**: Palabras clave detectadas en el texto

Las reglas se definen en `classification_rules.json`.

## Deduplicación

- **Capa 1 (Exact Match)**: SHA-256 sobre texto normalizado (lowercase, sin artículos, sin puntuación).
- Índice único en `HashFingerprint` en la base de datos.

## Stack tecnológico

- **Runtime**: .NET 8.0
- **ORM**: Entity Framework Core 8.x (SQL Server)
- **Browser Automation**: Microsoft Playwright
- **HTML Parsing**: HtmlAgilityPack, AngleSharp
- **Resilience**: Polly (retry + circuit breaker)
- **Logging**: Serilog (Console + File sinks)
- **Scheduling**: Cronos (cron expressions)
- **Tests**: xUnit + Moq
