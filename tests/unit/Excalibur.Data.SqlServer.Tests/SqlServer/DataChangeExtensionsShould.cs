// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class DataChangeExtensionsShould
{
	[Fact]
	public void GetNewValueByColumnName()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "Name", NewValue = "Alice", OldValue = "Bob" },
			new() { ColumnName = "Age", NewValue = 30, OldValue = 25 },
		};

		// Act
		var name = changes.GetNewValue<string>("Name");

		// Assert
		name.ShouldBe("Alice");
	}

	[Fact]
	public void GetOldValueByColumnName()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "Name", NewValue = "Alice", OldValue = "Bob" },
		};

		// Act
		var oldName = changes.GetOldValue<string>("Name");

		// Assert
		oldName.ShouldBe("Bob");
	}

	[Fact]
	public void ReturnDefaultWhenColumnNotFound()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "Name", NewValue = "Alice" },
		};

		// Act
		var result = changes.GetNewValue<string?>("Missing");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenNonNullableColumnNotFound()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "Name", NewValue = "Alice" },
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => changes.GetNewValue<int>("Missing"));
	}

	[Fact]
	public void ConvertValueToTargetType()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "Count", NewValue = "42" },
		};

		// Act
		var count = changes.GetNewValue<int>("Count");

		// Assert
		count.ShouldBe(42);
	}

	[Fact]
	public void ReturnDirectlyWhenValueMatchesType()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "Value", NewValue = 42 },
		};

		// Act
		var result = changes.GetNewValue<int>("Value");

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public void HandleCaseInsensitiveColumnNames()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "MyColumn", NewValue = "value" },
		};

		// Act
		var result = changes.GetNewValue<string>("mycolumn");

		// Assert
		result.ShouldBe("value");
	}

	[Fact]
	public void ThrowWhenConversionFails()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "Value", NewValue = "not-a-number" },
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => changes.GetNewValue<int>("Value"));
	}

	[Fact]
	public void GetNewValueFromDataChangeEvent()
	{
		// Arrange
		var evt = new DataChangeEvent
		{
			TableName = "Users",
			ChangeType = DataChangeType.Update,
			Changes =
			[
				new DataChange { ColumnName = "Name", NewValue = "Alice" },
			],
		};

		// Act
		var name = evt.GetNewValue<string>("Name");

		// Assert
		name.ShouldBe("Alice");
	}

	[Fact]
	public void GetOldValueFromDataChangeEvent()
	{
		// Arrange
		var evt = new DataChangeEvent
		{
			TableName = "Users",
			ChangeType = DataChangeType.Update,
			Changes =
			[
				new DataChange { ColumnName = "Name", OldValue = "Bob" },
			],
		};

		// Act
		var name = evt.GetOldValue<string>("Name");

		// Assert
		name.ShouldBe("Bob");
	}

	[Fact]
	public void HandleNullableTypeWithNullValue()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "Value", NewValue = null },
		};

		// Act
		var result = changes.GetNewValue<int?>("Value");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnDefaultForOldValueWhenNullOnNullableType()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "Value", OldValue = null },
		};

		// Act
		var result = changes.GetOldValue<string?>("Value");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ThrowForOldValueWhenNonNullableAndMissing()
	{
		// Arrange
		var changes = new List<DataChange>
		{
			new() { ColumnName = "Other", OldValue = null },
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => changes.GetOldValue<int>("Missing"));
	}
}
