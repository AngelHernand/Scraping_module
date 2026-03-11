using InterviewSimulator.Scraping.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewSimulator.Scraping.Data.EntityConfigurations;

public class ScrapedQuestionConfiguration : IEntityTypeConfiguration<ScrapedQuestion>
{
    public void Configure(EntityTypeBuilder<ScrapedQuestion> builder)
    {
        builder.ToTable("ScrapedQuestions");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.QuestionText)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(q => q.QuestionTextNormalized)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(q => q.Category)
            .IsRequired();

        builder.Property(q => q.Subcategory)
            .HasMaxLength(200);

        builder.Property(q => q.DifficultyLevel)
            .IsRequired();

        builder.Property(q => q.Tags)
            .HasMaxLength(500);

        builder.Property(q => q.OriginalLanguage)
            .HasMaxLength(10)
            .HasDefaultValue("en");

        builder.Property(q => q.SourceUrl)
            .HasMaxLength(1000);

        builder.Property(q => q.RawContent)
            .HasColumnType("nvarchar(max)");

        builder.Property(q => q.AnswerText)
            .HasColumnType("nvarchar(max)");

        builder.Property(q => q.Technology)
            .HasMaxLength(100);

        builder.Property(q => q.HashFingerprint)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(q => q.HashFingerprint)
            .IsUnique();

        builder.HasIndex(q => q.Category);
        builder.HasIndex(q => q.DifficultyLevel);
        builder.HasIndex(q => q.Technology);

        builder.HasOne(q => q.DuplicateOf)
            .WithMany()
            .HasForeignKey(q => q.DuplicateOfId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
