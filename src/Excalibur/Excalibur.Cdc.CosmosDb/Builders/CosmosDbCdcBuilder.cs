// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Internal implementation of the CosmosDB CDC builder.
/// </summary>
internal sealed class CosmosDbCdcBuilder : ICosmosDbCdcBuilder
{
	private readonly CosmosDbCdcOptions _options;

	internal CosmosDbCdcBuilder(CosmosDbCdcOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>Gets the state store configure callback.</summary>
	internal Action<ICdcStateStoreBuilder>? StateStoreConfigure { get; private set; }

	/// <summary>Gets the source BindConfiguration section path.</summary>
	internal string? SourceBindConfigurationPath { get; private set; }

	/// <inheritdoc/>
	public ICosmosDbCdcBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_options.ConnectionString = connectionString;
		return this;
	}

	/// <inheritdoc/>
	public ICosmosDbCdcBuilder DatabaseId(string databaseId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseId);
		_options.DatabaseId = databaseId;
		return this;
	}

	/// <inheritdoc/>
	public ICosmosDbCdcBuilder ContainerId(string containerId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
		_options.ContainerId = containerId;
		return this;
	}

	/// <inheritdoc/>
	public ICosmosDbCdcBuilder ProcessorName(string processorName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		_options.ProcessorName = processorName;
		return this;
	}

	/// <inheritdoc/>
	public ICosmosDbCdcBuilder ChangeFeed(Action<CosmosDbChangeFeedOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options.ChangeFeed);
		return this;
	}

	/// <inheritdoc/>
	public ICosmosDbCdcBuilder WithStateStore(Action<ICdcStateStoreBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		StateStoreConfigure = configure;
		return this;
	}

	/// <inheritdoc/>
	public ICosmosDbCdcBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		SourceBindConfigurationPath = sectionPath;
		return this;
	}
}
