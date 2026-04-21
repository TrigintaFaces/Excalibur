// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;

namespace Excalibur.Outbox.CosmosDb;

/// <summary>
/// Fluent builder for configuring CosmosDb outbox settings.
/// </summary>
public interface ICosmosDbOutboxBuilder
{
	/// <summary>Sets the CosmosDb connection string.</summary>
	ICosmosDbOutboxBuilder ConnectionString(string connectionString);

	/// <summary>Sets the CosmosDb endpoint and auth key.</summary>
	ICosmosDbOutboxBuilder Endpoint(string endpoint, string authKey);

	/// <summary>Sets a pre-configured <see cref="CosmosClient"/> instance.</summary>
	ICosmosDbOutboxBuilder Client(CosmosClient client);

	/// <summary>Sets a factory that resolves a <see cref="CosmosClient"/> from DI.</summary>
	ICosmosDbOutboxBuilder ClientFactory(Func<IServiceProvider, CosmosClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	ICosmosDbOutboxBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the database name.</summary>
	ICosmosDbOutboxBuilder DatabaseName(string databaseName);

	/// <summary>Sets the container name.</summary>
	ICosmosDbOutboxBuilder ContainerName(string containerName);

}
