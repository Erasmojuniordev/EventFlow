using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Exceptions;
using FluentAssertions;

namespace EventFlow.Domain.Tests.Entities;

public class EventTests
{
    // --- HELPERS ---
    private static Event CreateValidEvent(DateTime? startsAt = null) => Event.Create(
        title: "Rock in Rio 2025",
        description: "Festival de música",
        location: "Cidade do Rock, RJ",
        startsAt: startsAt ?? DateTime.UtcNow.AddDays(30),
        endsAt: (startsAt ?? DateTime.UtcNow.AddDays(30)).AddHours(8),
        capacity: 100,
        organizerId: Guid.NewGuid());

    // --- CRIAÇÃO ---

    [Fact]
    public void Create_WithValidData_ShouldCreateDraftEvent()
    {
        // Act
        var @event = CreateValidEvent();

        // Assert
        @event.Status.Should().Be(EventStatus.Draft);
        @event.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WhenEndsAtBeforeStartsAt_ShouldThrowDomainException()
    {
        // Act
        var act = () => Event.Create("Titulo", "Desc", "Local",
            startsAt: DateTime.UtcNow.AddDays(5),
            endsAt: DateTime.UtcNow.AddDays(1),  // antes do startsAt
            capacity: 100,
            organizerId: Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*término*início*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidCapacity_ShouldThrowDomainException(int capacity)
    {
        var act = () => Event.Create("Titulo", "Desc", "Local",
            DateTime.UtcNow.AddDays(5),
            DateTime.UtcNow.AddDays(6),
            capacity,
            Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*capacidade*zero*");
    }

    // --- PUBLISH ---

    [Fact]
    public void Publish_WhenEventIsDraft_ShouldSetStatusToPublished()
    {
        // Arrange
        var @event = CreateValidEvent();

        // Act
        @event.Publish();

        // Assert
        @event.Status.Should().Be(EventStatus.Published);
    }

    [Fact]
    public void Publish_WhenEventStartsInPast_ShouldThrowDomainException()
    {
        // Arrange: evento com data no passado
        var pastDate = DateTime.UtcNow.AddDays(-1);
        var @event = CreateValidEvent(startsAt: pastDate);

        // Act
        var act = () => @event.Publish();

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*passado*");
    }

    [Theory]
    [InlineData(EventStatus.Published)]
    [InlineData(EventStatus.Cancelled)]
    [InlineData(EventStatus.Completed)]
    public void Publish_WhenEventIsNotDraft_ShouldThrowDomainException(EventStatus invalidStatus)
    {
        var @event = CreateEventInStatus(invalidStatus);
        var act = () => @event.Publish();
        act.Should().Throw<DomainException>();
    }

    // --- CANCEL ---

    [Theory]
    [InlineData(EventStatus.Draft)]
    [InlineData(EventStatus.Published)]
    public void Cancel_WhenEventIsCancellable_ShouldSetStatusToCancelled(EventStatus cancellableStatus)
    {
        var @event = CreateEventInStatus(cancellableStatus);
        @event.Cancel();
        @event.Status.Should().Be(EventStatus.Cancelled);
    }

    [Theory]
    [InlineData(EventStatus.Cancelled)]
    [InlineData(EventStatus.Completed)]
    public void Cancel_WhenEventIsTerminal_ShouldThrowDomainException(EventStatus terminalStatus)
    {
        var @event = CreateEventInStatus(terminalStatus);
        var act = () => @event.Cancel();
        act.Should().Throw<DomainException>();
    }

    // --- COMPLETE ---

    [Fact]
    public void Complete_WhenEventIsPublished_ShouldSetStatusToCompleted()
    {
        var @event = CreateEventInStatus(EventStatus.Published);
        @event.Complete();
        @event.Status.Should().Be(EventStatus.Completed);
    }

    [Theory]
    [InlineData(EventStatus.Draft)]
    [InlineData(EventStatus.Cancelled)]
    [InlineData(EventStatus.Completed)]
    public void Complete_WhenEventIsNotPublished_ShouldThrowDomainException(EventStatus invalidStatus)
    {
        var @event = CreateEventInStatus(invalidStatus);
        var act = () => @event.Complete();
        act.Should().Throw<DomainException>();
    }

    // --- HELPER ---
    private static Event CreateEventInStatus(EventStatus status)
    {
        var @event = CreateValidEvent();

        switch (status)
        {
            case EventStatus.Published:
                @event.Publish();
                break;
            case EventStatus.Cancelled:
                @event.Cancel();
                break;
            case EventStatus.Completed:
                @event.Publish();
                @event.Complete();
                break;
            // Draft é o estado inicial
        }

        return @event;
    }
}
