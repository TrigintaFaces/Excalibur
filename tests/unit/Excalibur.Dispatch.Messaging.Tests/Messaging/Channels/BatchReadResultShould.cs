// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="BatchReadResult{T}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Channels")]
[Trait("Priority", "0")]
public sealed class BatchReadResultShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithItems_SetsItemsProperty()
	{
		// Arrange
		var items = new List<int> { 1, 2, 3 };

		// Act
		var result = new BatchReadResult<int>(items, hasItems: true);

		// Assert
		result.Items.ShouldBe(items);
	}

	[Fact]
	public void Constructor_WithHasItemsTrue_SetsHasItemsProperty()
	{
		// Arrange
		var items = new List<string> { "a" };

		// Act
		var result = new BatchReadResult<string>(items, hasItems: true);

		// Assert
		result.HasItems.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_WithHasItemsFalse_SetsHasItemsProperty()
	{
		// Arrange
		var items = Array.Empty<string>();

		// Act
		var result = new BatchReadResult<string>(items, hasItems: false);

		// Assert
		result.HasItems.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_WithNullItems_SetsItemsToNull()
	{
		// Act
		var result = new BatchReadResult<string>(null!, hasItems: false);

		// Assert
		result.Items.ShouldBeNull();
	}

	#endregion

	#region Count Property Tests

	[Fact]
	public void Count_WithItems_ReturnsCorrectCount()
	{
		// Arrange
		var items = new List<int> { 1, 2, 3, 4, 5 };
		var result = new BatchReadResult<int>(items, hasItems: true);

		// Assert
		result.Count.ShouldBe(5);
	}

	[Fact]
	public void Count_WithEmptyItems_ReturnsZero()
	{
		// Arrange
		var result = new BatchReadResult<string>(Array.Empty<string>(), hasItems: false);

		// Assert
		result.Count.ShouldBe(0);
	}

	[Fact]
	public void Count_WithNullItems_ReturnsZero()
	{
		// Arrange
		var result = new BatchReadResult<string>(null!, hasItems: false);

		// Assert
		result.Count.ShouldBe(0);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameInstance_ReturnsTrue()
	{
		// Arrange
		var items = new List<int> { 1, 2 };
		var result = new BatchReadResult<int>(items, hasItems: true);

		// Assert
		result.Equals(result).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentItems_ReturnsFalse()
	{
		// Arrange
		var items1 = new List<int> { 1, 2 };
		var items2 = new List<int> { 1, 2 };
		var result1 = new BatchReadResult<int>(items1, hasItems: true);
		var result2 = new BatchReadResult<int>(items2, hasItems: true);

		// Assert - Different list references mean not equal
		result1.Equals(result2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithSameItemsButDifferentHasItems_ReturnsFalse()
	{
		// Arrange
		var items = new List<int> { 1, 2 };
		var result1 = new BatchReadResult<int>(items, hasItems: true);
		var result2 = new BatchReadResult<int>(items, hasItems: false);

		// Assert
		result1.Equals(result2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithSameItemsAndHasItems_ReturnsTrue()
	{
		// Arrange
		var items = new List<int> { 1, 2 };
		var result1 = new BatchReadResult<int>(items, hasItems: true);
		var result2 = new BatchReadResult<int>(items, hasItems: true);

		// Assert - Same reference and same hasItems value
		result1.Equals(result2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithObject_ReturnsTrueForEqualResult()
	{
		// Arrange
		var items = new List<string> { "a" };
		var result = new BatchReadResult<string>(items, hasItems: true);
		object boxed = result;

		// Assert
		result.Equals(boxed).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var items = new List<string> { "a" };
		var result = new BatchReadResult<string>(items, hasItems: true);

		// Assert
		result.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentType_ReturnsFalse()
	{
		// Arrange
		var items = new List<string> { "a" };
		var result = new BatchReadResult<string>(items, hasItems: true);

		// Assert
		result.Equals("not a result").ShouldBeFalse();
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void EqualityOperator_WithSameInstance_ReturnsTrue()
	{
		// Arrange
		var items = new List<int> { 1, 2 };
		var result = new BatchReadResult<int>(items, hasItems: true);
		var sameResult = result;

		// Assert
		(result == sameResult).ShouldBeTrue();
	}

	[Fact]
	public void InequalityOperator_WithDifferentInstances_ReturnsTrue()
	{
		// Arrange
		var items1 = new List<int> { 1 };
		var items2 = new List<int> { 1 };
		var result1 = new BatchReadResult<int>(items1, hasItems: true);
		var result2 = new BatchReadResult<int>(items2, hasItems: true);

		// Assert
		(result1 != result2).ShouldBeTrue();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_ReturnsConsistentValue()
	{
		// Arrange
		var items = new List<string> { "a", "b" };
		var result = new BatchReadResult<string>(items, hasItems: true);

		// Act
		var hash1 = result.GetHashCode();
		var hash2 = result.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void GetHashCode_EqualResults_ReturnSameValue()
	{
		// Arrange
		var items = new List<int> { 1, 2, 3 };
		var result1 = new BatchReadResult<int>(items, hasItems: true);
		var result2 = new BatchReadResult<int>(items, hasItems: true);

		// Assert
		result1.GetHashCode().ShouldBe(result2.GetHashCode());
	}

	#endregion

	#region Default Struct Tests

	[Fact]
	public void DefaultResult_HasNullItems()
	{
		// Arrange
		var result = default(BatchReadResult<string>);

		// Assert
		result.Items.ShouldBeNull();
	}

	[Fact]
	public void DefaultResult_HasItemsIsFalse()
	{
		// Arrange
		var result = default(BatchReadResult<string>);

		// Assert
		result.HasItems.ShouldBeFalse();
	}

	[Fact]
	public void DefaultResult_CountReturnsZero()
	{
		// Arrange
		var result = default(BatchReadResult<string>);

		// Assert
		result.Count.ShouldBe(0);
	}

	#endregion
}
