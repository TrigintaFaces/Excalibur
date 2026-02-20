using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class KeyedAggregateRootCoverageShould
{
    [Fact]
    public void DefaultConstructor_InitializeWithDefaults()
    {
        // Act
        var aggregate = new TestKeyedAggregate();

        // Assert
        aggregate.Id.ShouldBe(Guid.Empty);
        aggregate.BusinessKey.ShouldBe(string.Empty);
        aggregate.Version.ShouldBe(0);
    }

    [Fact]
    public void ConstructorWithId_SetTechnicalId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var aggregate = new TestKeyedAggregate(id);

        // Assert
        aggregate.Id.ShouldBe(id);
    }

    [Fact]
    public void BusinessKey_ReturnDomainKey()
    {
        // Arrange
        var aggregate = new TestKeyedAggregate();
        aggregate.SetOrderNumber("ORD-001");

        // Act & Assert
        aggregate.BusinessKey.ShouldBe("ORD-001");
    }

    [Fact]
    public void BusinessKey_DifferentFromTechnicalId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregate = new TestKeyedAggregate(id);
        aggregate.SetOrderNumber("ORD-999");

        // Act & Assert
        aggregate.Id.ShouldBe(id);
        aggregate.BusinessKey.ShouldBe("ORD-999");
        aggregate.Id.ToString().ShouldNotBe(aggregate.BusinessKey);
    }

    [Fact]
    public void InheritsAggregateRootBehavior()
    {
        // Arrange
        var aggregate = new TestKeyedAggregate();

        // Act
        aggregate.RaiseTestEvent("test");

        // Assert
        aggregate.HasUncommittedEvents.ShouldBeTrue();
        aggregate.GetUncommittedEvents().Count.ShouldBe(1);
    }

    private sealed class TestKeyedAggregate : KeyedAggregateRoot<Guid, string>
    {
        private string _orderNumber = string.Empty;

        public TestKeyedAggregate() { }
        public TestKeyedAggregate(Guid id) : base(id) { }

        public override string BusinessKey => _orderNumber;

        public void SetOrderNumber(string orderNumber) => _orderNumber = orderNumber;

        public void RaiseTestEvent(string value) =>
            RaiseEvent(new TestKeyedEvent { Value = value });

        protected override void ApplyEventInternal(IDomainEvent @event)
        {
            if (@event is TestKeyedEvent e)
                _orderNumber = e.Value;
        }
    }

    private sealed record TestKeyedEvent : DomainEventBase
    {
        public string Value { get; init; } = string.Empty;
    }
}
