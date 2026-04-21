// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Firestore;

namespace Excalibur.Data.Firestore;

/// <summary>
/// Fluent builder for configuring Firestore data access settings.
/// </summary>
public interface IFirestoreDataBuilder
{
	/// <summary>Sets the Google Cloud project ID.</summary>
	IFirestoreDataBuilder ProjectId(string projectId);

	/// <summary>Sets the path to the service account JSON credentials file.</summary>
	IFirestoreDataBuilder CredentialsPath(string credentialsPath);

	/// <summary>Sets the JSON content of the service account credentials.</summary>
	IFirestoreDataBuilder CredentialsJson(string credentialsJson);

	/// <summary>Sets the Firestore emulator host for local development (e.g., "localhost:8080").</summary>
	IFirestoreDataBuilder EmulatorHost(string emulatorHost);

	/// <summary>Sets a pre-configured <see cref="FirestoreDb"/> client.</summary>
	IFirestoreDataBuilder Client(FirestoreDb client);

	/// <summary>Sets a factory that resolves a <see cref="FirestoreDb"/> from DI.</summary>
	IFirestoreDataBuilder ClientFactory(Func<IServiceProvider, FirestoreDb> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IFirestoreDataBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the default collection name.</summary>
	IFirestoreDataBuilder CollectionName(string collectionName);
}
