// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.CosmosDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.EventStore;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Conformance tests for <see cref="CosmosDbEventStore"/> using the EventStore Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the Cosmos DB implementation correctly implements the IEventStore interface
/// contract against the real Cosmos emulator (TestContainers). The store accepts a consumer-supplied
/// <c>CosmosClient</c>, so the fixture binds the SDK DEFAULT (Newtonsoft) serializer — the same surface
/// a consumer who supplies a raw client gets. The event document is dual-mapped
/// (<c>[JsonPropertyName]</c> + Newtonsoft <c>[JsonProperty]</c>), so the append/load round-trip,
/// optimistic-concurrency, and version-ordering contracts must hold under that default serializer — a
/// mocked container could never reproduce these wire-shape and concurrency semantics.
/// </remarks>
[Collection(CosmosDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbEventStoreConformanceShould : EventStoreConformanceTestBase, IClassFixture<CosmosDbEventStoreContainerFixture>
{
	private readonly CosmosDbEventStoreContainerFixture _fixture;
	private string? _containerName;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Cosmos DB container fixture.</param>
	public CosmosDbEventStoreConformanceShould(CosmosDbEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IEventStore> CreateStoreAsync()
	{
		// Real-infra lock: the conformance kit exercises Cosmos optimistic-concurrency (ETag/Conflict),
		// version ordering, and serialize -> store -> reload round-trip semantics that a mocked
		// IMongoCollection/Container cannot reproduce — this real-emulator suite must never be skipped.
		_fixture.DockerAvailable.ShouldBeTrue(
			"Cosmos DB EventStore conformance exercises real Cosmos concurrency-conflict detection and " +
			"default-serializer (Newtonsoft) round-trip behavior — this real-emulator suite must never be skipped");

		// Unique per-test container so each test is isolated; the store self-creates it lazily.
		_containerName = $"events_{Guid.NewGuid():N}";

		var options = Options.Create(new CosmosDbEventStoreOptions
		{
			EventsContainerName = _containerName,
			CreateContainerIfNotExists = true,
		});

		// Bind the DEFAULT (Newtonsoft) client surface — no custom serializer injected. The store has no
		// public InitializeAsync; it self-initializes lazily on first append/load.
		var store = new CosmosDbEventStore(
			_fixture.Client,
			options,
			NullLogger<CosmosDbEventStore>.Instance);

		return Task.FromResult<IEventStore>(store);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		if (_containerName is not null)
		{
			await _fixture.DeleteContainerAsync(_containerName).ConfigureAwait(false);
			_containerName = null;
		}
	}
}
