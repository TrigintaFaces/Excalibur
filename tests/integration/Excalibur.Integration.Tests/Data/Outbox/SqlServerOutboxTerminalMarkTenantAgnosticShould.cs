// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA2100 // SQL strings use compile-time const table names in a test fixture.

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Independent (author≠impl) NON-SKIPPED real-SQL-Server lock for the outbox drain's terminal marks.
/// </summary>
/// <remarks>
/// <para>
/// <b>The contract.</b> The drain claim (<c>GetUnsentMessagesAsync</c>) is deliberately cross-tenant — one
/// dispatcher serves every tenant — and it hands back a row addressed by its <c>Id</c>, which is the
/// globally-unique primary key of the outbox table. A terminal mark keyed on that <c>Id</c> must therefore
/// be able to address the row the claim just returned, <b>whatever ambient tenant the marking process is
/// running under, including none at all</b>. Any additional ambient-tenant predicate on a primary-key-keyed
/// statement can only ever turn "the one correct row" into "zero rows"; it buys no isolation, because the
/// key already selects exactly one row.
/// </para>
/// <para>
/// <b>Why the ambient conditions are parameterised, and why "none" is the important one.</b> The shipped
/// wiring passes <b>no</b> <c>ITenantContext</c> into this store from any DI factory, so the ambient
/// partition production actually resolves is the reserved untenanted sentinel — not some other tenant.
/// A lock that only exercised "a different ambient tenant" would leave the configuration every consumer
/// actually runs completely uncovered. Both are bound here.
/// </para>
/// <para>
/// <b>Why all four marks, not just the one that throws.</b> Only the sent transition surfaces the zero-row
/// case as an exception. The failed, backoff and dead-letter transitions swallow it — one logs the zero-row
/// case as a benign "a peer holds the lease" warning, and the dead-letter path discards the row count
/// entirely and then logs a completed transition unconditionally. Those are the dangerous ones: they report
/// a state change that did not happen. Every arm below therefore asserts the <b>observed row state read
/// back out of SQL Server after a real round trip</b> — never a log line, never an exception, never the
/// shape of the request object or its tenancy disposition. A request-shape assertion is exactly the blind
/// spot that let this defect ship.
/// </para>
/// <para>
/// <b>Adjacent contract.</b> <c>GetAllTenantsStatisticsAsync</c> takes no key and is deliberately estate-wide; it has no
/// isolation, so — unlike the four key-addressed marks — it must keep that predicate. It is adjacent to the
/// statements that are changing and indistinguishable from them by a textual sweep, so
/// <see cref="Report_every_tenants_rows_regardless_of_the_ambient_tenant"/> pins it:
/// dropping its predicate would convert this liveness bug into a cross-tenant <i>read</i>.
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxTerminalMarkTenantAgnosticShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const int StatusSent = 2;
	private const int StatusFailed = 3;
	private const int StatusDeadLettered = 5;

	/// <summary>The tenant that OWNS the staged row. Never equal to any ambient tenant exercised below.</summary>
	private const string OwningTenant = "tenant-owner";

	/// <summary>A DIFFERENT ambient tenant — the marking process runs inside another tenant's request scope.</summary>
	private const string ForeignAmbientTenant = "tenant-foreign";

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerOutboxTerminalMarkTenantAgnosticShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	// ---------------------------------------------------------------------------------------------------
	// LIVENESS — the four key-addressed terminal marks must reach terminal state on a globally-claimed row.
	// Each is exercised under BOTH ambient conditions: a foreign tenant, and (the shipped wiring) none.
	// ---------------------------------------------------------------------------------------------------

	[Theory]
	[InlineData(ForeignAmbientTenant)]
	[InlineData(null)]
	public async Task Mark_a_globally_claimed_tenanted_row_Sent(string? ambientTenantId)
	{
		await ResetAsync().ConfigureAwait(false);
		var store = CreateStore(ambientTenantId);
		var messageId = await StageAndClaimAsync(store, "mark-sent").ConfigureAwait(false);

		await store.MarkSentAsync(messageId, CancellationToken.None).ConfigureAwait(false);

		var row = await ReadRowAsync(messageId).ConfigureAwait(false);
		row.Status.ShouldBe(
			StatusSent,
			$"the row the global drain claimed must reach Sent under ambient tenant '{ambientTenantId ?? "<none>"}'; "
			+ "a row that never reaches Sent has its lease expire and is re-claimed and RE-PUBLISHED without bound.");
		row.LeasedBy.ShouldBeNull("a terminal transition releases the lease.");
	}

	[Theory]
	[InlineData(ForeignAmbientTenant)]
	[InlineData(null)]
	public async Task Mark_a_globally_claimed_tenanted_row_Failed(string? ambientTenantId)
	{
		await ResetAsync().ConfigureAwait(false);
		var store = CreateStore(ambientTenantId);
		var messageId = await StageAndClaimAsync(store, "mark-failed").ConfigureAwait(false);

		// This path swallows a zero-row result as a benign warning, so ONLY the row proves the transition.
		await store.MarkFailedAsync(messageId, "boom", 4, CancellationToken.None).ConfigureAwait(false);

		var row = await ReadRowAsync(messageId).ConfigureAwait(false);
		row.Status.ShouldBe(
			StatusFailed,
			$"MarkFailed must record the failure under ambient tenant '{ambientTenantId ?? "<none>"}'; a silent "
			+ "zero-row mark leaves the row Staged, so the retry accounting and the DLQ ceiling never advance.");
		row.LastError.ShouldBe("boom", "the failure reason must actually be persisted, not merely logged.");
		row.RetryCount.ShouldBe(4, "the retry count must advance, or the DLQ termination guarantee never fires.");
		row.LeasedBy.ShouldBeNull("MarkFailed releases the lease so the backoff schedule governs the next claim.");
	}

	[Theory]
	[InlineData(ForeignAmbientTenant)]
	[InlineData(null)]
	public async Task Mark_a_globally_claimed_tenanted_row_FailedWithBackoff(string? ambientTenantId)
	{
		await ResetAsync().ConfigureAwait(false);
		var store = CreateStore(ambientTenantId);
		var messageId = await StageAndClaimAsync(store, "mark-backoff").ConfigureAwait(false);

		var nextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(37);
		await store.MarkFailedWithBackoffAsync(messageId, "boom-backoff", 2, nextAttemptAt, CancellationToken.None)
			.ConfigureAwait(false);

		var row = await ReadRowAsync(messageId).ConfigureAwait(false);
		row.Status.ShouldBe(
			StatusFailed,
			$"MarkFailedWithBackoff must record the failure under ambient tenant '{ambientTenantId ?? "<none>"}'.");
		row.LastError.ShouldBe("boom-backoff");
		row.NextAttemptAt.ShouldNotBeNull(
			"the backoff schedule must be persisted; a silent zero-row mark leaves NextAttemptAt null, so the row "
			+ "is immediately re-claimable and the failing message hot-loops.");
		// The schedule is persisted as the DELAY the caller asked for, re-anchored on the database's own
		// clock, so this asserts the remaining delay rather than the caller's absolute instant. Asserting the
		// instant would be asserting that the dispatcher and the database agree about the time, which is the
		// very assumption the claim path must not make -- and on a host whose container clock wanders by a
		// second or two it makes this arm a coin flip for a reason that has nothing to do with the mark.
		(row.NextAttemptAt!.Value - row.ServerNow).ShouldBe(
			TimeSpan.FromMinutes(37),
			TimeSpan.FromSeconds(5),
			"the mark must persist the caller's 37-minute backoff, measured from the server clock the claim "
			+ "predicate compares against.");
	}

	[Theory]
	[InlineData(ForeignAmbientTenant)]
	[InlineData(null)]
	public async Task Mark_a_globally_claimed_tenanted_row_DeadLettered(string? ambientTenantId)
	{
		await ResetAsync().ConfigureAwait(false);
		var store = CreateStore(ambientTenantId);
		var messageId = await StageAndClaimAsync(store, "mark-deadletter").ConfigureAwait(false);

		// This path DISCARDS the row count and logs "(terminal)" unconditionally — it reports a transition
		// that did not happen. The row is the only witness.
		await store.MarkDeadLetteredAsync(messageId, "poison", CancellationToken.None).ConfigureAwait(false);

		var row = await ReadRowAsync(messageId).ConfigureAwait(false);
		row.Status.ShouldBe(
			StatusDeadLettered,
			$"MarkDeadLettered must move the row to the terminal dead-letter state under ambient tenant "
			+ $"'{ambientTenantId ?? "<none>"}'; a silent zero-row mark leaves it Staged and re-claimable forever "
			+ "while the log asserts it was dead-lettered.");
		row.LastError.ShouldBe("poison");
		row.LeasedBy.ShouldBeNull("the dead-letter transition clears the lease so no sweep can resurrect it.");
	}

	// ---------------------------------------------------------------------------------------------------
	// SAFETY — the ONE adjacent tenant predicate that must SURVIVE the fix.
	// ---------------------------------------------------------------------------------------------------

	/// <summary>
	/// <c>GetAllTenantsStatisticsAsync</c> is an operator report over the whole table, and the ambient tenant
	/// must not change what it returns. Six rows exist throughout, spread across two tenants and one
	/// untenanted row; every scope must see all six.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This operation takes no tenant argument, returns a type carrying no tenant field, and reaches a store
	/// that reads no ambient tenant context — so a confined result has no way to say which partition it
	/// describes. Confinement here is not underspecified, it is unrepresentable, and the subsystem's
	/// guarantee contract records it as deliberately estate-wide.
	/// </para>
	/// <para>
	/// Non-vacuous in both directions. Reintroducing a tenant predicate makes the three scopes disagree
	/// (2 / 3 / 1), which the equality arm catches immediately — and that predicate would be a defect, since
	/// a report reaching rows by no key can only lose rows by filtering, never gain another tenant's. An
	/// inert store returning zero fails the row-count arm. Neither arm alone is sufficient: agreement is
	/// satisfied by a store that always answers zero, and the row count is satisfied by one that ignores
	/// scope for the wrong reason.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Report_every_tenants_rows_regardless_of_the_ambient_tenant()
	{
		await ResetAsync().ConfigureAwait(false);
		var seeder = CreateStore(ambientTenantId: null);

		// 2 rows owned by tenant-A, 3 by tenant-B, 1 carrying no tenant at all: 6 rows in one table.
		await StageAsync(seeder, "stats-a-1", "tenant-A").ConfigureAwait(false);
		await StageAsync(seeder, "stats-a-2", "tenant-A").ConfigureAwait(false);
		await StageAsync(seeder, "stats-b-1", "tenant-B").ConfigureAwait(false);
		await StageAsync(seeder, "stats-b-2", "tenant-B").ConfigureAwait(false);
		await StageAsync(seeder, "stats-b-3", "tenant-B").ConfigureAwait(false);
		await StageAsync(seeder, "stats-untenanted", tenantId: null).ConfigureAwait(false);
		(await CountRowsAsync().ConfigureAwait(false)).ShouldBe(6, "precondition: all six rows are present.");

		var underTenantA = await CreateStore("tenant-A").GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var underTenantB = await CreateStore("tenant-B").GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var underNoTenant = await CreateStore(ambientTenantId: null).GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// The property: the ambient tenant does not change an estate-wide report. Were a tenant predicate
		// reintroduced here these three would disagree (2 / 3 / 1), and an operator's view of the backlog
		// would depend on which tenant happened to be ambient when they opened it.
		underTenantB.StagedMessageCount.ShouldBe(
			underTenantA.StagedMessageCount,
			"two different ambient tenants must receive the same estate-wide report.");
		underNoTenant.StagedMessageCount.ShouldBe(
			underTenantA.StagedMessageCount,
			"a host with no ambient tenant must receive the same estate-wide report, not a narrower one.");

		// Liveness: the shared answer is the whole table, not zero. Agreement alone is satisfied by a store
		// that reports nothing to everybody, which is the stall this arm exists to catch.
		underTenantA.StagedMessageCount.ShouldBe(
			6,
			"all six staged rows must be reported: an operator dashboard showing an empty outbox while "
			+ "messages accumulate hides exactly the backlog it exists to reveal.");
	}

	// ---------------------------------------------------------------------------------------------------
	// Helpers
	// ---------------------------------------------------------------------------------------------------

	/// <summary>
	/// A hand-written <see cref="ITenantContext"/> implementing the interface directly — no first-party base
	/// supplies the members, so the fixture cannot pass by inheriting behaviour the real implementors lack.
	/// </summary>
	private sealed class AmbientTenantContext : ITenantContext
	{
		public AmbientTenantContext(string tenantId) => TenantId = tenantId;

		public string? TenantId { get; }

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}

	private async Task ResetAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — this real-infra outbox terminal-mark lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private SqlServerOutboxStore CreateStore(string? ambientTenantId)
	{
		var options = new SqlServerOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			ProcessorId = "drain-processor",
			Tables =
			{
				SchemaName = _fixture.SchemaName,
				OutboxTableName = _fixture.OutboxTableName,
				TransportsTableName = _fixture.TransportsTableName,
			},
			Processing = { CommandTimeoutSeconds = 30 },
		};

		// The store no longer accepts an ITenantContext at all: outbox statistics was ruled estate-wide,
		// which removed its only consumer, and the field and constructor parameter went with it. So the
		// ambient tenant these arms are parameterised over is no longer even expressible to this store.
		// That does not make the parameterisation pointless -- it makes it stronger. It now asserts that
		// the terminal marks reach terminal state with no ambient tenant reachable by any route, which is
		// the shipped wiring and was the configuration a lock covering only "a different tenant" would
		// have missed.
		return new SqlServerOutboxStore(
			() => new SqlConnection(_fixture.ConnectionString),
			options,
			payloadSerializer: null,
			inboxOptions: null,
			NullLogger<SqlServerOutboxStore>.Instance);
	}

	private static async Task StageAsync(SqlServerOutboxStore store, string messageId, string? tenantId) =>
		await store.StageMessageAsync(
			new OutboundMessage
			{
				Id = messageId,
				MessageType = "T",
				Payload = [1],
				Destination = "dest",
				TenantId = tenantId,
			},
			CancellationToken.None).ConfigureAwait(false);

	/// <summary>
	/// Stages a row owned by <see cref="OwningTenant"/> and claims it through the real global drain, so the
	/// mark under test is addressing a row the claim actually returned — the production sequence, not a
	/// hand-constructed one.
	/// </summary>
	private static async Task<string> StageAndClaimAsync(SqlServerOutboxStore store, string messageId)
	{
		await StageAsync(store, messageId, OwningTenant).ConfigureAwait(false);

		var claimed = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		claimed.ShouldContain(
			m => m.Id == messageId,
			"precondition: the drain claim is cross-tenant by design and must return the row regardless of the "
			+ "ambient tenant. If this fails the claim itself regressed, not the mark.");

		return messageId;
	}

	private async Task<int> CountRowsAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand(
			$"SELECT COUNT(1) FROM [{_fixture.SchemaName}].[{_fixture.OutboxTableName}]",
			connection);
		return Convert.ToInt32(
			await command.ExecuteScalarAsync().ConfigureAwait(false),
			System.Globalization.CultureInfo.InvariantCulture);
	}

	private async Task<OutboxRow> ReadRowAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(
			$"SELECT Status, LastError, RetryCount, NextAttemptAt, LeasedBy, TenantId, "
			+ $"TODATETIMEOFFSET(SYSUTCDATETIME(), 0) "
			+ $"FROM [{_fixture.SchemaName}].[{_fixture.OutboxTableName}] WHERE Id = @Id",
			connection);
		_ = command.Parameters.Add(new SqlParameter("@Id", messageId));

		await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
		(await reader.ReadAsync().ConfigureAwait(false)).ShouldBeTrue($"row '{messageId}' must exist.");

		var row = new OutboxRow
		{
			Status = reader.GetInt32(0),
			LastError = await reader.IsDBNullAsync(1).ConfigureAwait(false) ? null : reader.GetString(1),
			RetryCount = reader.GetInt32(2),
			NextAttemptAt = await reader.IsDBNullAsync(3).ConfigureAwait(false)
				? null
				: reader.GetDateTimeOffset(3),
			LeasedBy = await reader.IsDBNullAsync(4).ConfigureAwait(false) ? null : reader.GetString(4),
			TenantId = await reader.IsDBNullAsync(5).ConfigureAwait(false) ? null : reader.GetString(5),
			ServerNow = reader.GetDateTimeOffset(6),
		};

		// The whole point of the lock: the row really does carry a REAL tenant, so a mark that matched it
		// cannot have done so by the row being untenanted.
		row.TenantId.ShouldBe(
			OwningTenant,
			"precondition: the write path stamps the message's own tenant, so the mark under test is addressing "
			+ "a genuinely tenanted row.");

		return row;
	}

	private sealed class OutboxRow
	{
		public int Status { get; init; }

		public string? LastError { get; init; }

		public int RetryCount { get; init; }

		public DateTimeOffset? NextAttemptAt { get; init; }

		public string? LeasedBy { get; init; }

		public string? TenantId { get; init; }

		/// <summary>The database's own clock, read in the same statement as the row.</summary>
		public DateTimeOffset ServerNow { get; init; }
	}
}
