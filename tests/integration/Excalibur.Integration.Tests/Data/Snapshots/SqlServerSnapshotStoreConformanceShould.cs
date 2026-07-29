// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="SqlServerSnapshotStore"/> using the
/// Snapshot Conformance Test Kit against a live SQL Server container.
/// </summary>
/// <remarks>
/// These tests verify that the SQL Server implementation correctly implements the
/// <see cref="ISnapshotStore"/> contract using TestContainers. They are never skipped:
/// when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass.
/// </remarks>
[Collection(SqlServerSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<SqlServerSnapshotStoreContainerFixture>
{
	private readonly SqlServerSnapshotStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The SQL Server container fixture.</param>
	public SqlServerSnapshotStoreConformanceShould(SqlServerSnapshotStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - real-infra conformance is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var logger = NullLogger<SqlServerSnapshotStore>.Instance;

		// The tenant-isolation arms establish tenants with TenantContextHolder.BeginScope, so the store
		// must be able to SEE that ambient tenant. The connection-string constructor takes no tenant
		// context at all, leaving TenantScope.FromContext(null) == None: every tenant then wrote the
		// reserved untenanted sentinel, all tenants collided on one row per aggregate id, and a later
		// tenant's save silently overwrote an earlier tenant's snapshot. Binding the ambient context is
		// what the shipped DI registrations now do too.
		return new SqlServerSnapshotStore(
			() => new SqlConnection(_fixture.ConnectionString),
			logger,
			"dbo",
			"EventStoreSnapshots",
			new AmbientTenantContext());
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
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
