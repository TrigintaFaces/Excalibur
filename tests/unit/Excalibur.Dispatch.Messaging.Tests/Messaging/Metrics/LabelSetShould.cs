// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="LabelSet"/>.
/// </summary>
/// <remarks>
/// Tests the label set struct used for metric categorization.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class LabelSetShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithValues_SetsValues()
	{
		// Arrange & Act
		var labelSet = new LabelSet("value1", "value2");

		// Assert
		labelSet.Values.Length.ShouldBe(2);
		labelSet.Values[0].ShouldBe("value1");
		labelSet.Values[1].ShouldBe("value2");
	}

	[Fact]
	public void Constructor_WithEmptyArray_CreatesEmptySet()
	{
		// Arrange & Act
		var labelSet = new LabelSet(Array.Empty<string>());

		// Assert
		labelSet.Values.Length.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithNullValues_CreatesEmptySet()
	{
		// Arrange & Act
		var labelSet = new LabelSet(null);

		// Assert
		labelSet.Values.Length.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithSingleValue_Works()
	{
		// Arrange & Act
		var labelSet = new LabelSet("single");

		// Assert
		labelSet.Values.Length.ShouldBe(1);
		labelSet.Values[0].ShouldBe("single");
	}

	[Fact]
	public void Constructor_WithManyValues_Works()
	{
		// Arrange
		var values = new[] { "a", "b", "c", "d", "e" };

		// Act
		var labelSet = new LabelSet(values);

		// Assert
		labelSet.Values.Length.ShouldBe(5);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameValues_ReturnsTrue()
	{
		// Arrange
		var set1 = new LabelSet("a", "b");
		var set2 = new LabelSet("a", "b");

		// Act & Assert
		set1.Equals(set2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentValues_ReturnsFalse()
	{
		// Arrange
		var set1 = new LabelSet("a", "b");
		var set2 = new LabelSet("c", "d");

		// Act & Assert
		set1.Equals(set2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentLengths_ReturnsFalse()
	{
		// Arrange
		var set1 = new LabelSet("a", "b");
		var set2 = new LabelSet("a");

		// Act & Assert
		set1.Equals(set2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentOrder_ReturnsFalse()
	{
		// Arrange
		var set1 = new LabelSet("a", "b");
		var set2 = new LabelSet("b", "a");

		// Act & Assert
		set1.Equals(set2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_BothEmpty_ReturnsTrue()
	{
		// Arrange - Use null parameter to create empty sets
		var set1 = new LabelSet(null);
		var set2 = new LabelSet(null);

		// Act & Assert
		set1.Equals(set2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithObject_ReturnsTrue()
	{
		// Arrange
		var set1 = new LabelSet("a", "b");
		object set2 = new LabelSet("a", "b");

		// Act & Assert
		set1.Equals(set2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithNonLabelSetObject_ReturnsFalse()
	{
		// Arrange
		var set = new LabelSet("a");

		// Act & Assert
		set.Equals("not a label set").ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var set = new LabelSet("a");

		// Act & Assert
		set.Equals(null).ShouldBeFalse();
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void EqualityOperator_WithEqualSets_ReturnsTrue()
	{
		// Arrange
		var set1 = new LabelSet("x", "y");
		var set2 = new LabelSet("x", "y");

		// Act & Assert
		(set1 == set2).ShouldBeTrue();
	}

	[Fact]
	public void EqualityOperator_WithDifferentSets_ReturnsFalse()
	{
		// Arrange
		var set1 = new LabelSet("x", "y");
		var set2 = new LabelSet("x", "z");

		// Act & Assert
		(set1 == set2).ShouldBeFalse();
	}

	[Fact]
	public void InequalityOperator_WithDifferentSets_ReturnsTrue()
	{
		// Arrange
		var set1 = new LabelSet("x", "y");
		var set2 = new LabelSet("x", "z");

		// Act & Assert
		(set1 != set2).ShouldBeTrue();
	}

	[Fact]
	public void InequalityOperator_WithEqualSets_ReturnsFalse()
	{
		// Arrange
		var set1 = new LabelSet("x", "y");
		var set2 = new LabelSet("x", "y");

		// Act & Assert
		(set1 != set2).ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_WithEqualSets_ReturnsSameHash()
	{
		// Arrange
		var set1 = new LabelSet("a", "b");
		var set2 = new LabelSet("a", "b");

		// Act & Assert
		set1.GetHashCode().ShouldBe(set2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_WithDifferentSets_ReturnsDifferentHash()
	{
		// Arrange
		var set1 = new LabelSet("a", "b");
		var set2 = new LabelSet("c", "d");

		// Act & Assert
		set1.GetHashCode().ShouldNotBe(set2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_IsConsistent()
	{
		// Arrange
		var set = new LabelSet("x", "y");

		// Act
		var hash1 = set.GetHashCode();
		var hash2 = set.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void GetHashCode_EmptySet_Works()
	{
		// Arrange - Use null parameter to create empty set
		var set = new LabelSet(null);

		// Act - Should not throw
		var hash = set.GetHashCode();

		// Assert
		hash.ShouldNotBe(0); // Initial value is 17
	}

	#endregion

	#region Values Property Tests

	[Fact]
	public void Values_ReturnsReadOnlySpan()
	{
		// Arrange
		var labelSet = new LabelSet("a", "b", "c");

		// Act
		var values = labelSet.Values;

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void Values_CanBeEnumerated()
	{
		// Arrange
		var labelSet = new LabelSet("first", "second", "third");
		var result = new List<string>();

		// Act
		foreach (var value in labelSet.Values)
		{
			result.Add(value);
		}

		// Assert
		result.Count.ShouldBe(3);
		result[0].ShouldBe("first");
		result[1].ShouldBe("second");
		result[2].ShouldBe("third");
	}

	#endregion

	#region Interface Tests

	[Fact]
	public void ImplementsIEquatable()
	{
		// Arrange
		var set = new LabelSet("a");

		// Assert
		_ = set.ShouldBeAssignableTo<IEquatable<LabelSet>>();
	}

	#endregion
}
