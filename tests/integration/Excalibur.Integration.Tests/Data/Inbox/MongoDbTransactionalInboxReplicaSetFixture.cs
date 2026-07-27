// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

using Testcontainers.MongoDb;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure fixture for the MongoDB <b>scoped transactional inbox</b> (exactly-once) seam. Unlike
/// <see cref="MongoDbInboxStoreContainerFixture"/> — a <b>standalone</b> <c>mongo:7</c> sufficient for the
/// idempotent first-writer-wins claim protocol — this fixture starts a single-node <b>replica set</b>
/// (<c>rs0</c>) because MongoDB multi-document transactions (<c>StartSession</c> + <c>StartTransaction</c>)
/// require a replica set (or sharded cluster); a standalone <c>mongod</c> cannot begin a transaction.
/// </summary>
/// <remarks>
/// A single-node replica set is strictly more capable than a standalone server and elects a primary before
/// <see cref="InitializeContainerAsync"/> returns, so the transactional path is exercisable end-to-end.
/// </remarks>
public sealed class MongoDbTransactionalInboxReplicaSetFixture : ContainerFixtureBase
{
	private MongoDbContainer? _container;

	/// <summary>
	/// Gets the database name for the transactional inbox tests.
	/// </summary>
	public string DatabaseName { get; } = "excalibur_inbox_txn";

	/// <summary>
	/// Gets the replica-set-aware connection string for the MongoDB container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MongoDbBuilder()
			.WithImage("mongo:7")
			.WithName($"mongo-inbox-txn-{Guid.NewGuid():N}")
			// Single-node replica set — a HARD requirement for multi-document transactions.
			.WithReplicaSet("rs0")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Drops the transactional-inbox database between tests to isolate the shared container.
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
