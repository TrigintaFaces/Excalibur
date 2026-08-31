// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Api.Gax;

using Google.Cloud.Firestore;

using Testcontainers.Firestore;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Firestore-emulator container fixture for the Firestore persistence-provider conformance suite.
/// </summary>
/// <remarks>
/// Mirrors the Firestore event-store fixture's emulator setup: <c>FIRESTORE_EMULATOR_HOST</c> is set from
/// this fixture's own container (so it cannot point at a foreign emulator) and the client is built with
/// <see cref="EmulatorDetection.EmulatorOnly"/>, which is the reliable way to reach the emulator — an
/// explicit endpoint alone leaves the SDK behaving as though this were a real deployment. Extends
/// <see cref="ContainerFixtureBase"/>: real-infra conformance is never skipped.
/// </remarks>
public sealed class FirestorePersistenceProviderContainerFixture : ContainerFixtureBase
{
	private FirestoreContainer? _container;

	/// <summary>
	/// Gets the emulator-connected Firestore client injected into the provider.
	/// </summary>
	public FirestoreDb Db { get; private set; } = null!;

	/// <summary>
	/// Gets the project id used for the emulator.
	/// </summary>
	public string ProjectId { get; } = "test-project";

	/// <summary>
	/// Gets the emulator endpoint.
	/// </summary>
	public string EmulatorEndpoint => _container?.GetEmulatorEndpoint()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new FirestoreBuilder()
			.WithImage("gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators")
			.WithName($"firestore-persistence-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", _container.GetEmulatorEndpoint());

		Db = await new FirestoreDbBuilder
		{
			ProjectId = ProjectId,
			EmulatorDetection = EmulatorDetection.EmulatorOnly,
		}.BuildAsync(cancellationToken).ConfigureAwait(false);
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

/// <summary>
/// xUnit collection definition for the Firestore persistence-provider conformance suite.
/// </summary>
public static class FirestorePersistenceProviderTestCollection
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = global::Excalibur.Integration.Tests.FirestoreSerialCollection.CollectionName;
}
