// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.InMemory;

namespace Excalibur.Tests.Cdc.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryCdcChange"/>.
/// Tests the in-memory CDC change model for testing scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCdcChangeShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HasCorrectDefaults()
	{
		// Arrange & Act
		var change = new InMemoryCdcChange();

		// Assert
		change.TableName.ShouldBe(string.Empty);
		change.ChangeType.ShouldBe(CdcChangeType.None);
		change.Changes.ShouldBeEmpty();
		change.Metadata.ShouldBeNull();
		change.Timestamp.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
	}

	#endregion

	#region Insert Factory Method Tests

	[Fact]
	public void Insert_CreatesInsertChange()
	{
		// Arrange
		var dataChange = new CdcDataChange { ColumnName = "Id", NewValue = 1 };

		// Act
		var change = InMemoryCdcChange.Insert("dbo.Orders", dataChange);

		// Assert
		change.TableName.ShouldBe("dbo.Orders");
		change.ChangeType.ShouldBe(CdcChangeType.Insert);
		change.Changes.Count.ShouldBe(1);
		change.Changes[0].ColumnName.ShouldBe("Id");
		change.Changes[0].NewValue.ShouldBe(1);
	}

	[Fact]
	public void Insert_CreatesChangeWithMultipleColumns()
	{
		// Arrange
		var changes = new[]
		{
			new CdcDataChange { ColumnName = "Id", NewValue = 1 },
			new CdcDataChange { ColumnName = "Name", NewValue = "Test Order" },
			new CdcDataChange { ColumnName = "CreatedAt", NewValue = DateTime.UtcNow }
		};

		// Act
		var change = InMemoryCdcChange.Insert("dbo.Orders", changes);

		// Assert
		change.ChangeType.ShouldBe(CdcChangeType.Insert);
		change.Changes.Count.ShouldBe(3);
	}

	#endregion

	#region Update Factory Method Tests

	[Fact]
	public void Update_CreatesUpdateChange()
	{
		// Arrange
		var dataChange = new CdcDataChange { ColumnName = "Status", OldValue = "Pending", NewValue = "Shipped" };

		// Act
		var change = InMemoryCdcChange.Update("dbo.Orders", dataChange);

		// Assert
		change.TableName.ShouldBe("dbo.Orders");
		change.ChangeType.ShouldBe(CdcChangeType.Update);
		change.Changes.Count.ShouldBe(1);
		change.Changes[0].ColumnName.ShouldBe("Status");
		change.Changes[0].OldValue.ShouldBe("Pending");
		change.Changes[0].NewValue.ShouldBe("Shipped");
	}

	[Fact]
	public void Update_CreatesChangeWithMultipleColumns()
	{
		// Arrange
		var changes = new[]
		{
			new CdcDataChange { ColumnName = "Status", OldValue = "Pending", NewValue = "Shipped" },
			new CdcDataChange { ColumnName = "UpdatedAt", OldValue = null, NewValue = DateTime.UtcNow }
		};

		// Act
		var change = InMemoryCdcChange.Update("dbo.Orders", changes);

		// Assert
		change.ChangeType.ShouldBe(CdcChangeType.Update);
		change.Changes.Count.ShouldBe(2);
	}

	#endregion

	#region Delete Factory Method Tests

	[Fact]
	public void Delete_CreatesDeleteChange()
	{
		// Arrange
		var dataChange = new CdcDataChange { ColumnName = "Id", OldValue = 42 };

		// Act
		var change = InMemoryCdcChange.Delete("dbo.Orders", dataChange);

		// Assert
		change.TableName.ShouldBe("dbo.Orders");
		change.ChangeType.ShouldBe(CdcChangeType.Delete);
		change.Changes.Count.ShouldBe(1);
		change.Changes[0].ColumnName.ShouldBe("Id");
		change.Changes[0].OldValue.ShouldBe(42);
	}

	[Fact]
	public void Delete_CreatesChangeWithMultipleColumns()
	{
		// Arrange
		var changes = new[]
		{
			new CdcDataChange { ColumnName = "Id", OldValue = 42 },
			new CdcDataChange { ColumnName = "Name", OldValue = "Test Order" }
		};

		// Act
		var change = InMemoryCdcChange.Delete("dbo.Orders", changes);

		// Assert
		change.ChangeType.ShouldBe(CdcChangeType.Delete);
		change.Changes.Count.ShouldBe(2);
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var change = InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 });

		// Act
		var result = change.ToString();

		// Assert
		result.ShouldContain("Insert");
		result.ShouldContain("dbo.Orders");
		result.ShouldContain("1 columns");
	}

	[Fact]
	public void ToString_ShowsCorrectColumnCount()
	{
		// Arrange
		var changes = new[]
		{
			new CdcDataChange { ColumnName = "Id", NewValue = 1 },
			new CdcDataChange { ColumnName = "Name", NewValue = "Test" },
			new CdcDataChange { ColumnName = "Status", NewValue = "Active" }
		};
		var change = InMemoryCdcChange.Update("dbo.Orders", changes);

		// Act
		var result = change.ToString();

		// Assert
		result.ShouldContain("Update");
		result.ShouldContain("3 columns");
	}

	#endregion

	#region Metadata Tests

	[Fact]
	public void CanSetMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, object?>
		{
			["TransactionId"] = Guid.NewGuid(),
			["UserId"] = 123,
			["Source"] = "TestSource"
		};

		// Act
		var change = new InMemoryCdcChange
		{
			TableName = "dbo.Orders",
			ChangeType = CdcChangeType.Insert,
			Metadata = metadata
		};

		// Assert
		change.Metadata.ShouldNotBeNull();
		change.Metadata.Count.ShouldBe(3);
		change.Metadata.ShouldContainKey("TransactionId");
		change.Metadata.ShouldContainKey("UserId");
		change.Metadata.ShouldContainKey("Source");
	}

	#endregion

	#region Timestamp Tests

	[Fact]
	public void CanSetCustomTimestamp()
	{
		// Arrange
		var customTimestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var change = new InMemoryCdcChange
		{
			TableName = "dbo.Orders",
			ChangeType = CdcChangeType.Insert,
			Timestamp = customTimestamp
		};

		// Assert
		change.Timestamp.ShouldBe(customTimestamp);
	}

	#endregion
}
