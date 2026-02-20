// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="Batch{T}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Channels")]
[Trait("Priority", "0")]
public sealed class BatchShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullItems_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new Batch<string>(null!));
	}

	[Fact]
	public void Constructor_WithEmptyList_SetsItemsProperty()
	{
		// Arrange
		var items = Array.Empty<string>();

		// Act
		var batch = new Batch<string>(items);

		// Assert
		batch.Items.ShouldBe(items);
	}

	[Fact]
	public void Constructor_WithItems_SetsItemsProperty()
	{
		// Arrange
		var items = new List<int> { 1, 2, 3 };

		// Act
		var batch = new Batch<int>(items);

		// Assert
		batch.Items.ShouldBe(items);
	}

	[Fact]
	public void Constructor_SetsTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;
		var items = new List<string> { "test" };

		// Act
		var batch = new Batch<string>(items);

		// Assert
		var after = DateTimeOffset.UtcNow;
		batch.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		batch.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	#endregion

	#region Count Property Tests

	[Fact]
	public void Count_WithEmptyItems_ReturnsZero()
	{
		// Arrange
		var batch = new Batch<string>(Array.Empty<string>());

		// Assert
		batch.Count.ShouldBe(0);
	}

	[Fact]
	public void Count_WithItems_ReturnsCorrectCount()
	{
		// Arrange
		var items = new List<int> { 1, 2, 3, 4, 5 };
		var batch = new Batch<int>(items);

		// Assert
		batch.Count.ShouldBe(5);
	}

	[Fact]
	public void Count_WithSingleItem_ReturnsOne()
	{
		// Arrange
		var items = new List<string> { "only one" };
		var batch = new Batch<string>(items);

		// Assert
		batch.Count.ShouldBe(1);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameInstance_ReturnsTrue()
	{
		// Arrange
		var items = new List<string> { "a", "b" };
		var batch = new Batch<string>(items);

		// Act & Assert - Compare with self (same reference and timestamp)
		batch.Equals(batch).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentItems_ReturnsFalse()
	{
		// Arrange
		var items1 = new List<string> { "a", "b" };
		var items2 = new List<string> { "a", "b" };
		var batch1 = new Batch<string>(items1);
		var batch2 = new Batch<string>(items2); // Different list reference

		// Assert - Different references means not equal
		batch1.Equals(batch2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithSameItemsReference_ReturnsFalseIfDifferentTimestamp()
	{
		// Arrange
		var items = new List<string> { "a", "b" };
		var batch1 = new Batch<string>(items);

		// Ensure different timestamps by waiting at least one tick
		Thread.Sleep(1);
		var batch2 = new Batch<string>(items);

		// Assert - Same items reference but different timestamps
		// Due to different timestamps, they won't be equal
		batch1.Equals(batch2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithObject_ReturnsTrueForEqualBatch()
	{
		// Arrange
		var items = new List<string> { "a" };
		var batch = new Batch<string>(items);
		object boxed = batch;

		// Assert
		batch.Equals(boxed).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var items = new List<string> { "a" };
		var batch = new Batch<string>(items);

		// Assert
		batch.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentType_ReturnsFalse()
	{
		// Arrange
		var items = new List<string> { "a" };
		var batch = new Batch<string>(items);

		// Assert
		batch.Equals("not a batch").ShouldBeFalse();
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void EqualityOperator_WithSameInstance_ReturnsTrue()
	{
		// Arrange
		var items = new List<int> { 1, 2 };
		var batch = new Batch<int>(items);
		var sameBatch = batch;

		// Assert
		(batch == sameBatch).ShouldBeTrue();
	}

	[Fact]
	public void InequalityOperator_WithDifferentInstances_ReturnsTrue()
	{
		// Arrange
		var items1 = new List<int> { 1 };
		var items2 = new List<int> { 1 };
		var batch1 = new Batch<int>(items1);
		var batch2 = new Batch<int>(items2);

		// Assert
		(batch1 != batch2).ShouldBeTrue();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_ReturnsConsistentValue()
	{
		// Arrange
		var items = new List<string> { "a", "b" };
		var batch = new Batch<string>(items);

		// Act
		var hash1 = batch.GetHashCode();
		var hash2 = batch.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void GetHashCode_DifferentBatches_ReturnsDifferentValues()
	{
		// Arrange
		var items1 = new List<int> { 1, 2, 3 };
		var items2 = new List<int> { 4, 5, 6 };
		var batch1 = new Batch<int>(items1);
		var batch2 = new Batch<int>(items2);

		// Assert - Different items and timestamps should usually have different hash codes
		batch1.GetHashCode().ShouldNotBe(batch2.GetHashCode());
	}

	#endregion

	#region Default Struct Tests

	[Fact]
	public void DefaultBatch_HasNullItems()
	{
		// Arrange
		var batch = default(Batch<string>);

		// Assert
		batch.Items.ShouldBeNull();
	}

	[Fact]
	public void DefaultBatch_CountReturnsZero()
	{
		// Arrange
		var batch = default(Batch<string>);

		// Assert
		batch.Count.ShouldBe(0);
	}

	#endregion

	#region Generic Type Tests

	[Fact]
	public void Batch_WorksWithValueTypes()
	{
		// Arrange
		var items = new List<int> { 1, 2, 3 };
		var batch = new Batch<int>(items);

		// Assert
		batch.Items.ShouldContain(1);
		batch.Items.ShouldContain(2);
		batch.Items.ShouldContain(3);
	}

	[Fact]
	public void Batch_WorksWithReferenceTypes()
	{
		// Arrange
		var items = new List<object> { "string", 42, DateTime.Now };
		var batch = new Batch<object>(items);

		// Assert
		batch.Count.ShouldBe(3);
	}

	[Fact]
	public void Batch_WorksWithNullableTypes()
	{
		// Arrange
		var items = new List<int?> { 1, null, 3 };
		var batch = new Batch<int?>(items);

		// Assert
		batch.Items[0].ShouldBe(1);
		batch.Items[1].ShouldBeNull();
		batch.Items[2].ShouldBe(3);
	}

	#endregion
}
