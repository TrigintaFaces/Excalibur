namespace Excalibur.Dispatch.Abstractions.Tests.EventSourcing;

/// <summary>
/// Contract tests for IDomainEvent interface.
/// Verifies the interface defines all required properties for domain event sourcing.
/// </summary>
[Trait("Category", "Unit")]
public class IDomainEventContractShould
{
	[Fact]
	public void Have_EventId_Property()
	{
		// Arrange
		var interfaceType = typeof(IDomainEvent);

		// Act
		var property = interfaceType.GetProperty(nameof(IDomainEvent.EventId));

		// Assert
		_ = property.ShouldNotBeNull("IDomainEvent must define EventId property");
		property.PropertyType.ShouldBe(typeof(string), "EventId must be of type string");
		property.CanRead.ShouldBeTrue("EventId must be readable");
	}

	[Fact]
	public void Have_AggregateId_Property()
	{
		// Arrange
		var interfaceType = typeof(IDomainEvent);

		// Act
		var property = interfaceType.GetProperty(nameof(IDomainEvent.AggregateId));

		// Assert
		_ = property.ShouldNotBeNull("IDomainEvent must define AggregateId property");
		property.PropertyType.ShouldBe(typeof(string), "AggregateId must be of type string");
		property.CanRead.ShouldBeTrue("AggregateId must be readable");
	}

	[Fact]
	public void Have_Version_Property()
	{
		// Arrange
		var interfaceType = typeof(IDomainEvent);

		// Act
		var property = interfaceType.GetProperty(nameof(IDomainEvent.Version));

		// Assert
		_ = property.ShouldNotBeNull("IDomainEvent must define Version property");
		property.PropertyType.ShouldBe(typeof(long), "Version must be of type long");
		property.CanRead.ShouldBeTrue("Version must be readable");
	}

	[Fact]
	public void Have_OccurredAt_Property()
	{
		// Arrange
		var interfaceType = typeof(IDomainEvent);

		// Act
		var property = interfaceType.GetProperty(nameof(IDomainEvent.OccurredAt));

		// Assert
		_ = property.ShouldNotBeNull("IDomainEvent must define OccurredAt property");
		property.PropertyType.ShouldBe(typeof(DateTimeOffset), "OccurredAt must be of type DateTimeOffset");
		property.CanRead.ShouldBeTrue("OccurredAt must be readable");
	}

	[Fact]
	public void Have_EventType_Property()
	{
		// Arrange
		var interfaceType = typeof(IDomainEvent);

		// Act
		var property = interfaceType.GetProperty(nameof(IDomainEvent.EventType));

		// Assert
		_ = property.ShouldNotBeNull("IDomainEvent must define EventType property");
		property.PropertyType.ShouldBe(typeof(string), "EventType must be of type string");
		property.CanRead.ShouldBeTrue("EventType must be readable");
	}

	[Fact]
	public void Have_Metadata_Property()
	{
		// Arrange
		var interfaceType = typeof(IDomainEvent);

		// Act
		var property = interfaceType.GetProperty(nameof(IDomainEvent.Metadata));

		// Assert
		_ = property.ShouldNotBeNull("IDomainEvent must define Metadata property");
		property.PropertyType.ShouldBe(typeof(IDictionary<string, object>), "Metadata must be of type IDictionary<string, object>");
		property.CanRead.ShouldBeTrue("Metadata must be readable");
	}
}
