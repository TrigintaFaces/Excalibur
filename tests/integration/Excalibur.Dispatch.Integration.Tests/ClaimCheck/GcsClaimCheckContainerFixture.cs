// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Google.Cloud.Storage.V1;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.ClaimCheck;

/// <summary>
/// Fixture for a Cloud Storage emulator backing the real-infrastructure
/// <see cref="Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage.GcsClaimCheckStore"/> conformance suite.
/// </summary>
/// <remarks>
/// <para>
/// There is no Testcontainers module for Cloud Storage, so this drives the generic
/// <see cref="ContainerBuilder"/> against the same emulator image the tiered-storage suites use. The
/// emulator matters here rather than a fake: the store's delete contract is answered by translating the
/// SDK's not-found response, and only a real server produces that response.
/// </para>
/// <para>
/// The client is pointed at the emulator through <see cref="StorageClientBuilder.BaseUri"/> rather than
/// the <c>STORAGE_EMULATOR_HOST</c> environment variable. That variable is process-global, and this
/// assembly runs its collections in parallel, so setting it would redirect every Cloud Storage client in
/// the process — including ones belonging to other suites — at this fixture's container.
/// </para>
/// <para>
/// Inherits <see cref="ContainerFixtureBase"/> without overriding <c>AllowGracefulDegradation</c>, so a
/// container that cannot start throws instead of silently degrading. A conformance arm that passes by
/// being skipped is indistinguishable from one that passed by working.
/// </para>
/// </remarks>
public sealed class GcsClaimCheckContainerFixture : ContainerFixtureBase
{
	private const ushort ServerPort = 4443;

	private IContainer? _container;
	private StorageClient? _storageClient;

	/// <summary>
	/// Gets the bucket provisioned for this fixture's lifetime. Every conformance test in the collection
	/// shares it; claim-check ids are GUID-based, so object names never collide.
	/// </summary>
	public string BucketName { get; } = $"claimcheck-{Guid.NewGuid():N}";

	/// <summary>
	/// Gets the Cloud Storage client pointed at the emulator container.
	/// </summary>
	public StorageClient StorageClient => _storageClient
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ContainerBuilder()
			.WithImage("fsouza/fake-gcs-server:1.56.0")
			.WithName($"gcs-claimcheck-{Guid.NewGuid():N}")
			.WithPortBinding(ServerPort, true)
			.WithCommand("-scheme", "http", "-backend", "memory")
			.WithWaitStrategy(Wait.ForUnixContainer()
				.UntilHttpRequestIsSucceeded(r => r.ForPath("/storage/v1/b").ForPort(ServerPort)))
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var endpoint = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(ServerPort)}";

		_storageClient = new StorageClientBuilder
		{
			BaseUri = $"{endpoint}/storage/v1/",
			UnauthenticatedAccess = true,
		}.Build();

		_ = await _storageClient.CreateBucketAsync("test-project", BucketName, cancellationToken: cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		_storageClient?.Dispose();
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}

/// <summary>
/// Collection definition for the real-infrastructure Cloud Storage claim-check conformance tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class GcsClaimCheckTestCollection : ICollectionFixture<GcsClaimCheckContainerFixture>
{
	public const string Name = "ClaimCheck-Gcs";
}
