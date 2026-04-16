// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Storage.V1;

namespace Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage;

internal sealed class ClaimCheckGcsBuilder : IClaimCheckGcsBuilder
{
	internal string? ProjectIdValue { get; private set; }
	internal string? BucketNameValue { get; private set; }
	internal string? PrefixValue { get; private set; }
	internal string? CredentialsPathValue { get; private set; }
	internal string? CredentialsJsonValue { get; private set; }
	internal StorageClient? ClientInstance { get; private set; }
	internal Func<IServiceProvider, StorageClient>? ClientFactoryFunc { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	public IClaimCheckGcsBuilder ProjectId(string projectId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
		ProjectIdValue = projectId;
		return this;
	}

	public IClaimCheckGcsBuilder BucketName(string bucketName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
		BucketNameValue = bucketName;
		return this;
	}

	public IClaimCheckGcsBuilder Prefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		PrefixValue = prefix;
		return this;
	}

	public IClaimCheckGcsBuilder CredentialsPath(string credentialsPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(credentialsPath);
		CredentialsPathValue = credentialsPath;
		CredentialsJsonValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IClaimCheckGcsBuilder CredentialsJson(string credentialsJson)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(credentialsJson);
		CredentialsJsonValue = credentialsJson;
		CredentialsPathValue = null;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IClaimCheckGcsBuilder Client(StorageClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ClientInstance = client;
		ClientFactoryFunc = null;
		CredentialsPathValue = null;
		CredentialsJsonValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IClaimCheckGcsBuilder ClientFactory(Func<IServiceProvider, StorageClient> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		ClientFactoryFunc = clientFactory;
		ClientInstance = null;
		CredentialsPathValue = null;
		CredentialsJsonValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IClaimCheckGcsBuilder BindConfiguration(string sectionPath)
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
