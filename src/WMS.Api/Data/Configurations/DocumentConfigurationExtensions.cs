using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models.Documents;

namespace WMS.Api.Data.Configurations;

public static class DocumentConfigurationExtensions
{
    /// <summary>Shared mapping of <see cref="WarehouseDocument"/> header columns.</summary>
    public static void ConfigureDocument<T>(this EntityTypeBuilder<T> builder, string tableName)
        where T : WarehouseDocument
    {
        builder.ToTable(tableName, table => table.HasCheckConstraint(
            $"CK_{tableName}_Status",
            "[Status] IN ('Draft', 'PendingApproval', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled')"));

        builder.HasKey(document => document.Id);

        // Target of composite FKs from lines: a line always belongs to the header's warehouse.
        builder.HasAlternateKey(document => new { document.Id, document.WarehouseId });

        builder.Property(document => document.DocumentNo).HasMaxLength(30).IsRequired();
        builder.HasIndex(document => document.DocumentNo).IsUnique();

        builder.Property(document => document.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(document => new { document.WarehouseId, document.Status });
        builder.HasIndex(document => document.CreatedAt);

        builder.Property(document => document.Note).HasMaxLength(1000);
        builder.Property(document => document.RejectionReason).HasMaxLength(500);
        builder.Property(document => document.CancellationReason).HasMaxLength(500);
        builder.Property(document => document.RowVersion).IsRowVersion();

        builder.Property(document => document.CreatedAt).HasConversion(UtcDateTimeConverters.Required);
        builder.Property(document => document.UpdatedAt).HasConversion(UtcDateTimeConverters.Optional);
        builder.Property(document => document.SubmittedAt).HasConversion(UtcDateTimeConverters.Optional);
        builder.Property(document => document.ApprovedAt).HasConversion(UtcDateTimeConverters.Optional);
        builder.Property(document => document.RejectedAt).HasConversion(UtcDateTimeConverters.Optional);
        builder.Property(document => document.CompletedAt).HasConversion(UtcDateTimeConverters.Optional);
        builder.Property(document => document.CancelledAt).HasConversion(UtcDateTimeConverters.Optional);

        builder.HasOne(document => document.Warehouse)
            .WithMany()
            .HasForeignKey(document => document.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
