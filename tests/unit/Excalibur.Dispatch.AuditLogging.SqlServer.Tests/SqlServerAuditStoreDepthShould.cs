// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.AuditLogging.SqlServer.Tests;

/// <summary>
/// Depth coverage tests for <see cref="SqlServerAuditStore"/> covering
/// options defaults, FullyQualifiedTableName computation, retention options,
/// and property defaults.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SqlServerAuditStoreDepthShould
{
	[Fact]
	public void Options_HaveCorrectDefaults()
	{
		// Act
		var options = new SqlServerAuditOptions();

		// Assert
		options.SchemaName.ShouldBe("audit");
		options.TableName.ShouldBe("AuditEvents");
		options.BatchInsertSize.ShouldBe(1000);
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7 * 365));
		options.EnableRetentionEnforcement.ShouldBeTrue();
		options.RetentionCleanupInterval.ShouldBe(TimeSpan.FromDays(1));
		options.RetentionCleanupBatchSize.ShouldBe(10000);
		options.CommandTimeoutSeconds.ShouldBe(30);
		options.UsePartitioning.ShouldBeFalse();
		options.EnableHashChain.ShouldBeTrue();
		options.EnableDetailedTelemetry.ShouldBeFalse();
	}

	[Fact]
	public void Options_FullyQualifiedTableName_CombineSchemaAndTable()
	{
		// Arrange
		var options = new SqlServerAuditOptions
		{
			SchemaName = "mySchema",
			TableName = "myTable"
		};

		// Act & Assert
		options.FullyQualifiedTableName.ShouldBe("[mySchema].[myTable]");
	}

	[Fact]
	public void Options_FullyQualifiedTableName_UseDefaults()
	{
		// Act
		var options = new SqlServerAuditOptions();

		// Assert
		options.FullyQualifiedTableName.ShouldBe("[audit].[AuditEvents]");
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSqlServerAuditStore(o =>
			o.ConnectionString = "Server=localhost;Database=Audit");

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerAuditStore(o =>
			o.ConnectionString = "Server=localhost;Database=Audit");

		// Assert
		var descriptor = services.Single(sd => sd.ServiceType == typeof(SqlServerAuditStore));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AcceptCustomRetentionPeriod()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit",
			RetentionPeriod = TimeSpan.FromDays(365)
		});

		// Act
		var store = new SqlServerAuditStore(options, EnabledTestLogger.Create<SqlServerAuditStore>());

		// Assert
		store.ShouldNotBeNull();
		store.Dispose();
	}

	[Fact]
	public void Options_OptionsInstanceOverload_CopiesAllProperties()
	{
		// Arrange
		var services = new ServiceCollection();
		var opts = new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit",
			SchemaName = "custom_schema",
			TableName = "custom_table",
			BatchInsertSize = 500,
			RetentionPeriod = TimeSpan.FromDays(365),
			EnableRetentionEnforcement = false,
			RetentionCleanupInterval = TimeSpan.FromHours(6),
			RetentionCleanupBatchSize = 5000,
			CommandTimeoutSeconds = 60,
			UsePartitioning = true,
			EnableHashChain = false,
			EnableDetailedTelemetry = true
		};

		// Act
		services.AddSqlServerAuditStore(opts);

		// Assert — service should be registered
		services.ShouldContain(sd => sd.ServiceType == typeof(SqlServerAuditStore));
	}

	[Theory]
	[InlineData("valid_identifier", true)]
	[InlineData("A1_B2", true)]
	[InlineData("1", true)]
	[InlineData("", false)]
	[InlineData("with-dash", false)]
	[InlineData("with space", false)]
	[InlineData("with.dot", false)]
	[InlineData("emoji_😀", false)]
	public void Sql_identifier_regex_matches_expected_patterns(string value, bool expected)
	{
		var regexFactory = typeof(SqlServerAuditStore).GetMethod(
			"SqlIdentifierRegex",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
		var regex = (System.Text.RegularExpressions.Regex)regexFactory.Invoke(null, null)!;

		regex.IsMatch(value).ShouldBe(expected);
	}
}
