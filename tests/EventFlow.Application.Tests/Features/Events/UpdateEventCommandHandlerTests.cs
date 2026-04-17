using EventFlow.Application.Features.Events.Commands;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace EventFlow.Application.Tests.Features.Events;

public class UpdateEventCommandHandlerTests
{
    private readonly Mock<IEventRepository> _eventRepoMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private UpdateEventCommandHandler CreateHandler() =>
        new(_eventRepoMock.Object, _currentUserMock.Object, _unitOfWorkMock.Object);

    [Fact]
    public async Task Handle_WhenEventNotFound_ShouldReturnNotFound()
    {
        // Arrange
        _eventRepoMock
            .Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null); // simula "não encontrado no banco"

        var command = new UpdateEventCommand(Guid.NewGuid(), "T", "D", "L",
            DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(11), 100);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(Common.ResultErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotOwner_ShouldReturnForbidden()
    {
        // Arrange: evento pertence ao Organizer A, mas usuário logado é o Organizer B
        var ownerOrganizerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();

        var @event = Event.Create("T", "D", "L",
            DateTime.UtcNow.AddDays(10),
            DateTime.UtcNow.AddDays(11),
            100,
            ownerOrganizerId);

        _eventRepoMock
            .Setup(r => r.GetByIdTrackedAsync(@event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        // Usuário logado é DIFERENTE do dono
        _currentUserMock.Setup(u => u.Id).Returns(differentUserId);
        _currentUserMock.Setup(u => u.IsInRole(Domain.Enums.UserRole.Admin)).Returns(false);

        var command = new UpdateEventCommand(@event.Id, "Novo Título", "D", "L",
            DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(11), 100);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(Common.ResultErrorType.Forbidden);

        // Garante que o banco não foi modificado
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsAdmin_ShouldAllowUpdateEvenIfNotOwner()
    {
        // Arrange: Admin pode editar qualquer evento, independente do dono
        var ownerOrganizerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var @event = Event.Create("T", "D", "L",
            DateTime.UtcNow.AddDays(10),
            DateTime.UtcNow.AddDays(11),
            100,
            ownerOrganizerId);

        _eventRepoMock
            .Setup(r => r.GetByIdTrackedAsync(@event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _currentUserMock.Setup(u => u.Id).Returns(adminId); // Admin ≠ dono
        _currentUserMock.Setup(u => u.IsInRole(Domain.Enums.UserRole.Admin)).Returns(true); // mas é Admin
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var command = new UpdateEventCommand(@event.Id, "Novo Título", "D", "L",
            DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(11), 100);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
