using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class UploadedFileConfiguration : IEntityTypeConfiguration<UploadedFile>
{
    public void Configure(EntityTypeBuilder<UploadedFile> builder)
    {
        builder.ToTable("UploadedFiles", table => table.HasCheckConstraint(
            "CK_UploadedFiles_Document",
            "([DocumentType] IS NULL AND [DocumentId] IS NULL) OR " +
            "([DocumentType] IS NOT NULL AND [DocumentType] BETWEEN 1 AND 5 AND [DocumentId] IS NOT NULL AND [DocumentId] > 0)"));

        builder.HasKey(file => file.Id);
        builder.HasIndex(file => new { file.DocumentType, file.DocumentId });

        builder.Property(file => file.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(file => file.StoredFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(file => file.FilePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(file => file.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(file => file.UploadedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(UtcDateTimeConverters.Required);
    }
}
