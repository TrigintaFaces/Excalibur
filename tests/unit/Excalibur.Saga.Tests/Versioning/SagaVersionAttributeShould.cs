using Excalibur.Saga.Versioning;

namespace Excalibur.Saga.Tests.Versioning;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaVersionAttributeShould
{
	[Fact]
	public void StoreFromVersion()
	{
		// Arrange & Act
		var attr = new SagaVersionAttribute(1, 2);

		// Assert
		attr.FromVersion.ShouldBe(1);
	}

	[Fact]
	public void StoreToVersion()
	{
		// Arrange & Act
		var attr = new SagaVersionAttribute(1, 2);

		// Assert
		attr.ToVersion.ShouldBe(2);
	}

	[Fact]
	public void ThrowWhenFromVersionIsLessThanOne()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new SagaVersionAttribute(0, 2));
		Should.Throw<ArgumentOutOfRangeException>(() => new SagaVersionAttribute(-1, 2));
	}

	[Fact]
	public void ThrowWhenToVersionIsLessThanOrEqualToFromVersion()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new SagaVersionAttribute(2, 2));
		Should.Throw<ArgumentOutOfRangeException>(() => new SagaVersionAttribute(3, 2));
		Should.Throw<ArgumentOutOfRangeException>(() => new SagaVersionAttribute(5, 1));
	}

	[Fact]
	public void AcceptValidVersionRange()
	{
		// Arrange & Act
		var attr = new SagaVersionAttribute(1, 5);

		// Assert
		attr.FromVersion.ShouldBe(1);
		attr.ToVersion.ShouldBe(5);
	}

	[Fact]
	public void BeAttributeType()
	{
		typeof(Attribute).IsAssignableFrom(typeof(SagaVersionAttribute)).ShouldBeTrue();
	}

	[Fact]
	public void NotAllowMultiple()
	{
		// Arrange
		var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
			typeof(SagaVersionAttribute), typeof(AttributeUsageAttribute))!;

		// Assert
		usage.AllowMultiple.ShouldBeFalse();
	}

	[Fact]
	public void NotBeInherited()
	{
		// Arrange
		var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
			typeof(SagaVersionAttribute), typeof(AttributeUsageAttribute))!;

		// Assert
		usage.Inherited.ShouldBeFalse();
	}

	[Fact]
	public void TargetClassOnly()
	{
		// Arrange
		var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
			typeof(SagaVersionAttribute), typeof(AttributeUsageAttribute))!;

		// Assert
		usage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void AcceptConsecutiveVersions()
	{
		// Arrange & Act
		var attr = new SagaVersionAttribute(1, 2);

		// Assert
		attr.FromVersion.ShouldBe(1);
		attr.ToVersion.ShouldBe(2);
	}

	[Fact]
	public void AcceptLargeVersionGap()
	{
		// Arrange & Act
		var attr = new SagaVersionAttribute(1, 100);

		// Assert
		attr.FromVersion.ShouldBe(1);
		attr.ToVersion.ShouldBe(100);
	}
}
