// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.SqlServer;

namespace Excalibur.Dispatch.Security.Tests.Compliance.SqlServer.Encryption;

/// <summary>
/// Unit tests for <see cref="SqlServerKeyEscrowOptions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SqlServerKeyEscrowOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void HaveEmptyConnectionString_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.ConnectionString.ShouldBeEmpty();
	}

	[Fact]
	public void HaveComplianceSchema_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.Schema.ShouldBe("compliance");
	}

	[Fact]
	public void HaveKeyEscrowTableName_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.TableName.ShouldBe("KeyEscrow");
	}

	[Fact]
	public void HaveRecoveryTokensTableName_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.TokensTableName.ShouldBe("RecoveryTokens");
	}

	[Fact]
	public void Have30SecondCommandTimeout_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveAutoExpireTokensEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.AutoExpireTokens.ShouldBeTrue();
	}

	[Fact]
	public void Have24HourDefaultTokenExpiration_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.DefaultTokenExpiration.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void Have5DefaultCustodianCount_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.DefaultCustodianCount.ShouldBe(5);
	}

	[Fact]
	public void Have3DefaultThreshold_ByDefault()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.DefaultThreshold.ShouldBe(3);
	}

	#endregion Default Values Tests

	#region Fully Qualified Table Name Tests

	[Fact]
	public void ReturnFullyQualifiedTableName_WithDefaultValues()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.FullyQualifiedTableName.ShouldBe("[compliance].[KeyEscrow]");
	}

	[Fact]
	public void ReturnFullyQualifiedTableName_WithCustomSchema()
	{
		// Arrange
		var options = new SqlServerKeyEscrowOptions
		{
			Schema = "security"
		};

		// Act & Assert
		options.FullyQualifiedTableName.ShouldBe("[security].[KeyEscrow]");
	}

	[Fact]
	public void ReturnFullyQualifiedTableName_WithCustomTableName()
	{
		// Arrange
		var options = new SqlServerKeyEscrowOptions
		{
			TableName = "EscrowedKeys"
		};

		// Act & Assert
		options.FullyQualifiedTableName.ShouldBe("[compliance].[EscrowedKeys]");
	}

	[Fact]
	public void ReturnFullyQualifiedTableName_WithCustomSchemaAndTableName()
	{
		// Arrange
		var options = new SqlServerKeyEscrowOptions
		{
			Schema = "security",
			TableName = "EscrowedKeys"
		};

		// Act & Assert
		options.FullyQualifiedTableName.ShouldBe("[security].[EscrowedKeys]");
	}

	#endregion Fully Qualified Table Name Tests

	#region Fully Qualified Tokens Table Name Tests

	[Fact]
	public void ReturnFullyQualifiedTokensTableName_WithDefaultValues()
	{
		// Arrange & Act
		var options = new SqlServerKeyEscrowOptions();

		// Assert
		options.FullyQualifiedTokensTableName.ShouldBe("[compliance].[RecoveryTokens]");
	}

	[Fact]
	public void ReturnFullyQualifiedTokensTableName_WithCustomSchema()
	{
		// Arrange
		var options = new SqlServerKeyEscrowOptions
		{
			Schema = "security"
		};

		// Act & Assert
		options.FullyQualifiedTokensTableName.ShouldBe("[security].[RecoveryTokens]");
	}

	[Fact]
	public void ReturnFullyQualifiedTokensTableName_WithCustomTokensTableName()
	{
		// Arrange
		var options = new SqlServerKeyEscrowOptions
		{
			TokensTableName = "KeyRecoveryTokens"
		};

		// Act & Assert
		options.FullyQualifiedTokensTableName.ShouldBe("[compliance].[KeyRecoveryTokens]");
	}

	#endregion Fully Qualified Tokens Table Name Tests

	#region Property Assignment Tests

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange
		var options = new SqlServerKeyEscrowOptions();
		const string connectionString = "Server=localhost;Database=KeyEscrow;";

		// Act
		options.ConnectionString = connectionString;

		// Assert
		options.ConnectionString.ShouldBe(connectionString);
	}

	[Fact]
	public void AllowSettingCommandTimeout()
	{
		// Arrange
		var options = new SqlServerKeyEscrowOptions();

		// Act
		options.CommandTimeoutSeconds = 60;

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingDefaultTokenExpiration()
	{
		// Arrange
		var options = new SqlServerKeyEscrowOptions();
		var expiration = TimeSpan.FromHours(48);

		// Act
		options.DefaultTokenExpiration = expiration;

		// Assert
		options.DefaultTokenExpiration.ShouldBe(expiration);
	}

	[Fact]
	public void AllowSettingCustomCustodianAndThreshold()
	{
		// Arrange
		var options = new SqlServerKeyEscrowOptions();

		// Act
		options.DefaultCustodianCount = 7;
		options.DefaultThreshold = 4;

		// Assert
		options.DefaultCustodianCount.ShouldBe(7);
		options.DefaultThreshold.ShouldBe(4);
	}

	#endregion Property Assignment Tests
}
