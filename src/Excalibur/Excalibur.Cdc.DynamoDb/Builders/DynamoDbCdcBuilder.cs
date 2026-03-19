// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

namespace Excalibur.Cdc.DynamoDb;

/// <summary>
/// Internal implementation of the DynamoDB CDC builder.
/// </summary>
internal sealed class DynamoDbCdcBuilder : IDynamoDbCdcBuilder
{
	private readonly DynamoDbCdcOptions _options;

	internal DynamoDbCdcBuilder(DynamoDbCdcOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>Gets the state client factory if set via <see cref="WithStateStore(Func{IServiceProvider, IAmazonDynamoDB})"/>.</summary>
	internal Func<IServiceProvider, IAmazonDynamoDB>? StateClientFactory { get; private set; }

	/// <summary>Gets the state store configure callback.</summary>
	internal Action<ICdcStateStoreBuilder>? StateStoreConfigure { get; private set; }

	/// <summary>Gets the source BindConfiguration section path.</summary>
	internal string? SourceBindConfigurationPath { get; private set; }

	/// <inheritdoc/>
	public IDynamoDbCdcBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.TableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public IDynamoDbCdcBuilder StreamArn(string streamArn)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(streamArn);
		_options.StreamArn = streamArn;
		return this;
	}

	/// <inheritdoc/>
	public IDynamoDbCdcBuilder ProcessorName(string processorName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		_options.ProcessorName = processorName;
		return this;
	}

	/// <inheritdoc/>
	public IDynamoDbCdcBuilder MaxBatchSize(int maxBatchSize)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBatchSize);
		_options.MaxBatchSize = maxBatchSize;
		return this;
	}

	/// <inheritdoc/>
	public IDynamoDbCdcBuilder PollInterval(TimeSpan interval)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
		_options.PollInterval = interval;
		return this;
	}

	/// <inheritdoc/>
	public IDynamoDbCdcBuilder WithStateStore(Func<IServiceProvider, IAmazonDynamoDB> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		StateClientFactory = clientFactory;
		return this;
	}

	/// <inheritdoc/>
	public IDynamoDbCdcBuilder WithStateStore(
		Func<IServiceProvider, IAmazonDynamoDB> clientFactory,
		Action<ICdcStateStoreBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configure);
		StateClientFactory = clientFactory;
		StateStoreConfigure = configure;
		return this;
	}

	/// <inheritdoc/>
	public IDynamoDbCdcBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		SourceBindConfigurationPath = sectionPath;
		return this;
	}
}
