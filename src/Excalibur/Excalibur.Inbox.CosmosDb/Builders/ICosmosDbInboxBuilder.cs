// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;

namespace Excalibur.Inbox.CosmosDb;

/// <summary>
/// Fluent builder for configuring CosmosDb inbox settings.
/// </summary>
public interface ICosmosDbInboxBuilder
{
	/// <summary>Sets the CosmosDb connection string.</summary>
	ICosmosDbInboxBuilder ConnectionString(string connectionString);

	/// <summary>Sets the CosmosDb endpoint and auth key.</summary>
	ICosmosDbInboxBuilder Endpoint(string endpoint, string authKey);

	/// <summary>Sets a pre-configured <see cref="CosmosClient"/> instance.</summary>
	ICosmosDbInboxBuilder Client(CosmosClient client);

	/// <summary>Sets a factory that resolves a <see cref="CosmosClient"/> from DI.</summary>
	ICosmosDbInboxBuilder ClientFactory(Func<IServiceProvider, CosmosClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	ICosmosDbInboxBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the database name.</summary>
	ICosmosDbInboxBuilder DatabaseName(string databaseName);

	/// <summary>Sets the container name.</summary>
	ICosmosDbInboxBuilder ContainerName(string containerName);

}
