# PROMPT: Dockerizar la infraestructura completa del proyecto InterviewSimulator

---

## CONTEXTO

Tengo un proyecto .NET 8 llamado **InterviewSimulator** que actualmente tiene:

- **Qdrant** ya dockerizado en `docker-compose.yml` (funcionando)
- **SQL Server** corriendo localmente en Windows (`DESKTOP-F1S0QNA\MSSQLSERVER01`)
- **Scraping Worker** — se ejecuta con `dotnet run` localmente
- **RAG Worker** — se ejecuta con `dotnet run` localmente

Quiero **mover TODO a Docker Compose** para que con un solo `docker compose up -d` levante:
1. SQL Server (contenedor)
2. Qdrant (ya está)
3. Scraping Worker (contenedor)
4. RAG Worker (contenedor)

---

## ESTRUCTURA ACTUAL DEL PROYECTO

```
Web-scraping_module/
├── docker-compose.yml              ← ACTUAL: solo Qdrant
├── Web-scraping_module.sln
├── src/
│   ├── InterviewSimulator.Scraping.Worker/    ← Worker Service (.NET 8)
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── InterviewSimulator.Scraping.Worker.csproj
│   │
│   ├── InterviewSimulator.RAG.Worker/         ← Worker Service (.NET 8)
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── InterviewSimulator.RAG.Worker.csproj
│   │
│   ├── InterviewSimulator.Scraping.Core/
│   ├── InterviewSimulator.Scraping.Data/
│   ├── InterviewSimulator.Scraping.Classifier/
│   ├── InterviewSimulator.Scraping.Scrapers/
│   ├── InterviewSimulator.RAG.Core/
│   ├── InterviewSimulator.RAG.Data/
│   ├── InterviewSimulator.RAG.Processing/
│   ├── InterviewSimulator.RAG.Embedding/
│   ├── InterviewSimulator.RAG.VectorStore/
│   └── InterviewSimulator.RAG.Retrieval/
│
├── tests/
│   ├── InterviewSimulator.Scraping.Tests.Unit/
│   └── InterviewSimulator.RAG.Tests.Unit/
│
└── .dockerignore                    ← Ya existe
```

---

## DOCKER-COMPOSE.YML ACTUAL

```yaml
version: '3.8'

services:
  qdrant:
    image: qdrant/qdrant:latest
    container_name: interview-simulator-qdrant
    ports:
      - "6333:6333"
      - "6334:6334"
    volumes:
      - qdrant_data:/qdrant/storage
    environment:
      - QDRANT__SERVICE__GRPC_PORT=6334
    restart: unless-stopped

volumes:
  qdrant_data:
    driver: local
```

---

## LO QUE NECESITO QUE HAGAS

### 1. Actualizar `docker-compose.yml` para incluir:

**SQL Server 2022:**
- Imagen: `mcr.microsoft.com/mssql/server:2022-latest`
- Puerto: `1433:1433`
- Password SA: `InterviewSim2026!` (o similar, fuerte)
- Volumen persistente para datos
- Health check para que los workers esperen a que SQL Server esté listo
- Variable `ACCEPT_EULA=Y`
- Base de datos `InterviewSimulator` (se crea automáticamente via EF Core `EnsureCreatedAsync`)

**Scraping Worker:**
- Build desde Dockerfile multi-stage
- Depende de: `sqlserver` (con `depends_on` + `condition: service_healthy`)
- NO depende de Qdrant (no lo necesita)
- Connection string apuntando al servicio `sqlserver` de Docker network
- Variables de entorno para overridear el connection string
- **IMPORTANTE**: Playwright necesita browsers instalados en el contenedor. El Dockerfile debe incluir la instalación de Playwright browsers (`playwright install --with-deps chromium`)
- Restart: `unless-stopped`

**RAG Worker:**
- Build desde Dockerfile multi-stage
- Depende de: `sqlserver` (healthy) + `qdrant` (healthy)
- Connection string apuntando a `sqlserver`
- Qdrant host apuntando a `qdrant` (nombre del servicio en Docker network)
- API Key de OpenAI via variable de entorno (`OPENAI__APIKEY`)
- Restart: `unless-stopped`

### 2. Crear Dockerfiles

**`src/InterviewSimulator.Scraping.Worker/Dockerfile`:**
- Multi-stage build: `mcr.microsoft.com/dotnet/sdk:8.0` para build, `mcr.microsoft.com/dotnet/aspnet:8.0` para runtime
- PERO como usa Playwright, el runtime necesita las dependencias de Chromium:
  ```
  # En el stage de runtime, instalar dependencias de Playwright
  RUN apt-get update && apt-get install -y \
      libglib2.0-0 libnss3 libnspr4 libdbus-1-3 libatk1.0-0 \
      libatk-bridge2.0-0 libcups2 libdrm2 libxkbcommon0 \
      libatspi2.0-0 libxcomposite1 libxdamage1 libxfixes3 \
      libxrandr2 libgbm1 libpango-1.0-0 libcairo2 libasound2 \
      libwayland-client0 fonts-liberation xdg-utils wget \
      && rm -rf /var/lib/apt/lists/*
  
  # Instalar Playwright browsers
  RUN dotnet tool install --global Microsoft.Playwright.CLI
  ENV PATH="${PATH}:/root/.dotnet/tools"
  RUN playwright install chromium --with-deps
  ```
- Copiar toda la solución (necesita los proyectos referenciados)
- Working directory: `/app`
- Entry point: el Worker Service

