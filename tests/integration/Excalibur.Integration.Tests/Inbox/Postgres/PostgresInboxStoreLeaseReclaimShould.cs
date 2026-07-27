// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Postgres;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Tests.Shared.Infrastructure;

#pragma warning disable CA2100 // SQL strings use a compile-time const table name in a test fixture.

namespace Excalibur.Integration.Tests.Inbox.Postgres;

/// <summary>
/// kj847e (S868) — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-Postgres concurrency lock for
/// the <b>lease-aware</b> atomic-claim overload
/// <c>IClaimableInboxStore.TryClaimAsync(messageId, handlerType, leaseDuration, ct)</c>. Converges the
/// full-inbox TOCTOU onto a store-clock CAS: claim IFF <c>absent OR Received OR (Processing AND
/// leaseExpiry &lt; now)</c>, and <b>NEVER</b> when terminal <see cref="InboxStatus.Processed"/>.
/// </summary>
/// <remarks>
/// <para>
/// Complements the existing <see cref="PostgresInboxStoreClaimAtomicityShould"/> (the no-auto-expiry 2-arg
/// overload, which STAYS): this lock binds the additive 3-arg lease overload. Per
/// <c>verify-against-real-infra</c> the claim primitive's atomicity + expiry is provable only against a real
/// database — the CAS uses the <b>store's own server clock</b> (<c>now()</c>), never an app-side clock, so a
/// mocked store cannot certify it.
/// </para>
/// <para>
/// <b>Determinism (no faked clock):</b> the reclaim invariant is exercised with a <b>short lease</b> and
/// <b>real elapsed time</b> (poll past expiry, bounded). Assertions are lower-bounds / eventual-truth, never
/// upper-bound timing, so load only lengthens the poll — it cannot flip the outcome.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on a no-lease impl):</b> an overload that ignores <c>leaseDuration</c> and behaves
/// like the 2-arg claim (claim IFF absent) leaves an expired <see cref="InboxStatus.Processing"/> entry
/// permanently un-reclaimable ⇒ the reclaim poll times out ⇒ RED. GREEN only on the store-clock lease CAS.
/// </para>
/// <para>
/// <b>SEAM (SA pin, kj847e):</b> signature <c>ValueTask&lt;bool&gt; TryClaimAsync(string, string, TimeSpan,
/// CancellationToken)</c>; column <c>lease_expires_at timestamptz NULL</c> (nullable absolute-UTC expiry).
/// DDL below mirrors that pin — reconcile VERBATIM against BackendDeveloper's landed store skeleton before
/// integration (F-5 stale-DDL guard).
/// </para>
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Inbox")]
public sealed class PostgresInboxStoreLeaseReclaimShould : IClassFixture<PostgresEventStoreContainerFixture>
{
	private const string TableName = "inbox_lease_reclaim_test";
	private const int Concurrency = 16;
	private static readonly TimeSpan ShortLease = TimeSpan.FromMilliseconds(250);
	private static readonly TimeSpan LongLease = TimeSpan.FromSeconds(30);

	private readonly PostgresEventStoreContainerFixture _fixture;

	public PostgresInboxStoreLeaseReclaimShould(PostgresEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_exactly_one_lease_claim_when_concurrent_callers_race_the_same_message()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "msg-lease-concurrent";
		const string handlerType = "TestHandler";

		var tasks = Enumerable.Range(0, Concurrency)
			.Select(_ => Task.Run(() => store.TryClaimAsync(messageId, handlerType, LongLease, CancellationToken.None).AsTask()))
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		results.Count(claimed => claimed).ShouldBe(
			1,
			$"the lease CAS must admit exactly one of {Concurrency} concurrent claims; got [{string.Join(",", results)}]");
	}

	[Fact]
	public async Task Deny_a_second_claimer_while_the_lease_is_live()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "msg-lease-live";
		const string handlerType = "TestHandler";

