// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Tasks;

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="SqliteSnapshotStore"/> using the
/// Snapshot Conformance Test Kit against an embedded SQLite database.
/// </summary>
/// <remarks>
/// SQLite is a local, file-based database - it is itself the real infrastructure, so these tests
/// require no Docker container and are never skipped. The fixture provisions a unique temporary
/// database file; the store auto-creates its schema on first use.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<SqliteSnapshotStoreFixture>
{
	private readonly SqliteSnapshotStoreFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The SQLite snapshot store fixture.</param>
	public SqliteSnapshotStoreConformanceShould(SqliteSnapshotStoreFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		var logger = NullLogger<SqliteSnapshotStore>.Instance;

		// Bind the default connection-string constructor (the surface most consumers use).
		// The store auto-creates its table on first use, so no DDL bootstrap is required.
		return Task.FromResult<ISnapshotStore>(
			new SqliteSnapshotStore(_fixture.ConnectionString, logger));
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);
	}
}
