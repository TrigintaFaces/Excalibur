// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Stores;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class PostgresComplianceStoreShould
{
	private readonly ILogger<PostgresComplianceStore> _logger = NullLogger<PostgresComplianceStore>.Instance;

	[Fact]
	public void ThrowWhenConnectionFactoryIsNull()
	{
		// Arrange
		var options = new PostgresComplianceOptions();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new PostgresComplianceStore(
				(Func<NpgsqlConnection>)null!,
				options,
				_logger));
	}

	[Fact]
	public void ThrowWhenOptionsIsNullInFactoryConstructor()
	{
		// Arrange
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new PostgresComplianceStore(
				factory,
				(PostgresComplianceOptions?)null,
				_logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNullInFactoryConstructor()
	{
		// Arrange
		var options = new PostgresComplianceOptions();
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new PostgresComplianceStore(
				factory,
				options,
				null!));
	}

	[Fact]
	public void ThrowWhenOptionsIsNullInIOptionsConstructor()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new PostgresComplianceStore(
				(IOptions<PostgresComplianceOptions>)null!,
				_logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNullInIOptionsConstructor()
	{
		// Arrange
		var options = MsOptions.Create(new PostgresComplianceOptions { ConnectionString = "Host=localhost" });

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new PostgresComplianceStore(options, null!));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsNullInIOptionsConstructor()
	{
		// Arrange
		var options = MsOptions.Create(new PostgresComplianceOptions { ConnectionString = null });

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new PostgresComplianceStore(options, _logger));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsEmptyInIOptionsConstructor()
	{
		// Arrange
		var options = MsOptions.Create(new PostgresComplianceOptions { ConnectionString = "  " });

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new PostgresComplianceStore(options, _logger));
	}

	[Theory]
	[InlineData("DROP TABLE; --")]
	[InlineData("schema.name")]
	[InlineData("table-name")]
	[InlineData("name with spaces")]
	[InlineData("name'injection")]
	[InlineData("name;delete")]
	public void ThrowWhenSchemaNameContainsInvalidCharacters(string invalidSchema)
	{
		// Arrange
		var options = new PostgresComplianceOptions { SchemaName = invalidSchema };
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new PostgresComplianceStore(factory, options, _logger));
	}

	[Theory]
	[InlineData("DROP TABLE; --")]
	[InlineData("prefix.name")]
	[InlineData("prefix-name")]
	[InlineData("prefix injection")]
	public void ThrowWhenTablePrefixContainsInvalidCharacters(string invalidPrefix)
	{
		// Arrange
		var options = new PostgresComplianceOptions { TablePrefix = invalidPrefix };
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new PostgresComplianceStore(factory, options, _logger));
	}

	[Theory]
	[InlineData("compliance")]
	[InlineData("my_schema")]
	[InlineData("Schema123")]
	[InlineData("_leading")]
	public void AcceptValidSchemaNames(string validSchema)
	{
		// Arrange
		var options = new PostgresComplianceOptions { SchemaName = validSchema };
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();

		// Act -- should not throw
		var store = new PostgresComplianceStore(factory, options, _logger);

		// Assert
		store.ShouldNotBeNull();
	}

	[Theory]
	[InlineData("dispatch_")]
	[InlineData("app_")]
	[InlineData("prefix123_")]
	[InlineData("_")]
	public void AcceptValidTablePrefixes(string validPrefix)
	{
		// Arrange
		var options = new PostgresComplianceOptions { TablePrefix = validPrefix };
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();

		// Act -- should not throw
		var store = new PostgresComplianceStore(factory, options, _logger);

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowWhenStoreConsentRecordIsNull()
	{
		// Arrange
		var options = new PostgresComplianceOptions();
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();
		var store = new PostgresComplianceStore(factory, options, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => store.StoreConsentAsync(null!, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ThrowWhenGetConsentSubjectIdIsNullOrWhitespace(string? subjectId)
	{
		// Arrange
		var options = new PostgresComplianceOptions();
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();
		var store = new PostgresComplianceStore(factory, options, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetConsentAsync(subjectId!, "purpose", CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ThrowWhenGetConsentPurposeIsNullOrWhitespace(string? purpose)
	{
		// Arrange
		var options = new PostgresComplianceOptions();
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();
		var store = new PostgresComplianceStore(factory, options, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetConsentAsync("subject-1", purpose!, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ThrowWhenStoreErasureLogSubjectIdIsNullOrWhitespace(string? subjectId)
	{
		// Arrange
		var options = new PostgresComplianceOptions();
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();
		var store = new PostgresComplianceStore(factory, options, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.StoreErasureLogAsync(subjectId!, "details", DateTimeOffset.UtcNow, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenStoreSubjectAccessRequestResultIsNull()
	{
		// Arrange
		var options = new PostgresComplianceOptions();
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();
		var store = new PostgresComplianceStore(factory, options, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => store.StoreSubjectAccessRequestAsync(null!, CancellationToken.None));
	}
}
