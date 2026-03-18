using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace InterviewSimulator.RAG.Embedding;

public class EmbeddingCache
{
    private readonly ConcurrentDictionary<string, float[]> _cache = new();
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
        return _cache.TryGetValue(key, out var vector) ? vector : null;
    }

    public void Set(string text, float[] vector)
    {
        if (_cache.Count >= _maxEntries)
        {
            _logger.LogWarning("Embedding cache full ({Count} entries), clearing oldest half", _cache.Count);
            ClearHalf();
        }

        string key = ComputeKey(text);
        _cache.TryAdd(key, vector);
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

    private void ClearHalf()
    {
        var keys = _cache.Keys.Take(_cache.Count / 2).ToList();
        foreach (var key in keys)
            _cache.TryRemove(key, out _);
    }
}
