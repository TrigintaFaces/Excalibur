// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Cdc.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="CdcTrackedTablesPostConfigureOptions"/> (T.8 / fhrb1).
/// Validates config-driven tracked table merging via IPostConfigureOptions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcTrackedTablesPostConfigureOptionsShould : UnitTestBase
{
	[Fact]
	public void ThrowArgumentNullException_WhenConfigurationIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new CdcTrackedTablesPostConfigureOptions(null!, "Cdc:Tables"));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenSectionPathIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new CdcTrackedTablesPostConfigureOptions(config, null!));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();
		var sut = new CdcTrackedTablesPostConfigureOptions(config, "Cdc:Tables");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.PostConfigure(null, null!));
	}

	[Fact]
	public void DoNothing_WhenConfigSectionDoesNotExist()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();
		var sut = new CdcTrackedTablesPostConfigureOptions(config, "Cdc:Tables");
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.ShouldBeEmpty();
	}

	[Fact]
	public void MergeTablesFromConfiguration()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Cdc:Tables:0:TableName"] = "dbo.Orders",
				["Cdc:Tables:1:TableName"] = "dbo.Customers",
			})
			.Build();

		var sut = new CdcTrackedTablesPostConfigureOptions(config, "Cdc:Tables");
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.Count.ShouldBe(2);
		options.TrackedTables[0].TableName.ShouldBe("dbo.Orders");
		options.TrackedTables[1].TableName.ShouldBe("dbo.Customers");
	}

	[Fact]
	public void SkipDuplicates_WhenCodeRegisteredTablesExist()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Cdc:Tables:0:TableName"] = "dbo.Orders",
				["Cdc:Tables:1:TableName"] = "dbo.Customers",
			})
			.Build();

		var sut = new CdcTrackedTablesPostConfigureOptions(config, "Cdc:Tables");
		var options = new CdcOptions();
		options.TrackedTables.Add(new CdcTableTrackingOptions { TableName = "dbo.Orders" });

		// Act
		sut.PostConfigure(null, options);

		// Assert — code-registered "dbo.Orders" stays, config adds only "dbo.Customers"
		options.TrackedTables.Count.ShouldBe(2);
		options.TrackedTables[0].TableName.ShouldBe("dbo.Orders");
		options.TrackedTables[1].TableName.ShouldBe("dbo.Customers");
	}

	[Fact]
	public void DoNothing_WhenConfigSectionIsEmpty()
	{
		// Arrange — section exists but has no children
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Cdc:Tables"] = "",
			})
			.Build();

		var sut = new CdcTrackedTablesPostConfigureOptions(config, "Cdc:Tables");
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.ShouldBeEmpty();
	}

	[Fact]
	public void PreserveExistingTables_WhenConfigIsEmpty()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();
		var sut = new CdcTrackedTablesPostConfigureOptions(config, "Cdc:Tables");
		var options = new CdcOptions();
		options.TrackedTables.Add(new CdcTableTrackingOptions { TableName = "dbo.Orders" });

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.Count.ShouldBe(1);
		options.TrackedTables[0].TableName.ShouldBe("dbo.Orders");
	}

	[Fact]
	public void BindCaptureInstance_FromConfiguration()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Cdc:Tables:0:TableName"] = "dbo.Orders",
				["Cdc:Tables:0:CaptureInstance"] = "dbo_Orders_v2",
			})
			.Build();

		var sut = new CdcTrackedTablesPostConfigureOptions(config, "Cdc:Tables");
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.Count.ShouldBe(1);
		options.TrackedTables[0].TableName.ShouldBe("dbo.Orders");
		options.TrackedTables[0].CaptureInstance.ShouldBe("dbo_Orders_v2");
	}
}
