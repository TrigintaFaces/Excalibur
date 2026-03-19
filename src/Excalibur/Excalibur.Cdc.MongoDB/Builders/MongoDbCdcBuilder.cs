// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Internal implementation of the MongoDB CDC builder.
/// </summary>
internal sealed class MongoDbCdcBuilder : IMongoDbCdcBuilder
{
	private readonly MongoDbCdcOptions _options;

	internal MongoDbCdcBuilder(MongoDbCdcOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>Gets the state connection string if set via <see cref="WithStateStore(string)"/>.</summary>
	internal string? StateConnectionString { get; private set; }

	/// <summary>Gets the state client factory if set via <see cref="WithStateStore(Func{IServiceProvider, IMongoClient})"/>.</summary>
	internal Func<IServiceProvider, IMongoClient>? StateClientFactory { get; private set; }

	/// <summary>Gets the state store configure callback.</summary>
	internal Action<ICdcStateStoreBuilder>? StateStoreConfigure { get; private set; }

	/// <summary>Gets the source BindConfiguration section path.</summary>
	internal string? SourceBindConfigurationPath { get; private set; }

	/// <inheritdoc/>
	public IMongoDbCdcBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_options.Connection.ConnectionString = connectionString;
		return this;
	}

	/// <inheritdoc/>
	public IMongoDbCdcBuilder DatabaseName(string databaseName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
		_options.DatabaseName = databaseName;
		return this;
	}

	/// <inheritdoc/>
	public IMongoDbCdcBuilder CollectionNames(params string[] collectionNames)
	{
		ArgumentNullException.ThrowIfNull(collectionNames);
		_options.CollectionNames = collectionNames;
		return this;
	}

	/// <inheritdoc/>
	public IMongoDbCdcBuilder ProcessorId(string processorId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		_options.ProcessorId = processorId;
		return this;
	}

	/// <inheritdoc/>
	public IMongoDbCdcBuilder BatchSize(int batchSize)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
		_options.BatchSize = batchSize;
		return this;
	}

	/// <inheritdoc/>
	public IMongoDbCdcBuilder ReconnectInterval(TimeSpan interval)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
		_options.ReconnectInterval = interval;
		return this;
	}

	/// <inheritdoc/>
	public IMongoDbCdcBuilder WithStateStore(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		StateConnectionString = connectionString;
		return this;
	}

	/// <inheritdoc/>
	public IMongoDbCdcBuilder WithStateStore(string connectionString, Action<ICdcStateStoreBuilder> configure)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(configure);
		StateConnectionString = connectionString;
		StateStoreConfigure = configure;
		return this;
	}

	/// <inheritdoc/>
	public IMongoDbCdcBuilder WithStateStore(Func<IServiceProvider, IMongoClient> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		StateClientFactory = clientFactory;
		return this;
	}

	/// <inheritdoc/>
	public IMongoDbCdcBuilder WithStateStore(
		Func<IServiceProvider, IMongoClient> clientFactory,
		Action<ICdcStateStoreBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configure);
		StateClientFactory = clientFactory;
		StateStoreConfigure = configure;
		return this;
	}

	/// <inheritdoc/>
	public IMongoDbCdcBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		SourceBindConfigurationPath = sectionPath;
		return this;
	}
}
