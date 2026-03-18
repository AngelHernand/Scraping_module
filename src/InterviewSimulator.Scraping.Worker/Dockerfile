# =============================================================================
# Stage 1: Build & Playwright browser download
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for NuGet restore caching
COPY src/InterviewSimulator.Scraping.Core/*.csproj src/InterviewSimulator.Scraping.Core/
COPY src/InterviewSimulator.Scraping.Data/*.csproj src/InterviewSimulator.Scraping.Data/
COPY src/InterviewSimulator.Scraping.Classifier/*.csproj src/InterviewSimulator.Scraping.Classifier/
COPY src/InterviewSimulator.Scraping.Scrapers/*.csproj src/InterviewSimulator.Scraping.Scrapers/
COPY src/InterviewSimulator.Scraping.Worker/*.csproj src/InterviewSimulator.Scraping.Worker/
RUN dotnet restore src/InterviewSimulator.Scraping.Worker/InterviewSimulator.Scraping.Worker.csproj

# Copy all source code and publish
COPY src/ src/
RUN dotnet publish src/InterviewSimulator.Scraping.Worker/InterviewSimulator.Scraping.Worker.csproj \
    -c Release -o /app/publish --no-restore

# Download Playwright Chromium using the bundled Node.js from the NuGet cache
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN PW_DIR=$(find /root/.nuget/packages/microsoft.playwright -name ".playwright" -type d | head -1) && \
    "$PW_DIR/node/linux-x64/node" "$PW_DIR/package/cli.js" install chromium

# =============================================================================
# Stage 2: Runtime
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# System dependencies required by Chromium (Playwright)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libnss3 libnspr4 libdbus-1-3 libatk1.0-0 libatk-bridge2.0-0 \
    libcups2 libdrm2 libxkbcommon0 libatspi2.0-0 libxcomposite1 \
    libxdamage1 libxfixes3 libxrandr2 libgbm1 libpango-1.0-0 \
    libcairo2 libasound2 libwayland-client0 fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

# Copy Playwright browsers from build stage
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
COPY --from=build /ms-playwright /ms-playwright

# Copy published application
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "InterviewSimulator.Scraping.Worker.dll"]
