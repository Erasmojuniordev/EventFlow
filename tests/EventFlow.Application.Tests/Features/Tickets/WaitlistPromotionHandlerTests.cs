using EventFlow.Application.Features.Tickets.Notifications;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace EventFlow.Application.Tests.Features.Tickets;

/// <summary>
/// Testa o WaitlistPromotionHandler — o INotificationHandler que reage ao cancelamento.
///
/// COMO TESTAR INotificationHandler?
/// Ao contrário de IRequestHandler, o INotificationHandler não retorna valor.
/// Testamos os EFEITOS COLATERAIS: se um Ticket foi criado e se a entrada foi promovida.
///
/// POR QUÊ o handler é chamado automaticamente?
/// MediatR.Publish() itera sobre todos os INotificationHandler<T> registrados no container.
/// Aqui testamos o handler diretamente (sem MediatR) para focar na lógica isolada.
/// </summary>
public class WaitlistPromotionHandlerTests
{
    private readonly Mock<IWaitlistRepository> _waitlistRepoMock = new();
    private readonly Mock<ITicketRepository> _ticketRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private WaitlistPromotionHandler CreateHandler() =>
        new(_waitlistRepoMock.Object, _ticketRepoMock.Object, _unitOfWorkMock.Object);

    [Fact]
    public async Task Handle_WhenQueueIsEmpty_ShouldDoNothingAndNotSave()
    {
        // Arrange: GetNextInQueueAsync retorna null (fila vazia)
        var eventId = Guid.NewGuid();
        _waitlistRepoMock
            .Setup(r => r.GetNextInQueueAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);

        var notification = new TicketCancelledNotification(Guid.NewGuid(), eventId, Guid.NewGuid());

        // Act
        await CreateHandler().Handle(notification, CancellationToken.None);

        // Assert: nenhum ticket criado, nenhum save
        _ticketRepoMock.Verify(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenQueueHasEntries_ShouldCreateTicketAndMarkEntryAsPromoted()
    {
        // Arrange: há alguém na fila esperando
        var eventId = Guid.NewGuid();
        var waitingAttendeeId = Guid.NewGuid();

        // Cria uma WaitlistEntry real — testamos a entidade e o handler juntos
        var entry = WaitlistEntry.Create(eventId, waitingAttendeeId, position: 1);

        _waitlistRepoMock
            .Setup(r => r.GetNextInQueueAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        Ticket? capturedTicket = null;
        _ticketRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((t, _) => capturedTicket = t)
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var notification = new TicketCancelledNotification(Guid.NewGuid(), eventId, Guid.NewGuid());

        // Act
        await CreateHandler().Handle(notification, CancellationToken.None);

        // Assert: ticket foi criado para o attendee que estava na fila
        capturedTicket.Should().NotBeNull();
        capturedTicket!.EventId.Should().Be(eventId);
        capturedTicket.AttendeeId.Should().Be(waitingAttendeeId);

        // A entrada da fila deve ter sido promovida
        entry.IsPromoted.Should().BeTrue();

        // SaveChanges deve ter sido chamado
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
