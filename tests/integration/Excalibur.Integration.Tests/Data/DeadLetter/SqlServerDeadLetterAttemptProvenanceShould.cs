// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.Data.DeadLetter;

/// <summary>
/// Real-SqlServer lock on the attempt count a dead-letter entry reports.
/// </summary>
/// <remarks>
/// <para>
/// <c>DeadLetterEntry.OriginalAttempts</c> is documented as the number of processing attempts before the
/// message was dead-lettered. The store must therefore record what its CALLER observed, which arrives in
/// the enqueue metadata under <c>Attempts</c>, and must never substitute a value of its own.
/// </para>
/// <para>
/// <b>Why this is a durable lock and not a spot check.</b> The count is public, projected on every read,
/// and filterable through <c>DeadLetterQueryFilter.MinAttempts</c>, which renders
/// <c>OriginalAttempts &gt;= @MinAttempts</c>. A store-side constant therefore does not merely misreport a
/// row: it makes every <c>MinAttempts</c> query above that constant return an empty set on every
/// deployment, and an operator asking which messages failed repeatedly reads that emptiness as an answer.
/// Nothing in the result distinguishes it from "no message failed repeatedly".
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Every arm uses an attempt count that is neither 1 nor 0, because those are the two
/// values a store can produce without consulting its caller — 1 was the fabricated literal, 0 is the
/// column's declared default and the deliberate "not stated" value. A fixture enqueueing a single message
/// at one attempt cannot tell a correct store from a fabricating one: they agree. Seven and nine disagree
/// with both.
/// </para>
/// <para>
/// <b>Real infrastructure, never skipped.</b> The <c>MinAttempts</c> arm asserts a predicate the engine
/// evaluates, over values the engine stored through a typed column. A mocked connection returns whatever
/// it was told and would certify a fabricating store as correct.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerDeadLetterTestCollection.CollectionName)]
public sealed class SqlServerDeadLetterAttemptProvenanceShould(SqlServerContainerFixture fixture)
{
	private const string Tenant = "tenant-attempts";

	private readonly SqlServerContainerFixture _fixture = fixture;

	[Fact]
	public async Task RecordTheAttemptCountItsCallerObserved()
	{
		// The count under test is 7: not 1 (the value a store fabricates), not 0 (the value a store writes
		// when its caller said nothing). Only a store that read the caller's metadata can produce it.
		await EnsureSchemaAsync().ConfigureAwait(false);

		var queue = QueueFor(Tenant);
		var id = await queue.EnqueueAsync(
			new OrderPayload("order-attempts"),
			DeadLetterReason.MaxRetriesExceeded,
			CancellationToken.None,
			metadata: Metadata(attempts: "7"))
			.ConfigureAwait(false);

		var entry = await queue.GetEntryAsync(id, CancellationToken.None).ConfigureAwait(false);

		entry.ShouldNotBeNull("the entry must be readable — an unreadable entry proves nothing about its count");
		entry.OriginalAttempts.ShouldBe(
			7,
			"OriginalAttempts is documented as the attempts made before dead-lettering and must carry the "
			+ "caller's observation, not a value the store chose. Observed " + entry.OriginalAttempts);

		// The sibling keys mined from the same dictionary are asserted alongside it, so a regression that
		// breaks metadata mining wholesale is attributable to that rather than to this column alone.
		entry.CorrelationId.ShouldBe("corr-attempts");
		entry.SourceQueue.ShouldBe("queue-attempts");
	}

	/// <summary>
	/// Pins the ENCODING chosen for an unstated attempt count, and says what that encoding cannot express.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Zero is the value, and it is chosen because it is this column's own declared default
	/// (<c>INT NOT NULL DEFAULT 0</c>) and because it never asserts that an attempt happened. One is the
	/// value that must NOT appear: it is a claim about the message, manufactured by the store.
	/// </para>
	/// <para>
	/// <b>What this arm does NOT establish.</b> The column is <c>NOT NULL</c>, so zero carries two meanings
	/// at once — "nobody told us" and "dead-lettered before its first charged attempt", which the
	/// first-encounter decode path genuinely produces. These arms pin the encoding; they cannot distinguish
	/// the meanings, and nothing downstream can either. A consumer reading zero learns that no attempt was
	/// stated, never that no attempt occurred. Separating the two needs a nullable column and is not done
	/// here, so this limitation is a property of the contract rather than a gap in the test.
	/// </para>
	/// </remarks>
	/// <param name="attempts">The <c>Attempts</c> metadata value, or <see langword="null"/> to omit the key.</param>
	[Theory]
	[InlineData(null)]          // no Attempts key at all
	[InlineData("")]            // present and empty
	[InlineData("not-a-number")] // present and unparseable
	[InlineData("-3")]          // present and negative — parses, but is not a count
	public async Task RecordZeroAttempts_WhenTheCallerStatesNoUsableCount(string? attempts)
	{
		await EnsureSchemaAsync().ConfigureAwait(false);

		var queue = QueueFor(Tenant);
		var id = await queue.EnqueueAsync(
			new OrderPayload("order-unstated"),
			DeadLetterReason.MaxRetriesExceeded,
			CancellationToken.None,
			metadata: Metadata(attempts))
			.ConfigureAwait(false);

		var entry = await queue.GetEntryAsync(id, CancellationToken.None).ConfigureAwait(false);

		entry.ShouldNotBeNull();
		entry.OriginalAttempts.ShouldBe(
			0,
			"an absent or unusable attempt count must be recorded as the column's own 'not stated' default, "
			+ "never as a fabricated count of 1. Observed " + entry.OriginalAttempts + " for input "
			+ (attempts is null ? "<no key>" : "'" + attempts + "'"));
	}

