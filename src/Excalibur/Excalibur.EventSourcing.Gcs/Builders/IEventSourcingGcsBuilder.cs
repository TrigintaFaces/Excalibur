// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Storage.V1;

namespace Excalibur.EventSourcing.Gcs;

/// <summary>
/// Fluent builder for configuring Google Cloud Storage cold event store settings.
/// </summary>
/// <remarks>
/// <para>
/// Connection methods (<see cref="CredentialsPath"/>, <see cref="CredentialsJson"/>,
/// <see cref="Client(StorageClient)"/>, <see cref="ClientFactory"/>,
/// <see cref="BindConfiguration"/>) use last-wins semantics: setting one
/// clears the others.
/// </para>
/// <para>
/// Non-connection methods (<see cref="ProjectId"/>, <see cref="BucketName"/>,
/// <see cref="ObjectPrefix"/>) are additive and can be combined with any
/// connection method.
/// </para>
/// </remarks>
public interface IEventSourcingGcsBuilder
{
	/// <summary>Sets the Google Cloud project ID.</summary>
	IEventSourcingGcsBuilder ProjectId(string projectId);

	/// <summary>Sets the GCS bucket name for cold event storage.</summary>
	IEventSourcingGcsBuilder BucketName(string bucketName);

	/// <summary>Sets the object prefix for archived event objects.</summary>
	IEventSourcingGcsBuilder ObjectPrefix(string objectPrefix);

	/// <summary>Sets the path to a service account credentials JSON file.</summary>
	IEventSourcingGcsBuilder CredentialsPath(string credentialsPath);

	/// <summary>Sets inline service account credentials as a JSON string.</summary>
	IEventSourcingGcsBuilder CredentialsJson(string credentialsJson);

	/// <summary>Sets a pre-configured <see cref="StorageClient"/>.</summary>
	IEventSourcingGcsBuilder Client(StorageClient client);

	/// <summary>Sets a factory that resolves a <see cref="StorageClient"/> from DI.</summary>
	IEventSourcingGcsBuilder ClientFactory(Func<IServiceProvider, StorageClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IEventSourcingGcsBuilder BindConfiguration(string sectionPath);
}
