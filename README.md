# Web Scraping + RAG Pipeline — Interview Simulator

Módulo de scraping y pipeline RAG para el **Interview Simulator**. Extrae, clasifica y almacena preguntas de entrevista técnica desde 30+ fuentes web (español e inglés), genera embeddings vectoriales con OpenAI y los almacena en Qdrant para búsqueda semántica.

> Para una descripción exhaustiva del proyecto, ver [CONTEXTO_PROYECTO.md](CONTEXTO_PROYECTO.md).

---

## Docker (Recomendado)

### Requisitos previos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (incluye Docker Compose v2)
- API Key de OpenAI (para el RAG Worker)

### Quick Start

```bash
# 1. Copiar template de variables de entorno y llenar secrets
cp .env.example .env
# Editar .env con tu OPENAI_API_KEY y un SA_PASSWORD fuerte

# 2. Levantar toda la infraestructura
docker compose up -d

# 3. Ver logs
docker compose logs scraping-worker -f   # Scraping
docker compose logs rag-worker -f         # RAG Pipeline
```

### Servicios

| Servicio | Puerto | Descripción |
|---|---|---|
| `sqlserver` | `1433` | SQL Server 2022 — base de datos relacional |
| `qdrant` | `6333` (REST), `6334` (gRPC) | Qdrant — base de datos vectorial |
| `scraping-worker` | — | Worker que scrapea 30+ sitios web (cron 3 AM) |
| `rag-worker` | — | Pipeline RAG: limpieza → chunking → embeddings → Qdrant (cron 4 AM) |

### Orden de arranque

1. **SQL Server** arranca y pasa health check (~30s)
2. **Qdrant** arranca (~2s)
3. **Scraping Worker** arranca → crea tablas en SQL Server → ejecuta scraping
4. **RAG Worker** arranca → crea tabla ProcessingStatus → procesa datos scrapeados

### Comandos útiles

```bash
# Estado de los servicios
docker compose ps

# Forzar ejecución del scraping (reinicia el worker)
docker compose restart scraping-worker

# Forzar ejecución del RAG pipeline
docker compose restart rag-worker

# Ver dashboard de Qdrant
# http://localhost:6333/dashboard

# Conectar a SQL Server desde SSMS
# Server: localhost,1433  |  User: sa  |  Password: (tu SA_PASSWORD)

# Parar todo
docker compose down

# Parar todo Y borrar datos persistidos
docker compose down -v

# Reconstruir imágenes (después de cambios en código)
docker compose up -d --build
```

### Variables de entorno (.env)

| Variable | Descripción |
|---|---|
| `SA_PASSWORD` | Password del usuario `sa` de SQL Server (mínimo 8 chars, mayúscula, número, símbolo) |
| `OPENAI_API_KEY` | API Key de OpenAI para generación de embeddings |

---

## Desarrollo local (sin Docker)
- **Resilience**: Polly (retry + circuit breaker)
- **Logging**: Serilog (Console + File sinks)
- **Scheduling**: Cronos (cron expressions)
- **Tests**: xUnit + Moq
