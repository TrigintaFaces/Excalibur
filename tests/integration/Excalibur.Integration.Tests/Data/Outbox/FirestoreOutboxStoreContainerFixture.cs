// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Infrastructure;
using Google.Api.Gax;

using Google.Cloud.Firestore;

using Testcontainers.Firestore;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Firestore-emulator container fixture for the Firestore <c>ICloudNativeOutboxStore</c>
/// real-infrastructure tests.
/// </summary>
/// <remarks>
/// Mirrors the Firestore inbox/event-store fixtures' emulator setup. Exposes the emulator's raw
/// endpoint as <see cref="EmulatorHost"/> (fed straight into <c>FirestoreOutboxOptions.EmulatorHost</c>,
/// which the store itself turns into the process-wide <c>FIRESTORE_EMULATOR_HOST</c> variable via
/// <c>FirestoreEmulatorHelper</c>) AND a directly-connected <see cref="Db"/> client for tests that need
/// to write documents bypassing the store (the legacy-row read-tolerance arm).
/// </remarks>
public sealed class FirestoreOutboxStoreContainerFixture : ContainerFixtureBase
{
	private FirestoreContainer? _container;

	/// <summary>
	/// Gets the emulator-connected Firestore client, for direct document manipulation in tests.
	/// </summary>
	public FirestoreDb Db { get; private set; } = null!;

	/// <summary>
	/// Gets the raw emulator endpoint (host:port), fed into <c>FirestoreOutboxOptions.EmulatorHost</c>.
	/// </summary>
	public string EmulatorHost { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the project id used for the emulator.
	/// </summary>
	public string ProjectId { get; } = "test-project";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new FirestoreBuilder()
			.WithImage(TestContainerImages.GoogleCloudEmulators)
			.WithName($"firestore-outbox-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		EmulatorHost = _container.GetEmulatorEndpoint();

		// Explicit endpoint + insecure credentials with the SDK's default serializer settings — mirrors
		// FirestoreInboxStoreContainerFixture. Set from THIS container's own endpoint so it cannot point
		// at a stale or foreign emulator.
		Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", EmulatorHost);

		Db = await new FirestoreDbBuilder
		{
			ProjectId = ProjectId,
			EmulatorDetection = EmulatorDetection.EmulatorOnly,
		}.BuildAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Deletes every document in the named collection (best effort), used for per-test cleanup.
	/// </summary>
	/// <param name="collectionName">The collection to clean up.</param>
	public async Task CleanupCollectionAsync(string collectionName)
	{
		var collection = Db.Collection(collectionName);

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
