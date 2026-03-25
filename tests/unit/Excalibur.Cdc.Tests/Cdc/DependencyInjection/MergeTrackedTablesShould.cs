// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Cdc.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the static <c>MergeTrackedTables</c> helper in
/// <see cref="CdcTrackedTablesPostConfigureOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MergeTrackedTablesShould
{
	[Fact]
	public void AddSourceTables_WhenTargetIsEmpty()
	{
		// Arrange
		var target = new List<CdcTableTrackingOptions>();
		var source = new List<CdcTableTrackingOptions>
		{
			new() { TableName = "dbo.Orders" },
			new() { TableName = "dbo.Customers" },
		};

		// Act
		CdcTrackedTablesPostConfigureOptions.MergeTrackedTables(target, source);

		// Assert
		target.Count.ShouldBe(2);
		target[0].TableName.ShouldBe("dbo.Orders");
		target[1].TableName.ShouldBe("dbo.Customers");
	}

	[Fact]
	public void SkipDuplicates_CaseInsensitive()
	{
		// Arrange
		var target = new List<CdcTableTrackingOptions>
		{
			new() { TableName = "dbo.Orders" },
		};
		var source = new List<CdcTableTrackingOptions>
		{
			new() { TableName = "DBO.ORDERS" },
			new() { TableName = "dbo.Customers" },
		};

		// Act
		CdcTrackedTablesPostConfigureOptions.MergeTrackedTables(target, source);

		// Assert
		target.Count.ShouldBe(2);
		target[0].TableName.ShouldBe("dbo.Orders");
		target[1].TableName.ShouldBe("dbo.Customers");
	}

	[Fact]
	public void SkipNullOrEmptyTableNames_InSource()
	{
		// Arrange
		var target = new List<CdcTableTrackingOptions>();
		var source = new List<CdcTableTrackingOptions>
		{
			new() { TableName = null! },
			new() { TableName = "" },
			new() { TableName = "dbo.Orders" },
		};

		// Act
		CdcTrackedTablesPostConfigureOptions.MergeTrackedTables(target, source);

		// Assert
		target.Count.ShouldBe(1);
		target[0].TableName.ShouldBe("dbo.Orders");
	}

	[Fact]
	public void IgnoreNullOrEmptyTableNames_InTarget_ForDedup()
	{
		// Arrange
		var target = new List<CdcTableTrackingOptions>
		{
			new() { TableName = null! },
			new() { TableName = "" },
		};
		var source = new List<CdcTableTrackingOptions>
		{
			new() { TableName = "dbo.Orders" },
		};

		// Act
		CdcTrackedTablesPostConfigureOptions.MergeTrackedTables(target, source);

		// Assert
		target.Count.ShouldBe(3);
		target[2].TableName.ShouldBe("dbo.Orders");
	}

	[Fact]
	public void NotAddAnything_WhenSourceIsEmpty()
	{
		// Arrange
		var target = new List<CdcTableTrackingOptions>
		{
			new() { TableName = "dbo.Orders" },
		};
		var source = new List<CdcTableTrackingOptions>();

		// Act
		CdcTrackedTablesPostConfigureOptions.MergeTrackedTables(target, source);

		// Assert
		target.Count.ShouldBe(1);
	}

	[Fact]
	public void SkipAllDuplicates_WhenAllSourceTablesAlreadyExist()
	{
		// Arrange
		var target = new List<CdcTableTrackingOptions>
		{
			new() { TableName = "dbo.Orders" },
			new() { TableName = "dbo.Customers" },
		};
		var source = new List<CdcTableTrackingOptions>
		{
			new() { TableName = "dbo.Orders" },
			new() { TableName = "dbo.Customers" },
		};

		// Act
		CdcTrackedTablesPostConfigureOptions.MergeTrackedTables(target, source);

		// Assert
		target.Count.ShouldBe(2);
	}

	[Fact]
	public void HandleDuplicates_WithinSourceList()
	{
		// Arrange
		var target = new List<CdcTableTrackingOptions>();
		var source = new List<CdcTableTrackingOptions>
		{
			new() { TableName = "dbo.Orders" },
			new() { TableName = "dbo.Orders" },
			new() { TableName = "DBO.ORDERS" },
		};

		// Act
		CdcTrackedTablesPostConfigureOptions.MergeTrackedTables(target, source);

		// Assert
		target.Count.ShouldBe(1);
		target[0].TableName.ShouldBe("dbo.Orders");
	}
}
