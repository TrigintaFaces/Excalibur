// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.DataAccess;

/// <summary>
/// Unit tests for <see cref="Change{T}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "DataAccess")]
[Trait("Priority", "0")]
public sealed class ChangeShould
{
	#region Test Types

	private sealed record TestEntity(int Id, string Name);

	#endregion

	#region Constructor Tests

	[Fact]
	public void Constructor_SetsType()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var change = new Change<TestEntity>(
			ChangeType.Insert,
			null,
			new TestEntity(1, "Test"),
			position,
			timestamp);

		// Assert
		change.Type.ShouldBe(ChangeType.Insert);
	}

	[Fact]
	public void Constructor_SetsBefore()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var timestamp = DateTimeOffset.UtcNow;
		var before = new TestEntity(1, "Old");

		// Act
		var change = new Change<TestEntity>(
			ChangeType.Update,
			before,
			new TestEntity(1, "New"),
			position,
			timestamp);

		// Assert
		change.Before.ShouldBe(before);
	}

	[Fact]
	public void Constructor_SetsAfter()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var timestamp = DateTimeOffset.UtcNow;
		var after = new TestEntity(1, "New");

		// Act
		var change = new Change<TestEntity>(
			ChangeType.Update,
			new TestEntity(1, "Old"),
			after,
			position,
			timestamp);

		// Assert
		change.After.ShouldBe(after);
	}

	[Fact]
	public void Constructor_SetsPosition()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var change = new Change<TestEntity>(
			ChangeType.Insert,
			null,
			new TestEntity(1, "Test"),
			position,
			timestamp);

		// Assert
		change.Position.ShouldBe(position);
	}

	[Fact]
	public void Constructor_SetsTimestamp()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var change = new Change<TestEntity>(
			ChangeType.Insert,
			null,
			new TestEntity(1, "Test"),
			position,
			timestamp);

		// Assert
		change.Timestamp.ShouldBe(timestamp);
	}

	#endregion

	#region HasBeforeImage Tests

	[Fact]
	public void HasBeforeImage_WhenBeforeIsNotNull_ReturnsTrue()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Update,
			new TestEntity(1, "Old"),
			new TestEntity(1, "New"),
			position,
			DateTimeOffset.UtcNow);

		// Act & Assert
		change.HasBeforeImage.ShouldBeTrue();
	}

	[Fact]
	public void HasBeforeImage_WhenBeforeIsNull_ReturnsFalse()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Insert,
			null,
			new TestEntity(1, "New"),
			position,
			DateTimeOffset.UtcNow);

		// Act & Assert
		change.HasBeforeImage.ShouldBeFalse();
	}

	#endregion

	#region HasAfterImage Tests

	[Fact]
	public void HasAfterImage_WhenAfterIsNotNull_ReturnsTrue()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Insert,
			null,
			new TestEntity(1, "New"),
			position,
			DateTimeOffset.UtcNow);

		// Act & Assert
		change.HasAfterImage.ShouldBeTrue();
	}

	[Fact]
	public void HasAfterImage_WhenAfterIsNull_ReturnsFalse()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Delete,
			new TestEntity(1, "Old"),
			null,
			position,
			DateTimeOffset.UtcNow);

		// Act & Assert
		change.HasAfterImage.ShouldBeFalse();
	}

	#endregion

	#region GetKey Tests

	[Fact]
	public void GetKey_WhenAfterIsNotNull_ReturnsKeyFromAfter()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Insert,
			null,
			new TestEntity(42, "Test"),
			position,
			DateTimeOffset.UtcNow);

		// Act
		var key = change.GetKey(e => e.Id);

		// Assert
		key.ShouldBe(42);
	}

	[Fact]
	public void GetKey_WhenAfterIsNullAndBeforeIsNotNull_ReturnsKeyFromBefore()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Delete,
			new TestEntity(99, "Old"),
			null,
			position,
			DateTimeOffset.UtcNow);

		// Act
		var key = change.GetKey(e => e.Id);

		// Assert
		key.ShouldBe(99);
	}

	[Fact]
	public void GetKey_WhenBothAreNull_ThrowsInvalidOperationException()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Delete,
			null,
			null,
			position,
			DateTimeOffset.UtcNow);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => change.GetKey(e => e.Id));
	}

	[Fact]
	public void GetKey_WithNullKeySelector_ThrowsArgumentNullException()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Insert,
			null,
			new TestEntity(1, "Test"),
			position,
			DateTimeOffset.UtcNow);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => change.GetKey<int>(null!));
	}

	[Fact]
	public void GetKey_CanExtractStringKey()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Insert,
			null,
			new TestEntity(1, "EntityName"),
			position,
			DateTimeOffset.UtcNow);

		// Act
		var key = change.GetKey(e => e.Name);

		// Assert
		key.ShouldBe("EntityName");
	}

	#endregion

	#region Record Equality Tests

	[Fact]
	public void Equals_WithSameValues_ReturnsTrue()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var timestamp = DateTimeOffset.UtcNow;
		var before = new TestEntity(1, "Old");
		var after = new TestEntity(1, "New");

		var change1 = new Change<TestEntity>(ChangeType.Update, before, after, position, timestamp);
		var change2 = new Change<TestEntity>(ChangeType.Update, before, after, position, timestamp);

		// Act & Assert
		change1.ShouldBe(change2);
	}

	[Fact]
	public void Equals_WithDifferentType_ReturnsFalse()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var timestamp = DateTimeOffset.UtcNow;
		var entity = new TestEntity(1, "Test");

		var change1 = new Change<TestEntity>(ChangeType.Insert, null, entity, position, timestamp);
		var change2 = new Change<TestEntity>(ChangeType.Update, null, entity, position, timestamp);

		// Act & Assert
		change1.ShouldNotBe(change2);
	}

	#endregion

	#region Change Type Scenario Tests

	[Fact]
	public void Insert_HasNullBeforeAndNonNullAfter()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Insert,
			null,
			new TestEntity(1, "New"),
			position,
			DateTimeOffset.UtcNow);

		// Assert
		change.Type.ShouldBe(ChangeType.Insert);
		change.HasBeforeImage.ShouldBeFalse();
		change.HasAfterImage.ShouldBeTrue();
	}

	[Fact]
	public void Update_HasBothBeforeAndAfter()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Update,
			new TestEntity(1, "Old"),
			new TestEntity(1, "New"),
			position,
			DateTimeOffset.UtcNow);

		// Assert
		change.Type.ShouldBe(ChangeType.Update);
		change.HasBeforeImage.ShouldBeTrue();
		change.HasAfterImage.ShouldBeTrue();
	}

	[Fact]
	public void Delete_HasNonNullBeforeAndNullAfter()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var change = new Change<TestEntity>(
			ChangeType.Delete,
			new TestEntity(1, "Old"),
			null,
			position,
			DateTimeOffset.UtcNow);

		// Assert
		change.Type.ShouldBe(ChangeType.Delete);
		change.HasBeforeImage.ShouldBeTrue();
		change.HasAfterImage.ShouldBeFalse();
	}

	#endregion
}
