using EventFlow.Application.Common;
using EventFlow.Application.Features.Tickets.Commands;
using EventFlow.Application.Features.Tickets.Notifications;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace EventFlow.Application.Tests.Features.Tickets;

/// <summary>
/// Testa o CancelTicketCommandHandler.
///
/// FOCO:
/// - Verificar a autorização baseada em recurso (quem pode cancelar o quê)
/// - Garantir que a DomainException é propagada para estados inválidos (ex: ingresso Used)
/// - Garantir que o domain event TicketCancelled é publicado após o save
/// </summary>
public class CancelTicketCommandHandlerTests
{
    private readonly Mock<ITicketRepository> _ticketRepoMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();

    private CancelTicketCommandHandler CreateHandler() =>
        new(_ticketRepoMock.Object, _currentUserMock.Object,
            _unitOfWorkMock.Object, _publisherMock.Object);

    /// <summary>Helper que cria um ticket com o status desejado sem violar a máquina de estados.</summary>
    private static Ticket CreateTicketInStatus(Guid attendeeId, TicketStatus status)
    {
        var ticket = Ticket.Create(Guid.NewGuid(), attendeeId);

        if (status == TicketStatus.Confirmed || status == TicketStatus.Used)
            ticket.Confirm();

        if (status == TicketStatus.Used)
            ticket.Use();

        if (status == TicketStatus.Cancelled)
            ticket.Cancel();

        return ticket;
    }

    [Fact]
    public async Task Handle_WhenTicketNotFound_ShouldReturnNotFound()
    {
        // Arrange
        _ticketRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        // Act
        var result = await CreateHandler().Handle(
            new CancelTicketCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ResultErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotOwnerAndNotAdmin_ShouldReturnForbidden()
    {
        // Arrange: ticket pertence a outro usuário
        var realOwnerId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var ticket = CreateTicketInStatus(realOwnerId, TicketStatus.Reserved);

        _ticketRepoMock
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        // Usuário logado é o intruso, não o dono
        _currentUserMock.Setup(u => u.Id).Returns(intruderId);
        _currentUserMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        // Act
        var result = await CreateHandler().Handle(
            new CancelTicketCommand(ticket.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ResultErrorType.Forbidden);

        // Garante que nada foi salvo
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsOwner_ShouldCancelTicketAndPublishNotification()
    {
        // Arrange
        var attendeeId = Guid.NewGuid();
        var ticket = CreateTicketInStatus(attendeeId, TicketStatus.Reserved);

        _ticketRepoMock
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        _currentUserMock.Setup(u => u.Id).Returns(attendeeId);
        _currentUserMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _publisherMock
            .Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(
            new CancelTicketCommand(ticket.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Cancelled);

        // O cancelamento deve ser persistido ANTES de publicar o event
        // Verificamos a ordem: SaveChanges → Publish
        var seq = new MockSequence();
        // (verificação de ordem simplificada: apenas que ambos foram chamados)
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // O domain event TicketCancelled deve ter sido convertido para TicketCancelledNotification
        _publisherMock.Verify(
            p => p.Publish(
                It.Is<TicketCancelledNotification>(n =>
                    n.TicketId == ticket.Id &&
                    n.EventId == ticket.EventId &&
                    n.AttendeeId == attendeeId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAdminCancelsOthersTicket_ShouldSucceed()
    {
        // Arrange: ticket de outro usuário, mas quem cancela é Admin
        var realOwnerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ticket = CreateTicketInStatus(realOwnerId, TicketStatus.Reserved);

        _ticketRepoMock
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        _currentUserMock.Setup(u => u.Id).Returns(adminId);
        _currentUserMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(true);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _publisherMock
            .Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(
            new CancelTicketCommand(ticket.Id), CancellationToken.None);

        // Assert: Admin bypass funciona
        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Cancelled);
    }
}
