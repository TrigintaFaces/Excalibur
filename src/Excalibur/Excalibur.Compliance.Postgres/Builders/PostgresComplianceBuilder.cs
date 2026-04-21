// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Postgres.Erasure;

using Npgsql;

namespace Excalibur.Compliance.Postgres;

/// <summary>
/// Internal implementation of the Postgres compliance builder.
/// Connection overloads use last-wins semantics.
/// Sets connection on all sub-store options (Erasure, DataInventory, LegalHold).
/// </summary>
internal sealed class PostgresComplianceBuilder : IPostgresComplianceBuilder
{
	private readonly PostgresErasureStoreOptions _erasureOptions;
	private readonly PostgresDataInventoryStoreOptions _inventoryOptions;
	private readonly PostgresLegalHoldStoreOptions _legalHoldOptions;

	internal PostgresComplianceBuilder(
		PostgresErasureStoreOptions erasureOptions,
		PostgresDataInventoryStoreOptions inventoryOptions,
		PostgresLegalHoldStoreOptions legalHoldOptions)
	{
		_erasureOptions = erasureOptions ?? throw new ArgumentNullException(nameof(erasureOptions));
		_inventoryOptions = inventoryOptions ?? throw new ArgumentNullException(nameof(inventoryOptions));
		_legalHoldOptions = legalHoldOptions ?? throw new ArgumentNullException(nameof(legalHoldOptions));
	}

	internal Func<IServiceProvider, NpgsqlDataSource>? DataSourceFactoryFunc { get; private set; }
	internal NpgsqlDataSource? DataSourceInstance { get; private set; }
	internal string? ConnectionStringNameValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	public IPostgresComplianceBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_erasureOptions.ConnectionString = connectionString;
		_inventoryOptions.ConnectionString = connectionString;
		_legalHoldOptions.ConnectionString = connectionString;
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresComplianceBuilder DataSourceFactory(Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory)
	{
		ArgumentNullException.ThrowIfNull(dataSourceFactory);
		DataSourceFactoryFunc = dataSourceFactory;
		ClearConnectionStrings();
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresComplianceBuilder DataSource(NpgsqlDataSource dataSource)
	{
		ArgumentNullException.ThrowIfNull(dataSource);
		DataSourceInstance = dataSource;
		ClearConnectionStrings();
		DataSourceFactoryFunc = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresComplianceBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ConnectionStringNameValue = name;
		ClearConnectionStrings();
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresComplianceBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		ClearConnectionStrings();
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		return this;
	}

	private void ClearConnectionStrings()
	{
		_erasureOptions.ConnectionString = null!;
		_inventoryOptions.ConnectionString = null!;
		_legalHoldOptions.ConnectionString = null!;
	}
}
