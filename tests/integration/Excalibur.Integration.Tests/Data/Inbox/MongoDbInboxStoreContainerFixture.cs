// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

using Testcontainers.MongoDb;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Shared fixture for MongoDB InboxStore TestContainers.
/// </summary>
/// <remarks>
/// <para>
/// Creates and manages a MongoDB container for the inbox store conformance suite. The store
/// self-initializes its collection and indexes (handlerType, status, and the TTL index on ProcessedAt)
/// via its lazy <c>EnsureInitializedAsync</c> path, so the fixture only needs a running MongoDB container
/// and a database name — no manual schema/DDL is required. Cleanup drops the database between tests to
/// keep the shared container isolated.
/// </para>
/// <para>
/// A <b>standalone</b> <c>mongo:7</c> is used rather than a replica set: the inbox store performs no
/// multi-document transactions and opens no sessions. Idempotent first-writer-wins is enforced by a
/// single <c>InsertOneAsync</c> against the unique <c>_id</c> (messageId+handlerType), with duplicate-key
/// (11000) write errors surfacing the conflict; all other mutations are single-document
/// <c>UpdateOneAsync</c>/<c>DeleteOneAsync</c>/<c>DeleteManyAsync</c> operations. None of these require a
/// replica set, so the lighter standalone container is sufficient and starts faster.
/// </para>
/// </remarks>
public sealed class MongoDbInboxStoreContainerFixture : ContainerFixtureBase
{
	private MongoDbContainer? _container;

	/// <summary>
	/// Gets the database name for inbox entries.
	/// </summary>
	public string DatabaseName { get; } = "excalibur_inbox_conformance";

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
			.WithName($"mongo-inbox-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Cleans up the inbox store database between tests by dropping it.
	/// The store re-creates its collection and indexes on next use.
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
