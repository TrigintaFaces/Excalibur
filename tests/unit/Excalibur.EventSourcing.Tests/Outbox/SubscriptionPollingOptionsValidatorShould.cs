// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer.Outbox;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Outbox;

/// <summary>
/// Regression tests for <see cref="SubscriptionPollingOptionsValidator"/> (Sprint 686, T.1 ggjam).
/// Validates that SQL injection payloads in SchemaName/TableName are rejected at startup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SubscriptionPollingOptionsValidatorShould
{
	private readonly SubscriptionPollingOptionsValidator _validator = new();

	[Fact]
	public void Succeed_WithDefaultOptions()
	{
		// Arrange
		var options = new SubscriptionPollingOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_WithValidCustomIdentifiers()
	{
		// Arrange
		var options = new SubscriptionPollingOptions
		{
			SchemaName = "custom_schema",
			TableName = "my_outbox_table_2"
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData("dbo; DROP TABLE --")]
	[InlineData("schema' OR '1'='1")]
	[InlineData("test;DELETE FROM users")]
	[InlineData("schema name")]
	[InlineData("schema.name")]
	[InlineData("schema-name")]
	[InlineData("[dbo]")]
	public void Fail_WithInvalidSchemaName(string schemaName)
	{
		// Arrange
		var options = new SubscriptionPollingOptions
		{
			SchemaName = schemaName,
			TableName = "valid_table"
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
	}

	[Theory]
	[InlineData("table; DROP TABLE --")]
	[InlineData("table' OR '1'='1")]
	[InlineData("test;DELETE FROM users")]
	[InlineData("table name")]
	[InlineData("table.name")]
	[InlineData("[table]")]
	public void Fail_WithInvalidTableName(string tableName)
	{
		// Arrange
		var options = new SubscriptionPollingOptions
		{
			SchemaName = "dbo",
			TableName = tableName
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Fail_WithEmptyOrWhitespaceSchemaName(string? schemaName)
	{
		// Arrange
		var options = new SubscriptionPollingOptions
		{
			SchemaName = schemaName!,
			TableName = "valid_table"
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Fail_WithEmptyOrWhitespaceTableName(string? tableName)
	{
		// Arrange
		var options = new SubscriptionPollingOptions
		{
			SchemaName = "dbo",
			TableName = tableName!
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	[Fact]
	public void Fail_WithNullOptions()
	{
		// Act
		var result = _validator.Validate(null, null!);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void RegisteredViaServiceCollection_RejectsInvalidOptions()
	{
		// Arrange -- validate the DI wiring rejects SQL injection at startup
		var services = new ServiceCollection();
		services.AddOptions<SubscriptionPollingOptions>()
			.Configure(o =>
			{
				o.SchemaName = "dbo; DROP TABLE --";
				o.TableName = "valid_table";
			});
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SubscriptionPollingOptions>, SubscriptionPollingOptionsValidator>());

		var sp = services.BuildServiceProvider();
		var optionsMonitor = sp.GetRequiredService<IOptions<SubscriptionPollingOptions>>();

		// Act & Assert
		Should.Throw<OptionsValidationException>(() => _ = optionsMonitor.Value);
	}
}
