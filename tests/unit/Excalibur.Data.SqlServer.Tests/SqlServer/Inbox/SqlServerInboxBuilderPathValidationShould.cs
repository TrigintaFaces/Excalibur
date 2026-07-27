// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Inbox;

/// <summary>
/// Regression lock for the SQL Server inbox <b>fluent-builder</b> path
/// (<c>AddExcaliburInbox(i =&gt; i.UseSqlServer(...))</c>).
/// </summary>
/// <remarks>
/// The builder path previously registered only the connection validator and skipped the
/// <c>^[A-Za-z0-9_]+$</c> SQL-identifier allowlist that the options path
/// (<c>AddSqlServerInboxStore</c>) already enforced. A malicious SchemaName/TableName supplied
/// via <c>SchemaName()</c>/<c>TableName()</c> therefore reached the MERGE
/// <c>{QualifiedTableName}</c> unvalidated. These tests assert the builder path routes
/// identifiers through the allowlist for parity with the options path — non-vacuous:
/// they pass only when the allowlist validator is registered on the builder path.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.Inbox)]
public sealed class SqlServerInboxBuilderPathValidationShould
{
	[Theory]
	[InlineData("dbo; DROP TABLE --")]
	[InlineData("schema' OR '1'='1")]
	[InlineData("test;DELETE FROM users")]
	[InlineData("schema name")]
	[InlineData("schema.name")]
	[InlineData("[dbo]")]
	public void Reject_MaliciousSchemaName_ViaBuilderPath(string schemaName)
	{
		// Arrange -- configure a malicious schema through the fluent builder
		var sp = BuildViaBuilder(sql => sql.SchemaName(schemaName).TableName("valid_table"));

		// Act & Assert -- allowlist validator must reject on options materialization
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
	public void Reject_MaliciousTableName_ViaBuilderPath(string tableName)
	{
		// Arrange
		var sp = BuildViaBuilder(sql => sql.SchemaName("dbo").TableName(tableName));

		// Act & Assert
		var ex = Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value);
		ex.Message.ShouldContain("TableName");
	}

	[Fact]
	public void Accept_ValidIdentifiers_ViaBuilderPath()
	{
		// Arrange -- valid identifiers must still pass on the builder path (guards against
		// an over-broad validator that would reject legitimate config).
		var sp = BuildViaBuilder(sql => sql.SchemaName("custom_schema").TableName("my_inbox_table_2"));

		// Act
		var options = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value;

		// Assert
		options.SchemaName.ShouldBe("custom_schema");
		options.TableName.ShouldBe("my_inbox_table_2");
	}

	/// <summary>
	/// Builds a service provider through the fluent-builder registration path
	/// (<c>AddExcaliburInbox</c> + <c>UseSqlServer</c>), which is the path under test.
	/// </summary>
	private static ServiceProvider BuildViaBuilder(Action<ISqlServerInboxBuilder> configure)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddExcaliburInbox(inbox =>
			inbox.UseSqlServer(sql =>
			{
				_ = sql.ConnectionString("Server=test;Database=test;");
				configure(sql);
			}));
		return services.BuildServiceProvider();
	}
}
