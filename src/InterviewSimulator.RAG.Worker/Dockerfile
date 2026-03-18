# =============================================================================
# Stage 1: Build
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for NuGet restore caching
COPY src/InterviewSimulator.Scraping.Core/*.csproj src/InterviewSimulator.Scraping.Core/
COPY src/InterviewSimulator.Scraping.Data/*.csproj src/InterviewSimulator.Scraping.Data/
COPY src/InterviewSimulator.Scraping.Classifier/*.csproj src/InterviewSimulator.Scraping.Classifier/
COPY src/InterviewSimulator.RAG.Core/*.csproj src/InterviewSimulator.RAG.Core/
COPY src/InterviewSimulator.RAG.Data/*.csproj src/InterviewSimulator.RAG.Data/
COPY src/InterviewSimulator.RAG.Processing/*.csproj src/InterviewSimulator.RAG.Processing/
COPY src/InterviewSimulator.RAG.Embedding/*.csproj src/InterviewSimulator.RAG.Embedding/
COPY src/InterviewSimulator.RAG.VectorStore/*.csproj src/InterviewSimulator.RAG.VectorStore/
COPY src/InterviewSimulator.RAG.Retrieval/*.csproj src/InterviewSimulator.RAG.Retrieval/
COPY src/InterviewSimulator.RAG.Worker/*.csproj src/InterviewSimulator.RAG.Worker/
RUN dotnet restore src/InterviewSimulator.RAG.Worker/InterviewSimulator.RAG.Worker.csproj

# Copy all source code and publish
COPY src/ src/
RUN dotnet publish src/InterviewSimulator.RAG.Worker/InterviewSimulator.RAG.Worker.csproj \
    -c Release -o /app/publish --no-restore

# =============================================================================
# Stage 2: Runtime
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "InterviewSimulator.RAG.Worker.dll"]
