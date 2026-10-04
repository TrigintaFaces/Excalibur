// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net.Http.Headers;
using System.Diagnostics.CodeAnalysis;
using Google.Api.Gax.Rest;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Storage.v1;
using Google.Cloud.Storage.V1;

namespace Excalibur.EventSourcing.Gcs;

/// <summary>Creates framework-owned clients that preserve compressed archive bytes.</summary>
/// <remarks>
/// Uses the SDK base initializer for credentials, quota project, universe and endpoint settings.
/// Supplied credentials retain their existing scopes; registration must apply the same storage scope
/// as StorageClient.Create when loading its own credential. Borrowed clients never pass through here.
/// </remarks>
internal sealed class GcsRawStorageClientBuilder : ClientBuilderBase<StorageClient>
{
	private static readonly ScopedCredentialProvider CredentialProvider = new([StorageService.Scope.DevstorageFullControl]);

	internal GcsRawStorageClientBuilder() => UseJwtAccessWithScopes = true;

	public override StorageClient Build()
	{
		Validate();
		return CreateClient(CreateServiceInitializer());
	}

	public override async Task<StorageClient> BuildAsync(CancellationToken cancellationToken = default)
	{
		Validate();
		return CreateClient(await CreateServiceInitializerAsync(cancellationToken).ConfigureAwait(false));
	}

	protected override ScopedCredentialProvider GetScopedCredentialProvider() => CredentialProvider;
	protected override string GetDefaultApplicationName() => StorageClientImpl.ApplicationName;

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Ownership transfers to StorageClientImpl, whose Dispose disposes the service; construction failures dispose it here.")]
	private static StorageClient CreateClient(BaseClientService.Initializer initializer)
	{
		initializer.GZipEnabled = false;
		initializer.DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None;
		var service = new StorageService(initializer);
		try
		{
			service.HttpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
			return new StorageClientImpl(service);
		}
		catch
		{
			service.Dispose();
			throw;
		}
	}
}