**`src/InterviewSimulator.RAG.Worker/Dockerfile`:**
- Multi-stage build estándar (no necesita Playwright)
- `mcr.microsoft.com/dotnet/sdk:8.0` → `mcr.microsoft.com/dotnet/aspnet:8.0`
- Copiar toda la solución
- Entry point: el Worker Service

### 3. Actualizar connection strings y configuración

Los Workers deben poder recibir configuración via variables de entorno de Docker.
.NET soporta override de appsettings.json con variables de entorno usando `__` como separador.

En el `docker-compose.yml`, los servicios deben tener:

```yaml
scraping-worker:
  environment:
    - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=InterviewSimulator;User Id=sa;Password=InterviewSim2026!;TrustServerCertificate=True;
    - DOTNET_ENVIRONMENT=Production
    - ScrapingSettings__CronSchedule=0 3 * * *

rag-worker:
  environment:
    - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=InterviewSimulator;User Id=sa;Password=InterviewSim2026!;TrustServerCertificate=True;
    - Qdrant__Host=qdrant
    - Qdrant__Port=6333
    - OpenAI__ApiKey=${OPENAI_API_KEY}
    - DOTNET_ENVIRONMENT=Production
    - RagPipeline__CronSchedule=0 4 * * *
```

**NOTA sobre la API Key de OpenAI:** NO hardcodear en docker-compose.yml. Usar una variable de entorno del host o un archivo `.env`:

```env
# .env (en la raíz del proyecto, agregar a .gitignore)
OPENAI_API_KEY=sk-tu-key-aqui
SA_PASSWORD=InterviewSim2026!
```

### 4. Crear archivo `.env.example`

```env
# Copiar como .env y llenar con valores reales
OPENAI_API_KEY=sk-your-openai-api-key-here
SA_PASSWORD=YourStrongPassword123!
```

### 5. Agregar `.env` al `.gitignore`

```
# Secrets
.env
```

### 6. Health checks

SQL Server debe tener health check para que los workers no arranquen antes de que esté listo:

```yaml
sqlserver:
  healthcheck:
    test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$$SA_PASSWORD" -C -Q "SELECT 1" -b -o /dev/null
    interval: 10s
    timeout: 5s
    retries: 10
    start_period: 30s
```

Qdrant:

```yaml
qdrant:
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:6333/readyz"]
    interval: 10s
    timeout: 5s
    retries: 5
```

### 7. Orden de ejecución deseado

```
1. sqlserver arranca → health check pasa (~30s)
2. qdrant arranca → health check pasa (~10s)
3. scraping-worker arranca → conecta a sqlserver → ejecuta scraping
4. rag-worker arranca → conecta a sqlserver + qdrant → ejecuta pipeline
```

El scraping-worker NO debe esperar al rag-worker ni viceversa. Son independientes.
El rag-worker DEBE esperar a qdrant además de sqlserver.

---

## CONSIDERACIONES IMPORTANTES

### Playwright en Docker
El Scraping Worker usa Microsoft Playwright para Medium y Glassdoor. Playwright en Docker Linux requiere:
- Dependencias de sistema para Chromium (libglib, libnss, etc.)
- Instalación de los browsers con `playwright install chromium`
- El Dockerfile del Scraping Worker es más pesado por esto (~1.5GB)

### Red de Docker
Todos los servicios deben estar en la misma red Docker (default network del compose).
Los servicios se referencian por nombre: `sqlserver`, `qdrant`, `scraping-worker`, `rag-worker`.

### Volúmenes
- `sqlserver_data` — Persistencia de la base de datos SQL Server
- `qdrant_data` — Persistencia de la base de datos vectorial
- Los Workers no necesitan volúmenes (son stateless, los logs van a stdout)

### Logs
En Docker, los Workers deben loggear a **stdout/stderr** (Serilog Console sink), no a archivos.
Docker Compose captura los logs automáticamente: `docker compose logs scraping-worker -f`

### Primer arranque
En el primer `docker compose up -d`:
1. SQL Server arranca con BD vacía
2. Scraping Worker corre `EnsureCreatedAsync()` que crea las tablas
3. Scraping Worker ejecuta la primera sesión de scraping
4. RAG Worker corre `EnsureCreatedAsync()` para la tabla ProcessingStatus
5. RAG Worker procesa los datos scrapeados

### Ejecución manual del scraping
Si quiero forzar una ejecución sin esperar al cron, debería poder hacer:
```bash
docker compose restart scraping-worker
```
(Porque el Worker ejecuta inmediatamente al arrancar antes de entrar al loop del cron)

---

## RESULTADO ESPERADO

Después de tu trabajo, debería poder hacer:

```bash
# Crear archivo .env con mis secrets
cp .env.example .env
# Editar .env con mi OPENAI_API_KEY

# Levantar TODA la infraestructura
docker compose up -d

# Ver logs del scraping
docker compose logs scraping-worker -f

# Ver logs del RAG pipeline
docker compose logs rag-worker -f

# Ver dashboard de Qdrant
# Abrir http://localhost:6333/dashboard

# Conectarme a SQL Server desde SSMS
# Server: localhost,1433  User: sa  Password: (del .env)

# Parar todo
docker compose down

# Parar todo Y borrar datos
docker compose down -v
```

---

## ENTREGABLES

1. `docker-compose.yml` actualizado con los 4 servicios
2. `src/InterviewSimulator.Scraping.Worker/Dockerfile`
3. `src/InterviewSimulator.RAG.Worker/Dockerfile`
4. `.env.example`
5. `.gitignore` actualizado
6. `README.md` actualizado con instrucciones de Docker
7. Si es necesario, ajustes menores en `appsettings.json` de ambos Workers para que los defaults funcionen con Docker y los overrides con env vars
