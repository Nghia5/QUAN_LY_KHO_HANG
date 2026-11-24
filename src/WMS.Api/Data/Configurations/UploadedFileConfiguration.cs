using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class UploadedFileConfiguration : IEntityTypeConfiguration<UploadedFile>
{
    public void Configure(EntityTypeBuilder<UploadedFile> builder)
    {
        builder.ToTable("UploadedFiles");

        builder.HasKey(file => file.Id);

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
