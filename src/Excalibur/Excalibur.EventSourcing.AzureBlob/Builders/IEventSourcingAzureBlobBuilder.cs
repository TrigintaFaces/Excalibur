// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Storage.Blobs;

namespace Excalibur.EventSourcing.AzureBlob;

/// <summary>
/// Fluent builder for configuring Azure Blob Storage cold event store settings.
/// </summary>
/// <remarks>
/// <para>
/// Connection methods (<see cref="ConnectionString"/>,
/// <see cref="Client(BlobServiceClient)"/>, <see cref="ClientFactory"/>,
/// <see cref="BindConfiguration"/>) use last-wins semantics: setting one
/// clears the others.
/// </para>
/// <para>
/// Non-connection methods (<see cref="ContainerName"/>,
/// <see cref="CreateContainerIfNotExists"/>) are additive and can be combined
/// with any connection method.
/// </para>
/// </remarks>
public interface IEventSourcingAzureBlobBuilder
{
	/// <summary>Sets the Azure Blob Storage connection string.</summary>
	IEventSourcingAzureBlobBuilder ConnectionString(string connectionString);

	/// <summary>Sets the container name for cold event storage.</summary>
	IEventSourcingAzureBlobBuilder ContainerName(string containerName);

	/// <summary>Sets whether to create the container if it does not exist. Default is <see langword="true"/>.</summary>
	IEventSourcingAzureBlobBuilder CreateContainerIfNotExists(bool create = true);

	/// <summary>Sets a pre-configured <see cref="BlobServiceClient"/>.</summary>
	IEventSourcingAzureBlobBuilder Client(BlobServiceClient client);

	/// <summary>Sets a factory that resolves a <see cref="BlobServiceClient"/> from DI.</summary>
	IEventSourcingAzureBlobBuilder ClientFactory(Func<IServiceProvider, BlobServiceClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IEventSourcingAzureBlobBuilder BindConfiguration(string sectionPath);
}