		(await store.TryClaimAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("first caller acquires the lease");

		// A live (unexpired) lease held by another processor must deny a second claim.
		(await store.TryClaimAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a live lease must deny a concurrent second claim (no double-processing)");
	}

	[Fact]
	public async Task Reclaim_the_message_after_the_lease_expires()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "msg-lease-expire";
		const string handlerType = "TestHandler";

		// A dead processor takes the lease and never releases it (simulates a crash mid-processing).
		(await store.TryClaimAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("the dead processor acquires the initial lease");

		// Before expiry the lease is live ⇒ denied. (Immediately after the claim above.)
		(await store.TryClaimAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("the lease is still live immediately after it was taken");

		// After the lease expires (real elapsed time, store server clock), a new processor reclaims it.
		// RED on a no-lease impl: an expired Processing entry never becomes reclaimable ⇒ this times out.
		var reclaimed = await WaitHelpers.WaitUntilAsync(
			async () => await store.TryClaimAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false),
			timeout: TimeSpan.FromSeconds(10),
			pollInterval: TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		reclaimed.ShouldBeTrue(
			"an expired lease must let a new processor reclaim the abandoned message (store-clock expiry)");
	}

	[Fact]
	public async Task Never_reclaim_a_terminal_processed_message()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "msg-lease-processed";
		const string handlerType = "TestHandler";

		(await store.TryClaimAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("claim the message for processing");
		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);

		// Wait past the lease window: a terminal Processed entry must NEVER be reclaimed, even once the
		// original lease would have expired (Processed is terminal, not Processing-expired).
		await Task.Delay(ShortLease + TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);

		(await store.TryClaimAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a completed (Processed) message must never be reclaimed via the lease path");
	}

	// d2afxn: a Failed entry is RE-ADMITTABLE on redelivery (retry). RED on the pre-fix predicate
	// (absent | Received | expired-Processing) which denies Failed → TryClaim false → silent drop.
	[Fact]
	public async Task Readmit_and_retry_a_failed_entry_on_redelivery()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "msg-lease-failed-readmit";
		const string handlerType = "TestHandler";

		(await store.TryClaimAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("the initial claim acquires the lease");
		await store.MarkFailedAsync(messageId, handlerType, "handler boom", CancellationToken.None).ConfigureAwait(false);

		var afterFail = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		afterFail.ShouldNotBeNull();
		afterFail!.Status.ShouldBe(InboxStatus.Failed);

		(await store.TryClaimAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("a Failed entry MUST be re-admittable on redelivery (at-least-once + idempotent-handler contract)");

		var afterReclaim = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		afterReclaim.ShouldNotBeNull();
		afterReclaim!.Status.ShouldBe(
			InboxStatus.Processing,
			"re-admitting a Failed entry transitions it back to Processing under a fresh lease");

		// d2afxn monotonic-RetryCount guarantee (SA-confirmed preserve-only design): re-admit PRESERVES the
		// retry history (never resets to 0); RetryCount increments exactly once per failed attempt at the shared
		// finalize. Impl-agnostic monotonic assertion — non-decreasing across re-admit, strictly greater after a
		// second failed attempt. RED on a reset-to-0 re-admit.
		var retriesAfterFirstFail = afterFail!.RetryCount;
		retriesAfterFirstFail.ShouldBeGreaterThanOrEqualTo(1, "the first failed attempt must record at least one retry");
		afterReclaim.RetryCount.ShouldBeGreaterThanOrEqualTo(
			retriesAfterFirstFail,
			"re-admitting a Failed entry must PRESERVE the retry count (never reset it to 0)");

		await store.MarkFailedAsync(messageId, handlerType, "handler boom again", CancellationToken.None).ConfigureAwait(false);

		var afterSecondFail = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		afterSecondFail.ShouldNotBeNull();
		afterSecondFail!.RetryCount.ShouldBeGreaterThan(
			retriesAfterFirstFail,
			"RetryCount MUST be monotonic across re-admits — a second failed attempt strictly increases it, never resets");
	}

	private async Task<PostgresInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — real-infra lease lock is never skipped.");

		await EnsureTableAsync().ConfigureAwait(false);

		var connectionString = _fixture.ConnectionString;
		var options = new PostgresInboxOptions
		{
			ConnectionString = connectionString,
			SchemaName = "public",
			TableName = TableName,
		};
		return new PostgresInboxStore(
			() => new NpgsqlConnection(connectionString),
			options,
			NullLogger<PostgresInboxStore>.Instance);
	}

	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// SEAM: lease_expires_at is the pinned lease column (SA kj847e). Reconcile VERBATIM against
		// BackendDeveloper's landed Postgres skeleton before integration — a name/type divergence here
		// surfaces as a runtime "column does not exist" only on this shard (F-5 stale-DDL guard).
		const string sql = $"""
			DROP TABLE IF EXISTS public.{TableName};
			CREATE TABLE public.{TableName} (
				message_id VARCHAR(255) NOT NULL,
				handler_type VARCHAR(255) NOT NULL,
				message_type VARCHAR(255) NOT NULL,
				payload BYTEA NOT NULL,
				metadata JSONB,
				received_at TIMESTAMPTZ NOT NULL,
				processed_at TIMESTAMPTZ,
				status INT NOT NULL,
				retry_count INT NOT NULL,
				correlation_id VARCHAR(255),
				source VARCHAR(255),
				last_error TEXT,
				last_attempt_at TIMESTAMPTZ,
				lease_expires_at TIMESTAMPTZ NULL,
				PRIMARY KEY (message_id, handler_type)
			);
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
