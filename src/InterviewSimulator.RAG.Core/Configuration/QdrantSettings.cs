namespace InterviewSimulator.RAG.Core.Configuration;

public class QdrantSettings
{
    public const string SectionName = "Qdrant";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6333;
    public int GrpcPort { get; set; } = 6334;
    public bool UseGrpc { get; set; } = false;
    public string CollectionName { get; set; } = "interview_questions";
    public int UpsertBatchSize { get; set; } = 100;
    public string ApiKey { get; set; } = string.Empty;
}
