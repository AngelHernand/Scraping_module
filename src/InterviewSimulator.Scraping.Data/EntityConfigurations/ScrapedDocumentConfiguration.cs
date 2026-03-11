using InterviewSimulator.Scraping.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewSimulator.Scraping.Data.EntityConfigurations;

/// <summary>
/// Configuración EF Core para la entidad ScrapedDocument (corpus RAG).
/// </summary>
public class ScrapedDocumentConfiguration : IEntityTypeConfiguration<ScrapedDocument>
{
    public void Configure(EntityTypeBuilder<ScrapedDocument> builder)
    {
        builder.ToTable("ScrapedDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(d => d.ContentNormalized)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.Category)
            .IsRequired();

        builder.Property(d => d.Subcategory)
            .HasMaxLength(200);

        builder.Property(d => d.Tags)
            .HasMaxLength(1000);

        builder.Property(d => d.SourceUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(d => d.SourceSite)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Language)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("en");

        builder.Property(d => d.ContentType)
            .IsRequired();

        builder.Property(d => d.Difficulty)
            .IsRequired();

        builder.Property(d => d.Technology)
            .HasMaxLength(100);

        builder.Property(d => d.HashFingerprint)
            .IsRequired()
            .HasMaxLength(64);

        // Índices
        builder.HasIndex(d => d.HashFingerprint)
            .IsUnique();

        builder.HasIndex(d => d.Category);
        builder.HasIndex(d => d.ContentType);
        builder.HasIndex(d => d.Difficulty);
        builder.HasIndex(d => d.Technology);
        builder.HasIndex(d => d.Language);
        builder.HasIndex(d => d.SourceSite);

        // Índice compuesto para búsquedas frecuentes
        builder.HasIndex(d => new { d.Category, d.Technology, d.Language })
            .HasDatabaseName("IX_ScrapedDocuments_Category_Tech_Lang");

        // Relaciones
        builder.HasOne(d => d.Source)
            .WithMany(s => s.Documents)
            .HasForeignKey(d => d.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ParentDocument)
            .WithMany(d => d.Chunks)
            .HasForeignKey(d => d.ParentDocumentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(d => d.DuplicateOf)
            .WithMany()
            .HasForeignKey(d => d.DuplicateOfId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
