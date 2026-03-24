using InterviewSimulator.Scraping.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewSimulator.Scraping.Data.EntityConfigurations;

public class RedFlagConfiguration : IEntityTypeConfiguration<RedFlag>
{
    public void Configure(EntityTypeBuilder<RedFlag> builder)
    {
        builder.ToTable("RedFlags");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.FlagText)
            .IsRequired()
            .HasMaxLength(500);
    }
}
