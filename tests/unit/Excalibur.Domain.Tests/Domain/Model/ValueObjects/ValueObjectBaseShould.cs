// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

/// <summary>
/// Unit tests for <see cref="ValueObjectBase"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ValueObjectBaseShould
{
	#region T419.8: Core ValueObjectBase Tests

	[Fact]
	public void EqualityOperator_ReturnsTrue_ForEqualComponents()
	{
		// Arrange
		var vo1 = new TestValueObject("A", 1);
		var vo2 = new TestValueObject("A", 1);

		// Act & Assert
		(vo1 == vo2).ShouldBeTrue();
	}

	[Fact]
	public void EqualityOperator_ReturnsFalse_ForDifferentComponents()
	{
		// Arrange
		var vo1 = new TestValueObject("A", 1);
		var vo2 = new TestValueObject("B", 1);

		// Act & Assert
		(vo1 == vo2).ShouldBeFalse();
	}

	[Fact]
	public void InequalityOperator_ReturnsTrue_ForDifferentComponents()
	{
		// Arrange
		var vo1 = new TestValueObject("A", 1);
		var vo2 = new TestValueObject("A", 2);

		// Act & Assert
		(vo1 != vo2).ShouldBeTrue();
	}

	[Fact]
	public void InequalityOperator_ReturnsFalse_ForEqualComponents()
	{
		// Arrange
		var vo1 = new TestValueObject("A", 1);
		var vo2 = new TestValueObject("A", 1);

		// Act & Assert
		(vo1 != vo2).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_IsConsistent_ForEqualObjects()
	{
		// Arrange
		var vo1 = new TestValueObject("Test", 42);
		var vo2 = new TestValueObject("Test", 42);

		// Act & Assert
		vo1.GetHashCode().ShouldBe(vo2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_IsDifferent_ForDifferentObjects()
	{
		// Arrange
		var vo1 = new TestValueObject("A", 1);
		var vo2 = new TestValueObject("B", 2);

		// Act & Assert
		vo1.GetHashCode().ShouldNotBe(vo2.GetHashCode());
	}

	[Fact]
	public void GetEqualityComponents_ReturnsAllComponents()
	{
		// Arrange
		var vo = new TestValueObject("Test", 123);

		// Act
		var components = vo.GetEqualityComponents().ToList();

		// Assert
		components.Count.ShouldBe(2);
		components[0].ShouldBe("Test");
		components[1].ShouldBe(123);
	}

	[Fact]
	public void Equals_ReturnsTrue_ForSameReference()
	{
		// Arrange
		var vo = new TestValueObject("Test", 1);

		// Act & Assert
		vo.Equals(vo).ShouldBeTrue();
	}

	[Fact]
	public void Equals_ReturnsFalse_WhenOtherIsNull()
	{
		// Arrange
		var vo = new TestValueObject("Test", 1);

		// Act & Assert
		vo.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ReturnsFalse_ForDifferentType()
	{
		// Arrange
		var vo1 = new TestValueObject("Test", 1);
		var vo2 = new AnotherTestValueObject("Test");

		// Act & Assert
		vo1.Equals(vo2).ShouldBeFalse();
	}

	[Fact]
	public void EqualityOperator_ReturnsTrue_ForBothNull()
	{
		// Arrange
		TestValueObject? vo1 = null;
		TestValueObject? vo2 = null;

		// Act & Assert
		(vo1 == vo2).ShouldBeTrue();
	}

	[Fact]
	public void EqualityOperator_ReturnsFalse_WhenLeftIsNull()
	{
		// Arrange
		TestValueObject? vo1 = null;
		var vo2 = new TestValueObject("Test", 1);

		// Act & Assert
		(vo1 == vo2).ShouldBeFalse();
	}

	[Fact]
	public void EqualityOperator_ReturnsFalse_WhenRightIsNull()
	{
		// Arrange
		var vo1 = new TestValueObject("Test", 1);
		TestValueObject? vo2 = null;

		// Act & Assert
		(vo1 == vo2).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_HandlesNullComponents()
	{
		// Arrange
		var vo = new TestValueObjectWithNullableComponent(null, 42);

		// Act - should not throw
		var hashCode = vo.GetHashCode();

		// Assert
		hashCode.ShouldNotBe(0);
	}

	[Fact]
	public void Equals_ReturnsFalse_WhenObjectIsNotValueObject()
	{
		// Arrange
		var vo = new TestValueObject("Test", 1);
		var notAValueObject = "not a value object";

		// Act & Assert
		vo.Equals(notAValueObject).ShouldBeFalse();
	}

	[Fact]
	public void TypedEquals_ReturnsFalse_ForDifferentValueObjectTypes()
	{
		// Arrange - use typed Equals(ValueObjectBase? other) explicitly
		var vo1 = new TestValueObject("Test", 1);
		ValueObjectBase vo2 = new AnotherTestValueObject("Test");

		// Act - explicitly call the typed Equals method
		var result = vo1.Equals(vo2);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void TypedEquals_ReturnsTrue_ForSameReference()
	{
		// Arrange
		var vo = new TestValueObject("Test", 1);
		ValueObjectBase sameRef = vo;

		// Act - explicitly call the typed Equals method with same reference
		var result = vo.Equals(sameRef);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void TypedEquals_ReturnsFalse_ForNullValueObject()
	{
		// Arrange
		var vo = new TestValueObject("Test", 1);
		ValueObjectBase? nullVo = null;

		// Act - explicitly call the typed Equals method
		var result = vo.Equals(nullVo);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void TypedEquals_ReturnsTrue_ForEqualComponents()
	{
		// Arrange
		var vo1 = new TestValueObject("Test", 42);
		ValueObjectBase vo2 = new TestValueObject("Test", 42);

		// Act - explicitly call the typed Equals method
		var result = vo1.Equals(vo2);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void TypedEquals_ReturnsFalse_ForDifferentComponents()
	{
		// Arrange
		var vo1 = new TestValueObject("Test", 1);
		ValueObjectBase vo2 = new TestValueObject("Test", 2);

		// Act - explicitly call the typed Equals method
		var result = vo1.Equals(vo2);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion T419.8: Core ValueObjectBase Tests

	#region Test Value Objects

	/// <summary>
	/// Test value object with string and int components.
	/// </summary>
	private sealed class TestValueObject : ValueObjectBase
	{
		public string Name { get; }
		public int Value { get; }

		public TestValueObject(string name, int value)
		{
			Name = name;
			Value = value;
		}

		public override IEnumerable<object?> GetEqualityComponents()
		{
			yield return Name;
			yield return Value;
		}
	}

	/// <summary>
	/// Another test value object for type comparison tests.
	/// </summary>
	private sealed class AnotherTestValueObject : ValueObjectBase
	{
		public string Name { get; }

		public AnotherTestValueObject(string name)
		{
			Name = name;
		}

		public override IEnumerable<object?> GetEqualityComponents()
		{
			yield return Name;
		}
	}

	/// <summary>
	/// Test value object with nullable component.
	/// </summary>
	private sealed class TestValueObjectWithNullableComponent : ValueObjectBase
	{
		public string? NullableName { get; }
		public int Value { get; }

		public TestValueObjectWithNullableComponent(string? nullableName, int value)
		{
			NullableName = nullableName;
			Value = value;
		}

		public override IEnumerable<object?> GetEqualityComponents()
		{
			yield return NullableName;
			yield return Value;
		}
	}

	#endregion Test Value Objects
}
