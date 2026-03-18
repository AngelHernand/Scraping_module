using InterviewSimulator.RAG.Core.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace InterviewSimulator.RAG.VectorStore;

public class QdrantHealthCheck : IHealthCheck
{
    private readonly QdrantClient _client;
    private readonly QdrantSettings _settings;
    private readonly ILogger<QdrantHealthCheck> _logger;

    public QdrantHealthCheck(
        QdrantClient client,
        IOptions<QdrantSettings> settings,
        ILogger<QdrantHealthCheck> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            bool collectionExists = collections.Any(c => c == _settings.CollectionName);

            var data = new Dictionary<string, object>
            {
                ["host"] = $"{_settings.Host}:{_settings.Port}",
                ["collection"] = _settings.CollectionName,
                ["collection_exists"] = collectionExists
            };

            if (collectionExists)
            {
                var info = await _client.GetCollectionInfoAsync(
                    _settings.CollectionName,
                    cancellationToken: cancellationToken);
                data["point_count"] = info.PointsCount;
            }

            return HealthCheckResult.Healthy("Qdrant is reachable", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qdrant health check failed");
            return HealthCheckResult.Unhealthy("Qdrant is not reachable", ex);
        }
    }
}
