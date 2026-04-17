using EventFlow.Application.Common;
using EventFlow.Application.Features.Tickets.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Interfaces;
using MediatR;
using Polly;
using Polly.Retry;

namespace EventFlow.Application.Features.Tickets.Commands;

/// <summary>
/// Reserva um ingresso para o attendee logado.
///
/// REGRAS DE NEGÓCIO:
/// - Evento deve existir e estar Published
/// - Attendee não pode ter ingresso ativo no mesmo evento
/// - Se o evento está cheio: entra na lista de espera (em vez de erro)
/// - Se há vaga: cria o ingresso + aciona concorrência otimista no Event
///
/// CONCORRÊNCIA OTIMISTA:
/// Quando dois requests chegam ao mesmo tempo e o evento tem exatamente 1 vaga,
/// ambos passam na verificação de capacidade. O primeiro a fazer SaveChanges
/// atualiza o xmin do Event. O segundo recebe DbUpdateConcurrencyException.
/// Polly retenta: ao recarregar o Event, a vaga sumiu → vai para a fila.
/// </summary>
public record BookTicketCommand(Guid EventId) : IRequest<Result<BookTicketResponse>>;

public sealed class BookTicketCommandHandler(
    IEventRepository eventRepo,
    ITicketRepository ticketRepo,
    IWaitlistRepository waitlistRepo,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<BookTicketCommand, Result<BookTicketResponse>>
{
    // Política de retry do Polly:
    // - Retenta até 3 vezes em caso de conflito de concorrência otimista
    // - Não usa exponential backoff porque o conflito é resolvido no próprio retry
    //   (ao recarregar o Event, ou a vaga aparece ou o attendee vai para a fila)
    //
    // POLLY v8: usa ResiliencePipeline ao invés do builder antigo (AddPolicy)
    // O novo builder é mais explícito sobre o que estamos configurando
    private static readonly ResiliencePipeline _retryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<ConcurrencyException>(),
            MaxRetryAttempts = 3,
            // Sem delay: o conflito de concorrência é resolvido instantaneamente
            // (recarregamos o estado atual do banco e tomamos uma nova decisão)
            Delay = TimeSpan.Zero
        })
        .Build();

    public async Task<Result<BookTicketResponse>> Handle(
        BookTicketCommand request,
        CancellationToken cancellationToken)
    {
        var attendeeId = currentUser.Id;

        // Verificar duplicidade ANTES de entrar no retry — não precisa repetir isso
        var alreadyHasTicket = await ticketRepo.HasActiveTicketAsync(
            request.EventId, attendeeId, cancellationToken);

        if (alreadyHasTicket)
            return Result<BookTicketResponse>.Failure("Você já possui um ingresso ativo para este evento.");

        // Verificar se já está na fila de espera
        var alreadyInQueue = await waitlistRepo.IsInQueueAsync(
            request.EventId, attendeeId, cancellationToken);

        if (alreadyInQueue)
            return Result<BookTicketResponse>.Failure("Você já está na lista de espera para este evento.");

        // Resultado que será preenchido dentro do pipeline de retry
        Result<BookTicketResponse>? outcome = null;

        await _retryPipeline.ExecuteAsync(async ct =>
        {
            // Carregar o evento COM tracking — EF precisa saber o xmin atual para a verificação
            var @event = await eventRepo.GetByIdTrackedAsync(request.EventId, ct);

            if (@event is null)
            {
                outcome = Result<BookTicketResponse>.NotFound("Evento não encontrado.");
                return;
            }

            if (@event.Status != EventStatus.Published)
            {
                outcome = Result<BookTicketResponse>.Failure("Só é possível reservar ingressos para eventos publicados.");
                return;
            }

            if (@event.IsFull)
            {
                // Evento cheio — entrar na fila de espera
                // (pode chegar aqui no retry após concorrência resolver a última vaga)
                var position = await waitlistRepo.GetQueueSizeAsync(request.EventId, ct) + 1;
                var entry = WaitlistEntry.Create(request.EventId, attendeeId, position);
                await waitlistRepo.AddAsync(entry, ct);
                await unitOfWork.SaveChangesAsync(ct);

                outcome = Result<BookTicketResponse>.Success(new BookTicketResponse(
                    IsTicket: false,
                    Ticket: null,
                    WaitlistPosition: new WaitlistPositionDto(request.EventId, position, position)));
                return;
            }

            // Vaga disponível — criar ingresso e "tocar" o evento para acionar o xmin
            var ticket = Ticket.Create(request.EventId, attendeeId);
            await ticketRepo.AddAsync(ticket, ct);

            // Touch() força um UPDATE na linha do Event, que incluirá "WHERE xmin = {valor lido}"
            // Sem isso, só inserimos um Ticket (linha nova) sem modificar o Event — e o xmin não seria verificado
            @event.Touch();

            // Se ConcurrencyException for lançada aqui, Polly captura e retenta.
            // O UnitOfWork já recarregou o Event (novo xmin) e detachou os Added antes de relançar.
            // No retry: GetByIdTrackedAsync retorna o Event com o xmin atualizado.
            await unitOfWork.SaveChangesAsync(ct);

            outcome = Result<BookTicketResponse>.Success(new BookTicketResponse(
                IsTicket: true,
                Ticket: ticket.ToDto(@event.Title),
                WaitlistPosition: null));

        }, cancellationToken);

        // outcome nunca é null aqui — sempre é atribuído no ExecuteAsync
        return outcome!;
    }
}
