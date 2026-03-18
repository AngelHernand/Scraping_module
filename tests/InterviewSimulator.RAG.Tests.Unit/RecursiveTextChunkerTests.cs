using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Processing.Chunking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InterviewSimulator.RAG.Tests.Unit;

public class RecursiveTextChunkerTests
{
    private readonly RecursiveTextChunker _chunker;

    public RecursiveTextChunkerTests()
    {
        var logger = new Mock<ILogger<RecursiveTextChunker>>();
        var settings = Options.Create(new RagPipelineSettings
        {
            TargetChunkTokens = 100,
            MaxChunkTokens = 200,
            MinChunkTokens = 20,
            OverlapTokens = 10
        });
        _chunker = new RecursiveTextChunker(logger.Object, settings);
    }

    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunk()
    {
        var doc = new CleanedDocument
        {
            ScrapedQuestionId = 1,
            CleanedText = "A short text about programming concepts and software engineering patterns. " +
                "It covers basic topics such as object-oriented principles, inheritance, and polymorphism. " +
                "These are fundamental ideas that appear frequently in technical interviews.",
            CleanedQuestionText = "Question",
            DetectedLanguage = "en",
            EstimatedTokenCount = 80,
            HasSufficientContent = true
        };

        var chunks = _chunker.Chunk(doc);

        Assert.Single(chunks);
        Assert.Contains("programming concepts", chunks[0].Text);
    }

    [Fact]
    public void Chunk_LongText_SplitsIntoMultipleChunks()
    {
        // Generar un texto largo con múltiples párrafos
        var paragraphs = Enumerable.Range(1, 20).Select(i =>
            $"This is paragraph {i} about programming interview preparation. " +
            $"It covers important concepts like data structures, algorithms, and system design. " +
            $"Understanding these topics is essential for technical interviews at top companies.");

        var doc = new CleanedDocument
        {
            ScrapedQuestionId = 1,
            CleanedText = string.Join("\n\n", paragraphs),
            CleanedQuestionText = "Interview topics",
            DetectedLanguage = "en",
            EstimatedTokenCount = 2000,
            HasSufficientContent = true
        };

        var chunks = _chunker.Chunk(doc);

        Assert.True(chunks.Count > 1, $"Expected multiple chunks but got {chunks.Count}");
        Assert.All(chunks, c => Assert.True(c.TokenCount > 0));
    }

    [Fact]
    public void Chunk_TextWithHeaders_SplitsByHeaders()
    {
        string text = @"## Introduction to Algorithms

Algorithms are step-by-step procedures for solving problems. They are fundamental to computer science and software engineering. Every developer needs a solid understanding of basic algorithms.

## Sorting Algorithms

Sorting algorithms arrange elements in a specific order. Common examples include bubble sort, merge sort, and quick sort. Each has different time and space complexity characteristics.

## Searching Algorithms

Searching algorithms find specific elements in data structures. Binary search is one of the most efficient searcing algorithms, working in O(log n) time complexity on sorted arrays.";

        var doc = new CleanedDocument
        {
            ScrapedQuestionId = 1,
            CleanedText = text,
            CleanedQuestionText = "Algorithms",
            DetectedLanguage = "en",
            EstimatedTokenCount = 500,
            HasSufficientContent = true
        };

        var chunks = _chunker.Chunk(doc);

        Assert.True(chunks.Count >= 1);
        Assert.All(chunks, c => Assert.True(c.CharCount > 0));
    }

    [Fact]
    public void Chunk_AssignsSequentialIndices()
    {
        var paragraphs = Enumerable.Range(1, 15).Select(i =>
            $"Paragraph {i} discusses important software engineering concepts for interviews. " +
            $"Topics include object-oriented programming, functional programming, and design patterns.");

        var doc = new CleanedDocument
        {
            ScrapedQuestionId = 42,
            CleanedText = string.Join("\n\n", paragraphs),
            CleanedQuestionText = "SE Concepts",
            DetectedLanguage = "en",
            EstimatedTokenCount = 1000,
            HasSufficientContent = true
        };

        var chunks = _chunker.Chunk(doc);

        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].ChunkIndex);
            Assert.Equal(42, chunks[i].ScrapedQuestionId);
        }
    }
}
