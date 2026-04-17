using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Events;
using EventFlow.Domain.Exceptions;
using FluentAssertions;

namespace EventFlow.Domain.Tests.Entities;

/// <summary>
/// Testes unitários da entidade Ticket.
///
/// NOMENCLATURA DOS TESTES: MethodName_StateUnderTest_ExpectedBehavior
/// Exemplos:
///   Cancel_WhenTicketIsConfirmed_ShouldSetStatusToCancelled
///   Cancel_WhenTicketIsUsed_ShouldThrowDomainException
///
/// POR QUÊ esse padrão de nomenclatura?
/// - O nome do teste É a especificação do comportamento esperado
/// - Quando o teste falha, você sabe exatamente o que quebrou sem ler o código
/// - Facilita geração de documentação (alguns frameworks geram docs a partir dos nomes)
///
/// POR QUÊ testar o Domain especificamente?
/// - Domain é a camada mais crítica — contém as regras de negócio
/// - Domain não tem dependências externas (banco, HTTP) → testes são RÁPIDOS
/// - Se a lógica de estado do Ticket estiver errada, os outros testes mascaram o problema
///
/// ARRANGE-ACT-ASSERT (AAA):
/// Padrão de organização de testes:
///   // Arrange: prepara o estado inicial
///   // Act: executa a ação sendo testada
///   // Assert: verifica o resultado
///
/// POR QUÊ FluentAssertions ao invés de Assert.X?
///   Assert.Equal(TicketStatus.Cancelled, ticket.Status)  → mensagem genérica ao falhar
///   ticket.Status.Should().Be(TicketStatus.Cancelled)    → mensagem descritiva + leitura fluente
/// </summary>
public class TicketTests
{
    // --- TESTES DE CRIAÇÃO ---

    [Fact]
    public void Create_WithValidData_ShouldCreateTicketWithReservedStatus()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var attendeeId = Guid.NewGuid();

        // Act
        var ticket = Ticket.Create(eventId, attendeeId);

        // Assert
        ticket.EventId.Should().Be(eventId);
        ticket.AttendeeId.Should().Be(attendeeId);
        ticket.Status.Should().Be(TicketStatus.Reserved);
        ticket.TicketCode.Should().BeNull(); // código só é gerado na confirmação
        ticket.DomainEvents.Should().BeEmpty();
    }

    // --- TESTES DE CONFIRM ---

    [Fact]
    public void Confirm_WhenTicketIsReserved_ShouldSetStatusToConfirmedAndGenerateCode()
    {
        // Arrange
        var ticket = Ticket.Create(Guid.NewGuid(), Guid.NewGuid());

        // Act
        ticket.Confirm();

        // Assert
        ticket.Status.Should().Be(TicketStatus.Confirmed);
        ticket.TicketCode.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(TicketStatus.Confirmed)]
    [InlineData(TicketStatus.Used)]
    [InlineData(TicketStatus.Cancelled)]
    public void Confirm_WhenTicketIsNotReserved_ShouldThrowDomainException(TicketStatus invalidStatus)
    {
        // Arrange
        var ticket = CreateTicketInStatus(invalidStatus);

        // Act
        var act = () => ticket.Confirm();

        // Assert
        // Por quê [Theory] + [InlineData]?
        // Testa o mesmo comportamento para múltiplos inputs sem duplicar o código do teste
        // Cada InlineData vira um caso de teste separado no test runner
        act.Should().Throw<DomainException>()
            .WithMessage("*Reserved*"); // verifica que a mensagem menciona o estado esperado
    }

    // --- TESTES DE USE (CHECK-IN) ---

    [Fact]
    public void Use_WhenTicketIsConfirmed_ShouldSetStatusToUsed()
    {
        // Arrange
        var ticket = Ticket.Create(Guid.NewGuid(), Guid.NewGuid());
        ticket.Confirm();

        // Act
        ticket.Use();

        // Assert
        ticket.Status.Should().Be(TicketStatus.Used);
    }

    [Theory]
    [InlineData(TicketStatus.Reserved)]
    [InlineData(TicketStatus.Cancelled)]
    public void Use_WhenTicketIsNotConfirmed_ShouldThrowDomainException(TicketStatus invalidStatus)
    {
        // Arrange
        var ticket = CreateTicketInStatus(invalidStatus);

        // Act
        var act = () => ticket.Use();

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Confirmed*");
    }

    // --- TESTES DE CANCEL ---

    [Fact]
    public void Cancel_WhenTicketIsReserved_ShouldSetStatusToCancelledAndEmitDomainEvent()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var attendeeId = Guid.NewGuid();
        var ticket = Ticket.Create(eventId, attendeeId);

        // Act
        ticket.Cancel();

        // Assert
        ticket.Status.Should().Be(TicketStatus.Cancelled);

        // Verifica que o domain event foi emitido com os dados corretos
        ticket.DomainEvents.Should().HaveCount(1);
        ticket.DomainEvents.First().Should().BeOfType<TicketCancelled>()
            .Which.EventId.Should().Be(eventId);
    }

    [Fact]
    public void Cancel_WhenTicketIsConfirmed_ShouldSucceed()
    {
        // Arrange
        var ticket = Ticket.Create(Guid.NewGuid(), Guid.NewGuid());
        ticket.Confirm();

        // Act
        var act = () => ticket.Cancel();

        // Assert
        act.Should().NotThrow(); // confirmado ainda pode ser cancelado
        ticket.Status.Should().Be(TicketStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenTicketIsUsed_ShouldThrowDomainException()
    {
        // Arrange
        var ticket = CreateTicketInStatus(TicketStatus.Used);

        // Act
        var act = () => ticket.Cancel();

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*utilizado*");
    }

    [Fact]
    public void Cancel_WhenTicketIsAlreadyCancelled_ShouldThrowDomainException()
    {
        // Arrange
        var ticket = CreateTicketInStatus(TicketStatus.Cancelled);

        // Act
        var act = () => ticket.Cancel();

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*já está cancelado*");
    }

    // --- TESTES DE DOMAIN EVENTS ---

    [Fact]
    public void ClearDomainEvents_AfterCancel_ShouldRemoveAllEvents()
    {
        // Arrange
        var ticket = Ticket.Create(Guid.NewGuid(), Guid.NewGuid());
        ticket.Cancel();
        ticket.DomainEvents.Should().HaveCount(1); // garante que tem evento

        // Act
        ticket.ClearDomainEvents();

        // Assert
        ticket.DomainEvents.Should().BeEmpty();
    }

    // --- HELPER: cria ticket em um status específico sem passar pelo fluxo normal ---
    // Por quê esse helper?
    // Tickets em estado Cancelled/Used não podem ser criados pelo fluxo normal de forma trivial
    // Esse helper permite testar cenários "inválidos" sem duplicar setup
    private static Ticket CreateTicketInStatus(TicketStatus status)
    {
        var ticket = Ticket.Create(Guid.NewGuid(), Guid.NewGuid());

        switch (status)
        {
            case TicketStatus.Confirmed:
                ticket.Confirm();
                break;
            case TicketStatus.Used:
                ticket.Confirm();
                ticket.Use();
                break;
            case TicketStatus.Cancelled:
                ticket.Cancel();
                break;
            // Reserved é o estado inicial — nenhuma ação necessária
        }

        return ticket;
    }
}
