// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.SqlServer;

using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.SqlServer;

/// <summary>
/// Unit tests for SqlServerSagaIdempotencyOptionsValidator.
/// </summary>
/// <remarks>
/// Sprint 623 (C.1): GeneratedRegex SQL identifier validation via SqlIdentifierValidator.
/// Validator is internal -- instantiated via reflection.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Excalibur")]
public sealed class SqlServerSagaIdempotencyOptionsValidatorShould
{
	private static readonly Type ValidatorType = typeof(SqlServerSagaIdempotencyOptions).Assembly
		.GetType("Excalibur.Saga.SqlServer.SqlServerSagaIdempotencyOptionsValidator")!;

	private readonly IValidateOptions<SqlServerSagaIdempotencyOptions> _validator;

	public SqlServerSagaIdempotencyOptionsValidatorShould()
	{
		ValidatorType.ShouldNotBeNull("SqlServerSagaIdempotencyOptionsValidator type not found in assembly");
		_validator = (IValidateOptions<SqlServerSagaIdempotencyOptions>)Activator.CreateInstance(ValidatorType)!;
	}

	private static SqlServerSagaIdempotencyOptions CreateValidOptions() => new()
	{
		ConnectionString = "Server=.;Database=TestDb;Trusted_Connection=True;",
		SchemaName = "dispatch",
		TableName = "saga_idempotency",
		RetentionPeriod = TimeSpan.FromDays(7),
	};

	#region Valid Options

	[Fact]
	public void SucceedForValidDefaultOptions()
	{
		var options = CreateValidOptions();

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData("dbo")]
	[InlineData("dispatch")]
	[InlineData("My_Schema_123")]
	[InlineData("_underscore")]
	public void SucceedForValidSchemaNames(string schemaName)
	{
		var options = CreateValidOptions();
		options.SchemaName = schemaName;

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData("saga_idempotency")]
	[InlineData("IdempotencyKeys")]
	[InlineData("Table_123")]
	public void SucceedForValidTableNames(string tableName)
	{
		var options = CreateValidOptions();
		options.TableName = tableName;

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Null Options

	[Fact]
	public void FailWhenOptionsIsNull()
	{
		var result = _validator.Validate(null, null!);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("null");
	}

	#endregion

	#region SchemaName Validation

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void FailWhenSchemaNameIsEmpty(string schemaName)
	{
		var options = CreateValidOptions();
		options.SchemaName = schemaName;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
	}

	[Theory]
	[InlineData("dbo; DROP TABLE --")]
	[InlineData("schema name")]
	[InlineData("schema.name")]
	[InlineData("schema-name")]
	[InlineData("[dbo]")]
	[InlineData("schema'injection")]
	public void FailWhenSchemaNameContainsInvalidCharacters(string schemaName)
	{
		var options = CreateValidOptions();
		options.SchemaName = schemaName;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("invalid characters");
	}

	#endregion

	#region TableName Validation

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void FailWhenTableNameIsEmpty(string tableName)
	{
		var options = CreateValidOptions();
		options.TableName = tableName;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	[Theory]
	[InlineData("table; DROP TABLE --")]
	[InlineData("table name")]
	[InlineData("table.name")]
	[InlineData("table-name")]
	[InlineData("[table]")]
	[InlineData("table'injection")]
	public void FailWhenTableNameContainsInvalidCharacters(string tableName)
	{
		var options = CreateValidOptions();
		options.TableName = tableName;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("invalid characters");
	}

	#endregion

	#region RetentionPeriod Validation

	[Fact]
	public void FailWhenRetentionPeriodIsZero()
	{
		var options = CreateValidOptions();
		options.RetentionPeriod = TimeSpan.Zero;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RetentionPeriod");
	}

	[Fact]
	public void FailWhenRetentionPeriodIsNegative()
	{
		var options = CreateValidOptions();
		options.RetentionPeriod = TimeSpan.FromDays(-1);

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RetentionPeriod");
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeInternal()
	{
		ValidatorType.IsPublic.ShouldBeFalse();
	}

	[Fact]
	public void ImplementIValidateOptions()
	{
		typeof(IValidateOptions<SqlServerSagaIdempotencyOptions>)
			.IsAssignableFrom(ValidatorType)
			.ShouldBeTrue();
	}

	#endregion
}
