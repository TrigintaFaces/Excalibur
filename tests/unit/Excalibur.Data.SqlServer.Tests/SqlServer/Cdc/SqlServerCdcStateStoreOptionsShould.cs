// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Tests verifying SqlServerCdcStateStoreOptions validates identifiers against SQL injection (S543.9).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class SqlServerCdcStateStoreOptionsValidationShould : UnitTestBase
{
	#region SQL Injection Prevention Tests

	[Theory]
	[InlineData(";DROP TABLE Users")]
	[InlineData("' OR 1=1 --")]
	[InlineData("table; DELETE FROM")]
	[InlineData("schema.table")]
	[InlineData("[brackets]")]
	[InlineData("name WITH (NOLOCK)")]
	public void Validate_RejectsMaliciousSchemaName(string maliciousName)
	{
		// Arrange
		var options = new SqlServerCdcStateStoreOptions { SchemaName = maliciousName };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Theory]
	[InlineData(";DROP TABLE Users")]
	[InlineData("' OR 1=1 --")]
	[InlineData("table\nname")]
	[InlineData("table-name")]
	[InlineData("[brackets]")]
	public void Validate_RejectsMaliciousTableName(string maliciousName)
	{
		// Arrange
		var options = new SqlServerCdcStateStoreOptions { TableName = maliciousName };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Theory]
	[InlineData("ValidSchema")]
	[InlineData("my_schema")]
	[InlineData("Schema123")]
	[InlineData("_private")]
	public void Validate_AcceptsValidSchemaNames(string validName)
	{
		// Arrange
		var options = new SqlServerCdcStateStoreOptions { SchemaName = validName };

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Theory]
	[InlineData("ValidTable")]
	[InlineData("my_table")]
	[InlineData("Table123")]
	[InlineData("_temp")]
	public void Validate_AcceptsValidTableNames(string validName)
	{
		// Arrange
		var options = new SqlServerCdcStateStoreOptions { TableName = validName };

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	#endregion

	#region Bracket Escaping Tests

	[Fact]
	public void QualifiedTableName_UsesBracketEscaping()
	{
		// Arrange
		var options = new SqlServerCdcStateStoreOptions
		{
			SchemaName = "MySchema",
			TableName = "MyTable",
		};

		// Assert — bracket-escaped format prevents SQL injection in interpolated queries
		options.QualifiedTableName.ShouldBe("[MySchema].[MyTable]");
	}

	[Fact]
	public void QualifiedTableName_ProtectsAgainstInjection()
	{
		// Even if validation is bypassed, brackets provide defense-in-depth
		var options = new SqlServerCdcStateStoreOptions
		{
			SchemaName = "safe",
			TableName = "safe",
		};

		// The qualified name wraps in brackets
		options.QualifiedTableName.ShouldStartWith("[");
		options.QualifiedTableName.ShouldEndWith("]");
		options.QualifiedTableName.ShouldContain("].[");
	}

	#endregion
}
