// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb;
using Excalibur.Data.CosmosDb.Snapshots;

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="CosmosDbSnapshotStore"/> using the Snapshot
/// Conformance Test Kit against a live CosmosDB Linux emulator.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that the Cosmos DB implementation correctly implements the
/// <see cref="ISnapshotStore"/> contract. They are never skipped: when the emulator is unavailable the
/// fixture fails fast, so a missing container surfaces as a failure rather than a silent pass.
/// </para>
/// <para>
/// The store creates its own <c>CosmosClient</c> from the connection string and self-configures its
/// serializer, so this exercises the production connection-string path consumers use. The store is
/// constructed directly (no DI), with gateway connection mode and the emulator's HttpClient so the
/// self-signed certificate is trusted.
/// </para>
/// </remarks>
[Collection(CosmosDbSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<CosmosDbSnapshotStoreContainerFixture>
{
	private readonly CosmosDbSnapshotStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Cosmos DB container fixture.</param>
	public CosmosDbSnapshotStoreConformanceShould(CosmosDbSnapshotStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Cosmos DB emulator must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new CosmosDbSnapshotStoreOptions
		{
			Client = new CosmosDbClientOptions
			{
				// The store builds its own client from this connection string (the default consumer path).
				ConnectionString = _fixture.ConnectionString,

				// Emulator requires gateway mode + the self-signed cert via the emulator HttpClient.
				UseDirectMode = false,
				HttpClientFactory = () => _fixture.EmulatorHttpClient,
			},
			DatabaseName = _fixture.DatabaseName,
			ContainerName = _fixture.ContainerName,
			PartitionKeyPath = "/aggregateType",
			CreateContainerIfNotExists = true,
			ContainerThroughput = 400,
		});

		var store = new CosmosDbSnapshotStore(options, NullLogger<CosmosDbSnapshotStore>.Instance);

		// Eagerly create the container so a wiring fault fails fast rather than on first operation.
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		return store;
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		await _fixture.CleanupDatabaseAsync().ConfigureAwait(false);
	}
}
