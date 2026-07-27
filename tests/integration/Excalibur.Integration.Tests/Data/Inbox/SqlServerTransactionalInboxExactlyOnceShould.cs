// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Inbox.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

#pragma warning disable CA2100 // SQL strings are safe - schema/table names are constants in test fixture

#pragma warning disable CA1812 // Instantiated by xUnit.

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// vpdiys (S872, 02sj2h) — real-infrastructure crash-consistency lock for the transactional inbox seam
/// <see cref="ITransactionalInboxStore"/> against a live SQL Server container. A transactional store gives
/// EXACTLY-ONCE processing: the handler runs and the message is marked processed inside a single enlisting
/// transaction, so a crash mid-processing neither drops nor double-applies the handler effect.
/// </summary>
/// <remarks>
/// <para>
/// NEVER SKIPPED — when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. The lock asserts EMITTED BEHAVIOR through the real seam (the durable
/// processed-mark and the handler-invocation count under a simulated crash + redelivery + duplicate), not
/// the mere presence of the capability interface.
/// </para>
/// <para>
/// NON-VACUOUS: RED against the pre-seam baseline. <see cref="SqlServerInboxStore"/> does not yet implement
/// <see cref="ITransactionalInboxStore"/> (it currently exposes the at-least-once claim protocol only), so
/// the capability cast below fails fast until Backend wires the transactional impl. It is also RED against a
/// non-atomic impl that marks the message processed even though the handler threw (the crash would then drop
/// the effect), or that re-invokes the handler on a duplicate delivery.
/// </para>
/// </remarks>
[Collection(SqlServerInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerTransactionalInboxExactlyOnceShould : IAsyncLifetime
{
	// A small side table the handler writes to ENLISTED in the store's transaction, so the test can prove the
	// handler's own data write commits/rolls back ATOMICALLY with the processed-mark (the headline atomicity proof).
	private const string SideTable = "[dbo].[inbox_txn_side_effect]";

	private readonly SqlServerInboxStoreContainerFixture _fixture;
	private SqlServerInboxStore _store = null!;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerTransactionalInboxExactlyOnceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared SQL Server inbox container fixture.</param>
	public SqlServerTransactionalInboxExactlyOnceShould(SqlServerInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - real-infra exactly-once lock is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var options = Options.Create(new SqlServerInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = _fixture.SchemaName,
			TableName = _fixture.TableName,
		});

		_store = new SqlServerInboxStore(options, NullLogger<SqlServerInboxStore>.Instance);

		await EnsureSideTableAsync().ConfigureAwait(false);
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
			 IF OBJECT_ID('{SideTable}', 'U') IS NULL
			 	CREATE TABLE {SideTable} ([Marker] NVARCHAR(255) NOT NULL);
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
			$"SELECT COUNT(*) FROM {SideTable} WHERE [Marker] = @Marker",
			new { Marker = marker }).ConfigureAwait(false);
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

	[Fact]
	public async Task TransactionalInbox_CrashMidProcessing_RedeliversWithoutDuplicatingCommittedEffect()
	{
		// The store must expose the transactional capability — RED until Backend wires
		// ITransactionalInboxStore onto SqlServerInboxStore (the pre-seam baseline).
		var transactional = (_store as IInboxStore) as ITransactionalInboxStore;
		_ = transactional.ShouldNotBeNull(
			"SqlServerInboxStore must implement ITransactionalInboxStore to provide exactly-once processing " +
			"(a transactional backing engine enlists the handler + processed-mark in one transaction).");

		var messageId = Guid.NewGuid().ToString();
		const string handlerType = "Handler.Type.ExactlyOnce";
		var handlerInvocations = 0;

		// Delivery 1 — the handler CRASHES mid-processing (throws). Under exactly-once the whole
		// transaction rolls back. The contract pins the DURABLE outcome (nothing marked processed → the
		// message stays redeliverable); it does not specify whether the failure rethrows or is returned, so
		// this tolerates either signal and asserts the rolled-back state.
		try
		{
			_ = await transactional.TryProcessTransactionallyAsync(
				messageId,
				handlerType,
				(_, _) =>
				{
					Interlocked.Increment(ref handlerInvocations);
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

		// Delivery 2 — redelivery, handler succeeds. The processed-mark commits atomically with the effect.
		var processedOnRetry = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			(_, _) =>
			{
				Interlocked.Increment(ref handlerInvocations);
				return ValueTask.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		processedOnRetry.ShouldBeTrue("the redelivered message is processed for the first successful time");
		(await _store.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("after a successful transactional process the message is durably marked processed");

		// Delivery 3 — a DUPLICATE of the already-processed message. The handler must NOT run again.
		var processedOnDuplicate = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			(_, _) =>
			{
				Interlocked.Increment(ref handlerInvocations);
				return ValueTask.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		processedOnDuplicate.ShouldBeFalse("an already-processed message is a duplicate — the handler is not invoked");

		// End-to-end exactly-once: the handler produced its committed effect exactly ONCE (the crashed
		// attempt rolled back, the duplicate never ran). The crash attempt invoked the delegate but rolled
		// back; only the single successful attempt produced a committed effect.
		handlerInvocations.ShouldBe(2,
			"the handler delegate ran on the crash attempt (rolled back) and once successfully — never on the duplicate");
	}

	[Fact]
	public async Task TransactionalInbox_HandlerWriteEnlistedInTx_RollsBackAtomicallyWithProcessedMark()
	{
		// The headline atomicity proof: a REAL data write the handler performs ENLISTED in the store's
		// transaction must roll back together with the processed-mark when the handler then throws — proving
		// the handler effect + the processed-mark are one atomic unit. RED on a non-transactional impl, which
		// would leave the side-table write committed even though nothing was marked processed.
		var transactional = (_store as IInboxStore) as ITransactionalInboxStore;
		_ = transactional.ShouldNotBeNull(
			"SqlServerInboxStore must implement ITransactionalInboxStore to provide atomic handler+mark commits.");

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
					// Enlist the write in the store's transaction: same connection, transaction: tx.
					_ = await tx.Connection!.ExecuteAsync(new CommandDefinition(
						$"INSERT INTO {SideTable} ([Marker]) VALUES (@Marker)",
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
					$"INSERT INTO {SideTable} ([Marker]) VALUES (@Marker)",
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
		// N concurrent processors of the SAME messageId+handlerType: the UPDLOCK/HOLDLOCK range-lock serializes
		// them so EXACTLY ONE processes (returns true) and its committed effect happens exactly once; the rest
		// observe the committed Processed state and return false without invoking the handler. Deterministic —
		// no wall-clock sleeps; correctness rests on the store's range-lock, not on timing.
		var transactional = (_store as IInboxStore) as ITransactionalInboxStore;
		_ = transactional.ShouldNotBeNull(
			"SqlServerInboxStore must implement ITransactionalInboxStore for concurrent duplicate suppression.");

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
							$"INSERT INTO {SideTable} ([Marker]) VALUES (@Marker)",
							new { Marker = marker },
							transaction: tx,
							cancellationToken: ct)).ConfigureAwait(false);
					},
					CancellationToken.None).AsTask()))
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		results.Count(processed => processed).ShouldBe(1,
			"exactly one concurrent processor wins the range-lock and processes the message; the rest see a duplicate");
		(await CountSideEffectsAsync(marker).ConfigureAwait(false)).ShouldBe(1,
			"the committed handler effect happens exactly once across all concurrent duplicate deliveries");
		(await _store.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("after concurrent duplicate suppression the message is durably marked processed");
	}
}
