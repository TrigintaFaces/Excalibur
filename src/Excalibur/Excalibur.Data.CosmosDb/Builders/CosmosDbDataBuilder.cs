// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb;

internal sealed class CosmosDbDataBuilder : ICosmosDbDataBuilder
{
	private readonly CosmosDbOptions _options;

	internal CosmosDbDataBuilder(CosmosDbOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal CosmosClient? ClientInstance { get; private set; }
	internal Func<IServiceProvider, CosmosClient>? ClientFactoryFunc { get; private set; }
	internal string? EndpointValue { get; private set; }
	internal string? AuthKeyValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }
	internal string? ConnectionStringValue { get; private set; }

	public ICosmosDbDataBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ConnectionStringValue = connectionString;
		ClientInstance = null; ClientFactoryFunc = null; EndpointValue = null; AuthKeyValue = null; BindConfigurationPath = null;
		return this;
	}

	public ICosmosDbDataBuilder Endpoint(string endpoint, string authKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
		ArgumentException.ThrowIfNullOrWhiteSpace(authKey);
		EndpointValue = endpoint; AuthKeyValue = authKey;
		ConnectionStringValue = null; ClientInstance = null; ClientFactoryFunc = null; BindConfigurationPath = null;
		return this;
	}

	public ICosmosDbDataBuilder Client(CosmosClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ClientInstance = client;
		ConnectionStringValue = null; ClientFactoryFunc = null; EndpointValue = null; AuthKeyValue = null; BindConfigurationPath = null;
		return this;
	}

	public ICosmosDbDataBuilder ClientFactory(Func<IServiceProvider, CosmosClient> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		ClientFactoryFunc = clientFactory;
		ConnectionStringValue = null; ClientInstance = null; EndpointValue = null; AuthKeyValue = null; BindConfigurationPath = null;
		return this;
	}

	public ICosmosDbDataBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		ConnectionStringValue = null; ClientInstance = null; ClientFactoryFunc = null; EndpointValue = null; AuthKeyValue = null;
		return this;
	}

	public ICosmosDbDataBuilder DatabaseName(string databaseName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
		_options.DatabaseName = databaseName;
		return this;
	}

	public ICosmosDbDataBuilder ContainerName(string containerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
		_options.DefaultContainerName = containerName;
		return this;
	}

}
