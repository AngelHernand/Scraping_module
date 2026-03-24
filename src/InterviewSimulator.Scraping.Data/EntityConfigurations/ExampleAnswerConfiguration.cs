using InterviewSimulator.Scraping.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewSimulator.Scraping.Data.EntityConfigurations;

public class ExampleAnswerConfiguration : IEntityTypeConfiguration<ExampleAnswer>
{
    public void Configure(EntityTypeBuilder<ExampleAnswer> builder)
    {
        builder.ToTable("ExampleAnswers");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.AnswerText)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.Score)
            .IsRequired();
    }
}
