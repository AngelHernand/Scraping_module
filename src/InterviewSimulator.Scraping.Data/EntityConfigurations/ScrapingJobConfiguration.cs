using InterviewSimulator.Scraping.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewSimulator.Scraping.Data.EntityConfigurations;

public class ScrapingJobConfiguration : IEntityTypeConfiguration<ScrapingJob>
{
    public void Configure(EntityTypeBuilder<ScrapingJob> builder)
    {
        builder.ToTable("ScrapingJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Status)
            .IsRequired();

        builder.Property(j => j.ErrorMessage)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.StartedAt);
    }
}
