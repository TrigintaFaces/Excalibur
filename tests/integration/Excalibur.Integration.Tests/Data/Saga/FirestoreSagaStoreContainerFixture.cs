// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Firestore;

using Grpc.Core;

using Testcontainers.Firestore;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Firestore-emulator container fixture for the Firestore saga-store optimistic-concurrency conformance
/// (e1tsq2, S853). Mirrors the event-store telemetry fixture's emulator setup (explicit endpoint +
/// insecure channel credentials, the reliable way to reach the emulator). Degrades gracefully
/// (<see cref="IsInitialized"/> = false) when the emulator can't start.
/// </summary>
public sealed class FirestoreSagaStoreContainerFixture : IAsyncLifetime
{
	private readonly FirestoreContainer _container;

	public FirestoreSagaStoreContainerFixture()
	{
		_container = new FirestoreBuilder()
			.WithImage("gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators")
			.WithName($"firestore-saga-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();
	}

	/// <summary>Gets a value indicating whether the emulator started + the client built.</summary>
	public bool IsInitialized { get; private set; }

	/// <summary>Gets the emulator-connected Firestore client (injected into the store).</summary>
	public FirestoreDb Db { get; private set; } = null!;

	/// <summary>Gets the project id used for the emulator.</summary>
	public string ProjectId { get; } = "test-project";

	/// <summary>Gets the emulator endpoint (also fed to options as EmulatorHost to satisfy Validate()).</summary>
	public string EmulatorEndpoint => _container.GetEmulatorEndpoint();

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		try
		{
			await _container.StartAsync().ConfigureAwait(false);

			// Explicit endpoint + insecure credentials — env-var-based emulator discovery is unreliable.
			Db = await new FirestoreDbBuilder
			{
				ProjectId = ProjectId,
				Endpoint = _container.GetEmulatorEndpoint(),
				ChannelCredentials = ChannelCredentials.Insecure,
			}.BuildAsync().ConfigureAwait(false);

			IsInitialized = true;
		}
		catch (Exception)
		{
			// Emulator may fail to start on constrained CI hosts.
			IsInitialized = false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		try
		{
			var disposeTask = _container.DisposeAsync().AsTask();
			var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
			if (completed == disposeTask)
			{
				await disposeTask.ConfigureAwait(false);
			}
		}
		catch
		{
			// Best effort — allow the test host to exit cleanly.
		}
	}
}
