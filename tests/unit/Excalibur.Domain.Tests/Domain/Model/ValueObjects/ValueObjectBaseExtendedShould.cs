using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ValueObjectBaseExtendedShould
{
	[Fact]
	public void EqualOperator_ReturnTrue_ForSameReference()
	{
		// Arrange
		var vo = new TestValueObject("a", 1);

		// Act & Assert
#pragma warning disable CS1718 // Comparison made to same variable
		(vo == vo).ShouldBeTrue();
#pragma warning restore CS1718
	}

	[Fact]
	public void EqualOperator_ReturnTrue_ForEqualValues()
	{
		// Arrange
		var a = new TestValueObject("hello", 42);
		var b = new TestValueObject("hello", 42);

		// Act & Assert
		(a == b).ShouldBeTrue();
	}

	[Fact]
	public void EqualOperator_ReturnFalse_ForDifferentValues()
	{
		// Arrange
		var a = new TestValueObject("hello", 42);
		var b = new TestValueObject("hello", 43);

		// Act & Assert
		(a == b).ShouldBeFalse();
	}

	[Fact]
	public void EqualOperator_ReturnFalse_WhenLeftIsNull()
	{
		// Arrange
		TestValueObject? a = null;
		var b = new TestValueObject("hello", 42);

		// Act & Assert
		(a == b).ShouldBeFalse();
	}

	[Fact]
	public void EqualOperator_ReturnFalse_WhenRightIsNull()
	{
		// Arrange
		var a = new TestValueObject("hello", 42);
		TestValueObject? b = null;

		// Act & Assert
		(a == b).ShouldBeFalse();
	}

	[Fact]
	public void EqualOperator_ReturnTrue_WhenBothNull()
	{
		// Arrange
		TestValueObject? a = null;
		TestValueObject? b = null;

		// Act & Assert
		(a == b).ShouldBeTrue();
	}

	[Fact]
	public void NotEqualOperator_ReturnTrue_ForDifferentValues()
	{
		// Arrange
		var a = new TestValueObject("hello", 42);
		var b = new TestValueObject("world", 42);

		// Act & Assert
		(a != b).ShouldBeTrue();
	}

	[Fact]
	public void Equals_ReturnFalse_ForNull()
	{
		// Arrange
		var vo = new TestValueObject("a", 1);

		// Act & Assert
		vo.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ReturnFalse_ForDifferentType()
	{
		// Arrange
		var vo = new TestValueObject("a", 1);

		// Act & Assert
		vo.Equals("not a value object").ShouldBeFalse();
	}

	[Fact]
	public void Equals_ReturnTrue_ForSameReference()
	{
		// Arrange
		var vo = new TestValueObject("a", 1);

		// Act & Assert
		vo.Equals((ValueObjectBase)vo).ShouldBeTrue();
	}

	[Fact]
	public void Equals_ReturnFalse_ForDifferentDerivedType()
	{
		// Arrange
		var vo1 = new TestValueObject("a", 1);
		var vo2 = new OtherTestValueObject("a", 1);

		// Act & Assert
		vo1.Equals(vo2).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_ReturnZero_WhenNoComponents()
	{
		// Arrange
		var vo = new EmptyValueObject();

		// Act
		var hash = vo.GetHashCode();

		// Assert
		hash.ShouldBe(0);
	}

	[Fact]
	public void GetHashCode_HandlesNullComponents()
	{
		// Arrange
		var vo = new NullableValueObject(null, 42);

		// Act & Assert — should not throw
		_ = vo.GetHashCode();
	}

	[Fact]
	public void GetHashCode_IsConsistent_ForEqualObjects()
	{
		// Arrange
		var a = new TestValueObject("hello", 42);
		var b = new TestValueObject("hello", 42);

		// Assert
		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	private sealed class TestValueObject : ValueObjectBase
	{
		private readonly string _value;
		private readonly int _number;

		public TestValueObject(string value, int number)
		{
			_value = value;
			_number = number;
		}

		public override IEnumerable<object?> GetEqualityComponents()
		{
			yield return _value;
			yield return _number;
		}
	}

	private sealed class OtherTestValueObject : ValueObjectBase
	{
		private readonly string _value;
		private readonly int _number;

		public OtherTestValueObject(string value, int number)
		{
			_value = value;
			_number = number;
		}

		public override IEnumerable<object?> GetEqualityComponents()
		{
			yield return _value;
			yield return _number;
		}
	}

	private sealed class EmptyValueObject : ValueObjectBase
	{
		public override IEnumerable<object?> GetEqualityComponents()
		{
			yield break;
		}
	}

	private sealed class NullableValueObject : ValueObjectBase
	{
		private readonly string? _value;
		private readonly int _number;

		public NullableValueObject(string? value, int number)
		{
			_value = value;
			_number = number;
		}

		public override IEnumerable<object?> GetEqualityComponents()
		{
			yield return _value;
			yield return _number;
		}
	}
}
