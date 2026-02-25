using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class AggregateRootGetServiceShould
{
	[Fact]
	public void ReturnSelf_WhenServiceTypeMatchesAggregateType()
	{
		// Arrange
		var aggregate = new TestAggregate("test-id");

		// Act
		var result = aggregate.GetService(typeof(TestAggregate));

		// Assert
		result.ShouldBeSameAs(aggregate);
	}

	[Fact]
	public void ReturnSelf_WhenServiceTypeIsBaseType()
	{
		// Arrange
		var aggregate = new TestAggregate("test-id");

		// Act
		var result = aggregate.GetService(typeof(AggregateRoot));

		// Assert
		result.ShouldBeSameAs(aggregate);
	}

	[Fact]
	public void ReturnNull_WhenServiceTypeDoesNotMatch()
	{
		// Arrange
		var aggregate = new TestAggregate("test-id");

		// Act
		var result = aggregate.GetService(typeof(string));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ThrowOnNullServiceType()
	{
		// Arrange
		var aggregate = new TestAggregate("test-id");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => aggregate.GetService(null!));
	}

	[Fact]
	public void ReturnSelf_WhenServiceTypeIsInterface()
	{
		// Arrange
		var aggregate = new TestAggregate("test-id");

		// Act
		var result = aggregate.GetService(typeof(IAggregateRoot));

		// Assert
		result.ShouldBeSameAs(aggregate);
	}

	private sealed class TestAggregate : AggregateRoot
	{
		public TestAggregate(string id) : base(id) { }

		protected override void ApplyEventInternal(IDomainEvent @event) { }
	}
}
