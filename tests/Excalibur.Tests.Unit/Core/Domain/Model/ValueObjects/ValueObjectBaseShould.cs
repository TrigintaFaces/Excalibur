using Excalibur.Core.Domain.Model.ValueObjects;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Domain.Model.ValueObjects;

/// <summary>
///     Unit tests for the <see cref="ValueObjectBase" /> class.
/// </summary>
public class ValueObjectBaseShould
{
	[Fact]
	public void ReturnTrueWhenComparingEqualObjects()
	{
		// Arrange
		var valueObject1 = new TestValueObject(1, "Test");
		var valueObject2 = new TestValueObject(1, "Test");

		// Act
		var result = valueObject1.Equals(valueObject2);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseWhenComparingDifferentObjects()
	{
		// Arrange
		var valueObject1 = new TestValueObject(1, "Test");
		var valueObject2 = new TestValueObject(2, "Test");

		// Act
		var result = valueObject1.Equals(valueObject2);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseWhenComparingWithNull()
	{
		// Arrange
		var valueObject = new TestValueObject(1, "Test");

		// Act
#pragma warning disable CA1508 // Avoid dead conditional code
		var result = valueObject.Equals(null);
#pragma warning restore CA1508 // Avoid dead conditional code

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseWhenComparingDifferentTypes()
	{
		// Arrange
		var valueObject1 = new TestValueObject(1, "Test");
		var valueObject2 = new AnotherTestValueObject(1, "Test");

		// Act
		var result = valueObject1.Equals(valueObject2);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnSameHashCodeForEqualObjects()
	{
		// Arrange
		var valueObject1 = new TestValueObject(1, "Test");
		var valueObject2 = new TestValueObject(1, "Test");

		// Act
		var hashCode1 = valueObject1.GetHashCode();
		var hashCode2 = valueObject2.GetHashCode();

		// Assert
		hashCode1.ShouldBe(hashCode2);
	}

	[Fact]
	public void ReturnDifferentHashCodeForDifferentObjects()
	{
		// Arrange
		var valueObject1 = new TestValueObject(1, "Test");
		var valueObject2 = new TestValueObject(2, "Test");

		// Act
		var hashCode1 = valueObject1.GetHashCode();
		var hashCode2 = valueObject2.GetHashCode();

		// Assert
		hashCode1.ShouldNotBe(hashCode2);
	}

	[Fact]
	public void IncludeTypeInHashCode()
	{
		// Arrange
		var valueObject1 = new TestValueObject(1, "Test");
		var valueObject2 = new AnotherTestValueObject(1, "Test");

		// Act
		var hashCode1 = valueObject1.GetHashCode();
		var hashCode2 = valueObject2.GetHashCode();

		// Assert
		hashCode1.ShouldNotBe(hashCode2);
	}

	/// <summary>
	///     Implement of value object base for testing.
	/// </summary>
	private sealed class TestValueObject(int id, string name) : ValueObjectBase
	{
		public int Id { get; } = id;
		public string Name { get; } = name;

		protected override bool EqualsInternal(IValueObject? other)
		{
			if (other is not TestValueObject testValueObject)
			{
				return false;
			}

			return Id == testValueObject.Id && Name == testValueObject.Name;
		}

		protected override int GetHashCodeInternal()
		{
			return HashCode.Combine(Id, Name);
		}
	}

	/// <summary>
	///     Another implementation of value object base with the same properties but different behavior for testing.
	/// </summary>
	private sealed class AnotherTestValueObject(int id, string name) : ValueObjectBase
	{
		public int Id { get; } = id;
		public string Name { get; } = name;

		protected override bool EqualsInternal(IValueObject? other)
		{
			// Only compares by Id
			if (other is not AnotherTestValueObject testValueObject)
			{
				return false;
			}

			return Id == testValueObject.Id;
		}

		protected override int GetHashCodeInternal()
		{
			return HashCode.Combine(Id);
		}
	}
}
