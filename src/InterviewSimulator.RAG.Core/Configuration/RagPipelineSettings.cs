namespace InterviewSimulator.RAG.Core.Configuration;

public class RagPipelineSettings
{
    public const string SectionName = "RagPipeline";

    public int ProcessingBatchSize { get; set; } = 100;
    public string CronSchedule { get; set; } = "0 4 * * *";
    public int MaxChunkTokens { get; set; } = 1000;
    public int MinChunkTokens { get; set; } = 50;
    public int TargetChunkTokens { get; set; } = 750;
    public int OverlapTokens { get; set; } = 75;
    public int MaxContextTokens { get; set; } = 5000;
    public int MaxContextChunks { get; set; } = 10;
    public float MinSimilarityScore { get; set; } = 0.5f;
    public int MaxRetryCount { get; set; } = 3;
    public bool EnableEmbeddingCache { get; set; } = true;
}
