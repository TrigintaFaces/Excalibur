// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.SqlServer.Encryption;

/// <summary>
/// Regression tests for SqlServerKeyEscrowOptionsValidator (Sprint 687, T.3 w7m9h).
/// The validator is internal, so tests go through the DI/Options validation pipeline.
/// Validates that SQL injection payloads in Schema/TableName/TokensTableName are rejected.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "SqlInjection")]
public sealed class SqlServerKeyEscrowOptionsValidatorShould
{
	[Fact]
	public void Accept_DefaultOptions()
	{
		// Arrange -- default values ("compliance", "KeyEscrow", "KeyEscrowTokens") should be valid
		var sp = BuildServiceProvider(o => { });

		// Act
		var options = sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value;

		// Assert
		options.Schema.ShouldBe("compliance");
		options.TableName.ShouldBe("KeyEscrow");
	}

	[Fact]
	public void Accept_ValidCustomIdentifiers()
	{
		// Arrange
		var sp = BuildServiceProvider(o =>
		{
			o.Schema = "custom_schema";
			o.TableName = "my_escrow_table";
			o.TokensTableName = "my_tokens";
		});

		// Act
		var options = sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value;

		// Assert
		options.Schema.ShouldBe("custom_schema");
	}

	[Theory]
	[InlineData("dbo; DROP TABLE --")]
	[InlineData("schema' OR '1'='1")]
	[InlineData("test;DELETE FROM users")]
	[InlineData("schema name")]
	[InlineData("[dbo]")]
	public void Reject_InvalidSchema(string schema)
	{
		// Arrange
		var sp = BuildServiceProvider(o => o.Schema = schema);

		// Act & Assert
		Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value);
	}

	[Theory]
	[InlineData("table; DROP TABLE --")]
	[InlineData("table' OR '1'='1")]
	[InlineData("[table]")]
	public void Reject_InvalidTableName(string tableName)
	{
		// Arrange
		var sp = BuildServiceProvider(o => o.TableName = tableName);

		// Act & Assert
		Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value);
	}

	[Theory]
	[InlineData("tokens; DROP TABLE --")]
	[InlineData("tokens' OR '1'='1")]
	[InlineData("[tokens]")]
	public void Reject_InvalidTokensTableName(string tokensTableName)
	{
		// Arrange
		var sp = BuildServiceProvider(o => o.TokensTableName = tokensTableName);

		// Act & Assert
		Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_EmptySchema(string schema)
	{
		// Arrange
		var sp = BuildServiceProvider(o => o.Schema = schema);

		// Act & Assert
		Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_EmptyTableName(string tableName)
	{
		// Arrange
		var sp = BuildServiceProvider(o => o.TableName = tableName);

		// Act & Assert
		Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_EmptyTokensTableName(string tokensTableName)
	{
		// Arrange
		var sp = BuildServiceProvider(o => o.TokensTableName = tokensTableName);

		// Act & Assert
		Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value);
	}

	[Fact]
	public void Reject_EmptyConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSqlServerKeyEscrow(o =>
		{
			o.ConnectionString = "";
		});
		var sp = services.BuildServiceProvider();

		// Act & Assert
		Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value);
	}

	private static ServiceProvider BuildServiceProvider(Action<SqlServerKeyEscrowOptions> configure)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSqlServerKeyEscrow(o =>
		{
			o.ConnectionString = "Server=test;Database=test;";
			configure(o);
		});
		return services.BuildServiceProvider();
	}
}
