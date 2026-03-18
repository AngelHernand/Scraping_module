namespace InterviewSimulator.RAG.Core.Models.Enums;

public enum ProcessingState
{
    Pending = 0,
    Cleaned = 1,
    Chunked = 2,
    Embedded = 3,
    Stored = 4,
    Failed = 5,
    Skipped = 6
}
