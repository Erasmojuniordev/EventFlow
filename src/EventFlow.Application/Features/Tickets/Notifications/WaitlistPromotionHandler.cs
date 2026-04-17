using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Tickets.Notifications;

/// <summary>
/// Promove automaticamente o próximo da fila quando um ingresso é cancelado.
///
/// COMO FUNCIONA:
/// 1. Attendee A cancela ingresso → CancelTicketHandler salva o cancelamento
/// 2. CancelTicketHandler publica TicketCancelledNotification via MediatR
/// 3. MediatR chama este handler automaticamente
/// 4. Este handler busca a próxima entrada não-promovida da fila (menor Position)
/// 5. Cria um novo ingresso para esse attendee e marca a entrada como promovida
///
/// POR QUÊ INotificationHandler e não chamar diretamente de CancelTicketHandler?
///
/// DESACOPLAMENTO: CancelTicketCommand não precisa conhecer WaitlistRepository.
/// - Hoje: cancelar → promover fila
/// - Futuro: cancelar → promover fila + enviar email + atualizar analytics
/// - Com INotificationHandler: cada "reação" é um handler independente
/// - Sem INotificationHandler: CancelTicketHandler acumularia todas as responsabilidades
///
/// Este é o PRINCÍPIO DE RESPONSABILIDADE ÚNICA (SRP) em nível de handler:
/// CancelTicketHandler sabe cancelar. WaitlistPromotionHandler sabe promover.
///
/// GARANTIA DE EXECUÇÃO:
/// MediatR.Publish() chama TODOS os handlers registrados para a notification.
/// Se este handler lançar exceção, o caller (CancelTicketHandler) também receberá.
/// O ingresso já foi cancelado e salvo — o promotionHandler é um "best effort" aqui.
/// Em produção: usar Outbox Pattern ou Saga para garantia transacional.
/// </summary>
public sealed class WaitlistPromotionHandler(
    IWaitlistRepository waitlistRepo,
    ITicketRepository ticketRepo,
    IUnitOfWork unitOfWork) : INotificationHandler<TicketCancelledNotification>
{
    public async Task Handle(
        TicketCancelledNotification notification,
        CancellationToken cancellationToken)
    {
        // Busca o próximo na fila — menor Position que ainda não foi promovido
        var nextInQueue = await waitlistRepo.GetNextInQueueAsync(
            notification.EventId, cancellationToken);

        if (nextInQueue is null)
        {
            // Fila vazia — ninguém para promover. Isso é normal.
            return;
        }

        // Criar novo ingresso para o attendee promovido
        // O ingresso começa em Reserved — o attendee precisará confirmar (ConfirmTicketCommand)
        var ticket = Ticket.Create(notification.EventId, nextInQueue.AttendeeId);
        await ticketRepo.AddAsync(ticket, cancellationToken);

        // Marcar a entrada da fila como promovida para não ser promovida novamente
        nextInQueue.Promote();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
