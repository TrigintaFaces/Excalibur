// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	public IMongoDbCdcBuilder WithStateStore(Action<ICdcStateStoreBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
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
