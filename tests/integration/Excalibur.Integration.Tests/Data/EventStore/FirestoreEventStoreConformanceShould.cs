// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Firestore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Conformance tests for <see cref="FirestoreEventStore"/> using the EventStore Conformance Test Kit
/// against a real Firestore emulator.
/// </summary>
/// <remarks>
/// These tests verify that the Firestore implementation correctly implements the IEventStore interface
/// contract against real infrastructure (the Firestore emulator via TestContainers), exercising the
/// emulator-connected <see cref="FirestoreDb"/> built with the SDK's default serializer settings.
/// </remarks>
[Collection(FirestoreEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
public sealed class FirestoreEventStoreConformanceShould : EventStoreConformanceTestBase, IClassFixture<FirestoreEventStoreContainerFixture>
{
	private readonly FirestoreEventStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Firestore container fixture.</param>
	public FirestoreEventStoreConformanceShould(FirestoreEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IEventStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator must be available for real-infrastructure conformance — never skipped.");

		var options = Options.Create(new FirestoreEventStoreOptions
		{
			ProjectId = _fixture.ProjectId,
			EventsCollectionName = _fixture.CollectionName,
			EmulatorHost = _fixture.EmulatorEndpoint,
		});

		// Bind the emulator-connected FirestoreDb (default serializer) directly so the store
		// talks to real infrastructure rather than building its own client.
		var store = new FirestoreEventStore(
			_fixture.Db,
			options,
			NullLogger<FirestoreEventStore>.Instance);

		return Task.FromResult<IEventStore>(store);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupCollectionAsync().ConfigureAwait(false);
	}
}
