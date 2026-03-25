// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Cdc.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="CdcHandlerDiscoveryPostConfigureOptions"/> (T.9 / j5oj1).
/// Validates auto-discovery of tracked tables from ICdcTableProvider implementations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcHandlerDiscoveryPostConfigureOptionsShould : UnitTestBase
{
	[Fact]
	public void ThrowArgumentNullException_WhenServiceProviderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new CdcHandlerDiscoveryPostConfigureOptions(null!));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var sp = new ServiceCollection().BuildServiceProvider();
		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.PostConfigure(null, null!));
	}

	[Fact]
	public void DoNothing_WhenNoProvidersRegistered()
	{
		// Arrange
		var sp = new ServiceCollection().BuildServiceProvider();
		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.ShouldBeEmpty();
	}

	[Fact]
	public void DiscoverTablesFromSingleProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ICdcTableProvider>(new TestTableProvider("dbo.Orders", "dbo.Customers"));
		var sp = services.BuildServiceProvider();

		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.Count.ShouldBe(2);
		options.TrackedTables.ShouldContain(t => t.TableName == "dbo.Orders");
		options.TrackedTables.ShouldContain(t => t.TableName == "dbo.Customers");
	}

	[Fact]
	public void DiscoverTablesFromMultipleProviders()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ICdcTableProvider>(new TestTableProvider("dbo.Orders"));
		services.AddSingleton<ICdcTableProvider>(new TestTableProvider("dbo.Customers"));
		var sp = services.BuildServiceProvider();

		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.Count.ShouldBe(2);
		options.TrackedTables.ShouldContain(t => t.TableName == "dbo.Orders");
		options.TrackedTables.ShouldContain(t => t.TableName == "dbo.Customers");
	}

	[Fact]
	public void SkipNullTableNames()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ICdcTableProvider>(new TestTableProvider(null!));
		var sp = services.BuildServiceProvider();

		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.ShouldBeEmpty();
	}

	[Fact]
	public void SkipProviders_WithNullTableNamesArray()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ICdcTableProvider>(new NullTableNamesProvider());
		var sp = services.BuildServiceProvider();

		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.ShouldBeEmpty();
	}

	[Fact]
	public void SkipProviders_WithEmptyTableNamesArray()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ICdcTableProvider>(new TestTableProvider());
		var sp = services.BuildServiceProvider();

		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.ShouldBeEmpty();
	}

	[Fact]
	public void SkipEmptyStrings_InTableNames()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ICdcTableProvider>(new TestTableProvider("", "dbo.Orders", ""));
		var sp = services.BuildServiceProvider();

		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.Count.ShouldBe(1);
		options.TrackedTables[0].TableName.ShouldBe("dbo.Orders");
	}

	[Fact]
	public void SkipDuplicates_WhenCodeRegisteredTablesExist()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ICdcTableProvider>(new TestTableProvider("dbo.Orders", "dbo.Customers"));
		var sp = services.BuildServiceProvider();

		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);
		var options = new CdcOptions();
		options.TrackedTables.Add(new CdcTableTrackingOptions { TableName = "dbo.Orders" });

		// Act
		sut.PostConfigure(null, options);

		// Assert — code-registered "dbo.Orders" preserved, only "dbo.Customers" added
		options.TrackedTables.Count.ShouldBe(2);
		options.TrackedTables[0].TableName.ShouldBe("dbo.Orders");
		options.TrackedTables[1].TableName.ShouldBe("dbo.Customers");
	}

	[Fact]
	public void DeduplicateAcrossProviders()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ICdcTableProvider>(new TestTableProvider("dbo.Orders"));
		services.AddSingleton<ICdcTableProvider>(new TestTableProvider("dbo.Orders", "dbo.Customers"));
		var sp = services.BuildServiceProvider();

		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);
		var options = new CdcOptions();

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.Count.ShouldBe(2);
		options.TrackedTables.ShouldContain(t => t.TableName == "dbo.Orders");
		options.TrackedTables.ShouldContain(t => t.TableName == "dbo.Customers");
	}

	[Fact]
	public void PreserveExistingTables_WhenNoProvidersRegistered()
	{
		// Arrange
		var sp = new ServiceCollection().BuildServiceProvider();
		var sut = new CdcHandlerDiscoveryPostConfigureOptions(sp);
		var options = new CdcOptions();
		options.TrackedTables.Add(new CdcTableTrackingOptions { TableName = "dbo.Orders" });

		// Act
		sut.PostConfigure(null, options);

		// Assert
		options.TrackedTables.Count.ShouldBe(1);
		options.TrackedTables[0].TableName.ShouldBe("dbo.Orders");
	}

	#region Test helpers

	private sealed class TestTableProvider : ICdcTableProvider
	{
		public TestTableProvider(params string[] tableNames)
		{
			TableNames = tableNames;
		}

		public string[] TableNames { get; }
	}

	private sealed class NullTableNamesProvider : ICdcTableProvider
	{
		public string[] TableNames => null!;
	}

	#endregion
}
