# MEGA-PROMPT: Agente de Desarrollo — Pipeline de Procesamiento RAG para Simulador de Entrevistas Laborales

---

## CONTEXTO DEL PROYECTO

Eres un agente de desarrollo de software senior especializado en C#/.NET y arquitecturas RAG (Retrieval-Augmented Generation). Tu tarea es diseñar e implementar el **pipeline de procesamiento de datos** que transforma los datos crudos del web scraping en una base de conocimiento vectorial consultable, dentro de un proyecto de titulación (Trabajo Terminal) de ESCOM-IPN.

### Estado actual del proyecto

El proyecto es un **simulador de entrevistas laborales** para estudiantes de Ingeniería en Sistemas Computacionales. Actualmente se tienen implementados:

1. **Módulo de usuarios (CRUD)** — Completado. ASP.NET Core Web API con JWT auth.
2. **Módulo de entrevista básica** — Completado. Flujo estático de 15 preguntas placeholder.
3. **Módulo de Web Scraping** — Completado. Extrae preguntas de entrevistas de IT desde Dev.to (API REST), Medium (Playwright), LeetCode (GraphQL), Glassdoor e Indeed (Playwright). Los datos se almacenan en SQL Server con los campos:
   - `QuestionText` — La pregunta extraída.
   - `RawContent` — El contenido original completo del artículo/problema (HTML/texto sin procesar).
   - `Category` — Clasificación: Technical, Behavioral, Situational, General, Unknown.
   - `Subcategory` — e.g., "Bases de Datos", "Algoritmos y Estructuras de Datos".
   - `DifficultyLevel` — Junior, Mid, Senior, Unknown.
   - `Tags` — JSON array con keywords: ["SQL", "JOIN", "Normalization"].
   - `OriginalLanguage` — "es" o "en".
   - `SourceUrl` — URL de donde se extrajo.
   - `HashFingerprint` — SHA-256 para deduplicación.

4. **Pipeline de procesamiento RAG** — **ESTE ES TU MÓDULO.** Debe tomar los datos scrapeados y transformarlos en una base de conocimiento vectorial que el LLM pueda consultar para generar preguntas personalizadas.

### Stack tecnológico existente

- **Backend:** ASP.NET Core Web API (.NET 8+)
- **Arquitectura:** N-Tier (Modelos → Repositorios → Servicios → Controllers)
- **Base de datos relacional:** SQL Server con Entity Framework Core
- **ORM:** Entity Framework Core (Code-First para tablas nuevas)
- **Autenticación:** JWT Claims-based
- **Inyección de dependencias:** Nativo de ASP.NET Core
- **Logging:** Serilog (ya configurado en el módulo de scraping)
- **Resiliencia:** Polly (ya configurado)

### Nuevas tecnologías a integrar (TU módulo)

- **Embeddings:** OpenAI API (`text-embedding-3-small` o `text-embedding-3-large`)
- **Base de datos vectorial:** Qdrant (corriendo en Docker)
- **Procesamiento de texto:** Lógica propia en C# + HtmlAgilityPack (ya disponible)

---

## OBJETIVO DEL MÓDULO

Construir un pipeline que ejecute los siguientes pasos en orden:

```
┌─────────────────────────────────────────────────────────────────┐
│                    PIPELINE RAG COMPLETO                         │
│                                                                  │
│  [SQL Server]                                                    │
│  ScrapedQuestions ──→ RawContent + QuestionText + Metadata        │
│       │                                                          │
│       ▼                                                          │
│  [PASO 1] Limpieza                                               │
│  Quitar HTML, ads, footers, normalizar texto, detectar idioma    │
│       │                                                          │
│       ▼                                                          │
│  [PASO 2] Chunking                                               │
│  Dividir en fragmentos inteligentes de ~500-1000 tokens          │
│  Preservar contexto: 1 pregunta + explicación = 1 chunk ideal    │
│       │                                                          │
│       ▼                                                          │
│  [PASO 3] Enrichment (Enriquecimiento de metadata)               │
│  Agregar metadata a cada chunk: categoría, idioma, fuente, etc.  │
│       │                                                          │
│       ▼                                                          │
│  [PASO 4] Embedding                                              │
│  Enviar cada chunk a OpenAI API → obtener vector numérico        │
│       │                                                          │
│       ▼                                                          │
│  [PASO 5] Almacenamiento Vectorial                               │
│  Guardar vector + metadata + texto original en Qdrant            │
│       │                                                          │
│       ▼                                                          │
│  [PASO 6] Retrieval Service                                      │
│  Dado un perfil de usuario, recuperar los chunks más relevantes  │
│  para que el LLM genere preguntas personalizadas                 │
│                                                                  │
│  [Qdrant Docker] ←──→ [Retrieval API] ──→ [LLM (futuro)]        │
└─────────────────────────────────────────────────────────────────┘
```

---

## ARQUITECTURA DEL MÓDULO

### Estructura de proyectos dentro de la solución

```
InterviewSimulator.sln
│
├── src/
│   ├── InterviewSimulator.WebAPI/                    ← Existente
│   ├── InterviewSimulator.Scraping.*/                ← Existente (5 proyectos)
│   │
│   ├── InterviewSimulator.RAG.Core/                  ← Class Library: Interfaces, modelos, DTOs
│   │   ├── Interfaces/
│   │   │   ├── ITextCleaner.cs
│   │   │   ├── IChunkingService.cs
│   │   │   ├── IEmbeddingService.cs
│   │   │   ├── IVectorStoreService.cs
│   │   │   ├── IRetrievalService.cs
│   │   │   ├── IRagPipelineOrchestrator.cs
│   │   │   └── IProcessingStatusRepository.cs
│   │   ├── Models/
│   │   │   ├── CleanedDocument.cs
│   │   │   ├── TextChunk.cs
│   │   │   ├── EmbeddedChunk.cs
│   │   │   ├── RetrievalQuery.cs
│   │   │   ├── RetrievalResult.cs
│   │   │   ├── ProcessingStatus.cs
│   │   │   └── Enums/
│   │   │       ├── ChunkType.cs
│   │   │       ├── ProcessingState.cs
│   │   │       └── EmbeddingModel.cs
│   │   ├── Configuration/
│   │   │   ├── RagPipelineSettings.cs
│   │   │   ├── OpenAISettings.cs
│   │   │   └── QdrantSettings.cs
│   │   └── Constants/
│   │       └── RagConstants.cs
│   │
│   ├── InterviewSimulator.RAG.Processing/            ← Class Library: Limpieza + Chunking
│   │   ├── Cleaning/
│   │   │   ├── HtmlTextCleaner.cs
│   │   │   ├── TextNormalizer.cs
│   │   │   └── LanguageDetector.cs
│   │   ├── Chunking/
│   │   │   ├── InterviewQuestionChunker.cs           ← Chunker especializado para Q&A
│   │   │   ├── RecursiveTextChunker.cs               ← Chunker genérico de fallback
│   │   │   ├── ChunkingStrategyFactory.cs
│   │   │   └── TokenCounter.cs
│   │   └── Enrichment/
│   │       └── ChunkMetadataEnricher.cs
│   │
│   ├── InterviewSimulator.RAG.Embedding/             ← Class Library: Generación de embeddings
│   │   ├── OpenAIEmbeddingService.cs
│   │   ├── EmbeddingBatchProcessor.cs
│   │   └── EmbeddingCache.cs
│   │
│   ├── InterviewSimulator.RAG.VectorStore/           ← Class Library: Interacción con Qdrant
│   │   ├── QdrantVectorStoreService.cs
│   │   ├── QdrantCollectionManager.cs
│   │   └── QdrantHealthCheck.cs
│   │
│   ├── InterviewSimulator.RAG.Retrieval/             ← Class Library: Búsqueda y recuperación
│   │   ├── RetrievalService.cs
│   │   ├── QueryBuilder.cs
│   │   ├── RetrievalReranker.cs
│   │   └── ContextAssembler.cs
│   │
│   ├── InterviewSimulator.RAG.Data/                  ← Class Library: Persistencia (tracking)
│   │   ├── ProcessingDbContext.cs
│   │   ├── Repositories/
│   │   │   └── ProcessingStatusRepository.cs
│   │   ├── Migrations/
│   │   └── EntityConfigurations/
│   │       └── ProcessingStatusConfiguration.cs
│   │
│   └── InterviewSimulator.RAG.Worker/                ← Worker Service: Ejecución del pipeline
│       ├── Program.cs
│       ├── RagPipelineWorker.cs
│       ├── appsettings.json
│       └── appsettings.Development.json
│
└── tests/
    └── InterviewSimulator.RAG.Tests.Unit/
        ├── Cleaning/
        │   └── HtmlTextCleanerTests.cs
        ├── Chunking/
        │   ├── InterviewQuestionChunkerTests.cs
        │   └── RecursiveTextChunkerTests.cs
        └── Retrieval/
            └── QueryBuilderTests.cs
```

