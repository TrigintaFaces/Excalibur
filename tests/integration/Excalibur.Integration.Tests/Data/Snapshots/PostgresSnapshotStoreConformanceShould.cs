// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.Dispatch;

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
		// event-sourcing persistence concern). The tenant-isolation arms establish tenants via
		// TenantContextHolder.BeginScope, which a null context cannot see: CurrentTenantScope
		// is None, so every tenant wrote the untenanted sentinel and collided on one row per aggregate id.
		var excaliburStore = new PostgresSnapshotStore(
			_dataSource,
			NullLogger<PostgresSnapshotStore>.Instance,
			tenantContext: new AmbientTenantContext(),
			schema: _fixture.SchemaName,
			table: _fixture.TableName);

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

	/// <summary>
	/// Reads the tenant established by <see cref="TenantContextHolder.BeginScope"/>. The production
	/// equivalent is internal to Excalibur.Dispatch, so a directly-constructed store needs this here.
	/// </summary>
	private sealed class AmbientTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}
}
