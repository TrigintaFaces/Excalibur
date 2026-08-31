// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using MsOptions = Microsoft.Extensions.Options.Options;

using Excalibur.Compliance.Stores.Postgres;

using Excalibur.Compliance;namespace Excalibur.Compliance.Tests.Stores;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class PostgresComplianceStoreShould
{
	private readonly ILogger<PostgresComplianceStore> _logger = NullLogger<PostgresComplianceStore>.Instance;

	/// <summary>
	/// A single-tenant host's context. The store now takes the context and the deployment mode as separate,
	/// required inputs, so these arms state both explicitly instead of letting an omitted argument decide.
	/// </summary>
	private sealed class SingleTenantTestContext : ITenantContext
	{
		public static readonly SingleTenantTestContext Instance = new();

		public string? TenantId => TenantDefaults.DefaultTenantId;

		public bool HasTenant => true;
	}

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
				SingleTenantTestContext.Instance,
				MsOptions.Create(new TenantContextOptions()),
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
				SingleTenantTestContext.Instance,
				MsOptions.Create(new TenantContextOptions()),
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
				SingleTenantTestContext.Instance,
				MsOptions.Create(new TenantContextOptions()),
				null!));
	}

	[Fact]
	public void ThrowWhenOptionsIsNullInIOptionsConstructor()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new PostgresComplianceStore(
				(IOptions<PostgresComplianceOptions>)null!,
				SingleTenantTestContext.Instance,
				MsOptions.Create(new TenantContextOptions()),
				_logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNullInIOptionsConstructor()
	{
		// Arrange
		var options = MsOptions.Create(new PostgresComplianceOptions { ConnectionString = "Host=localhost" });

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new PostgresComplianceStore(options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), null!));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsNullInIOptionsConstructor()
	{
		// Arrange
		var options = MsOptions.Create(new PostgresComplianceOptions { ConnectionString = null });

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new PostgresComplianceStore(options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsEmptyInIOptionsConstructor()
	{
		// Arrange
		var options = MsOptions.Create(new PostgresComplianceOptions { ConnectionString = "  " });

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new PostgresComplianceStore(options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger));
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
			() => new PostgresComplianceStore(factory, options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger));
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
			() => new PostgresComplianceStore(factory, options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger));
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
		var store = new PostgresComplianceStore(factory, options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger);

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
		var store = new PostgresComplianceStore(factory, options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger);

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowWhenStoreConsentRecordIsNull()
	{
		// Arrange
		var options = new PostgresComplianceOptions();
		Func<NpgsqlConnection> factory = () => new NpgsqlConnection();
		var store = new PostgresComplianceStore(factory, options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger);

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
		var store = new PostgresComplianceStore(factory, options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger);

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
		var store = new PostgresComplianceStore(factory, options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger);

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
		var store = new PostgresComplianceStore(factory, options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger);

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
		var store = new PostgresComplianceStore(factory, options, SingleTenantTestContext.Instance, MsOptions.Create(new TenantContextOptions()), _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => store.StoreSubjectAccessRequestAsync(null!, CancellationToken.None));
	}
}
