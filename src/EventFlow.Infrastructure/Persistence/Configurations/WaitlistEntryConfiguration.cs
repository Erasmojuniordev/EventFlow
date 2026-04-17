using EventFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventFlow.Infrastructure.Persistence.Configurations;

public class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("waitlist_entries");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(w => w.AttendeeId).HasColumnName("attendee_id").IsRequired();
        builder.Property(w => w.Position).HasColumnName("position").IsRequired();
        builder.Property(w => w.IsPromoted).HasColumnName("is_promoted").IsRequired();
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");

        // Índice composto para buscar "próximo na fila" eficientemente
        // ORDER BY position WHERE event_id = X AND is_promoted = false
        builder.HasIndex(w => new { w.EventId, w.Position, w.IsPromoted })
            .HasDatabaseName("ix_waitlist_event_position");

        // Garante que um attendee não entra duas vezes na mesma fila
        builder.HasIndex(w => new { w.EventId, w.AttendeeId })
            .IsUnique()
            .HasDatabaseName("ix_waitlist_event_attendee_unique");
    }
}
