using InterviewSimulator.Scraping.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewSimulator.Scraping.Data.EntityConfigurations;

public class EvaluationCriteriaConfiguration : IEntityTypeConfiguration<EvaluationCriteria>
{
    public void Configure(EntityTypeBuilder<EvaluationCriteria> builder)
    {
        builder.ToTable("EvaluationCriteria");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CriteriaText)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Weight)
            .HasDefaultValue(1);

        builder.Property(e => e.OrderIndex)
            .IsRequired();
    }
}
