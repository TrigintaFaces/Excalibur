// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Firestore;

namespace Excalibur.Data.Firestore;

internal sealed class FirestoreDataBuilder : IFirestoreDataBuilder
{
	private readonly FirestoreOptions _options;

	internal FirestoreDataBuilder(FirestoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal FirestoreDb? ClientInstance { get; private set; }
	internal Func<IServiceProvider, FirestoreDb>? ClientFactoryFunc { get; private set; }
	internal string? BindConfigurationPath { get; private set; }
	internal string? ProjectIdValue { get; private set; }
	internal string? EmulatorHostValue { get; private set; }
	internal string? CredentialsPathValue { get; private set; }
	internal string? CredentialsJsonValue { get; private set; }
	internal string? CollectionNameValue { get; private set; }

	public IFirestoreDataBuilder ProjectId(string projectId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
		ProjectIdValue = projectId;
		_options.ProjectId = projectId;
		// ProjectId is ADDITIVE — do not clear other connection values
		return this;
	}

	public IFirestoreDataBuilder CredentialsPath(string credentialsPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(credentialsPath);
		CredentialsPathValue = credentialsPath;
		_options.CredentialsPath = credentialsPath;
		CredentialsJsonValue = null;
		_options.CredentialsJson = null;
		return this;
	}

	public IFirestoreDataBuilder CredentialsJson(string credentialsJson)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(credentialsJson);
		CredentialsJsonValue = credentialsJson;
		_options.CredentialsJson = credentialsJson;
		CredentialsPathValue = null;
		_options.CredentialsPath = null;
		return this;
	}

	public IFirestoreDataBuilder EmulatorHost(string emulatorHost)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(emulatorHost);
		EmulatorHostValue = emulatorHost;
		_options.EmulatorHost = emulatorHost;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		CredentialsPathValue = null;
		CredentialsJsonValue = null;
		_options.CredentialsPath = null;
		_options.CredentialsJson = null;
		return this;
	}

	public IFirestoreDataBuilder Client(FirestoreDb client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ClientInstance = client;
		ClientFactoryFunc = null;
		EmulatorHostValue = null;
		BindConfigurationPath = null;
		_options.EmulatorHost = null;
		return this;
	}

	public IFirestoreDataBuilder ClientFactory(Func<IServiceProvider, FirestoreDb> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		ClientFactoryFunc = clientFactory;
		ClientInstance = null;
		EmulatorHostValue = null;
		BindConfigurationPath = null;
		_options.EmulatorHost = null;
		return this;
	}

	public IFirestoreDataBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		ClientInstance = null;
		ClientFactoryFunc = null;
		EmulatorHostValue = null;
		_options.EmulatorHost = null;
		return this;
	}

	public IFirestoreDataBuilder CollectionName(string collectionName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
		CollectionNameValue = collectionName;
		_options.DefaultCollection = collectionName;
		return this;
	}
}
