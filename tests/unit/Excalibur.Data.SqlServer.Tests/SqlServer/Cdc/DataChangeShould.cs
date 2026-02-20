// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="DataChange"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class DataChangeShould : UnitTestBase
{
	[Fact]
	public void HaveEmptyColumnName_ByDefault()
	{
		// Arrange & Act
		var change = new DataChange();

		// Assert
		change.ColumnName.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveNullOldValue_ByDefault()
	{
		// Arrange & Act
		var change = new DataChange();

		// Assert
		change.OldValue.ShouldBeNull();
	}

	[Fact]
	public void HaveNullNewValue_ByDefault()
	{
		// Arrange & Act
		var change = new DataChange();

		// Assert
		change.NewValue.ShouldBeNull();
	}

	[Fact]
	public void HaveNullDataType_ByDefault()
	{
		// Arrange & Act
		var change = new DataChange();

		// Assert
		change.DataType.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingColumnName()
	{
		// Act
		var change = new DataChange
		{
			ColumnName = "UserId"
		};

		// Assert
		change.ColumnName.ShouldBe("UserId");
	}

	[Fact]
	public void AllowSettingOldValue()
	{
		// Act
		var change = new DataChange
		{
			OldValue = 42
		};

		// Assert
		change.OldValue.ShouldBe(42);
	}

	[Fact]
	public void AllowSettingNewValue()
	{
		// Act
		var change = new DataChange
		{
			NewValue = 100
		};

		// Assert
		change.NewValue.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingDataType()
	{
		// Act
		var change = new DataChange
		{
			DataType = typeof(int)
		};

		// Assert
		change.DataType.ShouldBe(typeof(int));
	}

	[Fact]
	public void ToStringIncludesColumnName()
	{
		// Arrange
		var change = new DataChange
		{
			ColumnName = "Price",
			OldValue = 10.50m,
			NewValue = 15.00m,
			DataType = typeof(decimal)
		};

		// Act
		var result = change.ToString();

		// Assert
		result.ShouldContain("Price");
	}

	[Fact]
	public void ToStringIncludesOldValue()
	{
		// Arrange
		var change = new DataChange
		{
			ColumnName = "Status",
			OldValue = "Pending",
			NewValue = "Approved",
			DataType = typeof(string)
		};

		// Act
		var result = change.ToString();

		// Assert
		result.ShouldContain("Pending");
	}

	[Fact]
	public void ToStringIncludesNewValue()
	{
		// Arrange
		var change = new DataChange
		{
			ColumnName = "Status",
			OldValue = "Pending",
			NewValue = "Approved",
			DataType = typeof(string)
		};

		// Act
		var result = change.ToString();

		// Assert
		result.ShouldContain("Approved");
	}

	[Fact]
	public void ToStringIncludesDataTypeName()
	{
		// Arrange
		var change = new DataChange
		{
			ColumnName = "Count",
			OldValue = 5,
			NewValue = 10,
			DataType = typeof(int)
		};

		// Act
		var result = change.ToString();

		// Assert
		result.ShouldContain("Int32");
	}

	[Fact]
	public void ToStringShowsUnknown_WhenDataTypeIsNull()
	{
		// Arrange
		var change = new DataChange
		{
			ColumnName = "Unknown",
			OldValue = "old",
			NewValue = "new"
		};

		// Act
		var result = change.ToString();

		// Assert
		result.ShouldContain("Unknown");
	}

	[Fact]
	public void SupportNullValues_ForInsertOperation()
	{
		// Arrange - Insert has no old value
		var change = new DataChange
		{
			ColumnName = "Id",
			OldValue = null,
			NewValue = 1,
			DataType = typeof(int)
		};

		// Assert
		change.OldValue.ShouldBeNull();
		change.NewValue.ShouldBe(1);
	}

	[Fact]
	public void SupportNullValues_ForDeleteOperation()
	{
		// Arrange - Delete has no new value
		var change = new DataChange
		{
			ColumnName = "Id",
			OldValue = 1,
			NewValue = null,
			DataType = typeof(int)
		};

		// Assert
		change.OldValue.ShouldBe(1);
		change.NewValue.ShouldBeNull();
	}

	[Fact]
	public void InitializeWithAllProperties()
	{
		// Act
		var change = new DataChange
		{
			ColumnName = "Balance",
			OldValue = 100.00m,
			NewValue = 150.00m,
			DataType = typeof(decimal)
		};

		// Assert
		change.ColumnName.ShouldBe("Balance");
		change.OldValue.ShouldBe(100.00m);
		change.NewValue.ShouldBe(150.00m);
		change.DataType.ShouldBe(typeof(decimal));
	}
}
