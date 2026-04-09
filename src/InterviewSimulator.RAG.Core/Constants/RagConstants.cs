namespace InterviewSimulator.RAG.Core.Constants;

public static class RagConstants
{
    public const string DefaultCollectionName = "interview_questions";
    public const int DefaultEmbeddingDimensions = 768;
    public const int LargeEmbeddingDimensions = 3072;
    public const string SmallModelName = "nomic-embed-text";
    public const string LargeModelName = "text-embedding-3-large";
    public const decimal CostPerMillionTokensSmall = 0.020m;
    public const decimal CostPerMillionTokensLarge = 0.130m;
    public const int MinTokensForContent = 50;
    public const int LowContentThreshold = 100;
    public const int MinQuestionsForQAStrategy = 3;
    public const int MaxChunksPerSource = 3;
}
