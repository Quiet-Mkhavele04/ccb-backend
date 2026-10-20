using CCB.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CCB.API.Data;

public partial class CCBDbContext
{
    public virtual DbSet<PhoneVerificationCode>
        PhoneVerificationCodes { get; set; }

    partial void OnModelCreatingPartial(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PhoneVerificationCode>(entity =>
        {
            entity.ToTable("PhoneVerificationCodes");

            entity.HasKey(e =>
                e.PhoneVerificationCodeId);

            entity.Property(e =>
                    e.PhoneVerificationCodeId)
                .HasColumnName(
                    "PhoneVerificationCodeId");

            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.CodeHash)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.ExpiresAt)
                .HasColumnType("datetime2");

            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime2");

            entity.HasIndex(e => e.Phone)
                .HasDatabaseName(
                    "IX_PhoneVerificationCodes_Phone");
        });
        modelBuilder.Entity<Profile>(entity =>
{
    entity.Property(e => e.ProfilePhotoUrl)
        .HasMaxLength(500);
});
    }
}