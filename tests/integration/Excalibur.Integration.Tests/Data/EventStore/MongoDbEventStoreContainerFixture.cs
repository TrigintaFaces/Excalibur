// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

using Testcontainers.MongoDb;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Shared fixture for MongoDB EventStore TestContainers.
/// </summary>
/// <remarks>
/// <para>
/// Creates and manages a MongoDB container for the event store conformance suite. The store
/// self-initializes its collections and indexes (including the UNIQUE compound index that backs
/// optimistic concurrency) via its lazy <c>EnsureInitializedAsync</c> path, so the fixture only needs a
/// running MongoDB container and a database name — no manual schema/DDL is required. Cleanup drops the
/// database between tests to keep the shared container isolated.
/// </para>
/// <para>
/// A <b>single-node replica set</b> is used rather than a standalone server, because the store commits a
/// multi-event append inside a transaction and MongoDB provides transactions only on a replica set.
/// </para>
/// <para>
/// This fixture previously ran standalone, on the rationale that the store "performs no multi-document
/// transactions and opens no sessions" — true when written, and untrue once batch-append atomicity was
/// added. Standalone kept passing for a while because the discrepancy is invisible to a single-event
/// append: one document needs no transaction. Only appends of two or more events failed, which is why
/// the conformance and batch-atomicity suites broke while the rest of the suite stayed green.
/// </para>
/// <para>
/// Optimistic concurrency is still enforced by a pre-write version read plus a UNIQUE compound index on
/// (streamId, aggregateType, version), surfacing a conflict as duplicate-key (11000). That part is
/// unchanged and needs no replica set; the transaction requirement comes from batch atomicity alone.
/// </para>
/// </remarks>
public sealed class MongoDbEventStoreContainerFixture : ContainerFixtureBase
{
	private MongoDbContainer? _container;

	/// <summary>
	/// Gets the database name for events.
	/// </summary>
	public string DatabaseName { get; } = "excalibur_eventstore_conformance";

	/// <summary>
	/// Gets the connection string for the MongoDB container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MongoDbBuilder()
			.WithImage("mongo:7")
			.WithName($"mongo-eventstore-test-{Guid.NewGuid():N}")
			// A single-node replica set, not standalone. A MULTI-event append commits inside a
			// transaction so the batch lands all-or-nothing, and MongoDB offers transactions only on a
			// replica set — on standalone the driver refuses with "Standalone servers do not support
			// transactions" and every such append fails. Single-event appends are one document and need
			// no transaction, which is why standalone appeared to work: the suites that append one event
			// passed while every batch append failed.
			.WithReplicaSet("rs0")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Cleans up the event store database between tests by dropping it.
	/// The store re-creates its collections and indexes on next use.
	/// </summary>
	public async Task CleanupAsync()
	{
		var client = new MongoClient(ConnectionString);
		await client.DropDatabaseAsync(DatabaseName).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}
}
