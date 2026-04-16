// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Storage.V1;

namespace Excalibur.EventSourcing.Gcs;

internal sealed class EventSourcingGcsBuilder : IEventSourcingGcsBuilder
{
	internal string? ProjectIdValue { get; private set; }
	internal string? BucketNameValue { get; private set; }
	internal string? ObjectPrefixValue { get; private set; }
	internal string? CredentialsPathValue { get; private set; }
	internal string? CredentialsJsonValue { get; private set; }
	internal StorageClient? ClientInstance { get; private set; }
	internal Func<IServiceProvider, StorageClient>? ClientFactoryFunc { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	public IEventSourcingGcsBuilder ProjectId(string projectId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
		ProjectIdValue = projectId;
		return this;
	}

	public IEventSourcingGcsBuilder BucketName(string bucketName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
		BucketNameValue = bucketName;
		return this;
	}

	public IEventSourcingGcsBuilder ObjectPrefix(string objectPrefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPrefix);
		ObjectPrefixValue = objectPrefix;
		return this;
	}

	public IEventSourcingGcsBuilder CredentialsPath(string credentialsPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(credentialsPath);
		CredentialsPathValue = credentialsPath;
		CredentialsJsonValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingGcsBuilder CredentialsJson(string credentialsJson)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(credentialsJson);
		CredentialsJsonValue = credentialsJson;
		CredentialsPathValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingGcsBuilder Client(StorageClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ClientInstance = client;
		ClientFactoryFunc = null;
		CredentialsPathValue = null;
		CredentialsJsonValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingGcsBuilder ClientFactory(Func<IServiceProvider, StorageClient> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		ClientFactoryFunc = clientFactory;
		ClientInstance = null;
		CredentialsPathValue = null;
		CredentialsJsonValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingGcsBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		ClientInstance = null;
		ClientFactoryFunc = null;
		CredentialsPathValue = null;
		CredentialsJsonValue = null;
		return this;
	}
}
