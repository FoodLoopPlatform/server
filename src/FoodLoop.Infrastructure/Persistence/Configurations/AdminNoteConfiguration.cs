using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class AdminNoteConfiguration : IEntityTypeConfiguration<AdminNote>
{
    public void Configure(EntityTypeBuilder<AdminNote> builder)
    {
        builder.ToTable("AdminNotes");

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(4000).IsRequired();
        builder.Property(n => n.Category).HasMaxLength(50).IsRequired();
        builder.Property(n => n.Template).HasMaxLength(100);

        // Indexes to support the two primary query patterns:
        //   GET /admin/users/{id}/notes  → filter by RecipientUserId
        //   audit queries                → filter by SentByAdminId
        builder.HasIndex(n => n.RecipientUserId);
        builder.HasIndex(n => n.SentByAdminId);

        // No FK cascade configuration needed — admin notes survive user changes.
        // SentByAdminId and RecipientUserId are plain Guid FKs (no navigation properties
        // to avoid circular EF graph issues with ApplicationUser).
    }
}
