// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.SqlServer;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Data.Tests.SqlServer.Inbox;

/// <summary>
/// Regression tests for SqlServerInboxOptionsValidator (Sprint 686, T.1 ggjam).
/// The validator is internal, so tests go through the DI/Options validation pipeline.
/// Validates that SQL injection payloads in SchemaName/TableName are rejected.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.Inbox)]
public sealed class SqlServerInboxOptionsValidatorShould
{
	[Fact]
	public void Accept_DefaultOptions()
	{
		// Arrange -- default values ("dbo", "inbox_messages") should be valid
		var sp = BuildServiceProvider(o => { });

		// Act
		var options = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value;

		// Assert
		options.SchemaName.ShouldBe("dbo");
		options.TableName.ShouldBe("inbox_messages");
	}

	[Fact]
	public void Accept_ValidCustomIdentifiers()
	{
		// Arrange
		var sp = BuildServiceProvider(o =>
		{
			o.SchemaName = "custom_schema";
			o.TableName = "my_inbox_table_2";
		});

		// Act
		var options = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value;

		// Assert
		options.SchemaName.ShouldBe("custom_schema");
	}

	[Theory]
	[InlineData("dbo; DROP TABLE --")]
	[InlineData("schema' OR '1'='1")]
	[InlineData("test;DELETE FROM users")]
	[InlineData("schema name")]
	[InlineData("schema.name")]
	[InlineData("[dbo]")]
	public void Reject_InvalidSchemaName(string schemaName)
	{
		// Arrange
		var sp = BuildServiceProvider(o =>
		{
			o.SchemaName = schemaName;
			o.TableName = "valid_table";
		});

		// Act & Assert
		var ex = Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value);
		ex.Message.ShouldContain("SchemaName");
	}

	[Theory]
	[InlineData("table; DROP TABLE --")]
	[InlineData("table' OR '1'='1")]
	[InlineData("test;DELETE FROM users")]
	[InlineData("table name")]
	[InlineData("[table]")]
	public void Reject_InvalidTableName(string tableName)
	{
		// Arrange
		var sp = BuildServiceProvider(o =>
		{
			o.SchemaName = "dbo";
			o.TableName = tableName;
		});

		// Act & Assert
		var ex = Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value);
		ex.Message.ShouldContain("TableName");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_EmptyOrWhitespaceSchemaName(string schemaName)
	{
		// Arrange
		var sp = BuildServiceProvider(o =>
		{
			o.SchemaName = schemaName;
			o.TableName = "valid_table";
		});

		// Act & Assert
		var ex = Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value);
		ex.Message.ShouldContain("SchemaName");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_EmptyOrWhitespaceTableName(string tableName)
	{
		// Arrange
		var sp = BuildServiceProvider(o =>
		{
			o.SchemaName = "dbo";
			o.TableName = tableName;
		});

		// Act & Assert
		var ex = Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value);
		ex.Message.ShouldContain("TableName");
	}

	/// <summary>
	/// Builds a minimal service provider with the inbox validator wired through AddSqlServerInboxStore.
	/// </summary>
	private static ServiceProvider BuildServiceProvider(Action<SqlServerInboxOptions> configure)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSqlServerInboxStore(o =>
		{
			o.ConnectionString = "Server=test;Database=test;";
			configure(o);
		});
		return services.BuildServiceProvider();
	}
}
