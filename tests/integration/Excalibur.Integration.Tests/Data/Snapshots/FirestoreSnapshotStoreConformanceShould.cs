// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Snapshots;

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="FirestoreSnapshotStore"/> using the
/// Snapshot Conformance Test Kit against a live Firestore emulator.
/// </summary>
/// <remarks>
/// These tests verify that the Firestore implementation correctly implements the
/// <see cref="ISnapshotStore"/> contract against the emulator. They are never skipped:
/// when Docker is unavailable the fixture fails fast, so a missing emulator surfaces as a
/// failure rather than a silent pass. The store binds the FirestoreDb the fixture built with
/// the SDK's default serializer settings (no custom converter), so the round-trip exercises
/// the wire shape consumers actually get.
/// </remarks>
[Collection(FirestoreSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
public sealed class FirestoreSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<FirestoreSnapshotStoreContainerFixture>
{
	private readonly FirestoreSnapshotStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Firestore container fixture.</param>
	public FirestoreSnapshotStoreConformanceShould(FirestoreSnapshotStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new FirestoreSnapshotStoreOptions
		{
			ProjectId = _fixture.ProjectId,
			CollectionName = _fixture.CollectionName,
			EmulatorHost = _fixture.EmulatorEndpoint,
		});

		// Bind the emulator-connected FirestoreDb (default serializer settings) so the round-trip
		// exercises the wire shape consumers actually get.
		return Task.FromResult<ISnapshotStore>(
			new FirestoreSnapshotStore(
				_fixture.Db,
				options,
				NullLogger<FirestoreSnapshotStore>.Instance));
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		await _fixture.CleanupCollectionAsync().ConfigureAwait(false);
	}
}
