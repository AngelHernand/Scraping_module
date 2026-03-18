using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Core.Models.Enums;
using InterviewSimulator.RAG.Processing.Chunking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InterviewSimulator.RAG.Tests.Unit;

public class InterviewQuestionChunkerTests
{
    private readonly InterviewQuestionChunker _chunker;

    public InterviewQuestionChunkerTests()
    {
        var logger = new Mock<ILogger<InterviewQuestionChunker>>();
        var settings = Options.Create(new RagPipelineSettings
        {
            TargetChunkTokens = 750,
            MaxChunkTokens = 1000,
            MinChunkTokens = 50,
            OverlapTokens = 75
        });
        _chunker = new InterviewQuestionChunker(logger.Object, settings);
    }

    [Fact]
    public void CountDetectedQuestions_NumberedQuestions_DetectsAll()
    {
        string text = @"1. What is polymorphism?
Some answer text.
2. What is inheritance?
Some answer text.
3. What is encapsulation?
Some answer text.";

        int count = _chunker.CountDetectedQuestions(text);
        Assert.True(count >= 3);
    }

    [Fact]
    public void Chunk_MultipleQABlocks_CreatesChunksPerQuestion()
    {
        string content = @"Q: What is a binary tree?
A binary tree is a data structure where each node has at most two children, referred to as the left child and the right child. Binary trees are used for search operations and sorting algorithms in computer science.

Q: What is a hash table?
A hash table is a data structure that maps keys to values using a hash function. It provides O(1) average-case access time and is widely used in programming for efficient data lookup operations.

Q: What is a linked list?
A linked list is a linear data structure where elements are stored in nodes, with each node containing a reference to the next node. It allows efficient insertion and deletion of elements at any position.";

        var doc = new CleanedDocument
        {
            ScrapedQuestionId = 1,
            CleanedText = content,
            CleanedQuestionText = "Data structures",
            DetectedLanguage = "en",
            EstimatedTokenCount = 500,
            HasSufficientContent = true
        };

        var chunks = _chunker.Chunk(doc);

        Assert.True(chunks.Count >= 3);
        Assert.All(chunks, c => Assert.Equal(ChunkType.QuestionAnswer, c.Type));
        Assert.All(chunks, c => Assert.Equal("en", c.Language));
    }

    [Fact]
    public void Chunk_NoQuestionsDetected_ReturnsSingleChunk()
    {
        string content = "This is a general explanation about programming concepts. " +
            "It covers various topics including algorithms, data structures, and software design patterns. " +
            "These are fundamental concepts that every software developer should understand thoroughly.";

        var doc = new CleanedDocument
        {
            ScrapedQuestionId = 1,
            CleanedText = content,
            CleanedQuestionText = "Programming",
            DetectedLanguage = "en",
            EstimatedTokenCount = 150,
            HasSufficientContent = true
        };

        var chunks = _chunker.Chunk(doc);

        Assert.Single(chunks);
        Assert.Equal(ChunkType.GeneralContent, chunks[0].Type);
    }

    [Fact]
    public void CountDetectedQuestions_MarkdownHeaders_DetectsQuestions()
    {
        string text = @"## What is thread safety?
Answer about thread safety.
## How does garbage collection work?
Answer about GC.
## What are design patterns?
Answer about patterns.";

        int count = _chunker.CountDetectedQuestions(text);
        Assert.True(count >= 3);
    }
}
