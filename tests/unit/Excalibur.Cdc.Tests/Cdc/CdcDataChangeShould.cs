// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Tests.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcDataChange"/>.
/// Tests the CDC data change model.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcDataChangeShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HasCorrectDefaults()
	{
		// Arrange & Act
		var change = new CdcDataChange();

		// Assert
		change.ColumnName.ShouldBe(string.Empty);
		change.OldValue.ShouldBeNull();
		change.NewValue.ShouldBeNull();
		change.DataType.ShouldBeNull();
	}

	#endregion

	#region Property Tests

	[Fact]
	public void ColumnName_CanBeSet()
	{
		// Act
		var change = new CdcDataChange { ColumnName = "OrderId" };

		// Assert
		change.ColumnName.ShouldBe("OrderId");
	}

	[Fact]
	public void OldValue_CanBeSet()
	{
		// Act
		var change = new CdcDataChange { OldValue = "PreviousValue" };

		// Assert
		change.OldValue.ShouldBe("PreviousValue");
	}

	[Fact]
	public void NewValue_CanBeSet()
	{
		// Act
		var change = new CdcDataChange { NewValue = "NewValue" };

		// Assert
		change.NewValue.ShouldBe("NewValue");
	}

	[Fact]
	public void DataType_CanBeSet()
	{
		// Act
		var change = new CdcDataChange { DataType = typeof(string) };

		// Assert
		change.DataType.ShouldBe(typeof(string));
	}

	[Fact]
	public void SupportsVariousValueTypes()
	{
		// Arrange & Act
		var stringChange = new CdcDataChange { ColumnName = "Name", OldValue = "Old", NewValue = "New", DataType = typeof(string) };
		var intChange = new CdcDataChange { ColumnName = "Id", OldValue = 1, NewValue = 2, DataType = typeof(int) };
		var guidChange = new CdcDataChange { ColumnName = "Guid", OldValue = Guid.Empty, NewValue = Guid.NewGuid(), DataType = typeof(Guid) };
		var dateChange = new CdcDataChange { ColumnName = "Date", OldValue = DateTime.MinValue, NewValue = DateTime.UtcNow, DataType = typeof(DateTime) };
		var boolChange = new CdcDataChange { ColumnName = "Active", OldValue = false, NewValue = true, DataType = typeof(bool) };
		var decimalChange = new CdcDataChange { ColumnName = "Price", OldValue = 9.99m, NewValue = 19.99m, DataType = typeof(decimal) };

		// Assert
		stringChange.DataType.ShouldBe(typeof(string));
		intChange.DataType.ShouldBe(typeof(int));
		guidChange.DataType.ShouldBe(typeof(Guid));
		dateChange.DataType.ShouldBe(typeof(DateTime));
		boolChange.DataType.ShouldBe(typeof(bool));
		decimalChange.DataType.ShouldBe(typeof(decimal));
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var change = new CdcDataChange
		{
			ColumnName = "Status",
			OldValue = "Pending",
			NewValue = "Shipped",
			DataType = typeof(string)
		};

		// Act
		var result = change.ToString();

		// Assert
		result.ShouldContain("Status");
		result.ShouldContain("Pending");
		result.ShouldContain("Shipped");
		result.ShouldContain("String");
	}

	[Fact]
	public void ToString_HandlesNullDataType()
	{
		// Arrange
		var change = new CdcDataChange
		{
			ColumnName = "Test",
			OldValue = null,
			NewValue = "Value",
			DataType = null
		};

		// Act
		var result = change.ToString();

		// Assert
		result.ShouldContain("Unknown");
	}

	[Fact]
	public void ToString_HandlesNullValues()
	{
		// Arrange
		var change = new CdcDataChange
		{
			ColumnName = "Test",
			OldValue = null,
			NewValue = null,
			DataType = typeof(string)
		};

		// Act
		var result = change.ToString();

		// Assert - should not throw and should contain column name
		result.ShouldContain("Test");
	}

	[Fact]
	public void ToString_ShowsTransitionBetweenOldAndNewValues()
	{
		// Arrange
		var change = new CdcDataChange
		{
			ColumnName = "Value",
			OldValue = 1,
			NewValue = 2,
			DataType = typeof(int)
		};

		// Act
		var result = change.ToString();

		// Assert
		result.ShouldContain("Value");
		result.ShouldContain("1");
		result.ShouldContain("2");
		// The format uses a Unicode arrow character
		result.ShouldContain("\u2192");
	}

	#endregion

	#region Insert/Update/Delete Scenario Tests

	[Fact]
	public void RepresentsInsertChange_WithNullOldValue()
	{
		// Arrange
		var change = new CdcDataChange
		{
			ColumnName = "Id",
			OldValue = null,
			NewValue = 42,
			DataType = typeof(int)
		};

		// Assert
		change.OldValue.ShouldBeNull();
		change.NewValue.ShouldBe(42);
	}

	[Fact]
	public void RepresentsUpdateChange_WithBothValues()
	{
		// Arrange
		var change = new CdcDataChange
		{
			ColumnName = "Status",
			OldValue = "Draft",
			NewValue = "Published",
			DataType = typeof(string)
		};

		// Assert
		change.OldValue.ShouldBe("Draft");
		change.NewValue.ShouldBe("Published");
	}

	[Fact]
	public void RepresentsDeleteChange_WithNullNewValue()
	{
		// Arrange
		var change = new CdcDataChange
		{
			ColumnName = "Id",
			OldValue = 42,
			NewValue = null,
			DataType = typeof(int)
		};

		// Assert
		change.OldValue.ShouldBe(42);
		change.NewValue.ShouldBeNull();
	}

	#endregion
}
