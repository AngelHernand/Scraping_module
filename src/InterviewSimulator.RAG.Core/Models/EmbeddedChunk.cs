namespace InterviewSimulator.RAG.Core.Models;

/// <summary>
/// Un TextChunk con su vector embedding generado por OpenAI.
/// </summary>
public class EmbeddedChunk
{
    public TextChunk Chunk { get; set; } = null!;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public string ModelUsed { get; set; } = string.Empty;
    public int Dimensions { get; set; }
}
