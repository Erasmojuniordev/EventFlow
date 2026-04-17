using EventFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventFlow.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(t => t.AttendeeId).HasColumnName("attendee_id").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.TicketCode)
            .HasColumnName("ticket_code")
            .HasMaxLength(100);  // HMAC assinado na Fase 5 pode ser mais longo

        // Índice composto: buscar ingressos ativos de um attendee em um evento específico
        // Usado na verificação "já tem ingresso ativo?"
        builder.HasIndex(t => new { t.EventId, t.AttendeeId })
            .HasDatabaseName("ix_tickets_event_attendee");

        builder.HasIndex(t => t.AttendeeId)
            .HasDatabaseName("ix_tickets_attendee_id");

        // Ignora a coleção de DomainEvents — não é persistida no banco
        builder.Ignore(t => t.DomainEvents);
    }
}
