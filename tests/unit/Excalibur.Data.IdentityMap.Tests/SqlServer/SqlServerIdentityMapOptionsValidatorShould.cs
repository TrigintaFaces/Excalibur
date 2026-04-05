// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.IdentityMap.SqlServer;

namespace Excalibur.Data.IdentityMap.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerIdentityMapOptionsValidator" />.
/// Validates SQL injection protection via SafeIdentifierRegex and cross-property constraints.
/// </summary>
[Trait("Component", "IdentityMap")]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class SqlServerIdentityMapOptionsValidatorShould
{
	private readonly SqlServerIdentityMapOptionsValidator _sut = new();

	#region Valid Options

	[Fact]
	public void Succeed_WhenAllOptionsAreValid()
	{
		var options = CreateValidOptions();

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData("dbo")]
	[InlineData("MySchema")]
	[InlineData("schema_1")]
	[InlineData("A")]
	[InlineData("abc123")]
	[InlineData("_underscore")]
	public void Succeed_WhenSchemaNameContainsOnlySafeCharacters(string schemaName)
	{
		var options = CreateValidOptions();
		options.SchemaName = schemaName;

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData("IdentityMap")]
	[InlineData("identity_map")]
	[InlineData("Table1")]
	[InlineData("_t")]
	public void Succeed_WhenTableNameContainsOnlySafeCharacters(string tableName)
	{
		var options = CreateValidOptions();
		options.TableName = tableName;

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region ConnectionString Validation

	[Fact]
	public void Fail_WhenConnectionStringIsNull()
	{
		var options = CreateValidOptions();
		options.ConnectionString = null;

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Fail_WhenConnectionStringIsEmpty()
	{
		var options = CreateValidOptions();
		options.ConnectionString = "";

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Fail_WhenConnectionStringIsWhitespace()
	{
		var options = CreateValidOptions();
		options.ConnectionString = "   ";

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ConnectionString");
	}

	#endregion

	#region SchemaName Validation -- SQL Injection Protection

	[Fact]
	public void Fail_WhenSchemaNameIsNull()
	{
		var options = CreateValidOptions();
		options.SchemaName = null!;

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
	}

	[Fact]
	public void Fail_WhenSchemaNameIsEmpty()
	{
		var options = CreateValidOptions();
		options.SchemaName = "";

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
	}

	[Theory]
	[InlineData("dbo; DROP TABLE --")]
	[InlineData("schema.name")]
	[InlineData("schema name")]
	[InlineData("[dbo]")]
	[InlineData("schema'injection")]
	[InlineData("schema\"injection")]
	[InlineData("a-b")]
	[InlineData("schema@name")]
	[InlineData("schema#name")]
	public void Fail_WhenSchemaNameContainsUnsafeCharacters(string schemaName)
	{
		var options = CreateValidOptions();
		options.SchemaName = schemaName;

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
		result.FailureMessage.ShouldContain("invalid characters");
	}

	#endregion

	#region TableName Validation -- SQL Injection Protection

	[Fact]
	public void Fail_WhenTableNameIsNull()
	{
		var options = CreateValidOptions();
		options.TableName = null!;

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	[Fact]
	public void Fail_WhenTableNameIsEmpty()
	{
		var options = CreateValidOptions();
		options.TableName = "";

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	[Theory]
	[InlineData("Table; DROP TABLE --")]
	[InlineData("table.name")]
	[InlineData("table name")]
	[InlineData("[Table]")]
	[InlineData("table'injection")]
	[InlineData("table\"injection")]
	[InlineData("a-b")]
	public void Fail_WhenTableNameContainsUnsafeCharacters(string tableName)
	{
		var options = CreateValidOptions();
		options.TableName = tableName;

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
		result.FailureMessage.ShouldContain("invalid characters");
	}

	#endregion

	#region CommandTimeoutSeconds Validation

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void Fail_WhenCommandTimeoutSecondsIsNotPositive(int timeout)
	{
		var options = CreateValidOptions();
		options.CommandTimeoutSeconds = timeout;

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CommandTimeoutSeconds");
	}

	#endregion

	#region MaxBatchSize Validation

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void Fail_WhenMaxBatchSizeIsNotPositive(int batchSize)
	{
		var options = CreateValidOptions();
		options.MaxBatchSize = batchSize;

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxBatchSize");
	}

	#endregion

	#region Multiple Failures

	[Fact]
	public void ReportAllFailures_WhenMultipleOptionsAreInvalid()
	{
		var options = new SqlServerIdentityMapOptions
		{
			ConnectionString = null,
			SchemaName = "",
			TableName = "",
			CommandTimeoutSeconds = 0,
			MaxBatchSize = 0,
		};

		var result = _sut.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ConnectionString");
		result.FailureMessage.ShouldContain("SchemaName");
		result.FailureMessage.ShouldContain("TableName");
		result.FailureMessage.ShouldContain("CommandTimeoutSeconds");
		result.FailureMessage.ShouldContain("MaxBatchSize");
	}

	#endregion

	private static SqlServerIdentityMapOptions CreateValidOptions() => new()
	{
		ConnectionString = "Server=.;Database=TestDb;Trusted_Connection=True;",
		SchemaName = "dbo",
		TableName = "IdentityMap",
		CommandTimeoutSeconds = 30,
		MaxBatchSize = 100,
	};
}
