// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Storage.Blobs;

namespace Excalibur.EventSourcing.AzureBlob;

internal sealed class EventSourcingAzureBlobBuilder : IEventSourcingAzureBlobBuilder
{
	internal string? ConnectionStringValue { get; private set; }
	internal string? ContainerNameValue { get; private set; }
	internal bool? CreateContainerIfNotExistsValue { get; private set; }
	internal BlobServiceClient? ClientInstance { get; private set; }
	internal Func<IServiceProvider, BlobServiceClient>? ClientFactoryFunc { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	public IEventSourcingAzureBlobBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ConnectionStringValue = connectionString;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingAzureBlobBuilder ContainerName(string containerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
		ContainerNameValue = containerName;
		return this;
	}

	public IEventSourcingAzureBlobBuilder CreateContainerIfNotExists(bool create = true)
	{
		CreateContainerIfNotExistsValue = create;
		return this;
	}

	public IEventSourcingAzureBlobBuilder Client(BlobServiceClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ClientInstance = client;
		ClientFactoryFunc = null;
		ConnectionStringValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingAzureBlobBuilder ClientFactory(Func<IServiceProvider, BlobServiceClient> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		ClientFactoryFunc = clientFactory;
		ClientInstance = null;
		ConnectionStringValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingAzureBlobBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		ClientInstance = null;
		ClientFactoryFunc = null;
		ConnectionStringValue = null;
		return this;
	}
}
