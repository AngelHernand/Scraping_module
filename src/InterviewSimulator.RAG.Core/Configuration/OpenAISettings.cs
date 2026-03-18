namespace InterviewSimulator.RAG.Core.Configuration;

public class OpenAISettings
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public int EmbeddingDimensions { get; set; } = 1536;
    public int MaxTokensPerRequest { get; set; } = 8191;
    public int BatchSize { get; set; } = 100;
    public int RequestDelayMs { get; set; } = 200;
    public int MaxRetries { get; set; } = 3;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}