---

## MODELO DE DATOS

### Tabla de tracking del procesamiento (SQL Server)

Esta tabla registra qué ScrapedQuestions ya fueron procesadas por el pipeline, para evitar reprocesar y para auditoría.

```
┌──────────────────────────────────────────────┐
│          ProcessingStatus                     │
├──────────────────────────────────────────────┤
│ Id (PK, int, identity)                        │
│ ScrapedQuestionId (FK → ScrapedQuestion.Id)   │  ← Relación con datos del scraping
│ State (enum ProcessingState)                  │  ← Pending, Cleaned, Chunked, Embedded, Stored, Failed
│ ChunksGenerated (int)                         │  ← Cantidad de chunks producidos
│ EmbeddingModel (nvarchar 100)                 │  ← "text-embedding-3-small" / "text-embedding-3-large"
│ QdrantCollectionName (nvarchar 100)           │  ← Nombre de la collection en Qdrant
│ QdrantPointIds (nvarchar max)                 │  ← JSON array de los point IDs en Qdrant
│ ProcessedAt (datetime2)                       │
│ ErrorMessage (nvarchar max)                   │  ← null si no hubo error
│ RetryCount (int, default 0)                   │
│ CreatedAt (datetime2)                         │
│ UpdatedAt (datetime2?)                        │
└──────────────────────────────────────────────┘
```

### Enum ProcessingState

```csharp
public enum ProcessingState
{
    Pending = 0,        // Registrado pero no procesado
    Cleaned = 1,        // Texto limpio generado
    Chunked = 2,        // Chunks generados
    Embedded = 3,       // Embeddings obtenidos de OpenAI
    Stored = 4,         // Almacenado en Qdrant — ESTADO FINAL EXITOSO
    Failed = 5,         // Error en algún paso
    Skipped = 6         // Omitido (contenido insuficiente, duplicado, etc.)
}
```

### Estructura de un punto (point) en Qdrant

Cada chunk se almacena como un "point" en Qdrant con la siguiente estructura:

```json
{
  "id": "uuid-v4-generado",
  "vector": [0.0123, -0.0456, 0.0789, ...],   // 1536 dimensiones (text-embedding-3-small) o 3072 (large)
  "payload": {
    "scraped_question_id": 42,
    "chunk_index": 0,
    "chunk_type": "question_answer",
    "text": "El texto limpio del chunk",
    "question_text": "¿Cuál es la diferencia entre INNER JOIN y LEFT JOIN?",
    "category": "Technical",
    "subcategory": "Bases de Datos",
    "difficulty_level": "Junior",
    "tags": ["SQL", "JOIN", "Database"],
    "original_language": "es",
    "source_url": "https://dev.to/...",
    "source_name": "DevTo",
    "scraped_at": "2026-03-01T10:00:00Z",
    "processed_at": "2026-03-01T12:00:00Z",
    "token_count": 487,
    "char_count": 1842
  }
}
```

**Payload fields explicados:**

| Campo | Tipo | Uso en Retrieval | Descripción |
|-------|------|-----------------|-------------|
| `scraped_question_id` | integer | Trazabilidad | FK a la tabla SQL original |
| `chunk_index` | integer | Ordenamiento | Posición del chunk dentro del documento (0, 1, 2...) |
| `chunk_type` | keyword | Filtro | `"question_answer"`, `"explanation"`, `"code_example"`, `"general_content"` |
| `text` | text | Display | El texto completo del chunk (se devuelve al LLM) |
| `question_text` | text | Display | La pregunta aislada si se pudo extraer |
| `category` | keyword | **Filtro principal** | Technical, Behavioral, Situational, General |
| `subcategory` | keyword | **Filtro principal** | Algoritmos, Bases de Datos, Redes, DevOps, etc. |
| `difficulty_level` | keyword | **Filtro principal** | Junior, Mid, Senior |
| `tags` | keyword[] | Filtro secundario | Array de tecnologías/temas mencionados |
| `original_language` | keyword | Filtro | "es" o "en" |
| `source_url` | text | Trazabilidad | URL original |
| `source_name` | keyword | Filtro | DevTo, Medium, LeetCode, etc. |
| `scraped_at` | datetime | Ordenamiento | Para priorizar contenido más reciente |
| `processed_at` | datetime | Auditoría | Cuándo se procesó |
| `token_count` | integer | Estadísticas | Tokens en el chunk |
| `char_count` | integer | Estadísticas | Caracteres en el chunk |

---

## PASO 1: LIMPIEZA (Text Cleaning)

### Archivo: `HtmlTextCleaner.cs`

**Input:** `ScrapedQuestion.RawContent` (HTML/texto crudo) + `ScrapedQuestion.QuestionText`
**Output:** `CleanedDocument` con texto limpio listo para chunking.

### Modelo de salida

```csharp
public class CleanedDocument
{
    public int ScrapedQuestionId { get; set; }
    public string CleanedText { get; set; } = string.Empty;           // Texto completo limpio
    public string CleanedQuestionText { get; set; } = string.Empty;   // Pregunta aislada limpia
    public string DetectedLanguage { get; set; } = "unknown";          // "es", "en"
    public int EstimatedTokenCount { get; set; }
    public int CharCount { get; set; }
    public bool HasSufficientContent { get; set; }                     // false si < 50 tokens
    public List<string> CleaningWarnings { get; set; } = new();        // Problemas encontrados
}
```

### Lógica de limpieza paso a paso

