using InterviewSimulator.Scraping.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewSimulator.Scraping.Data.EntityConfigurations;

public class ScrapedSourceConfiguration : IEntityTypeConfiguration<ScrapedSource>
{
    public void Configure(EntityTypeBuilder<ScrapedSource> builder)
    {
        builder.ToTable("ScrapedSources");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.BaseUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.SourceType)
            .IsRequired();

        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        builder.HasIndex(s => s.Name)
            .IsUnique();

        builder.HasMany(s => s.Questions)
            .WithOne(q => q.Source)
            .HasForeignKey(q => q.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Jobs)
            .WithOne(j => j.Source)
            .HasForeignKey(j => j.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
