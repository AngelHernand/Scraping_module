using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace InterviewSimulator.RAG.VectorStore;

public class QdrantCollectionManager
{
    private readonly QdrantClient _client;
    private readonly QdrantSettings _settings;
    private readonly ILogger<QdrantCollectionManager> _logger;

    public QdrantCollectionManager(
        QdrantClient client,
        IOptions<QdrantSettings> settings,
        ILogger<QdrantCollectionManager> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken);
        if (collections.Any(c => c == _settings.CollectionName))
        {
            _logger.LogInformation("Qdrant collection '{Collection}' already exists", _settings.CollectionName);
            return;
        }

        _logger.LogInformation("Creating Qdrant collection '{Collection}' with {Dims} dimensions",
            _settings.CollectionName, RagConstants.DefaultEmbeddingDimensions);

        await _client.CreateCollectionAsync(
            _settings.CollectionName,
            new VectorParams
            {
                Size = (ulong)RagConstants.DefaultEmbeddingDimensions,
                Distance = Distance.Cosine
            },
            cancellationToken: cancellationToken);

        // Crear índices de payload para filtrado eficiente
        await CreatePayloadIndicesAsync(cancellationToken);

        _logger.LogInformation("Qdrant collection '{Collection}' created successfully", _settings.CollectionName);
    }

    private async Task CreatePayloadIndicesAsync(CancellationToken cancellationToken)
    {
        var keywordFields = new[]
        {
            "category", "subcategory", "difficulty_level",
            "source_name", "original_language", "chunk_type"
        };

        foreach (var field in keywordFields)
        {
            await _client.CreatePayloadIndexAsync(
                _settings.CollectionName,
                field,
                PayloadSchemaType.Keyword,
                cancellationToken: cancellationToken);
        }

        // Índice para tags (array keyword)
        await _client.CreatePayloadIndexAsync(
            _settings.CollectionName,
            "tags",
            PayloadSchemaType.Keyword,
            cancellationToken: cancellationToken);

        // Índice para scraped_question_id (integer)
        await _client.CreatePayloadIndexAsync(
            _settings.CollectionName,
            "scraped_question_id",
            PayloadSchemaType.Integer,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Payload indices created for collection '{Collection}'", _settings.CollectionName);
    }
}
