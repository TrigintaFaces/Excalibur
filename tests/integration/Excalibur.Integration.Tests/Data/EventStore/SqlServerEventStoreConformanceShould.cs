// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Conformance.EventStore;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Conformance tests for <see cref="SqlServerEventStore"/> using the EventStore Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the SQL Server implementation correctly implements the
/// IEventStore interface contract against a real SQL Server instance using TestContainers.
/// The store is built with the consumer-default (connectionString, logger) constructor so the
/// default serializer surface is exercised — never a hand-configured one.
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventStoreConformanceShould : EventStoreConformanceTestBase, IClassFixture<SqlServerEventStoreContainerFixture>
{
	private readonly SqlServerEventStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The SQL Server container fixture.</param>
	public SqlServerEventStoreConformanceShould(SqlServerEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IEventStore> CreateStoreAsync()
	{
		// Real-infra lock: the container must be available — this test is never silently skipped.
		_fixture.DockerAvailable.ShouldBeTrue("SQL Server EventStore conformance runs against real infrastructure and is never skipped");

		// Ensure container is ready and schema is created.
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var logger = NullLogger<SqlServerEventStore>.Instance;

		// Build the store with the consumer-default (connectionString, logger) constructor.
		var store = new SqlServerEventStore(
			_fixture.ConnectionString,
			logger);

		return store;
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		// Clean up test data between tests.
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
