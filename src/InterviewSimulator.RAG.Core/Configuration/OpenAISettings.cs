namespace InterviewSimulator.RAG.Core.Configuration;

public class OpenAISettings
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int EmbeddingDimensions { get; set; } = 768;
    public int MaxTokensPerRequest { get; set; } = 8191;
    public int BatchSize { get; set; } = 50;
    public int RequestDelayMs { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";
}
