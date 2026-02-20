// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.EventSourcing;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.EventStore;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Conformance tests for <see cref="PostgresEventStore"/> using the EventStore Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the Postgres implementation correctly implements the
/// IEventStore interface contract using TestContainers.
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
public sealed class PostgresEventStoreConformanceShould : EventStoreConformanceTestBase, IClassFixture<PostgresEventStoreContainerFixture>
{
	private readonly PostgresEventStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Postgres container fixture.</param>
	public PostgresEventStoreConformanceShould(PostgresEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IEventStore> CreateStoreAsync()
	{
		// Ensure container is ready and schema is created
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var options = Options.Create(new PostgresEventStoreOptions
		{
			SchemaName = "public",
			EventsTableName = _fixture.TableName
		});

		var logger = NullLogger<PostgresEventStore>.Instance;

		// Create the store with connection factory
		var store = new PostgresEventStore(
			() => _fixture.CreateConnection(),
			options,
			logger);

		return store;
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		// Clean up test data between tests
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
