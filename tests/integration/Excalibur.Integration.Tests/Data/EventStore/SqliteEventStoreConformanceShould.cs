// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Tasks;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Conformance.EventStore;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Conformance tests for <see cref="SqliteEventStore"/> using the EventStore Conformance Test Kit.
/// </summary>
/// <remarks>
/// SQLite is an embedded, file-based database, so these tests run against real infrastructure with
/// no Docker container and are inherently non-skipped. They verify that the SQLite implementation
/// correctly implements the IEventStore interface contract.
/// </remarks>
[Collection(SqliteEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteEventStoreConformanceShould : EventStoreConformanceTestBase, IClassFixture<SqliteEventStoreFixture>
{
	private readonly SqliteEventStoreFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The SQLite EventStore fixture.</param>
	public SqliteEventStoreConformanceShould(SqliteEventStoreFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IEventStore> CreateStoreAsync()
	{
		// Bind the default connection-string ctor; the store auto-creates its Events table on first use.
		var store = new SqliteEventStore(
			_fixture.ConnectionString,
			NullLogger<SqliteEventStore>.Instance);

		return Task.FromResult<IEventStore>(store);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		// Clean up test data between test classes for isolation.
		await _fixture.CleanupAsync().ConfigureAwait(false);
	}
}
