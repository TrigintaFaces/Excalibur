using Excalibur.Domain.Specifications;

namespace Excalibur.Tests.Domain.Specifications;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class SpecificationShould
{
	[Fact]
	public void AndSpecification_ReturnTrue_WhenBothSatisfied()
	{
		// Arrange
		var greaterThan5 = new GreaterThanSpec(5);
		var lessThan10 = new LessThanSpec(10);
		var combined = greaterThan5.And(lessThan10);

		// Act & Assert
		combined.IsSatisfiedBy(7).ShouldBeTrue();
	}

	[Fact]
	public void AndSpecification_ReturnFalse_WhenLeftNotSatisfied()
	{
		// Arrange
		var greaterThan5 = new GreaterThanSpec(5);
		var lessThan10 = new LessThanSpec(10);
		var combined = greaterThan5.And(lessThan10);

		// Act & Assert
		combined.IsSatisfiedBy(3).ShouldBeFalse();
	}

	[Fact]
	public void AndSpecification_ReturnFalse_WhenRightNotSatisfied()
	{
		// Arrange
		var greaterThan5 = new GreaterThanSpec(5);
		var lessThan10 = new LessThanSpec(10);
		var combined = greaterThan5.And(lessThan10);

		// Act & Assert
		combined.IsSatisfiedBy(15).ShouldBeFalse();
	}

	[Fact]
	public void OrSpecification_ReturnTrue_WhenLeftSatisfied()
	{
		// Arrange
		var greaterThan5 = new GreaterThanSpec(5);
		var lessThan3 = new LessThanSpec(3);
		var combined = greaterThan5.Or(lessThan3);

		// Act & Assert
		combined.IsSatisfiedBy(10).ShouldBeTrue();
	}

	[Fact]
	public void OrSpecification_ReturnTrue_WhenRightSatisfied()
	{
		// Arrange
		var greaterThan5 = new GreaterThanSpec(5);
		var lessThan3 = new LessThanSpec(3);
		var combined = greaterThan5.Or(lessThan3);

		// Act & Assert
		combined.IsSatisfiedBy(1).ShouldBeTrue();
	}

	[Fact]
	public void OrSpecification_ReturnFalse_WhenNeitherSatisfied()
	{
		// Arrange
		var greaterThan5 = new GreaterThanSpec(5);
		var lessThan3 = new LessThanSpec(3);
		var combined = greaterThan5.Or(lessThan3);

		// Act & Assert
		combined.IsSatisfiedBy(4).ShouldBeFalse();
	}

	[Fact]
	public void NotSpecification_NegatesSatisfied()
	{
		// Arrange
		var greaterThan5 = new GreaterThanSpec(5);
		var notGreaterThan5 = greaterThan5.Not();

		// Act & Assert
		notGreaterThan5.IsSatisfiedBy(3).ShouldBeTrue();
		notGreaterThan5.IsSatisfiedBy(10).ShouldBeFalse();
	}

	[Fact]
	public void SupportComplexComposition()
	{
		// Arrange: (x > 5 AND x < 20) OR (x < 0)
		var greaterThan5 = new GreaterThanSpec(5);
		var lessThan20 = new LessThanSpec(20);
		var lessThan0 = new LessThanSpec(0);
		var combined = greaterThan5.And(lessThan20).Or(lessThan0);

		// Act & Assert
		combined.IsSatisfiedBy(10).ShouldBeTrue();  // passes first branch
		combined.IsSatisfiedBy(-5).ShouldBeTrue();   // passes second branch
		combined.IsSatisfiedBy(3).ShouldBeFalse();   // fails both
		combined.IsSatisfiedBy(25).ShouldBeFalse();  // fails both
	}

	[Fact]
	public void And_ThrowOnNull()
	{
		// Arrange
		var spec = new GreaterThanSpec(5);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => spec.And(null!));
	}

	[Fact]
	public void Or_ThrowOnNull()
	{
		// Arrange
		var spec = new GreaterThanSpec(5);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => spec.Or(null!));
	}

	[Fact]
	public void DoubleNot_ReturnsOriginalResult()
	{
		// Arrange
		var greaterThan5 = new GreaterThanSpec(5);
		var doubleNot = greaterThan5.Not().Not();

		// Act & Assert
		doubleNot.IsSatisfiedBy(10).ShouldBeTrue();
		doubleNot.IsSatisfiedBy(3).ShouldBeFalse();
	}

	private sealed class GreaterThanSpec : Specification<int>
	{
		private readonly int _threshold;

		public GreaterThanSpec(int threshold) => _threshold = threshold;

		public override bool IsSatisfiedBy(int candidate) => candidate > _threshold;
	}

	private sealed class LessThanSpec : Specification<int>
	{
		private readonly int _threshold;

		public LessThanSpec(int threshold) => _threshold = threshold;

		public override bool IsSatisfiedBy(int candidate) => candidate < _threshold;
	}
}
