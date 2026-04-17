namespace EventFlow.Application.Interfaces;

/// <summary>
/// Abstração do Unit of Work — encapsula o SaveChanges do EF Core.
///
/// O QUE É UNIT OF WORK?
/// É um padrão que agrupa múltiplas operações em uma única transação.
/// Todas as operações acontecem, ou nenhuma acontece (atomicidade).
///
/// POR QUÊ não chamar SaveChanges diretamente nos repositórios?
/// Se o repositório chamasse SaveChanges internamente, cada operação
/// seria uma transação separada. Se a segunda falhasse, a primeira
/// já teria sido commitada — estado inconsistente.
///
/// Com Unit of Work:
///   await eventRepo.AddAsync(evento);      // só registra a intenção
///   await ticketRepo.AddAsync(ingresso);   // só registra a intenção
///   await unitOfWork.SaveChangesAsync();   // commita TUDO de uma vez
///
/// IMPLEMENTAÇÃO: o EF Core já implementa Unit of Work internamente
/// via DbContext. O IUnitOfWork aqui é só uma interface fina para:
/// 1. Manter o Application layer desacoplado do EF Core
/// 2. Facilitar mock nos testes
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
