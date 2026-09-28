// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Google.Api.Gax;

using Google.Cloud.Firestore;

using Testcontainers.Firestore;

using Tests.Shared.Fixtures;
using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Firestore-emulator container fixture for the Firestore saga-store optimistic-concurrency
/// conformance suite.
/// </summary>
/// <remarks>
/// <para>
/// <b>This was a bespoke <c>IAsyncLifetime</c> that DISCARDED its startup exception</b> — a bare
/// <c>catch (Exception)</c> setting <c>IsInitialized = false</c> — and had no guard on its accessors.
/// That combination is the one that turns a container failure into an undiagnosable secondary error:
/// the cause is thrown away, nothing refuses on the way out, and the first property access raises a
/// Testcontainers message about a resource that was never created. The same shape on the DynamoDB
/// saga fixture produced 19 failures in a full-shard run whose actual cause was unrecoverable from
/// the output.
/// </para>
/// <para>
/// <see cref="ContainerFixtureBase"/> owns the attempt budget, records the failure in
/// <see cref="ContainerFixtureBase.InitializationError"/>, and implements the availability policy —
/// which is hard failure, not a skip, because a real-infrastructure test that passes by never
/// executing certifies a guarantee nothing checked.
/// </para>
/// </remarks>
public sealed class FirestoreSagaStoreContainerFixture : ContainerFixtureBase
{
	private FirestoreContainer? _container;

	/// <summary>Gets the emulator-connected Firestore client injected into the store.</summary>
	/// <value>The client, once the emulator is up.</value>
	public FirestoreDb Db
	{
		get
		{
			EnsureAvailable();
			return _db!;
		}
	}

	private FirestoreDb? _db;

	/// <summary>Gets the project id used for the emulator.</summary>
	/// <value>A fixed id; the emulator does not validate it.</value>
	public string ProjectId { get; } = "test-project";

	/// <summary>Gets the emulator endpoint, also fed to options to satisfy their validation.</summary>
	/// <value>The running container's emulator endpoint.</value>
	public string EmulatorEndpoint
	{
		get
		{
			EnsureAvailable();
			return _container!.GetEmulatorEndpoint();
		}
	}

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new FirestoreBuilder()
			.WithImage(TestContainerImages.GoogleCloudEmulators)
			.WithName($"firestore-saga-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		// Deliberately NOT wrapped in a try/catch: the base owns retry, the budget, and recording the
		// failure. Swallowing here is what made the original undiagnosable.
		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// EmulatorOnly and an explicit Endpoint are MUTUALLY EXCLUSIVE — EmulatorOnly makes the SDK
		// build its own channel from FIRESTORE_EMULATOR_HOST, so supplying Endpoint as well throws from
		// GaxPreconditions before a request is sent. Setting only Endpoint is worse than failing: the
		// SDK then behaves as though this were a real deployment and the emulator rejects admin-ish
		// calls with PermissionDenied. The variable is set from THIS fixture's own container, so it
		// cannot point at a foreign emulator.
		Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", _container.GetEmulatorEndpoint());

		_db = await new FirestoreDbBuilder
		{
			ProjectId = ProjectId,
			EmulatorDetection = EmulatorDetection.EmulatorOnly,
		}.BuildAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
