// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.EventSourcing.Abstractions;

using Excalibur.Data.Postgres.Snapshots;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Conformance tests for <see cref="PostgresSnapshotStore"/> using the Snapshot Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the Postgres implementation correctly implements the
/// ISnapshotStore interface contract using TestContainers.
/// </remarks>
[Collection(PostgresSnapshotStoreTestCollection.CollectionName)]
public sealed class PostgresSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<PostgresSnapshotStoreContainerFixture>
{
	private readonly PostgresSnapshotStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Postgres container fixture.</param>
	public PostgresSnapshotStoreConformanceShould(PostgresSnapshotStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		// Ensure container is ready and schema is created
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var options = Options.Create(new PostgresSnapshotStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			TableName = _fixture.TableName
		});

		var logger = NullLogger<PostgresSnapshotStore>.Instance;

		// Create the store with connection factory
		var excaliburStore = new PostgresSnapshotStore(
			() => _fixture.CreateConnection(),
			options,
			logger);

		// Adapt Excalibur.EventSourcing.Abstractions.ISnapshotStore to
		// Excalibur.Dispatch.Abstractions.EventSourcing.ISnapshotStore for conformance testing
		return new SnapshotStoreAdapter(excaliburStore);
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		// Clean up test data between tests
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
