using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Interfaces;
using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.RAG.Embedding;

public class OpenAIEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAISettings _settings;
    private readonly ILogger<OpenAIEmbeddingService> _logger;
    private readonly EmbeddingCache? _cache;

    public OpenAIEmbeddingService(
        HttpClient httpClient,
        IOptions<OpenAISettings> settings,
        ILogger<OpenAIEmbeddingService> logger,
        EmbeddingCache? cache = null)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _cache = cache;

        _httpClient.BaseAddress ??= new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization ??=
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public async Task<List<EmbeddedChunk>> GenerateEmbeddingsAsync(
        List<TextChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        var results = new List<EmbeddedChunk>();

        // Procesar en lotes
        var batches = chunks
            .Select((chunk, index) => new { chunk, index })
            .GroupBy(x => x.index / _settings.BatchSize)
            .Select(g => g.Select(x => x.chunk).ToList())
            .ToList();

        _logger.LogInformation("Processing {TotalChunks} chunks in {BatchCount} batches",
            chunks.Count, batches.Count);

        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            var batchResults = await ProcessBatchAsync(batch, cancellationToken);
            results.AddRange(batchResults);

            // Delay entre lotes (excepto el último)
            if (i < batches.Count - 1 && _settings.RequestDelayMs > 0)
                await Task.Delay(_settings.RequestDelayMs, cancellationToken);
        }

        _logger.LogInformation("Generated embeddings for {Count} chunks", results.Count);
        return results;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_cache != null)
        {
            var cached = _cache.Get(text);
            if (cached != null) return cached;
        }

        var response = await CallEmbeddingApiAsync(new[] { text }, cancellationToken);
        var vector = response.Data.FirstOrDefault()?.Embedding ?? Array.Empty<float>();

        _cache?.Set(text, vector);
        return vector;
    }

    private async Task<List<EmbeddedChunk>> ProcessBatchAsync(
        List<TextChunk> batch,
        CancellationToken cancellationToken)
    {
        var texts = batch.Select(FormatChunkForEmbedding).ToArray();

        // Revisar cache primero
        var uncachedIndices = new List<int>();
        var results = new EmbeddedChunk[batch.Count];

        for (int i = 0; i < batch.Count; i++)
        {
            if (_cache != null)
            {
                var cached = _cache.Get(texts[i]);
                if (cached != null)
                {
                    results[i] = CreateEmbeddedChunk(batch[i], cached);
                    continue;
                }
            }
            uncachedIndices.Add(i);
        }

        if (uncachedIndices.Count > 0)
        {
            var uncachedTexts = uncachedIndices.Select(i => texts[i]).ToArray();
            var response = await CallEmbeddingApiAsync(uncachedTexts, cancellationToken);

            for (int j = 0; j < response.Data.Count; j++)
            {
                int originalIndex = uncachedIndices[j];
                var vector = response.Data[j].Embedding;
                results[originalIndex] = CreateEmbeddedChunk(batch[originalIndex], vector);
                _cache?.Set(texts[originalIndex], vector);
            }
        }

        return results.ToList();
    }

    private async Task<OpenAIEmbeddingResponse> CallEmbeddingApiAsync(
        string[] texts,
        CancellationToken cancellationToken)
    {
        var request = new OpenAIEmbeddingRequest
        {
            Input = texts,
            Model = _settings.EmbeddingModel,
            Dimensions = _settings.EmbeddingDimensions
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from OpenAI embedding API");
    }

    private static string FormatChunkForEmbedding(TextChunk chunk)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(chunk.Category) && chunk.Category != "Unknown")
            parts.Add($"Category: {chunk.Category}");
        if (!string.IsNullOrWhiteSpace(chunk.Subcategory))
            parts.Add($"Subcategory: {chunk.Subcategory}");

        parts.Add(chunk.Text);

        return string.Join("\n", parts);
    }

    private EmbeddedChunk CreateEmbeddedChunk(TextChunk chunk, float[] vector)
    {
        return new EmbeddedChunk
        {
            Chunk = chunk,
            Vector = vector,
            ModelUsed = _settings.EmbeddingModel,
            Dimensions = _settings.EmbeddingDimensions
        };
    }

    // Request/Response DTOs internos
    private class OpenAIEmbeddingRequest
    {
        [JsonPropertyName("input")]
        public string[] Input { get; set; } = Array.Empty<string>();

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("dimensions")]
        public int Dimensions { get; set; }
    }

    private class OpenAIEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData> Data { get; set; } = new();

        [JsonPropertyName("usage")]
        public UsageData? Usage { get; set; }
    }

    private class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }

    private class UsageData
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}