```
PROCESO DE LIMPIEZA:

1. DETECCIÓN DE FORMATO
   - Si RawContent contiene tags HTML (<p>, <div>, <article>, etc.) → Tratar como HTML.
   - Si no → Tratar como texto plano con posible Markdown.

2. EXTRACCIÓN DE CONTENIDO PRINCIPAL (si es HTML)
   a. Parsear con HtmlAgilityPack.
   b. Remover nodos completamente:
      - <script>, <style>, <noscript>, <iframe>
      - <nav>, <footer>, <header> (navegación del sitio, NO del artículo)
      - <aside> (sidebars)
      - Elementos con class/id que contengan: "sidebar", "footer", "header", "nav",
        "menu", "ad", "advertisement", "banner", "cookie", "popup", "modal",
        "social", "share", "comment", "related", "recommended", "newsletter",
        "subscribe", "signup", "author-bio", "author-info"
   c. Extraer texto del <article> o <main> si existe. Si no existe, usar <body>.
   d. Preservar la estructura semántica:
      - <h1>-<h6> → Convertir a "## TÍTULO" (Markdown-like) para que el chunker detecte secciones.
      - <p> → Párrafo con doble newline.
      - <li> → Prefijo "- " o "N. " según <ul>/<ol>.
      - <pre>, <code> → Envolver en ``` para preservar bloques de código.
      - <strong>, <b> → Mantener el texto sin formato.
      - <a> → Mantener solo el texto, descartar el href.
      - <table> → Convertir a texto tabular simple o descartar si es muy compleja.
      - <img> → Descartar (pero loggear si tenía alt text relevante).

3. LIMPIEZA DE TEXTO PLANO / POST-HTML
   a. Normalizar encoding a UTF-8.
   b. Reemplazar entidades HTML (&amp; → &, &lt; → <, &nbsp; → espacio, etc.).
   c. Eliminar URLs sueltas (https://...) que no aporten contexto.
   d. Eliminar emails.
   e. Eliminar emojis (excepto los usados como bullet points como ✅, ❌, ⭐).
   f. Eliminar líneas que sean solo separadores: "---", "===", "***", "___".
   g. Eliminar líneas vacías excesivas (máximo 2 consecutivas).
   h. Eliminar espacios múltiples (reemplazar 2+ espacios por 1).
   i. Trim de cada línea.
   j. Eliminar líneas que sean solo "Read more", "Continue reading",
      "Follow me on", "Like and share", "Subscribe",
      "Leer más", "Seguir", "Suscríbete", "Compartir",
      y variantes comunes de CTAs.

4. LIMPIEZA ESPECÍFICA POR FUENTE
   - DevTo: Eliminar bloques de "Cover image", "Discussion (N comments)",
     "Top comments", líneas que empiecen con "Originally published at".
   - Medium: Eliminar "Member-only story", "X min read",
     bloques de "Written by", "Follow", "More from".
   - LeetCode: Preservar el enunciado completo del problema,
     eliminar "Accepted", "Submissions", "Acceptance Rate",
     secciones de "Similar Questions", "Related Topics" aisladas.
   - Glassdoor: Eliminar "Interview Question", "Add Tags",
     "No Answers Yet", ratings numéricos sueltos.
   - Indeed: Eliminar "Related:", "Tips:", "Read more:", 
     menús de navegación de career-advice.

5. DETECCIÓN DE IDIOMA
   Implementar un detector simple basado en palabras frecuentes:
   
   Palabras indicadoras de español:
   ["que", "qué", "como", "cómo", "para", "por", "una", "los", "las",
    "del", "con", "esta", "pero", "más", "también", "puede", "entre",
    "cuando", "sobre", "todo", "desde", "donde", "cual", "cuál",
    "ejemplo", "datos", "sistema", "proceso", "función"]
   
   Palabras indicadoras de inglés:
   ["the", "and", "for", "that", "with", "this", "from", "your",
    "which", "when", "what", "how", "about", "would", "should",
    "between", "example", "function", "return", "data"]
   
   Método:
   a. Tokenizar el texto limpio (split por espacios y puntuación).
   b. Convertir a lowercase.
   c. Contar cuántos tokens coinciden con cada lista.
   d. Si español_count > inglés_count * 1.2 → "es"
   e. Si inglés_count > español_count * 1.2 → "en"
   f. Si están cerca → "mixed" (loggear warning, defaultear a "en")

6. VALIDACIÓN DE CONTENIDO SUFICIENTE
   a. Contar tokens aproximados (palabras separadas por espacio ≈ tokens * 0.75 para inglés, * 0.65 para español).
   b. Si tokens < 50 → HasSufficientContent = false, State = Skipped.
   c. Si tokens > 50 pero < 100 → Agregar warning "Low content".
   d. Registrar EstimatedTokenCount y CharCount.

7. OUTPUT
   Devolver CleanedDocument con todo lo anterior.
```

### TokenCounter

```csharp
/// <summary>
/// Estimador de tokens compatible con modelos OpenAI.
/// Para conteo exacto se requeriría tiktoken, pero para el pipeline
/// una estimación es suficiente.
/// 
/// Regla general:
/// - Inglés: 1 token ≈ 4 caracteres, o ≈ 0.75 palabras
/// - Español: 1 token ≈ 3.5 caracteres, o ≈ 0.65 palabras
///   (español usa más tokens por palabra por los acentos y conjugaciones)
/// </summary>
public static class TokenCounter
{
    public static int EstimateTokens(string text, string language = "en")
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        // Método basado en caracteres (más consistente)
        double charsPerToken = language == "es" ? 3.5 : 4.0;
        return (int)Math.Ceiling(text.Length / charsPerToken);
    }

    public static int EstimateTokensByWords(string text, string language = "en")
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        int wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' },
            StringSplitOptions.RemoveEmptyEntries).Length;

        double wordsPerToken = language == "es" ? 0.65 : 0.75;
        return (int)Math.Ceiling(wordCount / wordsPerToken);
    }
}
```

---

## PASO 2: CHUNKING (Fragmentación inteligente)

### Estrategia dual de chunking

El módulo implementa DOS estrategias de chunking, seleccionadas automáticamente:

**Estrategia A: `InterviewQuestionChunker` (preferida)**
- Se activa cuando el documento tiene estructura de preguntas de entrevista.
- Detecta pares pregunta+respuesta/explicación y genera 1 chunk por cada par.
- Resultado: Chunks semánticamente completos y autocontenidos.

**Estrategia B: `RecursiveTextChunker` (fallback)**
- Se activa cuando el documento no tiene estructura Q&A clara.
- Divide recursivamente por: headers → párrafos → oraciones → caracteres.
- Resultado: Chunks de tamaño controlado con overlap para continuidad.

### Modelo de salida

```csharp
public class TextChunk
{
    public Guid ChunkId { get; set; } = Guid.NewGuid();
    public int ScrapedQuestionId { get; set; }
    public int ChunkIndex { get; set; }                    // Posición dentro del documento (0, 1, 2...)
    public string Text { get; set; } = string.Empty;        // Texto del chunk
    public string? QuestionText { get; set; }               // Pregunta aislada (si se detectó)
    public ChunkType Type { get; set; }
    public int TokenCount { get; set; }
    public int CharCount { get; set; }
    public string Language { get; set; } = "en";

    // Metadata heredada del ScrapedQuestion (se copia para tenerla disponible)
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public string DifficultyLevel { get; set; } = "Unknown";
    public List<string> Tags { get; set; } = new();
    public string SourceUrl { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public DateTime ScrapedAt { get; set; }
}

public enum ChunkType
{
    QuestionAnswer = 0,     // Par pregunta + respuesta/explicación
    Explanation = 1,        // Explicación extendida sin pregunta explícita
    CodeExample = 2,        // Bloque de código con contexto
    GeneralContent = 3      // Contenido general (fallback del recursive chunker)
}
```

### Lógica del `InterviewQuestionChunker`

```
DETECCIÓN DE ESTRUCTURA Q&A:

El chunker analiza el CleanedText buscando patrones que indiquen
que el documento es una lista de preguntas de entrevista.

PATRONES DE DETECCIÓN (regex):

Pattern_NumberedQuestion:
  ^\s*(\d{1,3})\s*[\.\)\-:]\s*(.+\?)\s*$
  Ejemplo: "1. What is the difference between abstract class and interface?"
  Ejemplo: "15) ¿Cuál es la diferencia entre TCP y UDP?"

