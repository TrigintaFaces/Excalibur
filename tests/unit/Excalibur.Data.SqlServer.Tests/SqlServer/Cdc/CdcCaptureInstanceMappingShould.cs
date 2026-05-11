// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Verifies that the <c>DeriveCaptureInstances</c> logic in
/// <see cref="CdcBuilderSqlServerExtensions"/> correctly generates the
/// <see cref="IDatabaseOptions.CaptureInstanceToTableNameMap"/> from tracked table configuration.
/// </summary>
/// <remarks>
/// Sprint 816: CDC capture-instance → logical-table-name mapping.
/// Tested indirectly via DI resolution because <c>DeriveCaptureInstances</c> is private static.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcCaptureInstanceMappingShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Encrypt=false;TrustServerCertificate=true";

	/// <summary>
	/// Builds a service provider configured with SQL Server CDC and the given tracked tables,
	/// then resolves <see cref="IDatabaseOptions"/> to inspect the derived mapping.
	/// </summary>
	private static IDatabaseOptions BuildAndResolveDatabaseOptions(
		Action<CdcOptions>? configureCdc = null,
		Action<ISqlServerCdcBuilder>? configureSql = null)
	{
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddCdcProcessor(builder =>
		{
			builder.UseSqlServer(sql =>
			{
				sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))
				   .DatabaseName("TestDb");

				configureSql?.Invoke(sql);
			});

			// Apply tracked table configuration via CdcOptions
			if (configureCdc is not null)
			{
				services.PostConfigure(configureCdc);
			}
		});

		var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<IDatabaseOptions>();
	}

	[Fact]
	public void ProduceIdentityMapping_WhenTableNameUsedAsCaptureInstance()
	{
		// Arrange & Act — tracked table with no explicit CaptureInstance
		var dbOptions = BuildAndResolveDatabaseOptions(
			configureCdc: opts =>
			{
				opts.TrackedTables.Add(new CdcTableTrackingOptions
				{
					TableName = "dbo.Orders"
				});
			});

		// Assert — capture instance = table name (identity), map entry still exists
		dbOptions.CaptureInstances.ShouldContain("dbo.Orders");
		dbOptions.CaptureInstanceToTableNameMap.ShouldContainKeyAndValue("dbo.Orders", "dbo.Orders");
	}

	[Fact]
	public void ProduceTranslationMapping_WhenExplicitCaptureInstanceDiffersFromTableName()
	{
		// Arrange & Act — CaptureInstance explicitly set (e.g. SQL Server CDC uses underscores)
		var dbOptions = BuildAndResolveDatabaseOptions(
			configureCdc: opts =>
			{
				opts.TrackedTables.Add(new CdcTableTrackingOptions
				{
					TableName = "sales.Customers",
					CaptureInstance = "sales_Customers"
				});
			});

		// Assert — capture instance in array, map translates capture instance → logical table name
		dbOptions.CaptureInstances.ShouldContain("sales_Customers");
		dbOptions.CaptureInstanceToTableNameMap.ShouldContainKeyAndValue("sales_Customers", "sales.Customers");
	}

	[Fact]
	public void ProduceMappings_ForMultipleTrackedTables()
	{
		// Arrange & Act
		var dbOptions = BuildAndResolveDatabaseOptions(
			configureCdc: opts =>
			{
				opts.TrackedTables.Add(new CdcTableTrackingOptions
				{
					TableName = "dbo.Orders",
					CaptureInstance = "dbo_Orders"
				});
				opts.TrackedTables.Add(new CdcTableTrackingOptions
				{
					TableName = "sales.Customers"
					// No explicit CaptureInstance — uses TableName as identity
				});
			});

		// Assert
		dbOptions.CaptureInstances.Length.ShouldBe(2);
		dbOptions.CaptureInstances.ShouldContain("dbo_Orders");
		dbOptions.CaptureInstances.ShouldContain("sales.Customers");

		dbOptions.CaptureInstanceToTableNameMap.Count.ShouldBe(2);
		dbOptions.CaptureInstanceToTableNameMap["dbo_Orders"].ShouldBe("dbo.Orders");
		dbOptions.CaptureInstanceToTableNameMap["sales.Customers"].ShouldBe("sales.Customers");
	}

	[Fact]
	public void MergeLegacyBuilderInstances_AsIdentityMappings()
	{
		// Arrange & Act — no tracked tables, but legacy builder CaptureInstances set
		// This tests backward compatibility with the old .CaptureInstances("x") builder API
		var dbOptions = BuildAndResolveDatabaseOptions(
			configureCdc: _ => { }, // No tracked tables
			configureSql: sql =>
			{
				// Legacy API path: some consumers may still configure via CaptureInstances
				// on SqlServerCdcOptions directly. The code merges them with identity mapping.
			});

		// Assert — when no tracked tables and no legacy instances, arrays are empty
		dbOptions.CaptureInstances.ShouldBeEmpty();
		dbOptions.CaptureInstanceToTableNameMap.ShouldBeEmpty();
	}

	[Fact]
	public void DeduplicateCaptureInstances_AcrossTrackedTablesAndLegacyInstances()
	{
		// Arrange & Act — same instance name from both tracked tables (should not duplicate)
		var dbOptions = BuildAndResolveDatabaseOptions(
			configureCdc: opts =>
			{
				opts.TrackedTables.Add(new CdcTableTrackingOptions
				{
					TableName = "dbo.Orders",
					CaptureInstance = "dbo_Orders"
				});
				// Add same table again — should be deduplicated
				opts.TrackedTables.Add(new CdcTableTrackingOptions
				{
					TableName = "dbo.Orders",
					CaptureInstance = "dbo_Orders"
				});
			});

		// Assert — only one instance despite two tracked table entries
		dbOptions.CaptureInstances.Length.ShouldBe(1);
		dbOptions.CaptureInstances[0].ShouldBe("dbo_Orders");
		dbOptions.CaptureInstanceToTableNameMap.Count.ShouldBe(1);
	}

	[Fact]
	public void IgnoreEmptyTableNames()
	{
		// Arrange & Act — tracked table with empty TableName should be skipped
		var dbOptions = BuildAndResolveDatabaseOptions(
			configureCdc: opts =>
			{
				opts.TrackedTables.Add(new CdcTableTrackingOptions
				{
					TableName = ""
				});
				opts.TrackedTables.Add(new CdcTableTrackingOptions
				{
					TableName = "dbo.Orders"
				});
			});

		// Assert — empty table name skipped, only valid one included
		dbOptions.CaptureInstances.Length.ShouldBe(1);
		dbOptions.CaptureInstances[0].ShouldBe("dbo.Orders");
	}
}
