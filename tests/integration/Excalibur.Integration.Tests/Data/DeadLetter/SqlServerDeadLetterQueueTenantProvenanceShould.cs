// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// Real-SqlServer provenance lock for the dead-letter queue.
/// </summary>
/// <remarks>
/// <para>
/// The DLQ stores the originating tenant as provenance so a replay re-enters the SAME tenant it was
/// dead-lettered from. An operator (no tenant) or a different tenant replaying tenant A's entry must execute
/// under A's context, or replay becomes a cross-tenant injection vector.
/// </para>
/// <para>
/// <b>Real infrastructure, never skipped.</b> The predicates under test are evaluated by the real engine
/// against a real composite primary key: whether a half-key <c>WHERE Id = @Id</c> reaches another tenant's
/// row is decided by the database, not by the caller. A mocked connection returns whatever it was told and
/// would certify the defect as fixed.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerDeadLetterTestCollection.CollectionName)]
public sealed class SqlServerDeadLetterQueueTenantProvenanceShould(SqlServerContainerFixture fixture)
{
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly SqlServerContainerFixture _fixture = fixture;

	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}

	[Fact]
	public async Task ReEnterTheStoredTenant_WhenReplayedByAnOperatorOrAnotherTenant()
	{
		// SAFETY — replay provenance. Tenant A dead-letters a message; an OPERATOR (no tenant context) and then
		// TENANT B replay it. Both must execute the handler under tenant A's context — the tenant STORED on the
		// entry, never the ambient caller's. RED if replay re-enters the ambient context (cross-tenant injection).
		await EnsureSchemaAsync().ConfigureAwait(false);

		var observed = new List<string?>();
		Task Handler(object _)
		{
			observed.Add(TenantContextHolder.Current);
			return Task.CompletedTask;
		}

		var entryId = await QueueFor(TenantA).EnqueueAsync(
			new OrderPayload("order-1"), DeadLetterReason.MaxRetriesExceeded, CancellationToken.None)
			.ConfigureAwait(false);

		// Operator: no ambient tenant at all.
		_ = await QueueFor(null, Handler).ReplayAsync(entryId, CancellationToken.None).ConfigureAwait(false);

		// A DIFFERENT tenant is ambient.
		using (TenantContextHolder.BeginScope(TenantB))
		{
			_ = await QueueFor(TenantB, Handler).ReplayAsync(entryId, CancellationToken.None).ConfigureAwait(false);
		}

		// NON-VACUITY, and it is load-bearing: `ShouldAllBe` is vacuously TRUE on an empty list, so a replay
		// path that silently never invoked the handler at all would satisfy the tenant assertion below while
		// proving nothing. Both replays must actually have run. This arm carries the whole class's safety
		// weight now that the scoping arm is parked, so the count is asserted FIRST and separately.
		observed.Count.ShouldBe(2,
			"both replays (operator and tenant-B) must actually invoke the handler — an empty observation list " +
			"makes the tenant assertion below vacuously true. Observed " + observed.Count + " invocation(s).");

		observed.ShouldAllBe(t => t == TenantA,
			"replay must re-enter the tenant STORED on the entry (" + TenantA + "), never the ambient caller's — " +
			"an operator or another tenant replaying a dead letter must not execute it under their own context. " +
			"Observed: [" + string.Join(", ", observed.Select(t => t ?? "<none>")) + "]");
	}

	[Fact]
	public async Task RoundTripIntoTheSameTenant_WhenEnqueuedInspectedAndReplayed()
	{
		// LIVENESS — proves the safety arm is not satisfied by a queue that replays NOTHING (an inert store is
		// trivially "never cross-tenant"). The full admin journey must work end to end: enqueue under a tenant,
		// find the entry on the estate-wide inspect path, replay it, and land back in the SAME tenant.
		await EnsureSchemaAsync().ConfigureAwait(false);

		string? replayedUnder = null;
		Task Handler(object _)
		{
			replayedUnder = TenantContextHolder.Current;
			return Task.CompletedTask;
		}

		var entryId = await QueueFor(TenantA).EnqueueAsync(
			new OrderPayload("order-2"), DeadLetterReason.MaxRetriesExceeded, CancellationToken.None)
			.ConfigureAwait(false);

		// Estate-wide inspect: an admin surface sees the entry regardless of ambient tenant (by design).
		var entries = await QueueFor(null).GetEntriesAsync(CancellationToken.None, null).ConfigureAwait(false);
		entries.Select(e => e.Id).ShouldContain(entryId,
			"the estate-wide admin inspect path must surface the entry — a queue that returns nothing would " +
			"satisfy every cross-tenant SAFETY assertion while being completely inert");

		var replayed = await QueueFor(null, Handler).ReplayAsync(entryId, CancellationToken.None).ConfigureAwait(false);

		replayed.ShouldBeTrue("the replay must actually run — an inert replay proves nothing");
		replayedUnder.ShouldBe(TenantA, "the round trip must land back in the originating tenant");
	}

	// PARKED — arm 3 ("tenant B holding tenant A's entry id is refused on read/replay/purge") is
	// REMOVED, not deleted: its full body is preserved on the tracker. It asserted a tenancy contract
	// that IS NOT WRITTEN DOWN: `grep -ci "tenant" IDeadLetterQueue.cs` returns 0, so neither
	// "tenant-scoped" nor "estate-wide" is stated for GetEntryAsync/ReplayAsync. Asserting either one
	// here would hardcode an undecided contract into a lock and manufacture a requirement by test.
	// It returns — unchanged if scoping is ruled required — once the contract is DECIDED and WRITTEN
	// on the interface. Until then arms 1-2 below lock the one property that IS documented: replay
	// re-enters the STORED tenant, never the caller's.

	private SqlServerDeadLetterQueue QueueFor(string? tenantId, Func<object, Task>? replayHandler = null) =>
		new(
			() => new SqlConnection(_fixture.ConnectionString),
			new SqlServerDeadLetterQueueOptions(),
			NullLogger<SqlServerDeadLetterQueue>.Instance,
			replayHandler,
			new FixedTenantContext(tenantId));

	private async Task EnsureSchemaAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"dead-letter replay provenance is a cross-tenant security boundary — this real-SqlServer lock must " +
			"never be skipped; a skipped lock is the gap that ships the defect");

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		// Mirrors Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql — TenantId is NOT NULL with no
		// default and is part of the primary key. Kept in lockstep with the shipped DDL (F-5: fixture DDL is a
		// sibling artifact of a schema change).
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
}