Pattern_HeaderQuestion:
  ^#{1,4}\s+(.+\?)\s*$
  Ejemplo: "## What is polymorphism?"
  Ejemplo: "### ¿Qué es una API REST?"

Pattern_BoldQuestion:
  ^\*{1,2}(.+\?)\*{1,2}\s*$
  Ejemplo: "**What is a deadlock?**"

Pattern_QPrefix:
  ^(?:Q|Question|Pregunta|P)\s*[\.\:\-\d]*\s*(.+\?)\s*$
  Ejemplo: "Q1: What is dependency injection?"
  Ejemplo: "Pregunta 5: ¿Qué es un índice en SQL?"

Pattern_QuestionLine:
  ^(.{15,300}\?)\s*$
  (Cualquier línea que termine en "?" y tenga entre 15 y 300 caracteres)

ALGORITMO:

1. Dividir el CleanedText en líneas.
2. Escanear todas las líneas buscando matches con los patrones anteriores.
3. Si se detectan >= 3 preguntas → Es documento Q&A → Usar esta estrategia.
4. Si se detectan < 3 preguntas → No es Q&A → Delegar a RecursiveTextChunker.

GENERACIÓN DE CHUNKS Q&A:

5. Para cada pregunta detectada:
   a. La pregunta es la línea que matcheó el patrón.
   b. La "respuesta/explicación" es todo el texto desde la línea siguiente
      hasta la próxima pregunta detectada (o fin del documento).
   c. Crear chunk: QuestionText = la pregunta, Text = pregunta + explicación.

6. VALIDACIÓN DE TAMAÑO POR CHUNK:
   a. Si el chunk tiene < 30 tokens → Demasiado corto.
      - Intentar fusionar con el chunk siguiente si son del mismo tema.
      - Si no se puede fusionar → Mantener pero marcar como "thin_chunk" en warnings.
   b. Si el chunk tiene > 1500 tokens → Demasiado largo.
      - Cortar la explicación en sub-chunks usando RecursiveTextChunker.
      - El primer sub-chunk mantiene la pregunta, los siguientes son tipo Explanation.
   c. Rango ideal: 100-1000 tokens por chunk.

7. Si hay texto antes de la primera pregunta (introducción del artículo):
   - Si > 100 tokens → Crear chunk tipo GeneralContent.
   - Si < 100 tokens → Descartar (es probablemente solo el título/intro genérica).

