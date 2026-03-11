# Documentación Completa del Proyecto: Web Scraping Module – Interview Simulator

**Versión:** 1.0  
**Fecha:** 3 de marzo de 2026  
**Proyecto:** InterviewSimulator.Scraping  
**Plataforma:** .NET 8.0  

---

## Tabla de Contenidos

1. [Requerimientos Funcionales](#1-requerimientos-funcionales)
2. [Requerimientos No Funcionales](#2-requerimientos-no-funcionales)
3. [Requerimientos del Producto](#3-requerimientos-del-producto)
4. [Requerimientos de la Organización](#4-requerimientos-de-la-organización)
5. [Requerimientos Externos](#5-requerimientos-externos)
6. [Reglas de Negocio](#6-reglas-de-negocio)
7. [Casos de Uso](#7-casos-de-uso)
8. [Diseño](#8-diseño)
9. [Arquitectura](#9-arquitectura)
10. [Diseño de Base de Datos](#10-diseño-de-base-de-datos)
11. [Modelo de Datos](#11-modelo-de-datos)
12. [Diagrama Entidad-Relación](#12-diagrama-entidad-relación)
13. [Diagramas de Flujo](#13-diagramas-de-flujo)
14. [Diagramas de Actividades](#14-diagramas-de-actividades)
15. [Diagramas de Secuencia](#15-diagramas-de-secuencia)
16. [Mapa de Navegación](#16-mapa-de-navegación)

---

## 1. Requerimientos Funcionales

### RF-001: Extracción automatizada de preguntas de entrevista (Q&A)
- El sistema debe extraer preguntas de entrevista técnica desde **31 fuentes web** distintas (Dev.to, Medium, LeetCode, Glassdoor, Indeed, FreeCodeCamp, GeeksForGeeks, InterviewBit, FullStackCafe, JavaTPoint, TealHQ, KnowledgeHut, Simplilearn, Edureka, CSharpCorner, Baeldung, DotNetTricks, Turing, KeepCoding, Platzi, OpenWebinars, Talently, Epitech, TheBridge, EPAMAnywhere, MicrosoftLearn, MdnWebDocs, W3Schools, RefactoringGuru, DigitalOcean, StackOverflow).
- Cada pregunta extraída debe incluir: texto de la pregunta, texto normalizado, respuesta asociada, URL fuente, idioma original, contenido crudo (raw) y metadatos de scraping.

### RF-002: Extracción de documentos de conocimiento técnico (corpus RAG)
- El sistema debe extraer documentos/chunks de conocimiento técnico de las mismas fuentes web para alimentar un corpus RAG (Retrieval-Augmented Generation).
- Cada documento debe contener: título, contenido completo (con bloques de código preservados), URL fuente, sitio fuente, idioma, conteo de palabras, índice de chunk y referencia al documento padre.

### RF-003: Clasificación automática de preguntas
- El sistema debe clasificar automáticamente cada pregunta por:
  - **Categoría**: Technical, Behavioral, Situational, General, Unknown.
  - **Subcategoría** (solo Technical): Algoritmos y Estructuras de Datos, Bases de Datos, Redes y Sistemas, Desarrollo Web, Desarrollo Backend, DevOps y Cloud, Sistemas Operativos, Ingeniería de Software, Programación General.
  - **Nivel de dificultad**: Junior, Mid, Senior, Unknown.
  - **Tags**: lista de palabras clave detectadas (máx. 10).
  - **Tecnología principal**: Java, C#, Python, JavaScript, React, Angular, SQL, Docker, Kubernetes, AWS, Azure, etc.
- La clasificación se basa en keywords, patrones regex y score de confianza (0.0 - 1.0).

### RF-004: Clasificación automática de documentos RAG
- El sistema debe clasificar documentos de conocimiento por:
  - **Categoría de contenido** (ContentCategory): 130+ categorías que abarcan lenguajes, frameworks, BD, DevOps, conceptos de programación, arquitectura, estructuras de datos, algoritmos, fundamentos CS y áreas especializadas.
  - **Tipo de contenido** (ContentType): Documentation, Tutorial, Article, Reference, Guide, Cheatsheet, Pattern, Comparison, InterviewQA, GitHubRepo.
  - **Dificultad**: Junior, Mid, Senior.
  - **Tags y tecnología** detectados.
  - **Score de confianza** de la clasificación.

### RF-005: Deduplicación de contenido
- **Capa 1 (Exact Match)**: El sistema debe calcular un hash SHA-256 sobre el texto normalizado (lowercase, sin artículos en español/inglés, sin puntuación) para cada pregunta y documento.
- El hash (`HashFingerprint`) debe tener un índice único en la base de datos para evitar duplicados exactos.
- El sistema debe marcar preguntas/documentos como duplicados (`IsDuplicate`) y referenciar al original (`DuplicateOfId`).

### RF-006: Filtrado de relevancia IT
- El sistema debe filtrar automáticamente contenido no relevante para IT/desarrollo de software.
- Para preguntas Q&A: usando keywords de IT y una blacklist de temas no relacionados (cocina, deportes, moda, etc.).
- Para documentos RAG: usando `IsITRelevantContent()` que valida presencia de términos técnicos en título y contenido.

### RF-007: Filtrado por idioma
- Las preguntas Q&A solo deben almacenarse si están en **español** (`OriginalLanguage == "es"`).
- El sistema debe detectar idioma (español, inglés, portugués) analizando palabras, acentos y patrones lingüísticos exclusivos.
- Las preguntas en portugués deben rechazarse explícitamente (detección de rúbricas de evaluación brasileñas).

### RF-008: Filtrado por calidad de respuesta
- Solo se persisten preguntas que tengan una respuesta asociada no vacía con al menos 20 caracteres.
- Las respuestas se truncan a un máximo de 4,000 caracteres.
- Se rechazan respuestas que contengan rúbricas de evaluación (Nível/Resposta/Indicadores/Dedução).

### RF-009: Chunking de documentos largos
- Los documentos largos deben dividirse en chunks de 500-1,500 palabras.
- La división prioriza headers (H2/H3) como puntos de corte; si no hay headers suficientes, divide por párrafos.
- Cada chunk mantiene referencia al documento padre (`ParentDocumentId`) y su índice (`ChunkIndex`).

### RF-010: Conversión de HTML a Markdown
- El sistema debe convertir contenido HTML extraído a formato Markdown limpio, preservando:
  - Bloques de código (`<pre><code>` → `````)
  - Headers (`<h1>` → `#`, `<h2>` → `##`, etc.)
  - Listas (`<li>` → `- `)
  - Formato bold/italic (`<strong>` → `**`, `<em>` → `*`)
  - Código inline (`<code>` → `` ` ``)

### RF-011: Ejecución programada por cron
- El sistema ejecuta los scrapers automáticamente según una expresión cron configurable (default: `0 3 * * *` — todos los días a las 3:00 AM UTC).
- Se ejecuta inmediatamente al iniciar el servicio y luego según el cron.

### RF-012: Control de frecuencia por fuente
- Cada fuente tiene una frecuencia de scraping configurable en horas (FrequencyHours).
- El orquestador verifica si la fuente necesita scraping comparando `LastScrapedAt + FrequencyHours` contra la hora actual.
- Frecuencias configuradas: desde 12h (Dev.to) hasta 336h/2 semanas (RefactoringGuru).

### RF-013: Gestión de fuentes de scraping
- El sistema debe mantener un catálogo de fuentes (`ScrapedSources`) con: nombre, URL base, tipo de fuente, estado activo/inactivo, frecuencia de scraping y última ejecución.
- Soporta 10 tipos de fuente: BlogPlatform, JobBoard, CodingPlatform, ProfessionalNetwork, Forum, OfficialDocumentation, TechnicalEncyclopedia, EducationalPlatform, GitHubRepository, ArchitectureReference.

### RF-014: Registro de jobs de scraping
- Cada ejecución de un scraper genera un job (`ScrapingJob`) que registra: fuente, hora de inicio/fin, estado, preguntas encontradas/nuevas, documentos encontrados/nuevos, y mensaje de error.
- Estados del job: Pending, Running, Completed, CompletedWithErrors, Failed.

### RF-015: Búsqueda y consulta de preguntas
- El sistema provee métodos de búsqueda de preguntas por: tecnología, categoría, nivel de dificultad, si tiene respuesta (con paginación skip/take).
- Se pueden obtener las tecnologías disponibles (`GetAvailableTechnologiesAsync`).

### RF-016: Búsqueda y consulta de documentos RAG
- El sistema provee métodos de búsqueda de documentos por: tecnología, categoría de contenido, tipo de contenido, dificultad, idioma (con paginación skip/take).
- Se pueden obtener conteos por categoría y tipo de contenido.

### RF-017: Habilitación/deshabilitación selectiva de scrapers
- A través de configuración (`EnabledScrapers` y `Scrapers[name].Enabled`), cada scraper puede habilitarse o deshabilitarse individualmente sin afectar a los demás.

---

## 2. Requerimientos No Funcionales

### RNF-001: Rendimiento
- Delay aleatorio (jitter) entre requests de 2,000-5,000 ms para simular tráfico humano y evitar bloqueos.
- Timeout por request de 30 segundos.
- Máximo de páginas por fuente configurable (entre 3 y 80 según la fuente).

### RNF-002: Resiliencia y tolerancia a fallos
- **Retry con backoff exponencial**: 3 reintentos con espera de 2^n segundos usando Polly.
- Manejo de HTTP 429 (Too Many Requests) con reintento automático.
- Los errores en un scraper individual no detienen la ejecución de los demás scrapers.
- El orquestador captura excepciones por scraper y continúa con el siguiente.

### RNF-003: Escalabilidad
- Arquitectura modular que permite agregar nuevos scrapers implementando la interfaz `IScraper`.
- Registro por DI de scrapers con HttpClients nombrados.
- Soporte para 31+ fuentes simultáneas con configuración independiente.

### RNF-004: Logging y observabilidad
- Logging estructurado con **Serilog** a consola y archivo (rolling diario, retención de 30 archivos).
- Logs de inicio/fin de cada scraper con métricas (preguntas encontradas, nuevas, duplicadas, documentos, errores, duración).
- Resumen consolidado al finalizar cada sesión de scraping.

### RNF-005: Seguridad/Anti-detección
- **Rotación de User-Agent**: 5 user-agents diferentes (Chrome Windows, Chrome Mac, Firefox, Safari, Chrome Linux) seleccionados aleatoriamente.
- Rate limiting con jitter aleatorio para evitar patrones detectables.
- Soporte para browser automation headless (Playwright) para sitios con anti-bot (Glassdoor, Medium).

### RNF-006: Persistencia y consistencia de datos
- Base de datos SQL Server con Entity Framework Core 8.x.
- Índices únicos en `HashFingerprint` para garantizar no-duplicación a nivel de BD.
- Índices en campos de búsqueda frecuente: Category, DifficultyLevel, Technology, Language, SourceSite.
- Índice compuesto `IX_ScrapedDocuments_Category_Tech_Lang` para búsquedas combinadas.
- Delete behavior `Restrict` en relaciones Source→Questions/Documents/Jobs y `NoAction` en auto-referencias.

### RNF-007: Mantenibilidad
- Separación en 5 proyectos con responsabilidades claras.
- Interfaces bien definidas para cada componente (IScraper, IQuestionClassifier, IContentClassifier, IScrapedDataRepository, IScrapingOrchestrator).
- Reglas de clasificación externalizadas en `classification_rules.json`.
- Tests unitarios con xUnit + Moq cubriendo: clasificación, deduplicación, parsing de preguntas, extracción de documentos y chunking.

### RNF-008: Configurabilidad
- Toda la configuración en `appsettings.json` sin recompilación.
- Configuración por fuente individual (Enabled, FrequencyHours, MaxPages/MaxProblems).
- Configuración global (delays, timeouts, retries, user-agents, cron schedule).

### RNF-009: Disponibilidad
- El servicio funciona como **Background Service** (.NET Hosted Service) que se ejecuta de forma continua.
- Ante errores críticos, se loguea pero no se detiene el proceso (catch en `RunScrapingAsync`).

---

## 3. Requerimientos del Producto

### RP-001: Plataforma y runtime
- .NET 8.0 como plataforma de ejecución.
- Compatible con Windows (desarrollo en Windows confirmado).
- SQL Server como motor de base de datos.

### RP-002: Stack tecnológico requerido
| Componente | Tecnología | Versión |
|---|---|---|
| Runtime | .NET | 8.0 |
| ORM | Entity Framework Core | 8.x |
| Browser Automation | Microsoft Playwright | Latest |
| HTML Parsing | HtmlAgilityPack + AngleSharp | Latest |
| Resiliencia HTTP | Polly | Latest |
| Logging | Serilog (Console + File) | Latest |
| Scheduling | Cronos | Latest |
| Tests | xUnit + Moq | Latest |

### RP-003: Formato de almacenamiento
- Preguntas: texto normalizado max. 2,000 caracteres, respuestas nvarchar(max), tags como JSON array (max. 500 chars).
- Documentos: contenido nvarchar(max), tags hasta 1,000 chars, título max. 500 chars.
- Hashes: SHA-256 como string hexadecimal lowercase de 64 caracteres.

### RP-004: Interfaz del módulo
- Este módulo es un **Worker Service** (background service sin UI).
- Punto de entrada: `dotnet run --project src/InterviewSimulator.Scraping.Worker`.
- Forma parte de un sistema mayor (Interview Simulator) como módulo de recopilación de datos.

---

## 4. Requerimientos de la Organización

### RO-001: Estándares de codificación
- Clean Architecture con separación en capas (Core, Data, Classifier, Scrapers, Worker).
- Patrón Repository para acceso a datos.
- Inyección de dependencias para todos los servicios.
- Nomenclatura en inglés para código, con comentarios y logs en español.
- Configuración externalizada en archivos JSON.

### RO-002: Estándares de testing
- Tests unitarios obligatorios con xUnit.
- Mocking con Moq para dependencias externas.
- Cobertura en: clasificación de preguntas, clasificación de documentos, deduplicación (normalización + hashing), parsing/extracción de preguntas, extracción de documentos y chunking.

### RO-003: Control de versiones
- Solución Visual Studio (.sln) con estructura src/tests.
- Compilación con `dotnet build`, ejecución de tests con `dotnet test`.

### RO-004: Documentación
- README.md con instrucciones de compilación, ejecución y configuración.
- Comentarios XML en interfaces y clases principales.
- Reglas de clasificación documentadas en JSON configurable.

---

## 5. Requerimientos Externos

### RE-001: Fuentes de datos externas
- El sistema depende de la disponibilidad de 31 sitios web externos.
- Cada sitio puede cambiar su estructura HTML, requiriendo actualización del scraper correspondiente.
- Algunas fuentes requieren browser automation (Playwright) por protecciones anti-bot.

### RE-002: Cumplimiento legal y ético
- Rate limiting obligatorio para no sobrecargar servidores externos.
- Rotación de User-Agent para cumplir con mejores prácticas de scraping.
- Respeto de frecuencias de scraping razonables (mínimo 12h entre ejecuciones por fuente).

### RE-003: Dependencia de infraestructura
- SQL Server local o remoto accesible (cadena de conexión configurable).
- Conectividad a Internet para acceder a las fuentes de scraping.
- Permisos para instalar Playwright browsers en el primer uso.

### RE-004: Interoperabilidad
- Los datos extraídos (preguntas Q&A y documentos RAG) se almacenan en SQL Server para ser consumidos por otros módulos del Interview Simulator.
- Los documentos RAG están diseñados para alimentar un sistema de Retrieval-Augmented Generation.
- Las preguntas Q&A están diseñadas para alimentar un simulador de entrevistas.

---

## 6. Reglas de Negocio

### RN-001: Validación de preguntas
- Una pregunta válida debe:
  - Tener entre 20 y 500 caracteres.
  - Contener un signo de interrogación `?` antes del carácter 400.
  - No ser un texto narrativo (detectando inicios como "paso 1:", "introducción", etc.).
  - No contener rúbricas de evaluación brasileñas.
  - No estar en portugués.
  - No ser una frase vaga de menos de 30 caracteres.

### RN-002: Validación de respuestas
- Una respuesta válida debe tener al menos 20 caracteres después de la limpieza HTML.
- Las respuestas en portugués son rechazadas.
- Las respuestas con rúbricas de evaluación son rechazadas.
- Longitud máxima truncada a 4,000 caracteres.

### RN-003: Pipeline de procesamiento de preguntas Q&A
1. Asignar SourceId.
2. **Filtro 1**: Verificar que la pregunta tenga respuesta (≥20 chars).
3. **Filtro 2**: Verificar que esté en español.
4. **Filtro 3**: Verificar relevancia IT (keywords + blacklist).
5. Clasificar (categoría, subcategoría, dificultad, tags, tecnología).
6. Verificar deduplicación por hash SHA-256.
7. Persistir en base de datos.

### RN-004: Pipeline de procesamiento de documentos RAG
1. Asignar SourceId.
2. **Filtro**: Verificar relevancia IT del contenido.
3. Clasificar (categoría, subcategoría, tipo de contenido, dificultad, tags, tecnología, confianza).
4. Verificar deduplicación por hash SHA-256.
5. Persistir en base de datos.

### RN-005: Frecuencia de scraping
- Cada fuente tiene su propia frecuencia configurable.
- Si `DateTime.UtcNow < LastScrapedAt + FrequencyHours`, el scraper no se ejecuta.
- Si la fuente nunca ha sido scrapeada (`LastScrapedAt == null`), se ejecuta inmediatamente.

### RN-006: Categorías de clasificación de preguntas
| Categoría | Descripción |
|---|---|
| Technical | Preguntas sobre tecnología, programación, algoritmos, etc. |
| Behavioral | Preguntas sobre comportamiento pasado y experiencias |
| Situational | Preguntas hipotéticas "¿qué harías si...?" |
| General | Preguntas generales de entrevista (motivación, carrera) |

### RN-007: Niveles de dificultad
| Nivel | Indicadores |
|---|---|
| Junior | basic, what is, explain, define, simple, beginner, fundamental |
| Mid | implement, design, compare, optimize, trade-off |
| Senior | architect, scale, distributed, system design, fault tolerance, high availability |

### RN-008: Normalización de texto para deduplicación
- Se convierte a lowercase.
- Se remueven artículos en inglés (the, a, an) y español (el, la, un, una, los, las, unos, unas).
- Se elimina toda puntuación.
- Se normalizan espacios múltiples a uno solo.
- Para documentos: se remueven bloques de código y se trunca a 500 caracteres antes del hash.

### RN-009: Clasificación por score
- La categoría con mayor score de keywords/patterns gana.
- Se requiere un score mínimo de 2 puntos para clasificar (umbral de confianza).
- Los patterns regex valen 3 puntos; los keywords valen 1 punto.
- La confianza = score_ganador / score_total.

### RN-010: Tipos de fuente y su clasificación
| Tipo de Fuente | Ejemplos |
|---|---|
| BlogPlatform | Dev.to, Medium |
| JobBoard | Glassdoor, Indeed |
| CodingPlatform | LeetCode |
| OfficialDocumentation | MicrosoftLearn, MdnWebDocs |
| TechnicalEncyclopedia | W3Schools, GeeksForGeeks |
| EducationalPlatform | Platzi, KeepCoding, Simplilearn |
| ArchitectureReference | RefactoringGuru |
| Forum | StackOverflow |

---

## 7. Casos de Uso

### CU-001: Ejecutar scraping programado automático
- **Actor**: Sistema (Cron Scheduler)
- **Precondición**: El Worker Service está en ejecución.
- **Flujo principal**:
  1. El ScrapingWorker detecta que es hora de ejecutar (según cron o primera ejecución).
  2. Crea un scope de DI y obtiene el IScrapingOrchestrator.
  3. Invoca `RunAllScrapersAsync()`.
  4. El orquestador itera sobre cada scraper habilitado.
  5. Verifica frecuencia de cada fuente.
  6. Ejecuta los scrapers que lo necesitan.
  7. Procesa, clasifica, deduplica y persiste los resultados.
  8. Registra resumen en logs.
- **Postcondición**: Nuevas preguntas y documentos almacenados en BD. Jobs de scraping registrados.

### CU-002: Ejecutar un scraper específico
- **Actor**: Sistema (invocación programática)
- **Precondición**: El scraper solicitado existe en el sistema.
- **Flujo principal**:
  1. Se invoca `RunScraperAsync(sourceName)`.
  2. Se busca el scraper por nombre.
  3. Se ejecuta el pipeline completo (scrape → clasificar → deduplicar → persistir).
- **Flujo alternativo**: Si el scraper no existe, retorna error con `Success = false`.

### CU-003: Extraer preguntas Q&A de una fuente web
- **Actor**: Scraper individual
- **Precondición**: El scraper está habilitado y la fuente es accesible.
- **Flujo principal**:
  1. El scraper navega a las URLs de la fuente.
  2. Aplica rate limiting entre requests.
  3. Parsea el HTML/API para extraer pares pregunta-respuesta.
  4. Crea objetos `ScrapedQuestion` con normalización y hash.
  5. Retorna `ScrapingResult` con la lista de preguntas.

### CU-004: Extraer documentos técnicos para corpus RAG
- **Actor**: Scraper individual
- **Precondición**: El scraper soporta extracción de documentos.
- **Flujo principal**:
  1. El scraper navega a las URLs de la fuente.
  2. Extrae contenido HTML de las páginas.
  3. Convierte HTML a Markdown limpio.
  4. Divide contenido largo en chunks (500-1,500 palabras).
  5. Crea objetos `ScrapedDocument` para cada chunk.
  6. Retorna `ScrapingResult` con la lista de documentos.

### CU-005: Clasificar una pregunta de entrevista
- **Actor**: Orquestador (durante procesamiento)
- **Precondición**: La pregunta ha pasado los filtros de calidad e idioma.
- **Flujo principal**:
  1. Se normaliza el texto de la pregunta.
  2. Se calcula score por cada categoría (keywords + patterns).
  3. Se selecciona la categoría ganadora (score ≥ 2).
  4. Se determina subcategoría técnica si aplica.
  5. Se determina dificultad por indicadores.
  6. Se detecta tecnología principal.
  7. Se extraen tags.
  8. Se retorna `ClassificationResult`.

### CU-006: Clasificar un documento RAG
- **Actor**: Orquestador (durante procesamiento)
- **Precondición**: El documento ha pasado el filtro de relevancia IT.
- **Flujo principal**:
  1. Se normalizan título y contenido.
  2. Se calcula score por cada ContentCategory (130+ categorías).
  3. Se determina tipo de contenido (Tutorial, Documentation, Article, etc.).
  4. Se determina dificultad.
  5. Se extraen tags (tecnología + transversales).
  6. Se calcula score de confianza.
  7. Se retorna `DocumentClassificationResult`.

### CU-007: Deduplicar contenido
- **Actor**: Orquestador (durante procesamiento)
- **Precondición**: El contenido ha sido clasificado.
- **Flujo principal**:
  1. Se normaliza el texto (lowercase, sin artículos, sin puntuación).
  2. Se calcula SHA-256 del texto normalizado.
  3. Se consulta la BD por `HashFingerprint`.
  4. Si existe: incrementar contador de duplicados, marcar como duplicado.
  5. Si no existe: persistir como nuevo.

### CU-008: Consultar preguntas almacenadas
- **Actor**: Módulo consumidor (otro componente del Interview Simulator)
- **Precondición**: Existen preguntas en la BD.
- **Flujo principal**:
  1. Se invoca el repositorio con filtros opcionales (tecnología, categoría, dificultad, tiene respuesta).
  2. Se retornan preguntas activas, no duplicadas, ordenadas por fecha desc.
  3. Soporte de paginación (skip/take).

### CU-009: Consultar documentos RAG almacenados
- **Actor**: Sistema RAG (otro componente del Interview Simulator)
- **Precondición**: Existen documentos en la BD.
- **Flujo principal**:
  1. Se invoca el repositorio con filtros opcionales (tecnología, categoría, tipo, dificultad, idioma).
  2. Se retornan documentos activos, no duplicados, ordenados por fecha desc.
  3. Soporte de paginación (skip/take).

---

## 8. Diseño

### 8.1 Patrones de diseño utilizados

| Patrón | Aplicación en el proyecto |
|---|---|
| **Strategy** | Cada scraper implementa `IScraper` con su propia estrategia de extracción |
| **Template Method** | `BaseScraper` define el esqueleto del algoritmo de scraping; clases hijas implementan `ScrapeAsync()` |
| **Repository** | `ScrapedDataRepository` abstrae el acceso a datos detrás de `IScrapedDataRepository` |
| **Orchestrator** | `ScrapingOrchestrator` coordina la ejecución de todos los scrapers |
| **Factory** | HttpClient factory con `IHttpClientFactory` para crear clientes nombrados por scraper |
| **Dependency Injection** | Todos los componentes se registran y resuelven via DI del host .NET |
| **Options Pattern** | `ScrapingSettings` binding desde `appsettings.json` con `IOptions<T>` |
| **Worker Service** | `ScrapingWorker` como Background Service de .NET |
| **Chain of Responsibility** | Pipeline de procesamiento: filtro de respuesta → filtro idioma → filtro IT → clasificación → deduplicación |

### 8.2 Principios SOLID aplicados

| Principio | Aplicación |
|---|---|
| **S** – Single Responsibility | Cada proyecto tiene una responsabilidad única (Core=contratos, Data=persistencia, Classifier=clasificación, Scrapers=extracción, Worker=ejecución) |
| **O** – Open/Closed | Nuevos scrapers se agregan implementando `IScraper` sin modificar el orquestador |
| **L** – Liskov Substitution | Todos los scrapers son intercambiables a través de `IScraper` |
| **I** – Interface Segregation | Clasificadores separados: `IQuestionClassifier` e `IContentClassifier` |
| **D** – Dependency Inversion | Las capas altas (Worker, Scrapers) dependen de abstracciones (interfaces en Core) |

---

## 9. Arquitectura

### 9.1 Arquitectura Lógica

El sistema sigue una **arquitectura en capas (Layered Architecture)** con separación clara de responsabilidades:

```
┌─────────────────────────────────────────────────────────┐
│                    Worker (Punto de Entrada)             │
│          ScrapingWorker (BackgroundService + Cron)       │
├─────────────────────────────────────────────────────────┤
│              Scrapers (Lógica de Extracción)             │
│  ScrapingOrchestrator + 31 Scrapers concretos           │
│  BaseScraper (Template Method + funcionalidad común)     │
├─────────────────────────────────────────────────────────┤
│            Classifier (Lógica de Clasificación)          │
│  KeywordClassifier (Q&A) + ContentClassifier (RAG)      │
├─────────────────────────────────────────────────────────┤
│              Data (Acceso a Datos/Persistencia)          │
│  ScrapingDbContext + ScrapedDataRepository (EF Core)     │
├─────────────────────────────────────────────────────────┤
│                Core (Contratos y Modelos)                │
│  Interfaces, Models, Enums, Configuration               │
└─────────────────────────────────────────────────────────┘
```

### 9.2 Dependencias entre proyectos

```
Worker → Scrapers, Classifier, Data, Core
Scrapers → Core
Classifier → Core
Data → Core
Core → (ninguna dependencia interna)
```

### 9.3 Arquitectura de despliegue

```
┌─────────────────────┐     ┌─────────────────────────┐
│  Worker Service      │────▶│  SQL Server              │
│  (.NET 8 Host)       │     │  (InterviewSimulator DB) │
│                      │     └─────────────────────────┘
│  ┌────────────────┐  │
│  │ ScrapingWorker  │  │     ┌───────────────────────┐
│  │ (Cron-based)    │  │────▶│  31 Sitios Web         │
│  └────────────────┘  │     │  (HTTP/HTTPS)          │
│                      │     └───────────────────────┘
│  ┌────────────────┐  │
│  │ Serilog         │  │────▶ Archivos de log (logs/)
│  └────────────────┘  │
└─────────────────────┘
```

---

## 10. Diseño de Base de Datos

### 10.1 Tablas

#### Tabla: `ScrapedSources`
| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| Id | int | PK, Identity | Identificador único |
| Name | nvarchar(100) | NOT NULL, UNIQUE | Nombre de la fuente (DevTo, Medium, etc.) |
| BaseUrl | nvarchar(500) | NOT NULL | URL base de la fuente |
| SourceType | int | NOT NULL | Enum: BlogPlatform(0), JobBoard(1), etc. |
| IsActive | bit | NOT NULL, DEFAULT true | Si la fuente está activa |
| ScrapingFrequencyHours | int | NOT NULL, DEFAULT 24 | Frecuencia de scraping en horas |
| LastScrapedAt | datetime2 | NULL | Última ejecución de scraping |
| CreatedAt | datetime2 | NOT NULL | Fecha de creación |
| Notes | nvarchar(500) | NULL | Notas adicionales |

#### Tabla: `ScrapedQuestions`
| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| Id | int | PK, Identity | Identificador único |
| SourceId | int | FK → ScrapedSources(Id), NOT NULL | Fuente de origen |
| QuestionText | nvarchar(2000) | NOT NULL | Texto completo de la pregunta |
| QuestionTextNormalized | nvarchar(2000) | NOT NULL | Texto normalizado para dedup |
| Category | int | NOT NULL | Enum: Technical(0), Behavioral(1), etc. |
| Subcategory | nvarchar(200) | NULL | Subcategoría técnica |
| DifficultyLevel | int | NOT NULL | Enum: Junior(0), Mid(1), Senior(2) |
| Tags | nvarchar(500) | NULL | JSON array de tags |
| OriginalLanguage | nvarchar(10) | DEFAULT 'en' | Idioma: es, en |
| SourceUrl | nvarchar(1000) | NULL | URL exacta |
| AnswerText | nvarchar(max) | NULL | Texto de la respuesta |
| Technology | nvarchar(100) | NULL | Tecnología principal |
| RawContent | nvarchar(max) | NULL | Contenido crudo |
| IsActive | bit | NOT NULL, DEFAULT true | Si está activa |
| IsDuplicate | bit | NOT NULL | Si es duplicada |
| DuplicateOfId | int | FK → ScrapedQuestions(Id), NULL | Referencia al original |
| HashFingerprint | nvarchar(64) | NOT NULL, UNIQUE | SHA-256 para dedup |
| ScrapedAt | datetime2 | NOT NULL | Fecha de extracción |
| CreatedAt | datetime2 | NOT NULL | Fecha de creación |
| UpdatedAt | datetime2 | NULL | Última actualización |

#### Tabla: `ScrapedDocuments`
| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| Id | int | PK, Identity | Identificador único |
| SourceId | int | FK → ScrapedSources(Id), NOT NULL | Fuente de origen |
| Title | nvarchar(500) | NOT NULL | Título del documento |
| Content | nvarchar(max) | NOT NULL | Contenido completo |
| ContentNormalized | nvarchar(500) | NOT NULL | Normalizado para dedup |
| Category | int | NOT NULL | Enum ContentCategory |
| Subcategory | nvarchar(200) | NULL | Subcategoría específica |
| Tags | nvarchar(1000) | NULL | JSON array de tags |
| SourceUrl | nvarchar(1000) | NOT NULL | URL exacta |
| SourceSite | nvarchar(200) | NOT NULL | Nombre del sitio fuente |
| Language | nvarchar(10) | NOT NULL, DEFAULT 'en' | Idioma: es, en |
| ContentType | int | NOT NULL | Enum ContentType |
| Difficulty | int | NOT NULL | Enum DifficultyLevel |
| Technology | nvarchar(100) | NULL | Tecnología principal |
| WordCount | int | NOT NULL | Número de palabras |
| ChunkIndex | int | NOT NULL | Índice del chunk (0=primero) |
| ParentDocumentId | int | FK → ScrapedDocuments(Id), NULL | Doc padre si es chunk |
| HashFingerprint | nvarchar(64) | NOT NULL, UNIQUE | SHA-256 para dedup |
| IsActive | bit | NOT NULL, DEFAULT true | Si está activo |
| IsDuplicate | bit | NOT NULL | Si es duplicado |
| DuplicateOfId | int | FK → ScrapedDocuments(Id), NULL | Referencia al original |
| ClassificationConfidence | float | NOT NULL | Score de confianza (0-1) |
| ScrapedAt | datetime2 | NOT NULL | Fecha de extracción |
| CreatedAt | datetime2 | NOT NULL | Fecha de creación |
| UpdatedAt | datetime2 | NULL | Última actualización |

#### Tabla: `ScrapingJobs`
| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| Id | int | PK, Identity | Identificador único |
| SourceId | int | FK → ScrapedSources(Id), NOT NULL | Fuente del job |
| StartedAt | datetime2 | NOT NULL | Hora de inicio |
| FinishedAt | datetime2 | NULL | Hora de fin |
| Status | int | NOT NULL | Enum ScrapingStatus |
| QuestionsFound | int | NOT NULL | Preguntas encontradas |
| QuestionsNew | int | NOT NULL | Preguntas nuevas |
| DocumentsFound | int | NOT NULL | Documentos encontrados |
| DocumentsNew | int | NOT NULL | Documentos nuevos |
| ErrorMessage | nvarchar(max) | NULL | Mensaje de error |
| CreatedAt | datetime2 | NOT NULL | Fecha de creación |

### 10.2 Índices

| Tabla | Índice | Tipo | Columnas |
|---|---|---|---|
| ScrapedSources | IX_ScrapedSources_Name | Único | Name |
| ScrapedQuestions | IX_ScrapedQuestions_HashFingerprint | Único | HashFingerprint |
| ScrapedQuestions | IX_ScrapedQuestions_Category | Normal | Category |
| ScrapedQuestions | IX_ScrapedQuestions_DifficultyLevel | Normal | DifficultyLevel |
| ScrapedQuestions | IX_ScrapedQuestions_Technology | Normal | Technology |
| ScrapedDocuments | IX_ScrapedDocuments_HashFingerprint | Único | HashFingerprint |
| ScrapedDocuments | IX_ScrapedDocuments_Category | Normal | Category |
| ScrapedDocuments | IX_ScrapedDocuments_ContentType | Normal | ContentType |
| ScrapedDocuments | IX_ScrapedDocuments_Difficulty | Normal | Difficulty |
| ScrapedDocuments | IX_ScrapedDocuments_Technology | Normal | Technology |
| ScrapedDocuments | IX_ScrapedDocuments_Language | Normal | Language |
| ScrapedDocuments | IX_ScrapedDocuments_SourceSite | Normal | SourceSite |
| ScrapedDocuments | IX_ScrapedDocuments_Category_Tech_Lang | Compuesto | Category, Technology, Language |
| ScrapingJobs | IX_ScrapingJobs_Status | Normal | Status |
| ScrapingJobs | IX_ScrapingJobs_StartedAt | Normal | StartedAt |

---

## 11. Modelo de Datos

### 11.1 Entidades

```
ScrapedSource (1) ──────< (N) ScrapedQuestion
ScrapedSource (1) ──────< (N) ScrapedDocument
ScrapedSource (1) ──────< (N) ScrapingJob
ScrapedQuestion (1) ───── (0..1) ScrapedQuestion [DuplicateOf]
ScrapedDocument (1) ──────< (N) ScrapedDocument [Chunks/ParentDocument]
ScrapedDocument (1) ───── (0..1) ScrapedDocument [DuplicateOf]
```

### 11.2 Enumeraciones del modelo

**QuestionCategory**: Technical(0), Behavioral(1), Situational(2), General(3), Unknown(99)

**ContentCategory**: Java(1)..Cpp(13), React(20)..FastApi(34), Sql(40)..SQLite(47), Docker(50)..GitHubActions(60), Oop(70)..Tdd(77), Microservices(80)..Ddd(87), Arrays(90)..Heaps(97), Sorting(100)..ComplexityAnalysis(105), OperatingSystems(110)..MemoryManagement(114), Backend(120)..QaTesting(129), General(200), Unknown(999)

**ContentType**: Documentation(0), Tutorial(1), Article(2), Reference(3), Guide(4), Cheatsheet(5), Pattern(6), Comparison(7), InterviewQA(8), GitHubRepo(9), Unknown(99)

**DifficultyLevel**: Junior(0), Mid(1), Senior(2), Unknown(99)

**ScrapingStatus**: Pending(0), Running(1), Completed(2), CompletedWithErrors(3), Failed(4)

**SourceType**: BlogPlatform(0), JobBoard(1), CodingPlatform(2), ProfessionalNetwork(3), Forum(4), OfficialDocumentation(5), TechnicalEncyclopedia(6), EducationalPlatform(7), GitHubRepository(8), ArchitectureReference(9), Other(99)

---

## 12. Diagrama Entidad-Relación

```
┌───────────────────────────┐
│      ScrapedSources       │
├───────────────────────────┤
│ PK  Id           int      │
│     Name         nvc(100) │ ◄── UNIQUE
│     BaseUrl      nvc(500) │
│     SourceType   int      │
│     IsActive     bit      │
│     ScrapingFreq int      │
│     LastScrapedAt dt2     │
│     CreatedAt    dt2      │
│     Notes        nvc(500) │
└───────┬───────┬───────┬───┘
        │       │       │
        │1      │1      │1
        │       │       │
        │N      │N      │N
┌───────▼───────┐ ┌─────▼─────┐ ┌─────▼───────────┐
│ScrapedQuestions│ │ScrapingJobs│ │ScrapedDocuments  │
├────────────────┤ ├───────────┤ ├──────────────────┤
│PK Id           │ │PK Id      │ │PK Id             │
│FK SourceId     │ │FK SourceId│ │FK SourceId       │
│   QuestionText │ │   Started │ │   Title          │
│   Normalized   │ │   Finished│ │   Content        │
│   Category     │ │   Status  │ │   Normalized     │
│   Subcategory  │ │   QFound  │ │   Category       │
│   Difficulty   │ │   QNew    │ │   Subcategory    │
│   Tags         │ │   DFound  │ │   Tags           │
│   Language     │ │   DNew    │ │   SourceUrl      │
│   SourceUrl    │ │   Error   │ │   SourceSite     │
│   AnswerText   │ │   Created │ │   Language       │
│   Technology   │ └───────────┘ │   ContentType    │
│   RawContent   │               │   Difficulty     │
│   IsActive     │               │   Technology     │
│   IsDuplicate  │               │   WordCount      │
│FK DuplicateOfId│──┐            │   ChunkIndex     │
│   HashFinger   │◄─┘(self-ref)  │FK ParentDocId    │──┐
│   ScrapedAt    │               │   HashFinger     │  │(self-ref)
│   CreatedAt    │               │   IsActive       │◄─┘
│   UpdatedAt    │               │   IsDuplicate    │
└────────────────┘               │FK DuplicateOfId  │──┐
                                 │   Confidence     │  │(self-ref)
                                 │   ScrapedAt      │◄─┘
                                 │   CreatedAt      │
                                 │   UpdatedAt      │
                                 └──────────────────┘
```

---

## 13. Diagramas de Flujo

### 13.1 Flujo general del sistema

```
[Inicio Worker]
      │
      ▼
[Ejecutar scraping inmediato]
      │
      ▼
[Calcular próxima ejecución cron]
      │
      ▼
[Esperar hasta próxima ejecución] ◄──────────┐
      │                                       │
      ▼                                       │
[Obtener scrapers habilitados]                │
      │                                       │
      ▼                                       │
[Para cada scraper] ───────────────────┐      │
      │                                │      │
      ▼                                │      │
[¿Está habilitado?] ──No──▶ [Saltar]  │      │
      │ Sí                             │      │
      ▼                                │      │
[¿Necesita ejecutarse?] ──No──▶[Saltar]│      │
      │ Sí                             │      │
      ▼                                │      │
[Crear Job (Running)]                  │      │
      │                                │      │
      ▼                                │      │
[Ejecutar ScrapeAsync()]               │      │
      │                                │      │
      ▼                                │      │
[Procesar Q&A: Filtrar→              │      │
 Clasificar→Deduplicar→Persistir]     │      │
      │                                │      │
      ▼                                │      │
[Procesar Docs: Filtrar→              │      │
 Clasificar→Deduplicar→Persistir]     │      │
      │                                │      │
      ▼                                │      │
[Actualizar Job (Completed)]           │      │
      │                                │      │
      ▼                                │      │
[Siguiente scraper] ──────────────────┘      │
      │                                       │
      ▼                                       │
[Registrar resumen en logs]                   │
      │                                       │
      └───────────────────────────────────────┘
```

### 13.2 Flujo de procesamiento de preguntas Q&A

```
[Pregunta extraída]
      │
      ▼
[¿Tiene respuesta ≥20 chars?] ──No──▶ [Descartar]
      │ Sí
      ▼
[¿Idioma = español?] ──No──▶ [Descartar]
      │ Sí
      ▼
[¿Es relevante IT?] ──No──▶ [Descartar]
      │ Sí
      ▼
[Clasificar (categoría, dificultad, tags, tecnología)]
      │
      ▼
[Calcular SHA-256 del texto normalizado]
      │
      ▼
[¿Hash existe en BD?] ──Sí──▶ [Marcar duplicada, incrementar contador]
      │ No
      ▼
[Persistir en ScrapedQuestions]
      │
      ▼
[Incrementar contador de nuevas]
```

---

## 14. Diagramas de Actividades

### 14.1 Actividad: Sesión de scraping completa

```
┌─────────────────────┐
│ Inicio Sesión       │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Log: "INICIO DE     │
│ SCRAPING ORQUESTADO"│
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ Obtener fuentes     │
│ activas de BD       │
└─────────┬───────────┘
          │
          ▼
    ┌─────────────┐
    │ ¿Hay más    │──No──▶┌──────────────┐
    │ scrapers?   │       │ Fin: generar │
    └──────┬──────┘       │ resumen      │
           │ Sí           └──────────────┘
           ▼
    ┌─────────────┐
    │ ¿Cancelado? │──Sí──▶ [Abort]
    └──────┬──────┘
           │ No
           ▼
    ┌─────────────────┐
    │ ¿Habilitado en  │──No──▶ [Skip]
    │ EnabledScrapers?│
    └──────┬──────────┘
           │ Sí
           ▼
    ┌─────────────────┐
    │ ¿Config.Enabled │──No──▶ [Skip]
    │ = true?         │
    └──────┬──────────┘
           │ Sí
           ▼
    ┌─────────────────┐
    │ ¿FrecuenciaOK?  │──No──▶ [Skip: próxima run]
    │ Now > Last+Freq │
    └──────┬──────────┘
           │ Sí
           ▼
    ┌─────────────────┐
    │ RunSingleScraper│
    │ (con try/catch) │
    └──────┬──────────┘
           │
           ▼
    ┌─────────────────┐
    │ Acumular        │
    │ resultados      │
    └──────┬──────────┘
           │
           └──────▶ [Siguiente scraper]
```

---

## 15. Diagramas de Secuencia

### 15.1 Secuencia: Ejecución del Worker

```
ScrapingWorker          Orchestrator           Scraper(N)          Classifier(s)         Repository            DB
     │                      │                     │                     │                    │                  │
     │── RunAllScrapers ──▶│                     │                     │                    │                  │
     │                      │── GetActiveSources ────────────────────────────────────────────▶│                  │
     │                      │◀── List<Source> ───────────────────────────────────────────────│                  │
     │                      │                     │                     │                    │                  │
     │                      │    [loop: cada scraper habilitado]        │                    │                  │
     │                      │                     │                     │                    │                  │
     │                      │── GetOrCreateSource ──────────────────────────────────────────▶│── INSERT/SELECT ▶│
     │                      │◀── Source ─────────────────────────────────────────────────────│◀── Result ──────│
     │                      │                     │                     │                    │                  │
     │                      │── CreateJob ─────────────────────────────────────────────────▶│── INSERT ────────▶│
     │                      │◀── Job ──────────────────────────────────────────────────────│◀── OK ───────────│
     │                      │                     │                     │                    │                  │
     │                      │── ScrapeAsync() ──▶│                     │                    │                  │
     │                      │                     │── HTTP GET ────────▶│ [Web]              │                  │
     │                      │                     │◀── HTML/JSON ──────│                     │                  │
     │                      │                     │── ApplyRateLimit ──│                     │                  │
     │                      │◀── ScrapingResult ─│                     │                    │                  │
     │                      │                     │                     │                    │                  │
     │                      │    [loop: cada pregunta]                  │                    │                  │
     │                      │── Classify() ────────────────────────────▶│                    │                  │
     │                      │◀── ClassificationResult ─────────────────│                    │                  │
     │                      │── ExistsByHash ────────────────────────────────────────────────▶│── SELECT ───────▶│
     │                      │◀── bool ──────────────────────────────────────────────────────│◀── Result ──────│
     │                      │── AddQuestion ────────────────────────────────────────────────▶│── INSERT ────────▶│
     │                      │                     │                     │                    │                  │
     │                      │    [loop: cada documento]                 │                    │                  │
     │                      │── ClassifyDocument()─────────────────────▶│                    │                  │
     │                      │◀── DocClassificationResult ──────────────│                    │                  │
     │                      │── DocumentExistsByHash ───────────────────────────────────────▶│── SELECT ───────▶│
     │                      │── AddDocument ───────────────────────────────────────────────▶│── INSERT ────────▶│
     │                      │                     │                     │                    │                  │
     │                      │── UpdateJob ─────────────────────────────────────────────────▶│── UPDATE ────────▶│
     │                      │── UpdateSourceLastScraped ───────────────────────────────────▶│── UPDATE ────────▶│
     │                      │                     │                     │                    │                  │
     │◀── OrchestratorResult│                    │                     │                    │                  │
     │                      │                     │                     │                    │                  │
     │── Log Resumen ──────▶│ [console/file]      │                     │                    │                  │
```

### 15.2 Secuencia: Clasificación de una pregunta

```
Orchestrator          KeywordClassifier          classification_rules.json
     │                       │                            │
     │── Classify(text) ───▶│                            │
     │                       │── NormalizeText(text) ────│                            
     │                       │                            │
     │                       │    [loop: cada categoría en rules]
     │                       │── count keywords ────────▶│
     │                       │◀── score ────────────────│
     │                       │── match regex patterns ──▶│
     │                       │◀── score += 3 ──────────│
     │                       │                            │
     │                       │── Select best category    │
     │                       │── DetermineDifficulty()   │
     │                       │── DetermineTechnology()   │
     │                       │── Extract subcategory     │
     │                       │── Extract tags            │
     │                       │                            │
     │◀── ClassificationResult│                           │
```

---

## 16. Mapa de Navegación

Al ser un **Worker Service** sin interfaz de usuario, el mapa de navegación describe el **flujo de ejecución del proceso** en lugar de la navegación de pantallas:

```
                        ┌───────────────────────┐
                        │    INICIO DEL WORKER   │
                        │  (dotnet run)           │
                        └───────────┬─────────────┘
                                    │
                                    ▼
                        ┌───────────────────────┐
                        │  CONFIGURACIÓN         │
                        │  - Cargar appsettings  │
                        │  - Registrar DI        │
                        │  - Setup Serilog       │
                        │  - Setup EF Core       │
                        └───────────┬─────────────┘
                                    │
                                    ▼
                        ┌───────────────────────┐
                        │  VERIFICAR BD          │
                        │  EnsureCreatedAsync()  │
                        └───────────┬─────────────┘
                                    │
                                    ▼
                ┌───────────────────────────────────────┐
                │       CICLO PRINCIPAL DEL WORKER       │
                │                                        │
                │  ┌──────────────────────────┐          │
                │  │ Ejecución inmediata       │          │
                │  │ (primera vez)             │          │
                │  └────────────┬──────────────┘          │
                │               │                         │
                │               ▼                         │
                │  ┌──────────────────────────┐          │
                │  │ SESIÓN DE SCRAPING        │          │
                │  │                           │          │
                │  │  ┌─────────────────────┐  │          │
                │  │  │ Scraper DevTo       │  │          │
                │  │  │ (REST API)          │  │          │
                │  │  └─────────────────────┘  │          │
                │  │  ┌─────────────────────┐  │          │
                │  │  │ Scraper Medium      │  │          │
                │  │  │ (Playwright)        │  │          │
                │  │  └─────────────────────┘  │          │
                │  │  ┌─────────────────────┐  │          │
                │  │  │ Scraper LeetCode    │  │          │
                │  │  │ (GraphQL API)       │  │          │
                │  │  └─────────────────────┘  │          │
                │  │  ┌─────────────────────┐  │          │
                │  │  │ ... 28 scrapers más │  │          │
                │  │  └─────────────────────┘  │          │
                │  │                           │          │
                │  │  Para cada resultado:     │          │
                │  │  ┌─────────────────────┐  │          │
                │  │  │ PIPELINE Q&A        │  │          │
                │  │  │ Filter→Classify→    │  │          │
                │  │  │ Dedup→Persist       │  │          │
                │  │  └─────────────────────┘  │          │
                │  │  ┌─────────────────────┐  │          │
                │  │  │ PIPELINE RAG DOC    │  │          │
                │  │  │ Filter→Classify→    │  │          │
                │  │  │ Dedup→Persist       │  │          │
                │  │  └─────────────────────┘  │          │
                │  │                           │          │
                │  └────────────┬──────────────┘          │
                │               │                         │
                │               ▼                         │
                │  ┌──────────────────────────┐          │
                │  │ Log resumen              │          │
                │  │ (fuentes, Q&A, docs,     │          │
                │  │  errores, duración)       │          │
                │  └────────────┬──────────────┘          │
                │               │                         │
                │               ▼                         │
                │  ┌──────────────────────────┐          │
                │  │ Calcular próxima         │          │
                │  │ ejecución (cron)         │          │
                │  │ Esperar...               │◄─────────┘
                │  └──────────────────────────┘
                │
                └── [CancellationToken] ──▶ FIN
```

### Módulos de salida de datos (consumidores)

```
┌─────────────────────┐
│  BD: ScrapedQuestions│──▶ Módulo de Simulación de Entrevistas
│  (preguntas Q&A)    │    (entrevistas simuladas con preguntas reales)
└─────────────────────┘

┌─────────────────────┐
│  BD: ScrapedDocuments│──▶ Módulo RAG del Interview Simulator
│  (corpus técnico)    │    (contexto para respuestas generativas)
└─────────────────────┘

┌─────────────────────┐
│  BD: ScrapingJobs    │──▶ Monitoreo / Dashboard (futuro)
│  (historial de jobs) │    (métricas de scraping)
└─────────────────────┘

┌─────────────────────┐
│  Logs (Serilog)      │──▶ Sistema de monitoreo / Observabilidad
│  logs/scraping-*.log │
└─────────────────────┘
```

---

## Anexo A: Configuración de fuentes de scraping

| Fuente | Tipo | Frecuencia (h) | Máx. Páginas | Habilitado |
|---|---|---|---|---|
| DevTo | BlogPlatform | 12 | 5 | ✅ |
| Medium | BlogPlatform | 24 | 5 | ✅ |
| LeetCode | CodingPlatform | 168 | 150 problemas | ✅ |
| Glassdoor | JobBoard | 48 | 3 | ❌ |
| Indeed | JobBoard | 48 | 5 | ✅ |
| FreeCodeCamp | EducationalPlatform | — | — | ✅ |
| GeeksForGeeks | TechnicalEncyclopedia | 72 | 3 | ✅ |
| InterviewBit | CodingPlatform | 72 | 3 | ✅ |
| FullStackCafe | GitHubRepository | 168 | 5 | ✅ |
| JavaTPoint | TechnicalEncyclopedia | 72 | 5 | ✅ |
| TealHQ | JobBoard | 96 | 3 | ✅ |
| KnowledgeHut | EducationalPlatform | 96 | 3 | ✅ |
| Simplilearn | EducationalPlatform | 96 | 3 | ✅ |
| Edureka | EducationalPlatform | 96 | 3 | ✅ |
| CSharpCorner | BlogPlatform | 120 | 5 | ✅ |
| Baeldung | BlogPlatform | 120 | 3 | ✅ |
| DotNetTricks | BlogPlatform | 120 | 5 | ✅ |
| Turing | EducationalPlatform | 48 | 25 | ✅ |
| KeepCoding | EducationalPlatform | 72 | 20 | ✅ |
| Platzi | EducationalPlatform | 72 | 20 | ✅ |
| OpenWebinars | EducationalPlatform | 72 | 20 | ✅ |
| Talently | EducationalPlatform | 96 | 15 | ✅ |
| Epitech | EducationalPlatform | 96 | 10 | ✅ |
| TheBridge | EducationalPlatform | 96 | 10 | ✅ |
| EPAMAnywhere | ProfessionalNetwork | 96 | 15 | ✅ |
| MicrosoftLearn | OfficialDocumentation | 168 | 50 | ✅ |
| MdnWebDocs | OfficialDocumentation | 168 | 50 | ✅ |
| W3Schools | TechnicalEncyclopedia | 168 | 80 | ✅ |
| RefactoringGuru | ArchitectureReference | 336 | 30 | ✅ |
| DigitalOcean | BlogPlatform | 120 | 30 | ✅ |
| StackOverflow | Forum | 72 | 30 | ✅ |

## Anexo B: Stack tecnológico completo

| Capa | Tecnología | Propósito |
|---|---|---|
| Runtime | .NET 8.0 | Plataforma de ejecución |
| ORM | Entity Framework Core 8.x | Mapeo objeto-relacional, migraciones |
| Base de datos | SQL Server | Almacenamiento persistente |
| HTTP | HttpClient + IHttpClientFactory | Comunicación HTTP con fuentes |
| Resiliencia | Polly | Retry policies, circuit breaker |
| Browser | Microsoft Playwright | Scraping de sitios con JavaScript/Anti-bot |
| HTML Parsing | HtmlAgilityPack + AngleSharp | Análisis de DOM HTML |
| Logging | Serilog | Logging estructurado (Console + File) |
| Scheduling | Cronos | Evaluación de expresiones cron |
| Hashing | SHA-256 (System.Security.Cryptography) | Deduplicación por fingerprint |
| Tests | xUnit + Moq | Tests unitarios y mocking |
| DI | Microsoft.Extensions.DependencyInjection | Inyección de dependencias |
| Config | Microsoft.Extensions.Configuration | Binding de configuración desde JSON |
