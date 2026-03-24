using InterviewSimulator.Scraping.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewSimulator.Scraping.Data.EntityConfigurations;

public class BehavioralQuestionConfiguration : IEntityTypeConfiguration<BehavioralQuestion>
{
    public void Configure(EntityTypeBuilder<BehavioralQuestion> builder)
    {
        builder.ToTable("BehavioralQuestions");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.QuestionText)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Competency)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.EvaluationMethod)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.Difficulty)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(b => b.IsActive)
            .HasDefaultValue(true);

        builder.Property(b => b.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(b => b.Category);
        builder.HasIndex(b => b.Difficulty);
        builder.HasIndex(b => new { b.Category, b.Difficulty })
            .HasDatabaseName("IX_BehavioralQuestions_Category_Difficulty");

        builder.HasMany(b => b.EvaluationCriteria)
            .WithOne(e => e.BehavioralQuestion)
            .HasForeignKey(e => e.BehavioralQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.RedFlags)
            .WithOne(r => r.BehavioralQuestion)
            .HasForeignKey(r => r.BehavioralQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.ExampleAnswers)
            .WithOne(e => e.BehavioralQuestion)
            .HasForeignKey(e => e.BehavioralQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
