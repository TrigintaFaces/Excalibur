using Excalibur.Saga.Correlation;

namespace Excalibur.Saga.Tests.Correlation;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaMessageCorrelationAttributeShould
{
	[Fact]
	public void BeAnAttribute()
	{
		typeof(Attribute).IsAssignableFrom(typeof(SagaMessageCorrelationAttribute)).ShouldBeTrue();
	}

	[Fact]
	public void NotAllowMultiple()
	{
		// Arrange
		var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
			typeof(SagaMessageCorrelationAttribute), typeof(AttributeUsageAttribute))!;

		// Assert
		usage.AllowMultiple.ShouldBeFalse();
	}

	[Fact]
	public void BeInheritable()
	{
		// Arrange
		var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
			typeof(SagaMessageCorrelationAttribute), typeof(AttributeUsageAttribute))!;

		// Assert
		usage.Inherited.ShouldBeTrue();
	}

	[Fact]
	public void TargetPropertyOnly()
	{
		// Arrange
		var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
			typeof(SagaMessageCorrelationAttribute), typeof(AttributeUsageAttribute))!;

		// Assert
		usage.ValidOn.ShouldBe(AttributeTargets.Property);
	}

	[Fact]
	public void BeConstructableWithNoArguments()
	{
		// Act
		var attr = new SagaMessageCorrelationAttribute();

		// Assert
		attr.ShouldNotBeNull();
	}

	[Fact]
	public void BeDiscoverableOnDecoratedProperty()
	{
		// Arrange
		var prop = typeof(TestMessage).GetProperty(nameof(TestMessage.OrderId))!;

		// Act
		var attr = Attribute.GetCustomAttribute(prop, typeof(SagaMessageCorrelationAttribute));

		// Assert
		attr.ShouldNotBeNull();
	}

	[Fact]
	public void NotBeFoundOnUndecoratedProperty()
	{
		// Arrange
		var prop = typeof(TestMessage).GetProperty(nameof(TestMessage.Amount))!;

		// Act
		var attr = Attribute.GetCustomAttribute(prop, typeof(SagaMessageCorrelationAttribute));

		// Assert
		attr.ShouldBeNull();
	}

	[Fact]
	public void BeInheritedOnDerivedClassProperty()
	{
		// Arrange
		var prop = typeof(DerivedMessage).GetProperty(nameof(DerivedMessage.OrderId))!;

		// Act
		var attr = Attribute.GetCustomAttribute(prop, typeof(SagaMessageCorrelationAttribute), inherit: true);

		// Assert
		attr.ShouldNotBeNull();
	}

	private class TestMessage
	{
		[SagaMessageCorrelation]
		public virtual string? OrderId { get; set; }

		public decimal Amount { get; set; }
	}

	private class DerivedMessage : TestMessage
	{
		public override string? OrderId { get; set; }
	}
}
