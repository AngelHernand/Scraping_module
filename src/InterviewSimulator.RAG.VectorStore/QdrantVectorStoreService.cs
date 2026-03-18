using System.Text.Json;
using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Interfaces;
using InterviewSimulator.RAG.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace InterviewSimulator.RAG.VectorStore;

public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly QdrantClient _client;
    private readonly QdrantSettings _settings;
    private readonly ILogger<QdrantVectorStoreService> _logger;

    public QdrantVectorStoreService(
        QdrantClient client,
        IOptions<QdrantSettings> settings,
        ILogger<QdrantVectorStoreService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<Guid>> UpsertAsync(
        List<EmbeddedChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        var pointIds = new List<Guid>();
        var batches = chunks
            .Select((c, i) => new { Chunk = c, Index = i })
            .GroupBy(x => x.Index / _settings.UpsertBatchSize)
            .Select(g => g.Select(x => x.Chunk).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            var points = batch.Select(ec =>
            {
                var pointId = ec.Chunk.ChunkId;
                pointIds.Add(pointId);

                return new PointStruct
                {
                    Id = new PointId { Uuid = pointId.ToString() },
                    Vectors = ec.Vector,
                    Payload =
                    {
                        ["scraped_question_id"] = ec.Chunk.ScrapedQuestionId,
                        ["chunk_index"] = ec.Chunk.ChunkIndex,
                        ["text"] = ec.Chunk.Text,
                        ["question_text"] = ec.Chunk.QuestionText ?? string.Empty,
                        ["category"] = ec.Chunk.Category,
                        ["subcategory"] = ec.Chunk.Subcategory ?? string.Empty,
                        ["difficulty_level"] = ec.Chunk.DifficultyLevel,
                        ["source_name"] = ec.Chunk.SourceName,
                        ["source_url"] = ec.Chunk.SourceUrl,
                        ["original_language"] = ec.Chunk.Language,
                        ["chunk_type"] = ec.Chunk.Type.ToString(),
                        ["token_count"] = ec.Chunk.TokenCount,
                        ["tags"] = ec.Chunk.Tags.ToArray(),
                        ["scraped_at"] = ec.Chunk.ScrapedAt.ToString("o"),
                        ["embedding_model"] = ec.ModelUsed,
                    }
                };
            }).ToList();

            await _client.UpsertAsync(
                _settings.CollectionName,
                points,
                cancellationToken: cancellationToken);

            _logger.LogDebug("Upserted {Count} points to Qdrant", points.Count);
        }

        _logger.LogInformation("Upserted {Total} points to '{Collection}'", pointIds.Count, _settings.CollectionName);
        return pointIds;
    }

    public async Task<List<(string PointId, float Score, Dictionary<string, object> Payload)>> SearchAsync(
        float[] queryVector,
        Dictionary<string, object>? filters = null,
        List<int>? excludeScrapedQuestionIds = null,
        int topK = 10,
        float minScore = 0.5f,
        CancellationToken cancellationToken = default)
    {
        Filter? qdrantFilter = BuildFilter(filters, excludeScrapedQuestionIds);

        var searchResult = await _client.SearchAsync(
            _settings.CollectionName,
            queryVector,
            filter: qdrantFilter,
            limit: (ulong)topK,
            scoreThreshold: minScore,
            cancellationToken: cancellationToken);

        var results = searchResult.Select(point =>
        {
            var payload = point.Payload.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertValue(kvp.Value));

            return (
                PointId: point.Id.Uuid,
                Score: point.Score,
                Payload: payload
            );
        }).ToList();

        _logger.LogDebug("Search returned {Count} results (topK={TopK}, minScore={MinScore})",
            results.Count, topK, minScore);

        return results;
    }

    public async Task DeleteByScrapedQuestionIdAsync(
        int scrapedQuestionId,
        CancellationToken cancellationToken = default)
    {
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "scraped_question_id",
                        Match = new Match { Integer = scrapedQuestionId }
                    }
                }
            }
        };

        await _client.DeleteAsync(
            _settings.CollectionName,
            filter,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Deleted points for ScrapedQuestionId {Id} from '{Collection}'",
            scrapedQuestionId, _settings.CollectionName);
    }

    public async Task<(long PointCount, long SegmentCount)> GetCollectionStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var info = await _client.GetCollectionInfoAsync(
            _settings.CollectionName,
            cancellationToken: cancellationToken);

        return (
            PointCount: (long)info.PointsCount,
            SegmentCount: (long)info.SegmentsCount
        );
    }

    private static Filter? BuildFilter(
        Dictionary<string, object>? filters,
        List<int>? excludeScrapedQuestionIds)
    {
        if ((filters == null || filters.Count == 0) &&
            (excludeScrapedQuestionIds == null || excludeScrapedQuestionIds.Count == 0))
            return null;

        var filter = new Filter();

        if (filters != null)
        {
            foreach (var (key, value) in filters)
            {
                var fieldName = key switch
                {
                    "Category" => "category",
                    "Subcategory" => "subcategory",
                    "DifficultyLevel" => "difficulty_level",
                    "SourceName" => "source_name",
                    "Language" => "original_language",
                    "ChunkType" => "chunk_type",
                    _ => key.ToLowerInvariant()
                };

                if (value is string strValue)
                {
                    filter.Must.Add(new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = fieldName,
                            Match = new Match { Keyword = strValue }
                        }
                    });
                }
                else if (value is string[] strArray)
                {
                    // "should" para OR entre valores del mismo campo
                    var shouldFilter = new Filter();
                    foreach (var sv in strArray)
                    {
                        shouldFilter.Should.Add(new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = fieldName,
                                Match = new Match { Keyword = sv }
                            }
                        });
                    }
                    filter.Must.Add(new Condition { Filter = shouldFilter });
                }
            }
        }

        if (excludeScrapedQuestionIds is { Count: > 0 })
        {
            foreach (var id in excludeScrapedQuestionIds)
            {
                filter.MustNot.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "scraped_question_id",
                        Match = new Match { Integer = id }
                    }
                });
            }
        }

        return filter;
    }

    private static object ConvertValue(Value value)
    {
        return value.KindCase switch
        {
            Value.KindOneofCase.StringValue => value.StringValue,
            Value.KindOneofCase.IntegerValue => value.IntegerValue,
            Value.KindOneofCase.DoubleValue => value.DoubleValue,
            Value.KindOneofCase.BoolValue => value.BoolValue,
            Value.KindOneofCase.ListValue => value.ListValue.Values
                .Select(v => ConvertValue(v)).ToList(),
            _ => value.StringValue
        };
    }
}