8. Si hay bloques de código (```) dentro de una explicación:
   - Si el código tiene > 200 tokens → Crear chunk adicional tipo CodeExample
     con el contexto mínimo necesario (la pregunta a la que pertenece + el código).
   - Si el código tiene < 200 tokens → Mantener inline en el chunk de la pregunta.
```

### Lógica del `RecursiveTextChunker`

```
PARÁMETROS:
  - target_chunk_size: 750 tokens (configurable)
  - min_chunk_size: 100 tokens
  - max_chunk_size: 1200 tokens
  - overlap_tokens: 75 tokens (10% del target)

SEPARADORES (en orden de prioridad):
  1. Headers Markdown: "\n## ", "\n### ", "\n#### "
  2. Doble newline: "\n\n" (separador de párrafos)
  3. Newline simple: "\n"
  4. Punto seguido de espacio: ". " (separador de oraciones)
  5. Espacio: " " (último recurso, partir por palabras)

ALGORITMO RECURSIVO:

1. Si el texto completo tiene <= max_chunk_size tokens:
   → Devolver como un solo chunk de tipo GeneralContent.
   → FIN.

2. Intentar dividir usando el separador de mayor prioridad disponible:
   a. Split el texto por el separador.
   b. Agrupar los fragmentos resultantes en chunks que no excedan target_chunk_size.
   c. Si algún fragmento individual excede max_chunk_size → Recurrir con el siguiente separador.

3. OVERLAP:
   Para cada chunk (excepto el primero), prepend las últimas overlap_tokens del chunk anterior.
   Esto garantiza que no se pierda contexto entre chunks.

4. Cada chunk generado es tipo GeneralContent con ChunkIndex incremental.

EJEMPLO:

Texto de 3000 tokens sin estructura Q&A:
  Separador "\n\n" lo divide en 5 párrafos de [800, 600, 900, 400, 300] tokens.
  
  Chunk 0: Párrafo 1 (800 tokens) → OK, dentro del rango.
  Chunk 1: Párrafo 2 (600 tokens) + overlap(75) → 675 tokens → OK.
  Chunk 2: Párrafo 3 (900 tokens) + overlap(75) → 975 tokens → OK.
  Chunk 3: Párrafos 4+5 (400+300 = 700 tokens) + overlap(75) → 775 tokens → Fusionados porque individualmente < min_chunk_size.
```

### `ChunkingStrategyFactory`

```csharp
/// <summary>
/// Selecciona automáticamente la estrategia de chunking.
/// Si el documento tiene >= 3 preguntas detectables → InterviewQuestionChunker.
/// Si no → RecursiveTextChunker.
/// </summary>
public interface IChunkingStrategyFactory
{
    IChunkingService SelectStrategy(CleanedDocument document);
}
```

---

## PASO 3: ENRICHMENT (Enriquecimiento de metadata)

### Archivo: `ChunkMetadataEnricher.cs`

Este paso NO modifica el texto del chunk. Solo agrega/valida metadata antes de generar embeddings.

```
PROCESO:

1. Para cada TextChunk:
   a. Copiar metadata de la ScrapedQuestion original:
      - Category, Subcategory, DifficultyLevel, Tags, SourceUrl, SourceName, ScrapedAt
   
   b. Validar Category:
      - Si el chunk es tipo QuestionAnswer y Category == "Unknown":
        → Re-clasificar usando el mismo KeywordClassifier del módulo de scraping.
        → Pasar chunk.QuestionText (o chunk.Text si no hay QuestionText) al clasificador.
   
   c. Detectar tags adicionales del texto del chunk:
      - Buscar menciones de tecnologías conocidas en el texto:
        [".NET", "C#", "Java", "Python", "JavaScript", "TypeScript", "React", "Angular",
         "Vue", "Node.js", "SQL", "PostgreSQL", "MongoDB", "Redis", "Docker",
         "Kubernetes", "AWS", "Azure", "GCP", "REST", "GraphQL", "Git",
         "Linux", "TCP/IP", "HTTP", "HTML", "CSS", "Spring", "Django",
         "Flask", "ASP.NET", "Entity Framework", "Hibernate"]
      - Agregar al array Tags sin duplicados.
   
   d. Calcular TokenCount y CharCount finales del chunk.
   
   e. Asignar Language del CleanedDocument al chunk.

2. Devolver List<TextChunk> enriquecidos listos para embedding.
```

---

## PASO 4: EMBEDDINGS (Generación de vectores con OpenAI)

### Archivo: `OpenAIEmbeddingService.cs`

**Input:** Lista de `TextChunk` con su texto.
**Output:** Lista de `EmbeddedChunk` (TextChunk + vector).

### Modelo de salida

```csharp
public class EmbeddedChunk
{
    public TextChunk Chunk { get; set; } = null!;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public string ModelUsed { get; set; } = string.Empty;        // "text-embedding-3-small"
    public int Dimensions { get; set; }                           // 1536 o 3072
}
```

### Integración con OpenAI API

```
ENDPOINT: POST https://api.openai.com/v1/embeddings

HEADERS:
  Authorization: Bearer {OPENAI_API_KEY}
  Content-Type: application/json

REQUEST BODY:
{
  "input": ["texto del chunk 1", "texto del chunk 2", ...],
  "model": "text-embedding-3-small",
  "encoding_format": "float"
}

RESPONSE:
{
  "object": "list",
  "data": [
    {
      "object": "embedding",
      "index": 0,
      "embedding": [0.0123, -0.0456, 0.0789, ...]   // 1536 floats
    },
    ...
  ],
  "model": "text-embedding-3-small",
  "usage": {
    "prompt_tokens": 1234,
    "total_tokens": 1234
  }
}
```

### Estrategia de batching

```
REGLAS DE BATCHING PARA OPENAI:

- OpenAI acepta hasta 2048 textos por request.
- Pero el total de tokens por request no debe exceder ~8191 tokens por texto individual.
- Límite práctico: enviar batches de 50-100 chunks por request.
- Rate limit de OpenAI Embeddings: 3,000 RPM (requests per minute) y 1,000,000 TPM
  (tokens per minute) en tier 1. Más que suficiente para este proyecto.

ALGORITMO DE BATCHING:

1. Tomar la lista completa de TextChunks a procesar.
2. Dividir en batches de máximo 100 chunks (configurable).
3. Para cada batch:
   a. Extraer los textos: chunks.Select(c => c.Text).ToList()
   b. Enviar POST a OpenAI.
   c. Mapear cada embedding al chunk correspondiente por index.
   d. Esperar 200ms entre batches (rate limit safety).
4. Si un batch falla (HTTP 429 / 500 / timeout):
   a. Esperar con backoff exponencial: 1s, 2s, 4s.
   b. Reintentar hasta 3 veces.
   c. Si sigue fallando → Marcar esos chunks como Failed en ProcessingStatus.
5. Loggear: total chunks procesados, tokens consumidos, costo estimado.

ESTIMACIÓN DE COSTO:
  - text-embedding-3-small: $0.020 por 1M tokens
  - 5000 chunks × 750 tokens promedio = 3,750,000 tokens
  - Costo total: ~$0.075 USD (7.5 centavos)
  - text-embedding-3-large: $0.130 por 1M tokens → ~$0.49 USD
```

### Qué texto enviar al embedding

```
IMPORTANTE: El texto que se envía a OpenAI para generar el embedding determina
la calidad de la búsqueda posterior. NO enviar solo la pregunta ni solo la explicación.

FORMATO RECOMENDADO PARA EL INPUT DEL EMBEDDING:

Para chunks tipo QuestionAnswer:
  "{Category} interview question - {Subcategory}: {QuestionText}\n\n{ExplanationText}"
  
  Ejemplo:
  "Technical interview question - Bases de Datos: ¿Cuál es la diferencia entre 
   INNER JOIN y LEFT JOIN?\n\nINNER JOIN devuelve solo las filas que tienen 
   coincidencia en ambas tablas. LEFT JOIN devuelve todas las filas de la tabla 
   izquierda y las coincidencias de la derecha, rellenando con NULL donde no hay match."

Para chunks tipo CodeExample:
  "Technical coding question: {QuestionText}\n\nCode example:\n{CodeBlock}"

Para chunks tipo GeneralContent:
  "{Text}"  (sin prefijo, enviar tal cual)

Para chunks tipo Explanation:
  "Interview preparation context - {Subcategory}: {Text}"

RAZÓN: Prepender la categoría y subcategoría mejora la relevancia de la búsqueda
porque el embedding captura no solo el contenido sino el contexto de uso.
```

### EmbeddingCache

```
PROPÓSITO: Evitar pagar por embeddings duplicados si se reprocesa un chunk.

ESTRATEGIA:
- Calcular SHA-256 del texto exacto que se envía a OpenAI.
- Antes de llamar a la API, verificar si ese hash ya tiene un embedding en Qdrant.
- Si existe → Reusar el vector existente, no llamar a OpenAI.
- Si no existe → Llamar a OpenAI y almacenar.

NOTA: Esto es una optimización. Para la primera ejecución del pipeline,
no habrá cache. Para re-ejecuciones (e.g., después de actualizar el scraping),
el cache evita reprocesar chunks que no cambiaron.
```

---

## PASO 5: ALMACENAMIENTO VECTORIAL (Qdrant)

### Configuración de Qdrant en Docker

```yaml
# docker-compose.yml (agregar al docker-compose existente si lo hay)
services:
  qdrant:
    image: qdrant/qdrant:v1.13.2
    container_name: interview-simulator-qdrant
    ports:
      - "6333:6333"   # REST API
      - "6334:6334"   # gRPC (opcional, más rápido para producción)
    volumes:
      - qdrant_data:/qdrant/storage
    environment:
      - QDRANT__SERVICE__GRPC_PORT=6334
    restart: unless-stopped

volumes:
  qdrant_data:
    driver: local
```

**Comando para levantar:** `docker compose up -d qdrant`

**Verificar que funciona:** `curl http://localhost:6333/dashboard` o abrir en navegador.

### SDK de Qdrant para .NET

```xml
<PackageReference Include="Qdrant.Client" Version="1.12.*" />
```

### Archivo: `QdrantCollectionManager.cs`

```
GESTIÓN DE COLECCIONES:

El módulo usa UNA colección principal en Qdrant:

  Nombre: "interview_questions"
  Dimensiones del vector: 1536 (para text-embedding-3-small) o 3072 (para large)
  Distancia: Cosine (similitud coseno — estándar para embeddings de OpenAI)

CREACIÓN DE LA COLECCIÓN (si no existe):

  POST http://localhost:6333/collections/interview_questions
  {
    "vectors": {
      "size": 1536,
      "distance": "Cosine"
    },
    "optimizers_config": {
      "indexing_threshold": 20000   // Crear índice HNSW después de 20K puntos
    },
    "on_disk_payload": true         // Guardar payloads en disco (ahorra RAM)
  }

CREAR ÍNDICES EN PAYLOAD FIELDS (para filtrado eficiente):

  PUT http://localhost:6333/collections/interview_questions/index
  {
    "field_name": "category",
    "field_schema": "keyword"
  }

  Repetir para: "subcategory", "difficulty_level", "source_name", "original_language", "chunk_type"
  
  Para "tags" (array):
  {
    "field_name": "tags",
    "field_schema": "keyword"
  }
```

### Archivo: `QdrantVectorStoreService.cs`

```
OPERACIÓN: UPSERT DE PUNTOS

Para cada EmbeddedChunk:
1. Generar un UUID v4 como point ID (o usar el ChunkId del TextChunk).
2. Construir el payload con toda la metadata.
3. Hacer upsert al batch:

  PUT http://localhost:6333/collections/interview_questions/points
  {
    "points": [
      {
        "id": "uuid-del-chunk",
        "vector": [0.0123, -0.0456, ...],
        "payload": {
          "scraped_question_id": 42,
          "chunk_index": 0,
          "chunk_type": "question_answer",
          "text": "...",
          "question_text": "...",
          "category": "Technical",
          "subcategory": "Bases de Datos",
          "difficulty_level": "Junior",
          "tags": ["SQL", "JOIN"],
          "original_language": "es",
          "source_url": "https://...",
          "source_name": "DevTo",
          "scraped_at": "2026-03-01T10:00:00Z",
          "processed_at": "2026-03-09T12:00:00Z",
          "token_count": 487,
          "char_count": 1842
        }
      },
      // ... más puntos
    ]
  }

BATCHING: Enviar máximo 100 puntos por upsert request.

IDEMPOTENCIA: Usar upsert (no insert). Si el punto ya existe con el mismo ID,
se sobreescribe. Esto permite reprocesar sin crear duplicados en Qdrant.
```

### Archivo: `QdrantHealthCheck.cs`

```csharp
/// <summary>
/// Health check para verificar que Qdrant está accesible.
/// Se registra en el health check de ASP.NET Core.
/// GET /health debe incluir el estado de Qdrant.
/// </summary>
public class QdrantHealthCheck : IHealthCheck
{
    // Verifica:
    // 1. Qdrant responde en http://localhost:6333
    // 2. La colección "interview_questions" existe
    // 3. Devuelve el conteo de puntos actual
}
```

---

## PASO 6: RETRIEVAL (Búsqueda y recuperación)

### Archivo: `RetrievalService.cs`

Este es el servicio que será llamado por el futuro módulo del Motor de IA. Dado un perfil de usuario y el contexto de la entrevista, recupera los chunks más relevantes de Qdrant.

### Modelos de entrada y salida

```csharp
public class RetrievalQuery
{
    // Perfil del usuario
    public string? Category { get; set; }                 // "Technical", "Behavioral", etc.
    public string? Subcategory { get; set; }              // "Bases de Datos", "Redes", etc.
    public string? DifficultyLevel { get; set; }          // "Junior", "Mid", "Senior"
    public List<string>? Tags { get; set; }               // ["SQL", ".NET", "Docker"]
    public string? PreferredLanguage { get; set; }        // "es", "en", null (ambos)

    // Contexto de la búsqueda
    public string QueryText { get; set; } = string.Empty; // Texto libre para búsqueda semántica
    public int TopK { get; set; } = 10;                   // Cantidad de chunks a recuperar
    public float MinScore { get; set; } = 0.5f;           // Score mínimo de similitud (0-1)
    
    // Exclusiones (para no repetir preguntas ya hechas en la sesión)
    public List<int>? ExcludeScrapedQuestionIds { get; set; }
}

public class RetrievalResult
{
    public List<RetrievedChunk> Chunks { get; set; } = new();
    public int TotalFound { get; set; }
    public TimeSpan SearchDuration { get; set; }
}

public class RetrievedChunk
{
    public string Text { get; set; } = string.Empty;
    public string? QuestionText { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public string DifficultyLevel { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public float SimilarityScore { get; set; }              // 0-1, qué tan relevante es
    public string ChunkType { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}
```

### Lógica del Retrieval

```
PROCESO DE BÚSQUEDA:

1. CONSTRUIR EL QUERY EMBEDDING:
   a. Tomar el QueryText del RetrievalQuery.
   b. Si QueryText está vacío, construirlo a partir del perfil:
      "Interview questions for {DifficultyLevel} {Subcategory} {Category}"
      Ejemplo: "Interview questions for Junior Bases de Datos Technical"
   c. Enviar a OpenAI Embeddings → Obtener el vector de la query.

2. CONSTRUIR FILTROS DE QDRANT:
   Traducir los campos del RetrievalQuery a un filtro Qdrant.
   
   Ejemplo si el usuario es: Technical, Bases de Datos, Junior, tags: ["SQL", ".NET"]
   
   {
     "must": [
       { "key": "category", "match": { "value": "Technical" } },
       { "key": "difficulty_level", "match": { "value": "Junior" } }
     ],
     "should": [
       { "key": "subcategory", "match": { "value": "Bases de Datos" } },
       { "key": "tags", "match": { "any": ["SQL", ".NET"] } }
     ],
     "must_not": [
       { "key": "scraped_question_id", "match": { "any": [1, 5, 12] } }
     ]
   }
   
   Lógica de filtros:
   - "must" → Category y DifficultyLevel (obligatorios si se proporcionan).
   - "should" → Subcategory y Tags (preferentes pero no excluyentes).
     Usar "should" para que Qdrant también devuelva resultados parciales
     si no hay suficientes que cumplan todo.
   - "must_not" → ExcludeScrapedQuestionIds (preguntas ya usadas).

3. EJECUTAR BÚSQUEDA EN QDRANT:
   
   POST http://localhost:6333/collections/interview_questions/points/search
   {
     "vector": [0.0123, -0.0456, ...],
     "filter": { ... filtros construidos ... },
     "limit": {TopK},
     "score_threshold": {MinScore},
     "with_payload": true
   }

4. POST-PROCESAMIENTO DE RESULTADOS:
   a. Mapear los puntos de Qdrant a RetrievedChunk.
   b. Filtrar chunks con score < MinScore (doble validación).
   c. Ordenar por score descendente.
   d. Deduplicar: si dos chunks vienen de la misma ScrapedQuestion,
      mantener solo el de mayor score.

5. DEVOLVER RetrievalResult con los chunks y metadata de búsqueda.
```

### Archivo: `ContextAssembler.cs`

```
PROPÓSITO: Tomar los chunks recuperados y ensamblar un bloque de contexto
formateado para inyectar en el prompt del LLM.

INPUT: List<RetrievedChunk> (del RetrievalService)
OUTPUT: string (texto formateado para el prompt del LLM)

FORMATO DE SALIDA:

"""
[CONTEXTO DE BASE DE CONOCIMIENTO]

A continuación se presenta información relevante sobre preguntas de entrevistas
laborales para Ingeniería en Sistemas Computacionales. Utiliza esta información
como base para generar preguntas y retroalimentación. No inventes información
que no esté respaldada por este contexto.

---
[Fuente 1: {SourceName}] [Categoría: {Category}/{Subcategory}] [Nivel: {DifficultyLevel}]
{Text del chunk 1}

---
[Fuente 2: {SourceName}] [Categoría: {Category}/{Subcategory}] [Nivel: {DifficultyLevel}]
{Text del chunk 2}

---
[Fuente N: {SourceName}] [Categoría: {Category}/{Subcategory}] [Nivel: {DifficultyLevel}]
{Text del chunk N}

[FIN DEL CONTEXTO]
"""

REGLAS:
- Máximo 5000 tokens de contexto total (configurable).
- Si los chunks exceden el máximo, truncar los de menor score.
- Incluir entre 5 y 10 chunks por contexto.
- Variar las fuentes: no más de 3 chunks de la misma fuente.
```

---

## ORQUESTADOR DEL PIPELINE

### Archivo: `RagPipelineOrchestrator.cs`

```
FLUJO PRINCIPAL:

1. Consultar SQL Server: obtener ScrapedQuestions que NO tienen ProcessingStatus
   o tienen ProcessingStatus.State == Failed con RetryCount < 3.

2. Para cada ScrapedQuestion pendiente:
   a. Crear/actualizar ProcessingStatus con State = Pending.
   
   b. PASO 1 - LIMPIAR:
      - Llamar a ITextCleaner.CleanAsync(rawContent, questionText)
      - Si HasSufficientContent == false → State = Skipped, continuar.
      - Actualizar State = Cleaned.
   
   c. PASO 2 - CHUNKING:
      - Seleccionar estrategia con ChunkingStrategyFactory.
      - Llamar a IChunkingService.ChunkAsync(cleanedDocument)
      - Si no se generaron chunks → State = Skipped, continuar.
      - Actualizar State = Chunked, ChunksGenerated = count.
   
   d. PASO 3 - ENRICHMENT:
      - Llamar a ChunkMetadataEnricher.EnrichAsync(chunks, scrapedQuestion)
   
   e. PASO 4 - EMBEDDING:
      - Llamar a IEmbeddingService.GenerateEmbeddingsAsync(chunks)
      - Actualizar State = Embedded.
   
   f. PASO 5 - ALMACENAR EN QDRANT:
      - Llamar a IVectorStoreService.UpsertAsync(embeddedChunks)
      - Guardar los point IDs retornados en ProcessingStatus.QdrantPointIds.
      - Actualizar State = Stored.
   
   g. Si cualquier paso falla:
      - Capturar excepción.
      - Actualizar State = Failed, ErrorMessage = ex.Message.
      - Incrementar RetryCount.
      - Continuar con la siguiente ScrapedQuestion.

3. Al finalizar, loggear resumen:
   - Total procesadas, exitosas, skipped, failed.
   - Total chunks generados.
   - Total tokens enviados a OpenAI.
   - Costo estimado de embeddings.
   - Tiempo total de ejecución.
```

### Procesamiento por lotes

```
BATCHING DEL PIPELINE:

Para eficiencia, el pipeline NO debe procesar pregunta por pregunta sino
acumular chunks y hacer batch de embeddings y upserts.

FLUJO OPTIMIZADO:

1. Obtener N ScrapedQuestions pendientes (configurable, default 100).
2. Limpiar todas → Lista de CleanedDocuments.
3. Chunkear todas → Lista plana de TextChunks.
4. Enriquecer todas → Lista plana de TextChunks con metadata.
5. Embedding en batches de 100 → Lista de EmbeddedChunks.
6. Upsert en Qdrant en batches de 100.
7. Actualizar ProcessingStatus de cada ScrapedQuestion.

Esto minimiza las llamadas a OpenAI y Qdrant.
```

---

## CONFIGURACIÓN COMPLETA

### `appsettings.json` del Worker

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=InterviewSimulator;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "OpenAI": {
    "ApiKey": "",
    "EmbeddingModel": "text-embedding-3-small",
    "EmbeddingDimensions": 1536,
    "MaxTokensPerRequest": 8191,
    "BatchSize": 100,
    "RequestDelayMs": 200,
    "MaxRetries": 3,
    "BaseUrl": "https://api.openai.com/v1"
  },
  "Qdrant": {
    "Host": "localhost",
    "Port": 6333,
    "GrpcPort": 6334,
    "UseGrpc": false,
    "CollectionName": "interview_questions",
    "UpsertBatchSize": 100,
    "ApiKey": ""
  },
  "RagPipeline": {
    "ProcessingBatchSize": 100,
    "CronSchedule": "0 4 * * *",
    "MaxChunkTokens": 1000,
    "MinChunkTokens": 50,
    "TargetChunkTokens": 750,
    "OverlapTokens": 75,
    "MaxContextTokens": 5000,
    "MaxContextChunks": 10,
    "MinSimilarityScore": 0.5,
    "MaxRetryCount": 3,
    "EnableEmbeddingCache": true
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Qdrant": "Information"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/rag-pipeline-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

### Variables de entorno para secrets

```
NUNCA poner el API key de OpenAI en appsettings.json en producción.
Usar User Secrets en desarrollo y variables de entorno en despliegue.

Desarrollo:
  dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
  
Producción/Docker:
  OPENAI__APIKEY=sk-...
  QDRANT__APIKEY=...  (si se configura autenticación en Qdrant)
```

---

## PAQUETES NUGET REQUERIDOS

```xml
<!-- InterviewSimulator.RAG.Core -->
<!-- Solo modelos e interfaces, sin dependencias externas pesadas -->

<!-- InterviewSimulator.RAG.Processing -->
<PackageReference Include="HtmlAgilityPack" Version="1.11.*" />

<!-- InterviewSimulator.RAG.Embedding -->
<PackageReference Include="Microsoft.Extensions.Http" Version="9.*" />
<PackageReference Include="Polly" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="9.*" />
<PackageReference Include="System.Net.Http.Json" Version="9.*" />

<!-- InterviewSimulator.RAG.VectorStore -->
<PackageReference Include="Qdrant.Client" Version="1.12.*" />

<!-- InterviewSimulator.RAG.Data -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.*" />

<!-- InterviewSimulator.RAG.Worker -->
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.*" />
<PackageReference Include="Serilog" Version="4.*" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.*" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
<PackageReference Include="Serilog.Sinks.File" Version="6.*" />
<PackageReference Include="Cronos" Version="0.8.*" />

<!-- Tests -->
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="FluentAssertions" Version="7.*" />
```

---

## INTERFACES PRINCIPALES

```csharp
// ---- LIMPIEZA ----

public interface ITextCleaner
{
    /// <summary>
    /// Limpia el contenido crudo del scraping y devuelve texto procesado.
    /// </summary>
    Task<CleanedDocument> CleanAsync(string rawContent, string questionText, string sourceName);
}

// ---- CHUNKING ----

public interface IChunkingService
{
    /// <summary>
    /// Divide un documento limpio en chunks.
    /// </summary>
    Task<List<TextChunk>> ChunkAsync(CleanedDocument document);
}

// ---- EMBEDDING ----

public interface IEmbeddingService
{
    /// <summary>
    /// Genera embeddings para una lista de chunks usando OpenAI.
    /// </summary>
    Task<List<EmbeddedChunk>> GenerateEmbeddingsAsync(
        List<TextChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera embedding para un texto individual (usado por retrieval para la query).
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}

// ---- VECTOR STORE ----

public interface IVectorStoreService
{
    /// <summary>
    /// Almacena chunks con sus embeddings en Qdrant.
    /// Retorna los point IDs asignados.
    /// </summary>
    Task<List<Guid>> UpsertAsync(
        List<EmbeddedChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca los chunks más similares a un vector query con filtros opcionales.
    /// </summary>
    Task<List<(string PointId, float Score, Dictionary<string, object> Payload)>> SearchAsync(
        float[] queryVector,
        Dictionary<string, object>? filters = null,
        List<int>? excludeScrapedQuestionIds = null,
        int topK = 10,
        float minScore = 0.5f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina puntos por ScrapedQuestionId (para reprocesar).
    /// </summary>
    Task DeleteByScrapedQuestionIdAsync(
        int scrapedQuestionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene estadísticas de la colección.
    /// </summary>
    Task<(long PointCount, long SegmentCount)> GetCollectionStatsAsync(
        CancellationToken cancellationToken = default);
}

// ---- RETRIEVAL ----

public interface IRetrievalService
{
    /// <summary>
    /// Recupera los chunks más relevantes para un perfil de entrevista.
    /// </summary>
    Task<RetrievalResult> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera chunks y los ensambla en un bloque de contexto
    /// listo para inyectar en el prompt del LLM.
    /// </summary>
    Task<string> RetrieveAndAssembleContextAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default);
}

// ---- ORQUESTADOR ----

public interface IRagPipelineOrchestrator
{
    /// <summary>
    /// Ejecuta el pipeline completo para todas las ScrapedQuestions pendientes.
    /// </summary>
    Task<PipelineResult> ProcessPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reprocesa una ScrapedQuestion específica (útil para debug).
    /// </summary>
    Task<PipelineResult> ReprocessAsync(int scrapedQuestionId, CancellationToken cancellationToken = default);
}

public class PipelineResult
{
    public int TotalProcessed { get; set; }
    public int Successful { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int TotalChunksGenerated { get; set; }
    public int TotalTokensEmbedded { get; set; }
    public decimal EstimatedCostUSD { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
}
```

---

## API ENDPOINTS (Opcional — para integrar con WebAPI existente)

```
// Ejecución del pipeline
POST /api/rag/process              → Ejecutar pipeline para pendientes
POST /api/rag/reprocess/{id}       → Reprocesar una ScrapedQuestion
GET  /api/rag/status               → Estado general: chunks en Qdrant, pendientes, etc.

// Retrieval (será llamado por el Motor de IA)
POST /api/rag/retrieve             → Buscar chunks relevantes dado un perfil
    Body: RetrievalQuery

// Debug y administración
GET  /api/rag/collections/stats    → Estadísticas de Qdrant
GET  /api/rag/processing-status    → Lista de ProcessingStatus con filtros
DELETE /api/rag/collections/reset  → PELIGRO: Borrar y recrear la colección
```

---

## CRITERIOS DE ÉXITO

1. **Pipeline funcional:** Procesar al menos 100 ScrapedQuestions y almacenar sus chunks en Qdrant sin errores.
2. **Chunks de calidad:** Al menos 70% de los chunks deben ser tipo `QuestionAnswer` (no `GeneralContent`).
3. **Retrieval relevante:** Dada una query "SQL interview questions for junior developer", los top 5 resultados deben contener preguntas relacionadas con SQL.
4. **Idempotente:** Ejecutar el pipeline dos veces sobre los mismos datos no debe crear duplicados en Qdrant.
5. **Tracking completo:** Cada ScrapedQuestion debe tener un ProcessingStatus que refleje su estado actual.
6. **Costo controlado:** El procesamiento completo de 5000 chunks no debe exceder $0.50 USD en embeddings.
7. **Logging:** Cada ejecución genera logs detallados con métricas de procesamiento.
8. **Resiliente:** Si OpenAI o Qdrant están caídos, el pipeline falla gracefully y permite retry posterior.

---

## RESTRICCIONES Y REGLAS

1. **Todo en C#/.NET 8+.** Consistente con el proyecto existente.
2. **Async/await everywhere.** No operaciones síncronas para I/O.
3. **Inyección de dependencias nativa.** Registrar todos los servicios en el DI container.
4. **Configuración externalizada.** Todo en `appsettings.json` y User Secrets.
5. **No hardcodear el API key de OpenAI.** Usar User Secrets o env vars.
6. **Entity Framework Core** para la tabla ProcessingStatus.
7. **Serilog** para logging (consistente con el módulo de scraping).
8. **Polly** para retry policies en llamadas HTTP a OpenAI y Qdrant.
9. **XML documentation comments** en interfaces y clases públicas.
10. **El pipeline debe poder ejecutarse independientemente** del WebAPI para testing.
11. **El Retrieval Service debe ser inyectable** en el futuro Motor de IA sin cambios.

---

## ORDEN DE IMPLEMENTACIÓN SUGERIDO

1. **Core:** Modelos (CleanedDocument, TextChunk, EmbeddedChunk, RetrievalQuery/Result), enums, interfaces, configuración.
2. **Data:** ProcessingDbContext, repositorio, migración EF Core.
3. **Processing:** HtmlTextCleaner → TokenCounter → InterviewQuestionChunker → RecursiveTextChunker → ChunkingStrategyFactory → ChunkMetadataEnricher.
4. **Embedding:** OpenAIEmbeddingService con batching y retry.
5. **VectorStore:** QdrantCollectionManager → QdrantVectorStoreService → QdrantHealthCheck.
6. **Retrieval:** QueryBuilder → RetrievalService → ContextAssembler.
7. **Orchestrator:** RagPipelineOrchestrator integrando todos los pasos.
8. **Worker:** Background service con scheduling.
9. **Tests:** Unitarios para cleaner, chunkers, query builder.
10. **API Controller (opcional):** Para ejecución manual y debug.

---

## EJEMPLO DE FLUJO COMPLETO (End-to-End)

```
INPUT: ScrapedQuestion {
  Id: 42,
  QuestionText: "What is the difference between INNER JOIN and LEFT JOIN?",
  RawContent: "<article><h1>Top 20 SQL Interview Questions</h1><p>By John Doe...</p>
    <h2>1. What is the difference between INNER JOIN and LEFT JOIN?</h2>
    <p>INNER JOIN returns only rows that have matching values in both tables.
    LEFT JOIN returns all rows from the left table and the matched rows from
    the right table. If there is no match, NULL values are returned for the
    right table columns.</p>
    <pre><code>SELECT * FROM users INNER JOIN orders ON users.id = orders.user_id;</code></pre>
    <h2>2. What is normalization?</h2>
    <p>Normalization is the process of organizing data...</p>
    ... 18 preguntas más ...",
  Category: Technical,
  Subcategory: "Bases de Datos",
  DifficultyLevel: Junior,
  Tags: ["SQL", "JOIN"],
  OriginalLanguage: "en",
  SourceUrl: "https://dev.to/johndoe/top-20-sql-interview-questions",
  SourceName: "DevTo"
}

PASO 1 - LIMPIEZA:
  → Remueve <article> tags, bio del autor, ads.
  → Preserva estructura: headers como "## 1. What is..."
  → Detecta idioma: "en"
  → Output: CleanedDocument { CleanedText: "## 1. What is the difference...", EstimatedTokenCount: 3200 }

PASO 2 - CHUNKING:
  → ChunkingStrategyFactory detecta 20 preguntas → Usa InterviewQuestionChunker.
  → Genera 20 chunks tipo QuestionAnswer + 2 chunks tipo CodeExample.
  → Chunk 0: "What is the difference between INNER JOIN and LEFT JOIN? INNER JOIN returns..."
  → Chunk 1: "What is normalization? Normalization is the process..."
  → ... 20 chunks más

PASO 3 - ENRICHMENT:
  → Cada chunk recibe: Category=Technical, Subcategory="Bases de Datos",
    DifficultyLevel=Junior, Tags=["SQL", "JOIN"], etc.
  → Chunk 0 detecta tags adicionales: ["INNER JOIN", "LEFT JOIN", "NULL"]

PASO 4 - EMBEDDING:
  → Batch de 22 chunks → OpenAI API → 22 vectores de 1536 dimensiones.
  → Costo: ~0.003 USD

PASO 5 - QDRANT:
  → Upsert 22 puntos en collection "interview_questions".
  → Cada punto tiene vector + payload completo.

PASO 6 - RETRIEVAL (ejemplo futuro):
  → Usuario selecciona: "Backend Developer, SQL, Junior"
  → Query: "SQL interview questions for junior backend developer"
  → Qdrant devuelve: Chunk 0 (score: 0.92), Chunk 3 (score: 0.88), ...
  → ContextAssembler formatea los top 10 chunks.
  → Se inyectan en el prompt del LLM → El LLM genera la pregunta de entrevista.
```
