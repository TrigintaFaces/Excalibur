// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;

namespace Excalibur.Saga.CosmosDb;

/// <summary>
/// Fluent builder for configuring CosmosDb saga settings.
/// </summary>
public interface ICosmosDbSagaBuilder
{
	/// <summary>Sets the CosmosDb connection string.</summary>
	ICosmosDbSagaBuilder ConnectionString(string connectionString);

	/// <summary>Sets the CosmosDb endpoint and auth key.</summary>
	ICosmosDbSagaBuilder Endpoint(string endpoint, string authKey);

	/// <summary>Sets a pre-configured <see cref="CosmosClient"/> instance.</summary>
	ICosmosDbSagaBuilder Client(CosmosClient client);

	/// <summary>Sets a factory that resolves a <see cref="CosmosClient"/> from DI.</summary>
	ICosmosDbSagaBuilder ClientFactory(Func<IServiceProvider, CosmosClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	ICosmosDbSagaBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the database name.</summary>
	ICosmosDbSagaBuilder DatabaseName(string databaseName);

	/// <summary>Sets the container name.</summary>
	ICosmosDbSagaBuilder ContainerName(string containerName);

}
