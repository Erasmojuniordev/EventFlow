using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade Event para a tabela "events".
///
/// CONVENÇÕES ADOTADAS:
/// - snake_case para nomes de tabela e coluna (padrão PostgreSQL)
/// - Tabelas no plural (events, tickets, waitlist_entries)
/// - Colunas no singular (title, capacity, status)
///
/// POR QUÊ HasColumnName() em vez de deixar o EF gerar automaticamente?
/// - EF por padrão usa PascalCase (OrganizerId) — PostgreSQL é case-sensitive por padrão
/// - Com snake_case, queries SQL manuais ficam mais legíveis e idiomáticas no PostgreSQL
/// - Alternativa: usar o pacote EFCore.NamingConventions (configura globalmente) — válido também
/// </summary>
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.Location)
            .HasColumnName("location")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(e => e.StartsAt).HasColumnName("starts_at").IsRequired();
        builder.Property(e => e.EndsAt).HasColumnName("ends_at").IsRequired();
        builder.Property(e => e.Capacity).HasColumnName("capacity").IsRequired();
        builder.Property(e => e.OrganizerId).HasColumnName("organizer_id").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // Enum salvo como string no banco (não como int)
        // POR QUÊ string ao invés de int?
        // - Se reordenar o enum, os dados do banco não ficam desatualizados
        // - Queries manuais ficam legíveis: WHERE status = 'Published' vs WHERE status = 1
        // - Desvantagem: ocupa mais espaço. Para este domínio, irrelevante.
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Índice para queries de "eventos de um organizer"
        builder.HasIndex(e => e.OrganizerId).HasDatabaseName("ix_events_organizer_id");

        // Índice para filtrar por status (ex: listar só Published)
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_events_status");

        // Configura a coleção privada _tickets para o EF conseguir populá-la
        // sem precisar de setter público na entidade
        builder.HasMany(e => e.Tickets)
            .WithOne()
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.WaitlistEntries)
            .WithOne()
            .HasForeignKey(w => w.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // CONCORRÊNCIA OTIMISTA via xmin do PostgreSQL
        //
        // O que é xmin?
        // Todo registro no PostgreSQL tem uma coluna de sistema chamada "xmin" que guarda o ID
        // da transação que criou/modificou aquela versão da linha. É incrementado automaticamente
        // em cada UPDATE — sem precisar de coluna extra na aplicação.
        //
        // Como EF Core usa isso?
        // - Ao carregar o Event, EF registra o xmin atual como shadow property
        // - Ao fazer SaveChanges, gera: UPDATE events SET ... WHERE id = @id AND xmin = @xmin_original
        // - Se outro processo modificou o Event nesse intervalo, o WHERE não bate → 0 linhas afetadas
        // - EF Core lança DbUpdateConcurrencyException — convertida em ConcurrencyException pelo UnitOfWork
        //
        // FORMA ATUAL (Npgsql 8+): shadow property com tipo "xid" + IsRowVersion()
        // "xid" é o tipo PostgreSQL para transaction ID — mapeado para uint no .NET
        // IsRowVersion() = é um token de concorrência gerenciado pelo banco (não pela aplicação)
        //
        // FORMA ANTIGA (deprecated): builder.UseXminAsConcurrencyToken()
        // Foi depreciada no Npgsql 8 em favor do padrão EF Core abaixo.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();  // IsRowVersion() = IsConcurrencyToken() + ValueGeneratedOnAddOrUpdate()

        // Ignora propriedades calculadas (não mapeadas para coluna)
        builder.Ignore(e => e.AvailableSpots);
        builder.Ignore(e => e.IsFull);
    }
}
