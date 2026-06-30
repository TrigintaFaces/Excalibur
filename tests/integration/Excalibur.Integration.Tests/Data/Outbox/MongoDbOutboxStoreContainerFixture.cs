// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

using Testcontainers.MongoDb;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Shared fixture for MongoDB OutboxStore TestContainers.
/// </summary>
/// <remarks>
/// <para>
/// Creates and manages a MongoDB container for the outbox store conformance suite. The store
/// self-initializes its collection and indexes (status, scheduling, failed-message, and the TTL index)
/// via its lazy <c>EnsureInitializedAsync</c> path, so the fixture only needs a running MongoDB container
/// and a database name — no manual schema/DDL is required. Cleanup drops the database between tests to
/// keep the shared container isolated.
/// </para>
/// <para>
/// A <b>standalone</b> <c>mongo:7</c> is used rather than a replica set: the outbox store performs no
/// multi-document transactions and opens no sessions. Atomic status transitions are enforced by a single
/// <c>FindOneAndUpdate</c> with a status filter, and duplicate-id detection surfaces as a duplicate-key
/// (11000) write error — neither requires a replica set, so the lighter standalone container is
/// sufficient and starts faster.
/// </para>
/// </remarks>
public sealed class MongoDbOutboxStoreContainerFixture : ContainerFixtureBase
{
	private MongoDbContainer? _container;

	/// <summary>
	/// Gets the database name for outbox messages.
	/// </summary>
	public string DatabaseName { get; } = "excalibur_outbox_conformance";

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
			.WithName($"mongo-outbox-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Cleans up the outbox store database between tests by dropping it.
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
