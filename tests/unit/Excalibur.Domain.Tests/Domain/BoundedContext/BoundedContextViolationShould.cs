using Excalibur.Domain.BoundedContext;

namespace Excalibur.Tests.Domain.BoundedContext;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class BoundedContextViolationShould
{
	[Fact]
	public void StoreAllProperties()
	{
		// Arrange & Act
		var violation = new BoundedContextViolation(
			typeof(string),
			typeof(int),
			"OrdersContext",
			"InventoryContext",
			"Test violation description");

		// Assert
		violation.SourceType.ShouldBe(typeof(string));
		violation.TargetType.ShouldBe(typeof(int));
		violation.SourceContext.ShouldBe("OrdersContext");
		violation.TargetContext.ShouldBe("InventoryContext");
		violation.Description.ShouldBe("Test violation description");
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var violation1 = new BoundedContextViolation(typeof(string), typeof(int), "A", "B", "desc");
		var violation2 = new BoundedContextViolation(typeof(string), typeof(int), "A", "B", "desc");

		// Act & Assert
		violation1.ShouldBe(violation2);
		(violation1 == violation2).ShouldBeTrue();
	}

	[Fact]
	public void SupportRecordInequality()
	{
		// Arrange
		var violation1 = new BoundedContextViolation(typeof(string), typeof(int), "A", "B", "desc1");
		var violation2 = new BoundedContextViolation(typeof(string), typeof(int), "A", "B", "desc2");

		// Act & Assert
		violation1.ShouldNotBe(violation2);
	}
}
