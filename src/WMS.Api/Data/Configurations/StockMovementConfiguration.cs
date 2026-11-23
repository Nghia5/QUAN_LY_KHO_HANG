using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    /// <summary>INSTEAD OF UPDATE, DELETE trigger created in migration AddStockMovements.</summary>
    public const string ImmutableTriggerName = "TR_StockMovements_Immutable";

    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", table =>
        {
            table.HasTrigger(ImmutableTriggerName);
            table.HasCheckConstraint("CK_StockMovements_Quantity_NonZero", "[Quantity] <> 0");
            table.HasCheckConstraint("CK_StockMovements_Before_NonNegative", "[QuantityBefore] >= 0");
            table.HasCheckConstraint("CK_StockMovements_After_NonNegative", "[QuantityAfter] >= 0");
            table.HasCheckConstraint("CK_StockMovements_Balance", "[QuantityAfter] = [QuantityBefore] + [Quantity]");
            table.HasCheckConstraint(
                "CK_StockMovements_Direction",
                "([MovementType] IN ('Receipt', 'TransferIn', 'AdjustmentIn', 'StocktakeIn') AND [Quantity] > 0) OR " +
                "([MovementType] IN ('Issue', 'TransferOut', 'AdjustmentOut', 'StocktakeOut') AND [Quantity] < 0)");
        });

        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.MovementType)
            .HasConversion<string>()
            .HasMaxLength(20);

        foreach (var quantity in new[] { nameof(StockMovement.Quantity), nameof(StockMovement.QuantityBefore), nameof(StockMovement.QuantityAfter) })
        {
            builder.Property(quantity).HasPrecision(InventoryConfiguration.QuantityPrecision, InventoryConfiguration.QuantityScale);
        }

        builder.Property(movement => movement.ReferenceType)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(movement => movement.ReferenceNo)
            .HasMaxLength(30);

        builder.Property(movement => movement.Note)
            .HasMaxLength(500);

        builder.Property(movement => movement.CreatedAt)
            .HasConversion(UtcDateTimeConverters.Required);

        // History of one inventory row (also proves the before/after chain).
        builder.HasIndex(movement => new { movement.InventoryId, movement.Id });
        // Listing / report by warehouse + period, by product, by document.
        builder.HasIndex(movement => new { movement.WarehouseId, movement.CreatedAt })
            .IncludeProperties(movement => new { movement.ProductId, movement.Quantity });
        builder.HasIndex(movement => new { movement.ProductId, movement.CreatedAt });
        builder.HasIndex(movement => new { movement.ReferenceType, movement.ReferenceId });
        builder.HasIndex(movement => movement.LocationId);

        builder.HasOne<Inventory>()
            .WithMany()
            .HasForeignKey(movement => movement.InventoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Product)
            .WithMany()
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Warehouse)
            .WithMany()
            .HasForeignKey(movement => movement.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Location)
            .WithMany()
            .HasForeignKey(movement => new { movement.LocationId, movement.WarehouseId })
            .HasPrincipalKey(location => new { location.Id, location.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
