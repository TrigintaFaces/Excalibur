// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Firestore;

namespace Excalibur.Saga.Firestore;

/// <summary>
/// Fluent builder for configuring Firestore saga store settings.
/// </summary>
public interface IFirestoreSagaBuilder
{
	/// <summary>Sets the Google Cloud project ID.</summary>
	IFirestoreSagaBuilder ProjectId(string projectId);

	/// <summary>Sets the path to the service account JSON credentials file.</summary>
	IFirestoreSagaBuilder CredentialsPath(string credentialsPath);

	/// <summary>Sets the JSON content of the service account credentials.</summary>
	IFirestoreSagaBuilder CredentialsJson(string credentialsJson);

	/// <summary>Sets the Firestore emulator host for local development (e.g., "localhost:8080").</summary>
	IFirestoreSagaBuilder EmulatorHost(string emulatorHost);

	/// <summary>Sets a pre-configured <see cref="FirestoreDb"/> client.</summary>
	IFirestoreSagaBuilder Client(FirestoreDb client);

	/// <summary>Sets a factory that resolves a <see cref="FirestoreDb"/> from DI.</summary>
	IFirestoreSagaBuilder ClientFactory(Func<IServiceProvider, FirestoreDb> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IFirestoreSagaBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the sagas collection name.</summary>
	IFirestoreSagaBuilder CollectionName(string collectionName);
}
