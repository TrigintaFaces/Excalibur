// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Reflection;

using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Extensions.Options;

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="SqlServerCdcStateStoreOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServerCdcStateStoreOptions")]
public sealed class SqlServerCdcStateStoreOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var options = new SqlServerCdcStateStoreOptions();

		// Assert
		options.SchemaName.ShouldBe("Cdc");
		options.TableName.ShouldBe("CdcProcessingState");
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaults()
	{
		// Arrange
		var options = new SqlServerCdcStateStoreOptions();

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
		var options = new SqlServerCdcStateStoreOptions
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
		var options = new SqlServerCdcStateStoreOptions
		{
			TableName = tableName!,
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("TableName");
	}

	[Fact]
	public void AddCdcProcessor_RegistersStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(Array.Empty<Assembly>());
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
		options.Value.SchemaName.ShouldBe("Cdc");
		options.Value.TableName.ShouldBe("CdcProcessingState");
	}

	[Fact]
	public void AddCdcProcessor_WithInvalidConfiguration_ThrowsOptionsValidationException()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.Configure<SqlServerCdcStateStoreOptions>(options => options.TableName = " ");

		// Act
		_ = services.AddCdcProcessor(Array.Empty<Assembly>());
		using var provider = services.BuildServiceProvider();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() =>
				provider.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>().Value);
	}
}
