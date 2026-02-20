// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerSagaStoreOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServerSagaStoreOptions")]
public sealed class SqlServerSagaStoreOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var options = new SqlServerSagaStoreOptions();

		// Assert
		options.SchemaName.ShouldBe("dispatch");
		options.TableName.ShouldBe("sagas");
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaults()
	{
		// Arrange
		var options = new SqlServerSagaStoreOptions();

		// Act & Assert
		options.Validate();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenSchemaNameIsInvalid(string? schemaName)
	{
		// Arrange
		var options = new SqlServerSagaStoreOptions
		{
			SchemaName = schemaName!,
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("SchemaName");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenTableNameIsInvalid(string? tableName)
	{
		// Arrange
		var options = new SqlServerSagaStoreOptions
		{
			TableName = tableName!,
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("TableName");
	}

	[Fact]
	public void AddSqlServerSagaStore_RegistersOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddSqlServerSagaStore("Server=.;Database=Dispatch;Trusted_Connection=True;");
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
		options.Value.SchemaName.ShouldBe("dispatch");
		options.Value.TableName.ShouldBe("sagas");
	}

	[Fact]
	public void AddSqlServerSagaStore_WithInvalidConfiguration_ThrowsOptionsValidationException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddSqlServerSagaStore(
				"Server=.;Database=Dispatch;Trusted_Connection=True;",
				options => options.TableName = " ");
		using var provider = services.BuildServiceProvider();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() =>
				provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>().Value);
	}
}
