using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace InterviewSimulator.RAG.Embedding;

public class EmbeddingCache
{
    private readonly ConcurrentDictionary<string, (float[] Vector, long AccessTicks)> _cache = new();
    private readonly ILogger<EmbeddingCache> _logger;
    private readonly int _maxEntries;

    public EmbeddingCache(ILogger<EmbeddingCache> logger, int maxEntries = 10_000)
    {
        _logger = logger;
        _maxEntries = maxEntries;
    }

    public float[]? Get(string text)
    {
        string key = ComputeKey(text);
        if (_cache.TryGetValue(key, out var entry))
        {
            _cache[key] = (entry.Vector, DateTime.UtcNow.Ticks);
            return entry.Vector;
        }
        return null;
    }

    public void Set(string text, float[] vector)
    {
        if (_cache.Count >= _maxEntries)
        {
            _logger.LogWarning("Embedding cache full ({Count} entries), evicting LRU half", _cache.Count);
            EvictLruHalf();
        }

        string key = ComputeKey(text);
        _cache.TryAdd(key, (vector, DateTime.UtcNow.Ticks));
    }

    public int Count => _cache.Count;

    public void Clear()
    {
        _cache.Clear();
        _logger.LogInformation("Embedding cache cleared");
    }

    private static string ComputeKey(string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash);
    }

    /// <summary>Evicts the least-recently-used half of the cache.</summary>
    private void EvictLruHalf()
    {
        var toEvict = _cache
            .OrderBy(kvp => kvp.Value.AccessTicks)
            .Take(_cache.Count / 2)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in toEvict)
            _cache.TryRemove(key, out _);
    }
}
