// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

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

		// Bind the default connection-string constructor (the surface most consumers use).
		return new SqlServerSnapshotStore(_fixture.ConnectionString, logger);
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
