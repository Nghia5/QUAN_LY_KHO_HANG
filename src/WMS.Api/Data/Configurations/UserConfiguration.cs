using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", table =>
        {
            table.HasCheckConstraint("CK_Users_Username_MinLength", "LEN([Username]) >= 3");
            table.HasCheckConstraint("CK_Users_Email_Format", "[Email] LIKE '%_@__%.__%'");
            table.HasCheckConstraint("CK_Users_SoftDeleteState",
                "[DeletedAt] IS NULL OR ([IsActive] = 0 AND [DeletedBy] IS NOT NULL AND [DeletedBy] > 0)");
        });

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(user => user.Username)
            .IsUnique();

        builder.Property(user => user.Email)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(user => user.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(user => user.FullName);

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(user => user.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(user => user.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(user => user.IsActive);

        builder.Property(user => user.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(UtcDateTimeConverters.Required);

        builder.Property(user => user.UpdatedAt)
            .HasConversion(UtcDateTimeConverters.Optional);

        builder.Property(user => user.DeletedAt)
            .HasConversion(UtcDateTimeConverters.Optional);

        builder.HasMany(user => user.UserRoles)
            .WithOne(userRole => userRole.User)
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.RefreshTokens)
            .WithOne(refreshToken => refreshToken.User)
            .HasForeignKey(refreshToken => refreshToken.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.UploadedFiles)
            .WithOne(uploadedFile => uploadedFile.Uploader)
            .HasForeignKey(uploadedFile => uploadedFile.UploadedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
