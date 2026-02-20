// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

/// <summary>
/// Depth coverage tests for <see cref="ValueObjectBase"/>.
/// Covers operators, equality edge cases, null components, empty components.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ValueObjectBaseDepthShould
{
	[Fact]
	public void OperatorEquals_BothNull_ReturnsTrue()
	{
		// Arrange
		TestValueObject? left = null;
		TestValueObject? right = null;

		// Act & Assert
		(left == right).ShouldBeTrue();
	}

	[Fact]
	public void OperatorEquals_LeftNull_ReturnsFalse()
	{
		// Arrange
		TestValueObject? left = null;
		var right = new TestValueObject("a", 1);

		// Act & Assert
		(left == right).ShouldBeFalse();
	}

	[Fact]
	public void OperatorEquals_RightNull_ReturnsFalse()
	{
		// Arrange
		var left = new TestValueObject("a", 1);
		TestValueObject? right = null;

		// Act & Assert
		(left == right).ShouldBeFalse();
	}

	[Fact]
	public void OperatorEquals_SameReference_ReturnsTrue()
	{
		// Arrange
		var obj = new TestValueObject("a", 1);

		// Act & Assert
#pragma warning disable CS1718 // Comparison made to same variable
		(obj == obj).ShouldBeTrue();
#pragma warning restore CS1718
	}

	[Fact]
	public void OperatorNotEquals_DifferentValues_ReturnsTrue()
	{
		// Arrange
		var left = new TestValueObject("a", 1);
		var right = new TestValueObject("b", 2);

		// Act & Assert
		(left != right).ShouldBeTrue();
	}

	[Fact]
	public void Equals_Object_DifferentType_ReturnsFalse()
	{
		// Arrange
		var obj = new TestValueObject("a", 1);

		// Act & Assert
		obj.Equals("not-a-value-object").ShouldBeFalse();
	}

	[Fact]
	public void Equals_Object_Null_ReturnsFalse()
	{
		// Arrange
		var obj = new TestValueObject("a", 1);

		// Act & Assert
		obj.Equals((object?)null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ValueObject_Null_ReturnsFalse()
	{
		// Arrange
		var obj = new TestValueObject("a", 1);

		// Act & Assert
		obj.Equals((ValueObjectBase?)null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ValueObject_SameReference_ReturnsTrue()
	{
		// Arrange
		var obj = new TestValueObject("a", 1);

		// Act & Assert
		obj.Equals(obj).ShouldBeTrue();
	}

	[Fact]
	public void Equals_ValueObject_DifferentSubclass_ReturnsFalse()
	{
		// Arrange
		var obj1 = new TestValueObject("a", 1);
		var obj2 = new AnotherValueObject("a", 1);

		// Act & Assert
		obj1.Equals(obj2).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_SameForEqualObjects()
	{
		// Arrange
		var obj1 = new TestValueObject("a", 1);
		var obj2 = new TestValueObject("a", 1);

		// Act & Assert
		obj1.GetHashCode().ShouldBe(obj2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_DiffersForDifferentObjects()
	{
		// Arrange
		var obj1 = new TestValueObject("a", 1);
		var obj2 = new TestValueObject("b", 2);

		// Act & Assert
		obj1.GetHashCode().ShouldNotBe(obj2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_HandlesNullComponents()
	{
		// Arrange
		var obj = new NullableValueObject(null, 1);

		// Act
		var hash = obj.GetHashCode();

		// Assert — should not throw and should produce a valid hash
		hash.ShouldBeOfType<int>();
	}

	[Fact]
	public void GetHashCode_EmptyComponents_ReturnsZero()
	{
		// Arrange
		var obj = new EmptyValueObject();

		// Act & Assert
		obj.GetHashCode().ShouldBe(0);
	}

	[Fact]
	public void Equals_WithNullComponents_Matches()
	{
		// Arrange
		var obj1 = new NullableValueObject(null, 1);
		var obj2 = new NullableValueObject(null, 1);

		// Act & Assert
		obj1.Equals(obj2).ShouldBeTrue();
	}

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

	private sealed class AnotherValueObject : ValueObjectBase
	{
		public string Name { get; }
		public int Value { get; }

		public AnotherValueObject(string name, int value)
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

	private sealed class NullableValueObject : ValueObjectBase
	{
		public string? Name { get; }
		public int Value { get; }

		public NullableValueObject(string? name, int value)
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

	private sealed class EmptyValueObject : ValueObjectBase
	{
		public override IEnumerable<object?> GetEqualityComponents()
		{
			yield break;
		}
	}
}