	[Fact]
	public async Task PartitionEntriesByMinAttempts_TheConsumerVisibleHalf()
	{
		// This is the arm an operator's question actually travels through. MinAttempts renders
		// OriginalAttempts >= @MinAttempts against stored values, so a store-side constant collapses the
		// predicate: with every row at 1, MinAttempts = 5 returns nothing and MinAttempts = 1 returns
		// everything. Neither answer is about the messages.
		await EnsureSchemaAsync().ConfigureAwait(false);

		var queue = QueueFor(Tenant);
		var twoAttempts = await queue.EnqueueAsync(
			new OrderPayload("order-few"), DeadLetterReason.MaxRetriesExceeded, CancellationToken.None,
			metadata: Metadata("2")).ConfigureAwait(false);
		var nineAttempts = await queue.EnqueueAsync(
			new OrderPayload("order-many"), DeadLetterReason.MaxRetriesExceeded, CancellationToken.None,
			metadata: Metadata("9")).ConfigureAwait(false);

		// SAFETY — the threshold excludes the entry below it. RED on a fabricating store, which returns
		// both rows (constant 1 >= 1) or neither (constant 1 < 5) depending on the threshold, but never a
		// partition that follows the messages.
		var repeated = await queue.GetEntriesAsync(
			CancellationToken.None, new DeadLetterQueryFilter { MinAttempts = 5 }).ConfigureAwait(false);
		repeated.Select(e => e.Id).ShouldBe(
			[nineAttempts],
			ignoreOrder: true,
			customMessage: "MinAttempts = 5 must select the 9-attempt entry and exclude the 2-attempt one");

		// LIVENESS — and the threshold is not simply swallowing everything. Without this arm, a store that
		// returned the empty set to every query would satisfy the assertion above.
		var all = await queue.GetEntriesAsync(
			CancellationToken.None, new DeadLetterQueryFilter { MinAttempts = 2 }).ConfigureAwait(false);
		all.Select(e => e.Id).ShouldBe(
			[twoAttempts, nineAttempts],
			ignoreOrder: true,
			customMessage: "MinAttempts = 2 must select both entries — an inert query that returns nothing "
			+ "would pass the exclusion assertion above while proving nothing");
	}

	private static Dictionary<string, string> Metadata(string? attempts)
	{
		// The shape the production callers send: MessageType, CorrelationId, CausationId and SourceQueue are
		// all mined from this one dictionary, so Attempts is asserted in the company it actually travels in.
		var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["CorrelationId"] = "corr-attempts",
			["CausationId"] = "caus-attempts",
			["SourceQueue"] = "queue-attempts",
		};

		if (attempts is not null)
		{
			metadata["Attempts"] = attempts;
		}

		return metadata;
	}

	private SqlServerDeadLetterQueue QueueFor(string tenantId) =>
		new(
			() => new SqlConnection(_fixture.ConnectionString),
			new SqlServerDeadLetterQueueOptions(),
			NullLogger<SqlServerDeadLetterQueue>.Instance,
			new FixedTenantContext(tenantId));

	private async Task EnsureSchemaAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the attempt count is a consumer-visible, filterable value — this real-SqlServer lock must never "
			+ "be skipped; a skipped lock is the gap that ships the defect");

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		// Mirrors Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql. OriginalAttempts is
		// INT NOT NULL DEFAULT 0, and that default is what makes 0 the schema's own "not stated". Kept in
		// lockstep with the shipped DDL (fixture DDL is a sibling artifact of a schema change).
		_ = await connection.ExecuteAsync("""
			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterQueue]') AND type = N'U')
			BEGIN
			    CREATE TABLE [dbo].[DeadLetterQueue] (
			        Id                     UNIQUEIDENTIFIER NOT NULL,
			        TenantId               NVARCHAR(255)   NOT NULL,
			        MessageType            NVARCHAR(500)   NOT NULL,
			        Payload                VARBINARY(MAX)  NOT NULL,
			        Reason                 INT             NOT NULL,
			        ExceptionMessage       NVARCHAR(MAX)   NULL,
			        ExceptionStackTrace    NVARCHAR(MAX)   NULL,
			        EnqueuedAt             DATETIMEOFFSET  NOT NULL DEFAULT SYSDATETIMEOFFSET(),
			        OriginalAttempts       INT             NOT NULL DEFAULT 0,
			        Metadata               NVARCHAR(MAX)   NULL,
			        CorrelationId          NVARCHAR(255)   NULL,
			        CausationId            NVARCHAR(255)   NULL,
			        SourceQueue            NVARCHAR(255)   NULL,
			        IsReplayed             BIT             NOT NULL DEFAULT 0,
			        ReplayedAt             DATETIMEOFFSET  NULL,
			        CONSTRAINT PK_DeadLetterQueue PRIMARY KEY (Id, TenantId)
			    );
			END
			""").ConfigureAwait(false);

		_ = await connection.ExecuteAsync("DELETE FROM [dbo].[DeadLetterQueue]").ConfigureAwait(false);
	}

	private sealed record OrderPayload(string OrderId);

	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}
}
