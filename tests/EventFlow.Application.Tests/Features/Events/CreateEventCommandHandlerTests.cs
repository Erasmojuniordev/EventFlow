using EventFlow.Application.Features.Events.Commands;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace EventFlow.Application.Tests.Features.Events;

/// <summary>
/// Testes do handler CreateEventCommand.
///
/// COMO TESTAR O APPLICATION LAYER?
/// O handler tem dependências (repositório, ICurrentUser, IUnitOfWork).
/// Não queremos um banco de dados real aqui — isso seria teste de integração.
/// Usamos MOCKS: objetos falsos que simulam o comportamento das dependências.
///
/// O QUE É UM MOCK?
/// Um objeto que implementa a interface mas com comportamento controlado:
///   - mockRepo.Setup(r => r.AddAsync(...)) → define o que o método retorna
///   - mockRepo.Verify(r => r.AddAsync(...), Times.Once()) → verifica se foi chamado
///
/// POR QUÊ Moq especificamente?
/// É a biblioteca de mocking mais popular no ecossistema .NET.
/// Alternativas: NSubstitute (sintaxe mais limpa), FakeItEasy.
///
/// O QUE ESTAMOS TESTANDO AQUI?
/// Só a LÓGICA DO HANDLER — se ele chama o repositório correto,
/// se passa os dados certos, se retorna Success.
/// NÃO testamos se o banco persiste (isso é integração).
/// NÃO testamos as regras da entidade (isso é Domain.Tests).
/// </summary>
public class CreateEventCommandHandlerTests
{
    // Mocks das dependências — shared entre os testes da classe
    private readonly Mock<IEventRepository> _eventRepoMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private CreateEventCommandHandler CreateHandler() =>
        new(_eventRepoMock.Object, _currentUserMock.Object, _unitOfWorkMock.Object);

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccessWithEventDto()
    {
        // Arrange
        var organizerId = Guid.NewGuid();

        // Configura o mock para retornar o organizerId quando chamado
        _currentUserMock.Setup(u => u.Id).Returns(organizerId);

        // AddAsync e SaveChangesAsync não precisam retornar nada útil para este teste
        _eventRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Domain.Entities.Event>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new CreateEventCommand(
            Title: "Tech Conference 2025",
            Description: "Conferência de tecnologia",
            Location: "São Paulo, SP",
            StartsAt: DateTime.UtcNow.AddDays(30),
            EndsAt: DateTime.UtcNow.AddDays(30).AddHours(8),
            Capacity: 200);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Be(command.Title);
        result.Value.OrganizerId.Should().Be(organizerId);
        result.Value.Status.Should().Be("Draft"); // sempre começa em Draft

        // Verifica que o repositório foi chamado exatamente uma vez
        _eventRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Domain.Entities.Event>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verifica que o SaveChanges foi chamado
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldUseCurrentUserIdAsOrganizerId()
    {
        // Arrange: garante que o organizerId vem do ICurrentUser, não de outro lugar
        var expectedOrganizerId = Guid.NewGuid();
        _currentUserMock.Setup(u => u.Id).Returns(expectedOrganizerId);
        _eventRepoMock.Setup(r => r.AddAsync(It.IsAny<Domain.Entities.Event>(), default)).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var command = new CreateEventCommand("T", "D", "L",
            DateTime.UtcNow.AddDays(10),
            DateTime.UtcNow.AddDays(10).AddHours(4),
            50);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.Value!.OrganizerId.Should().Be(expectedOrganizerId);
    }
}
