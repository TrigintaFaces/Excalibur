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
		// SAFETY — replay provenance, in two halves. Tenant A dead-letters a message; an OPERATOR (no
		// tenant context) and then TENANT B attempt to replay it.
		//
		// The operator reaches it — inspection and replay are estate-wide on this admin surface — and must
		// execute it under the tenant STORED on the entry, never the ambient caller's. RED if replay
		// re-enters the ambient context (cross-tenant injection).
		//
		// Tenant B does not reach it at all: a tenant-scoped caller addresses only its own partition, so
		// another tenant's entry resolves as not found. RED if B can replay A's dead letter.
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

		// Operator: no ambient tenant at all, and it must SAY it wants the estate. ReplayAllTenantsEntryAsync
		// is the named operator seam; the entry is reached because the call site spelled the intent, not
		// because a scope was left unset. It must then run under the tenant STORED on the entry.
		var operatorReplayed = await QueueFor(null, Handler)
			.ReplayAllTenantsEntryAsync(entryId, CancellationToken.None)
			.ConfigureAwait(false);

		// A DIFFERENT tenant is ambient. It must not reach the entry AT ALL: a tenant-scoped caller
		// addresses only its own partition, so tenant A's entry resolves as not found.
		bool otherTenantReplayed;
		using (TenantContextHolder.BeginScope(TenantB))
		{
			otherTenantReplayed = await QueueFor(TenantB, Handler).ReplayAsync(entryId, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// NON-VACUITY, and it is load-bearing: `ShouldAllBe` is vacuously TRUE on an empty list, so a
		// replay path that silently never invoked the handler would satisfy the tenant assertion below
		// while proving nothing. The operator's replay must actually have run.
		operatorReplayed.ShouldBeTrue("an operator replays across the estate; the entry must be reachable");
		observed.Count.ShouldBe(1,
			"exactly one replay executes: the operator's. Observed " + observed.Count + " invocation(s).");

		// SAFETY 1 — replay re-enters the tenant STORED on the entry, never the caller's. Proven through
		// the operator, who carries no tenant at all: if replay used the ambient context the handler would
		// observe none, and if it used the caller's it would observe none here too. Only the stored tenant
		// produces TenantA.
		observed.ShouldAllBe(t => t == TenantA,
			"replay must re-enter the tenant STORED on the entry (" + TenantA + "), never the ambient " +
			"caller's. Observed: [" + string.Join(", ", observed.Select(t => t ?? "<none>")) + "]");

		// SAFETY 2 — and the stronger half. Tenant B does not get to replay tenant A's dead letter under
		// ANY context: it cannot address the entry, so the handler never runs for it.
		//
		// This arm previously expected B to replay it and merely land in A's context. That is a weaker
		// contract than the one implemented, which keeps another tenant's entry unaddressable rather than
		// executing it on their behalf, and it is the contract this surface documents: a tenant-scoped
		// caller supplies its partition and an entry outside it resolves as not found.
		otherTenantReplayed.ShouldBeFalse(
			"tenant B must not replay tenant A's dead letter — a scoped caller cannot address another " +
			"tenant's entry, so the replay reports not-found rather than executing it");
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

		// Estate-wide inspect, through the NAMED operator seam. An admin surface sees the entry regardless of
		// ambient tenant because the caller asked for the estate by name.
		var entries = await QueueFor(null).GetAllTenantsEntriesAsync(null, CancellationToken.None)
			.ConfigureAwait(false);
		entries.Select(e => e.Id).ShouldContain(entryId,
			"the estate-wide admin inspect path must surface the entry — a queue that returns nothing would " +
			"satisfy every cross-tenant SAFETY assertion while being completely inert");

		var replayed = await QueueFor(null, Handler)
			.ReplayAllTenantsEntryAsync(entryId, CancellationToken.None)
			.ConfigureAwait(false);

		replayed.ShouldBeTrue("the replay must actually run — an inert replay proves nothing");
		replayedUnder.ShouldBe(TenantA, "the round trip must land back in the originating tenant");
	}

	[Fact]
	public async Task RefuseTheScopedReadPathToAForeignTenantsEntryWhileTheNamedOperatorSeamReachesIt()
	{
		// This is the arm that was PARKED because the contract was not written down. It is written down now:
		// IDeadLetterQueue states it is tenant-scoped, and estate-wide inspection lives on the admin surface
		// under names that say so. The two halves are asserted against the SAME entry id so the difference
		// is attributable to the operation called and to nothing else.
		await EnsureSchemaAsync().ConfigureAwait(false);

		var entryId = await QueueFor(TenantA).EnqueueAsync(
			new OrderPayload("order-3"), DeadLetterReason.MaxRetriesExceeded, CancellationToken.None)
			.ConfigureAwait(false);

		// SAFETY — the SCOPED read refuses a foreign tenant's entry. Entries carry the failed message body,
		// so this is the half that must not regress: RED if the scoped path is ever widened back to the
		// estate, which is exactly what an unconditional predicate-less query would do.
		var scopedMiss = await QueueFor(TenantB).GetEntryAsync(entryId, CancellationToken.None)
			.ConfigureAwait(false);
		scopedMiss.ShouldBeNull(
			"a tenant-scoped caller must not read another tenant's dead-letter entry, body included");

		// LIVENESS — and the entry is genuinely there. Without this the assertion above is satisfied by a
		// store that has stopped returning anything to anyone, which is the failure the scoped read cannot
		// be allowed to hide behind.
		var operatorHit = await QueueFor(TenantB).GetAllTenantsEntryAsync(entryId, CancellationToken.None)
			.ConfigureAwait(false);
		operatorHit.ShouldNotBeNull(
			"the named estate-wide seam must reach the same entry — otherwise the miss above proves nothing");
		operatorHit.Id.ShouldBe(entryId);
	}

	// The arm above was PARKED while the tenancy contract for GetEntryAsync/ReplayAsync was undecided:
	// asserting either scoping or estate-wide reach would have manufactured a requirement by test. The
	// contract is now stated on the interface — tenant-scoped on IDeadLetterQueue, estate-wide under
	// explicitly named operations on IDeadLetterQueueAdmin — so the arm binds a written contract rather
	// than inventing one.

	/// <param name="tenantId">
	/// The caller's tenant, or <see langword="null"/> for an operator with no tenant established.
	/// </param>
	/// <param name="replayHandler">The handler invoked on replay.</param>
	/// <returns>A queue for that caller.</returns>
	private SqlServerDeadLetterQueue QueueFor(string? tenantId, Func<object, Task>? replayHandler = null) =>
		new(
			() => new SqlConnection(_fixture.ConnectionString),
			new SqlServerDeadLetterQueueOptions(),
			NullLogger<SqlServerDeadLetterQueue>.Instance,
			// The operator is modelled as the reserved untenanted partition: it is the term an absent
			// context used to resolve to, and it is the nearest constructible state now that the context is
			// required. It is NOT equivalent for these arms, and that is deliberately left visible rather
			// than papered over.
			//
			// The operator arms below need an ESTATE-WIDE read — they reach an entry stored under TenantA —
			// and they no longer get it from this parameter. AmbientScope() returns the caller's partition
			// on every path and no value here yields null, which is deliberate: estate-wide reach must not
			// be obtainable by leaving something unset. The arms call the NAMED seams instead
			// (GetAllTenantsEntriesAsync / ReplayAllTenantsEntryAsync), which pass the null scope
			// themselves. So this parameter now decides only which partition a SCOPED call addresses.
			//
			// A context resolving null is still not the substitute: tenancy active and unresolved fails
			// closed with TenantRequiredException, which threw before the assertion was ever reached.
			tenantId is null
				? UntenantedTestTenantContext.Instance
				: new FixedTenantContext(tenantId),
			replayHandler);

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
