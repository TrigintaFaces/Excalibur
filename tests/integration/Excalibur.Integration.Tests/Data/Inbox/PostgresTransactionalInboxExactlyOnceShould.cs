// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Inbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

#pragma warning disable CA1812 // Instantiated by xUnit.
#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// vpdiys (S872) — Postgres parity for the transactional-inbox exactly-once seam
/// <see cref="ITransactionalInboxStore"/> against a live Postgres container. Mirrors the SqlServer lock:
/// a handler write enlisted in the store's transaction commits/rolls back atomically with the processed-mark,
/// and concurrent duplicate deliveries of the same message process exactly once. The Postgres serialization
/// primitive is <c>INSERT ... ON CONFLICT DO NOTHING</c> + <c>SELECT ... FOR UPDATE</c>.
/// </summary>
/// <remarks>
/// NEVER SKIPPED — when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. The lock asserts EMITTED BEHAVIOR through the real seam (the durable
/// processed-mark, the atomic handler-write rollback, and single-commit under concurrency), not the mere
/// presence of the capability interface. NON-VACUOUS: RED against a non-transactional impl (which would leave
/// the handler write committed after a crash) and against one that re-invokes the handler on a duplicate.
/// </remarks>
[Collection(PostgresInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresTransactionalInboxExactlyOnceShould : IAsyncLifetime
{
	// A small side table the handler writes to ENLISTED in the store's transaction, so the test can prove the
	// handler's own data write commits/rolls back ATOMICALLY with the processed-mark (the headline atomicity proof).
	private const string SideTable = "\"public\".\"inbox_txn_side_effect\"";

	private readonly PostgresInboxStoreContainerFixture _fixture;
	private PostgresInboxStore _store = null!;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresTransactionalInboxExactlyOnceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Postgres inbox container fixture.</param>
	public PostgresTransactionalInboxExactlyOnceShould(PostgresInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - real-infra exactly-once lock is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var options = Options.Create(new PostgresInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = _fixture.SchemaName,
			TableName = _fixture.TableName,
		});

		_store = new PostgresInboxStore(options, NullLogger<PostgresInboxStore>.Instance);

		await EnsureSideTableAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		if ((object)_store is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if ((object)_store is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Creates (idempotently) and clears the side-effect table the handler writes to via the enlisting transaction.
	/// </summary>
	private async Task EnsureSideTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			$"""
			 CREATE TABLE IF NOT EXISTS {SideTable} (marker VARCHAR(255) NOT NULL);
			 TRUNCATE TABLE {SideTable};
			 """).ConfigureAwait(false);
	}

	/// <summary>
	/// Counts committed side-effect rows for the given marker on a FRESH connection (outside any test transaction),
	/// so only durably committed writes are visible.
	/// </summary>
	private async Task<int> CountSideEffectsAsync(string marker)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		return await connection.ExecuteScalarAsync<int>(
			$"SELECT COUNT(*) FROM {SideTable} WHERE marker = @Marker",
			new { Marker = marker }).ConfigureAwait(false);
	}

	[Fact]
	public async Task TransactionalInbox_CrashMidProcessing_RedeliversWithoutDuplicatingCommittedEffect()
	{
		var transactional = (_store as IInboxStore) as ITransactionalInboxStore;
		_ = transactional.ShouldNotBeNull(
			"PostgresInboxStore must implement ITransactionalInboxStore to provide exactly-once processing.");

		var messageId = Guid.NewGuid().ToString();
		const string handlerType = "Handler.Type.ExactlyOnce";
		var handlerInvocations = 0;

		try
		{
			_ = await transactional.TryProcessTransactionallyAsync(
				messageId,
				handlerType,
				(_, _) =>
				{
					_ = Interlocked.Increment(ref handlerInvocations);
					throw new InvalidOperationException("simulated consumer crash mid-processing");
				},
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected if the impl propagates the handler failure to the caller.
		}

		(await _store.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a crashed handler must roll back the processed-mark so the message is redelivered");

		var processedOnRetry = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			(_, _) =>
			{
				_ = Interlocked.Increment(ref handlerInvocations);
				return ValueTask.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		processedOnRetry.ShouldBeTrue("the redelivered message is processed for the first successful time");
		(await _store.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("after a successful transactional process the message is durably marked processed");

		var processedOnDuplicate = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			(_, _) =>
			{
				_ = Interlocked.Increment(ref handlerInvocations);
				return ValueTask.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		processedOnDuplicate.ShouldBeFalse("an already-processed message is a duplicate — the handler is not invoked");

		handlerInvocations.ShouldBe(2,
			"the handler delegate ran on the crash attempt (rolled back) and once successfully — never on the duplicate");
	}

	[Fact]
	public async Task TransactionalInbox_HandlerWriteEnlistedInTx_RollsBackAtomicallyWithProcessedMark()
	{
		var transactional = (_store as IInboxStore) as ITransactionalInboxStore;
		_ = transactional.ShouldNotBeNull(
			"PostgresInboxStore must implement ITransactionalInboxStore to provide atomic handler+mark commits.");

		var messageId = Guid.NewGuid().ToString();
		const string handlerType = "Handler.Type.EnlistedRollback";
		var marker = Guid.NewGuid().ToString();

		// Delivery 1 — handler writes to the side table via the ENLISTING transaction, then CRASHES.
		try
		{
			_ = await transactional.TryProcessTransactionallyAsync(
				messageId,
				handlerType,
				async (tx, ct) =>
				{
					_ = await tx.Connection!.ExecuteAsync(new CommandDefinition(
						$"INSERT INTO {SideTable} (marker) VALUES (@Marker)",
						new { Marker = marker },
						transaction: tx,
						cancellationToken: ct)).ConfigureAwait(false);

					throw new InvalidOperationException("simulated crash after an enlisted handler write");
				},
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected if the impl propagates the handler failure.
		}

		// (a) The message is NOT marked processed — it stays redeliverable.
		(await _store.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a crashed handler must roll back the processed-mark so the message is redelivered");

		// (b) The handler's enlisted data write is GONE — rolled back atomically with the mark.
		(await CountSideEffectsAsync(marker).ConfigureAwait(false))
			.ShouldBe(0, "the handler's enlisted write must roll back atomically with the processed-mark on crash");

		// Redelivery — handler succeeds; now BOTH the write and the mark commit atomically.
		var processed = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			async (tx, ct) =>
			{
				_ = await tx.Connection!.ExecuteAsync(new CommandDefinition(
					$"INSERT INTO {SideTable} (marker) VALUES (@Marker)",
					new { Marker = marker },
					transaction: tx,
					cancellationToken: ct)).ConfigureAwait(false);
			},
			CancellationToken.None).ConfigureAwait(false);

		processed.ShouldBeTrue("the redelivered message is processed for the first successful time");
		(await _store.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("after a successful transactional process the message is durably marked processed");
		(await CountSideEffectsAsync(marker).ConfigureAwait(false))
			.ShouldBe(1, "the handler's enlisted write commits exactly once, atomically with the processed-mark");
	}

	[Fact]
	public async Task TransactionalInbox_ConcurrentDuplicates_ProcessExactlyOnce()
	{
		// N concurrent processors of the SAME messageId+handlerType: ON CONFLICT DO NOTHING + FOR UPDATE serialize
		// them so EXACTLY ONE processes (returns true) and its committed effect happens exactly once; the rest
		// observe the committed Processed state and return false without invoking the handler. Deterministic —
		// no wall-clock sleeps; correctness rests on the store's row-lock, not on timing.
		var transactional = (_store as IInboxStore) as ITransactionalInboxStore;
		_ = transactional.ShouldNotBeNull(
			"PostgresInboxStore must implement ITransactionalInboxStore for concurrent duplicate suppression.");

		var messageId = Guid.NewGuid().ToString();
		const string handlerType = "Handler.Type.ConcurrentDuplicate";
		var marker = Guid.NewGuid().ToString();
		const int concurrency = 8;

		var tasks = Enumerable.Range(0, concurrency)
			.Select(_ => Task.Run(async () =>
				await transactional.TryProcessTransactionallyAsync(
					messageId,
					handlerType,
					async (tx, ct) =>
					{
						_ = await tx.Connection!.ExecuteAsync(new CommandDefinition(
							$"INSERT INTO {SideTable} (marker) VALUES (@Marker)",
							new { Marker = marker },
							transaction: tx,
							cancellationToken: ct)).ConfigureAwait(false);
					},
					CancellationToken.None).AsTask()))
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		results.Count(processed => processed).ShouldBe(1,
			"exactly one concurrent processor wins the row-lock and processes the message; the rest see a duplicate");
		(await CountSideEffectsAsync(marker).ConfigureAwait(false)).ShouldBe(1,
			"the committed handler effect happens exactly once across all concurrent duplicate deliveries");
		(await _store.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("after concurrent duplicate suppression the message is durably marked processed");
	}
}
