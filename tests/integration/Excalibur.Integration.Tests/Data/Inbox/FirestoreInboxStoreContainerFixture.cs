// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Firestore;

using Grpc.Core;

using Testcontainers.Firestore;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Firestore-emulator container fixture for the Firestore <c>IInboxStore</c> real-infrastructure conformance.
/// </summary>
/// <remarks>
/// Mirrors the Firestore event-store fixture's emulator setup (explicit endpoint + insecure channel
/// credentials, the reliable way to reach the emulator) so the store binds an emulator-connected
/// <see cref="FirestoreDb"/> built with the SDK's default serializer settings — no custom converter.
/// Extends <see cref="ContainerFixtureBase"/>: Docker is required, so a missing emulator surfaces as a
/// failure rather than a silent pass. A unique collection name per run isolates the suite, and
/// <see cref="CleanupCollectionAsync"/> deletes the collection's documents between tests.
/// </remarks>
public sealed class FirestoreInboxStoreContainerFixture : ContainerFixtureBase
{
	private FirestoreContainer? _container;

	/// <summary>
	/// Gets the emulator-connected Firestore client (injected into the store).
	/// </summary>
	public FirestoreDb Db { get; private set; } = null!;

	/// <summary>
	/// Gets the project id used for the emulator.
	/// </summary>
	public string ProjectId { get; } = "test-project";

	/// <summary>
	/// Gets the unique collection name for this fixture's inbox documents.
	/// </summary>
	public string CollectionName { get; } = $"inbox_{Guid.NewGuid():N}";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new FirestoreBuilder()
			.WithImage("gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators")
			.WithName($"firestore-inbox-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// Explicit endpoint + insecure credentials with the SDK's default serializer settings —
		// env-var-based emulator discovery is unreliable.
		Db = await new FirestoreDbBuilder
		{
			ProjectId = ProjectId,
			Endpoint = _container.GetEmulatorEndpoint(),
			ChannelCredentials = ChannelCredentials.Insecure,
		}.BuildAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Deletes all documents from the fixture's inbox collection between tests.
	/// </summary>
	public async Task CleanupCollectionAsync()
	{
		var collection = Db.Collection(CollectionName);

		await foreach (var document in collection.ListDocumentsAsync().ConfigureAwait(false))
		{
			_ = await document.DeleteAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}
}
