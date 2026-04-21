// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Firestore;

namespace Excalibur.Inbox.Firestore;

/// <summary>
/// Fluent builder for configuring Firestore inbox store settings.
/// </summary>
public interface IFirestoreInboxBuilder
{
	/// <summary>Sets the Google Cloud project ID.</summary>
	IFirestoreInboxBuilder ProjectId(string projectId);

	/// <summary>Sets the path to the service account JSON credentials file.</summary>
	IFirestoreInboxBuilder CredentialsPath(string credentialsPath);

	/// <summary>Sets the JSON content of the service account credentials.</summary>
	IFirestoreInboxBuilder CredentialsJson(string credentialsJson);

	/// <summary>Sets the Firestore emulator host for local development (e.g., "localhost:8080").</summary>
	IFirestoreInboxBuilder EmulatorHost(string emulatorHost);

	/// <summary>Sets a pre-configured <see cref="FirestoreDb"/> client.</summary>
	IFirestoreInboxBuilder Client(FirestoreDb client);

	/// <summary>Sets a factory that resolves a <see cref="FirestoreDb"/> from DI.</summary>
	IFirestoreInboxBuilder ClientFactory(Func<IServiceProvider, FirestoreDb> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IFirestoreInboxBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the inbox messages collection name.</summary>
	IFirestoreInboxBuilder CollectionName(string collectionName);
}
