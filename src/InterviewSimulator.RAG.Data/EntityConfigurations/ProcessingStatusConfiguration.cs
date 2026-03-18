using InterviewSimulator.RAG.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewSimulator.RAG.Data.EntityConfigurations;

public class ProcessingStatusConfiguration : IEntityTypeConfiguration<ProcessingStatus>
{
    public void Configure(EntityTypeBuilder<ProcessingStatus> builder)
    {
        builder.ToTable("ProcessingStatuses");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();

        builder.Property(p => p.ScrapedQuestionId).IsRequired();
        builder.HasIndex(p => p.ScrapedQuestionId).IsUnique();

        builder.Property(p => p.State).IsRequired();

        builder.Property(p => p.EmbeddingModel).HasMaxLength(100);
        builder.Property(p => p.QdrantCollectionName).HasMaxLength(100);

        builder.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(p => p.State);
    }
}
