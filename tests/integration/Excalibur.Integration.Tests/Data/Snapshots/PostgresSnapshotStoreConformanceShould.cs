// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Conformance tests for the canonical <see cref="PostgresSnapshotStore"/> (Excalibur.EventSourcing.Postgres)
/// using the Snapshot Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the Postgres implementation correctly implements the
/// ISnapshotStore interface contract using TestContainers.
/// </remarks>
[Collection(PostgresSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class PostgresSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<PostgresSnapshotStoreContainerFixture>
{
	private readonly PostgresSnapshotStoreContainerFixture _fixture;
	private NpgsqlDataSource? _dataSource;

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

		_dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);

		// The canonical snapshot store lives in Excalibur.EventSourcing.Postgres (snapshots are an
		// event-sourcing persistence concern). Single-tenant construction (no ambient tenant context)
		// exercises the general ISnapshotStore contract.
		var excaliburStore = new PostgresSnapshotStore(
			_dataSource,
			NullLogger<PostgresSnapshotStore>.Instance,
			_fixture.SchemaName,
			_fixture.TableName,
			tenantContext: null);

		// Adapt Excalibur.EventSourcing.ISnapshotStore to the conformance-kit ISnapshotStore.
		return new SnapshotStoreAdapter(excaliburStore);
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		// Clean up test data between tests
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		if (_dataSource is not null)
		{
			await _dataSource.DisposeAsync().ConfigureAwait(false);
			_dataSource = null;
		}
	}
}
