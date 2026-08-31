// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.Inbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Infrastructure;

#pragma warning disable CA2100 // SQL strings use a compile-time const table name in a test fixture.

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// kj847e (S868) — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-SQL-Server concurrency lock
/// for the <b>lease-aware</b> atomic-claim overload
/// <c>IClaimableInboxStore.TryAcquireLeaseAsync(messageId, handlerType, leaseDuration, ct)</c>. The CAS is a
/// <c>MERGE</c>/<c>UPDATE … WHERE</c> keyed on the nullable <c>LeaseExpiresAtUtc</c> column evaluated against
/// the <b>SQL Server clock</b> (<c>SYSUTCDATETIME()</c>): claim IFF <c>absent OR Received OR (Processing AND
/// leaseExpiry &lt; now)</c>, NEVER when terminal <see cref="InboxStatus.Processed"/>.
/// </summary>
/// <remarks>
/// SQL Server had no <c>*ClaimAtomicityShould</c> sibling — this lock fills that gap AND adds the lease
/// dimension. It hand-rolls an isolated table (so the shared conformance fixture is untouched) carrying the
/// pinned <c>LeaseExpiresAtUtc datetime2(3) NULL</c> column. Reclaim is proven with a short lease + real
/// elapsed time (bounded poll, lower-bound only — no faked clock; per <c>verify-against-real-infra</c>).
/// <b>RED on a no-lease impl</b> (inherits the interface's <see cref="System.NotSupportedException"/>
/// default, or a claim-IFF-absent override): an expired <see cref="InboxStatus.Processing"/> entry never
/// becomes reclaimable ⇒ the poll times out ⇒ RED.
/// <para>
/// <b>SEAM (Backend verbatim, kj847e):</b> column <c>LeaseExpiresAtUtc datetime2(3) NULL</c>. Reconcile
/// against BackendDeveloper's landed CAS SQL before integration (F-5 stale-DDL guard).
/// </para>
/// </remarks>
[Collection(SqlServerInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Inbox")]
public sealed class SqlServerInboxStoreLeaseReclaimShould : IClassFixture<SqlServerInboxStoreContainerFixture>
{
	private const string SchemaName = "dbo";
	private const string TableName = "inbox_lease_reclaim_test";
	private const int Concurrency = 16;
	private static readonly TimeSpan ShortLease = TimeSpan.FromMilliseconds(250);
	private static readonly TimeSpan LongLease = TimeSpan.FromSeconds(30);

	private readonly SqlServerInboxStoreContainerFixture _fixture;

	public SqlServerInboxStoreLeaseReclaimShould(SqlServerInboxStoreContainerFixture fixture)
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
			.Select(_ => Task.Run(() => store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, CancellationToken.None).AsTask()))
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		results.Count(claimed => claimed is not null).ShouldBe(
			1,
			$"the lease CAS must admit exactly one of {Concurrency} concurrent claims; got [{string.Join(",", results)}]");
	}

	[Fact]
	public async Task Deny_a_second_claimer_while_the_lease_is_live()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "msg-lease-live";
		const string handlerType = "TestHandler";

		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("first caller acquires the lease");
		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeNull("a live lease must deny a concurrent second claim (no double-processing)");
	}

	[Fact]
	public async Task Reclaim_the_message_after_the_lease_expires()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "msg-lease-expire";
		const string handlerType = "TestHandler";

		// Mark BEFORE the acquiring claim. The lease is stamped at the SERVER's SYSUTCDATETIME() *during*
		// that round trip, i.e. at or after this mark — so elapsed-from-here is a conservative UPPER bound
		// on how much of the lease has burned by the time the denial below is evaluated. Bounding it in
		// that direction is what keeps the inconclusive guard honest: it can only ever over-estimate the
		// burn, never under-estimate it into a false "the arm discriminated".
		var sinceLeaseAcquired = Stopwatch.StartNew();

		(await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("the dead processor acquires the initial lease");

		var secondClaimWhileLeaseShouldBeLive =
			await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false);
		var elapsedAcquireToDenial = sinceLeaseAcquired.Elapsed;

		// This denial is the DISCRIMINATOR for the reclaim arm below: without it, a no-lease-enforcement
		// impl (every Processing entry claimable) would sail through the reclaim poll on its first attempt
		// and the whole test would pass vacuously. So it must stay — but it is a SAFETY arm, and a safety
		// arm is only meaningful if its observation demonstrably landed INSIDE the window it asserts. Two
		// SQL Server round trips under CI load can exceed a 250 ms lease, in which case the lease had
		// genuinely expired and a successful second claim is CORRECT behaviour, not the defect this arm
		// hunts. When the measurement cannot tell those apart, say so instead of accusing the product.
		if (secondClaimWhileLeaseShouldBeLive is not null && elapsedAcquireToDenial >= ShortLease)
		{
			Assert.Fail(
				$"INCONCLUSIVE — this SAFETY arm could not run, and this is NOT a product-defect report. The "
				+ $"acquire→re-claim round trip took {elapsedAcquireToDenial.TotalMilliseconds:F0} ms, which "
				+ $"already reaches the {ShortLease.TotalMilliseconds:F0} ms lease, so a successful second "
				+ $"claim here is equally explained by a lease that legitimately expired under load and by a "
				+ $"lease CAS that never enforced expiry at all. The arm cannot discriminate; re-run on a less "
				+ $"loaded host. Deliberately NOT fixed by lengthening the lease — that would only make this "
				+ $"rarer, not correct.");
		}

		secondClaimWhileLeaseShouldBeLive.ShouldBeNull(
			"the lease is still live immediately after it was taken"
			+ $" (measured acquire→re-claim elapsed: {elapsedAcquireToDenial.TotalMilliseconds:F0} ms — inside"
			+ $" the {ShortLease.TotalMilliseconds:F0} ms lease, so this arm DID discriminate: the CAS admitted"
			+ " a claim against a lease that was still live, which is the no-lease-enforcement defect.)");

		// RED on a no-lease impl: an expired Processing entry never becomes reclaimable ⇒ this times out.
		// Already polled (bounded, lower-bound only) — the liveness direction only needs a generous budget,
		// so extra latency costs polls rather than a red. Left as-is deliberately.
		var reclaimed = await WaitHelpers.WaitUntilAsync(
			async () => await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false) is not null,
			timeout: TimeSpan.FromSeconds(10),
			pollInterval: TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		reclaimed.ShouldBeTrue(
			"an expired lease must let a new processor reclaim the abandoned message (SYSUTCDATETIME expiry)");
	}

	[Fact]
	public async Task Never_reclaim_a_terminal_processed_message()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "msg-lease-processed";
		const string handlerType = "TestHandler";

		(await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("claim the message for processing");
		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);

		await Task.Delay(ShortLease + TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);

		(await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeNull("a completed (Processed) message must never be reclaimed via the lease path");
	}

	// d2afxn: a Failed entry is RE-ADMITTABLE on redelivery (retry) — the lease CAS admission predicate
	// must include Failed, matching the non-lease fallback path. RED on the pre-fix predicate
	// (absent | Received | expired-Processing) which denies Failed → TryClaim false → silent drop.
	[Fact]
	public async Task Readmit_and_retry_a_failed_entry_on_redelivery()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "msg-lease-failed-readmit";
		const string handlerType = "TestHandler";

		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("the initial claim acquires the lease");
		await store.MarkFailedAsync(messageId, handlerType, "handler boom", CancellationToken.None).ConfigureAwait(false);

		var afterFail = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		afterFail.ShouldNotBeNull();
		afterFail!.Status.ShouldBe(InboxStatus.Failed);

		// The core d2afxn AC: redelivery of a Failed entry must be re-admitted, not dropped as a duplicate.
		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("a Failed entry MUST be re-admittable on redelivery (at-least-once + idempotent-handler contract)");

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

	private async Task<SqlServerInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — real-infra lease lock is never skipped.");

		await EnsureTableAsync().ConfigureAwait(false);

		var options = Options.Create(new SqlServerInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = SchemaName,
			TableName = TableName,
		});
		return new SqlServerInboxStore(options, NullLogger<SqlServerInboxStore>.Instance, SingleTenantTestContext.Instance, Options.Create(new TenantContextOptions()));
	}

	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// SEAM: LeaseExpiresAtUtc is the pinned lease column (Backend verbatim, kj847e). Mirrors the canonical
		// inbox_messages columns + the additive nullable lease column. Reconcile VERBATIM against Backend's
		// landed CAS SQL before integration (F-5 stale-DDL "Invalid column" guard).
		var sql = $"""
			IF OBJECT_ID('[{SchemaName}].[{TableName}]', 'U') IS NOT NULL DROP TABLE [{SchemaName}].[{TableName}];
			CREATE TABLE [{SchemaName}].[{TableName}] (
				[MessageId]        NVARCHAR(255)  NOT NULL,
				[HandlerType]      NVARCHAR(500)  NOT NULL,
				[MessageType]      NVARCHAR(500)  NOT NULL,
				[Payload]          VARBINARY(MAX) NOT NULL,
				[Metadata]         NVARCHAR(MAX)  NULL,
				[ReceivedAt]       DATETIMEOFFSET NOT NULL,
				[ProcessedAt]      DATETIMEOFFSET NULL,
				[Status]           INT            NOT NULL DEFAULT 0,
				[LastError]        NVARCHAR(MAX)  NULL,
				[RetryCount]       INT            NOT NULL DEFAULT 0,
				[LastAttemptAt]    DATETIMEOFFSET NULL,
				[NextAttemptAt]    DATETIMEOFFSET NULL,
				[CorrelationId]    NVARCHAR(255)  NULL,
				[Source]           NVARCHAR(255)  NULL,
				[LeaseExpiresAtUtc] DATETIME2(3)  NULL,
				CONSTRAINT [PK_{TableName}] PRIMARY KEY CLUSTERED ([MessageId], [HandlerType])
			);
			""";

		await using var command = new SqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
