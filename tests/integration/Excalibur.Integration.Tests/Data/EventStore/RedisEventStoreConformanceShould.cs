// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Tests.Shared.Conformance.EventStore;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Conformance tests for <see cref="RedisEventStore"/> using the EventStore Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the Redis implementation correctly implements the IEventStore interface
/// contract against a real Redis (TestContainers). The store binds the DEFAULT
/// <see cref="ConnectionMultiplexer"/> client surface that consumers use; the fixture isolates state
/// between tests with a <c>FLUSHDB</c> via a dedicated admin connection.
/// </remarks>
[Collection(RedisEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Redis")]
public sealed class RedisEventStoreConformanceShould : EventStoreConformanceTestBase, IClassFixture<RedisEventStoreContainerFixture>
{
	private readonly RedisEventStoreContainerFixture _fixture;
	private ConnectionMultiplexer? _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Redis container fixture.</param>
	public RedisEventStoreConformanceShould(RedisEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IEventStore> CreateStoreAsync()
	{
		// Real-infra lock: the conformance kit exercises Redis Streams concurrency + round-trip
		// semantics that a mocked IDatabase cannot reproduce, so this fixture must never be skipped.
		_fixture.DockerAvailable.ShouldBeTrue(
			"Redis EventStore conformance exercises real Redis Streams atomicity and round-trip behavior — this real-Redis suite must never be skipped");

		// Bind the DEFAULT client surface (ConnectAsync with the raw connection string) — the same
		// non-admin multiplexer a consumer would construct.
		_connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);

		var options = Options.Create(new RedisEventStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseIndex = -1,
		});

		return new RedisEventStore(_connection, options, NullLogger<RedisEventStore>.Instance);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		// Flush the keyspace so the next test starts clean.
		await _fixture.CleanupAsync().ConfigureAwait(false);

		// Dispose the per-test default multiplexer (RedisEventStore does not own it).
		if (_connection is not null)
		{
			await _connection.DisposeAsync().ConfigureAwait(false);
			_connection = null;
		}
	}
}
