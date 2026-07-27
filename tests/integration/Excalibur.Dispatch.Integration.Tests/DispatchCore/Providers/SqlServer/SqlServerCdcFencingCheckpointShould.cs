// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;

using Shouldly;

using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

#pragma warning disable CA1812 // Instantiated by the xUnit test runner.

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// Real-SQL-Server regression lock for the CDC single-active-consumer fencing invariant: the
/// <see cref="CdcStateStore"/> checkpoint MERGE is a non-decreasing compare-and-swap on the fencing
/// token, so a demoted-but-unaware (split-brain) leader presenting an <em>older</em> token cannot regress
/// or skip the persisted LSN checkpoint.
/// </summary>
/// <remarks>
/// <para>
/// Binds the <b>real</b> <see cref="CdcStateStore.UpdateLastProcessedPositionAsync"/> MERGE against a live
/// SQL Server container (never a mock — per <c>verify-against-real-infra-not-mock</c>), asserting the
/// <em>emitted</em> behaviour (rows written + the persisted LSN re-read on a fresh store instance), not
/// attribute/registration presence. A live container is a hard requirement (never skipped).
/// </para>
/// <para>
/// Safety∧liveness arms:
/// <list type="number">
/// <item><description><b>SAFETY (fencing)</b> — a stale (lower) token fails the CAS guard → 0 rows → the
/// stored LSN is unchanged (a fresh store re-reads the ORIGINAL LSN, not the zombie's). RED when the
/// <c>target.FencingToken &lt;= @fencingToken</c> guard is dropped from the MERGE.</description></item>
/// <item><description><b>LIVENESS (equal-or-higher admits)</b> — the current leader writing at an equal or
/// higher token advances the checkpoint, proving the guard is fencing-specific (non-decreasing), not a
/// blanket reject.</description></item>
/// <item><description><b>FAILOVER (liveness)</b> — after a zombie is fenced out, a NEW leader with a HIGHER
/// token advances the checkpoint (higher token wins; the feed resumes from the last committed
/// checkpoint).</description></item>
/// </list>
/// A pure-unit arm binds the terminal classification of <see cref="CdcLeadershipSupersededException"/> — the
/// exception the checkpoint manager throws on the stale-token 0-row case — so a demoted leader stops rather
/// than spinning. Author≠impl: authored by TestsDeveloper, not the lh1i1q implementer.
/// </para>
/// </remarks>
[Collection(ContainerCollections.SqlServer)]
[Trait("Category", "Integration")]
[Trait("Component", "Cdc")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerCdcFencingCheckpointShould
{
	private const string SchemaName = "Cdc";
	private const string DatabaseName = "TestDb";
	private const string TableName = "dbo.OrdersFeed";

	private readonly SqlServerContainerFixture _fixture;
	private readonly string _stateTable = $"CdcState_{Guid.NewGuid():N}";

	public SqlServerCdcFencingCheckpointShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY: a demoted leader's stale (lower) fencing-token write is rejected by the non-decreasing CAS —
	/// 0 rows written, the persisted LSN checkpoint left UNCHANGED (no regression, no skip). Re-read on a
	/// fresh store instance to prove durability across the store boundary.
	/// </summary>
	[Fact]
	public async Task Reject_a_demoted_leaders_stale_token_write_leaving_the_checkpoint_unchanged()
	{
		await EnsureStateTableAsync().ConfigureAwait(true);
		var connId = NewConnId();

		var l1 = Lsn(100);
		var l2Zombie = Lsn(200); // a NEWER LSN a zombie would advance to — must NOT be persisted.

		// The current leader (token = 5) writes the checkpoint at L1.
		(await WriteAsync(connId, l1, fencingToken: 5).ConfigureAwait(true))
			.ShouldBe(1, "the leading token's first checkpoint write must be applied.");

		// A demoted split-brain leader (older token = 3) tries to advance to a newer LSN.
		var rowsForStaleToken = await WriteAsync(connId, l2Zombie, fencingToken: 3).ConfigureAwait(true);

		rowsForStaleToken.ShouldBe(0,
			"a stale (lower) fencing token must fail the non-decreasing CAS — 0 rows written.");

		// The persisted checkpoint must still be L1 (the zombie's L2 must never land), re-read fresh.
		var persisted = await ReadLsnAsync(connId).ConfigureAwait(true);
		Hex(persisted).ShouldBe(Hex(l1),
			"the stale-token write must leave the LSN checkpoint UNCHANGED — no regression or skip.");
		Hex(persisted).ShouldNotBe(Hex(l2Zombie),
			"the zombie leader's newer LSN must NOT have overwritten the checkpoint (split-brain corruption).");
	}

	/// <summary>
	/// LIVENESS: the current leader presenting an EQUAL token advances the checkpoint — the CAS is
	/// non-decreasing (equal admitted), not a blanket reject. Proves the guard is fencing-specific.
	/// </summary>
	[Fact]
	public async Task Admit_the_current_leaders_equal_token_write_and_advance_the_checkpoint()
	{
		await EnsureStateTableAsync().ConfigureAwait(true);
		var connId = NewConnId();

		var l1 = Lsn(100);
		var l2 = Lsn(200);

		(await WriteAsync(connId, l1, fencingToken: 5).ConfigureAwait(true)).ShouldBe(1);

		// Same leadership tenure (equal token) advancing to a newer LSN — must be admitted.
		var rows = await WriteAsync(connId, l2, fencingToken: 5).ConfigureAwait(true);
		rows.ShouldBe(1, "an EQUAL fencing token must be admitted (non-decreasing CAS, not blanket reject).");

		Hex(await ReadLsnAsync(connId).ConfigureAwait(true)).ShouldBe(Hex(l2),
			"the equal-token write must advance the checkpoint to the new LSN.");
	}

	/// <summary>
	/// FAILOVER (liveness): after a zombie is fenced out, a NEW leader with a HIGHER token advances the
	/// checkpoint — higher token wins, the change feed resumes from the last committed checkpoint.
	/// </summary>
	[Fact]
	public async Task Admit_a_new_leaders_higher_token_write_after_fencing_out_the_zombie()
	{
		await EnsureStateTableAsync().ConfigureAwait(true);
		var connId = NewConnId();

		var l1 = Lsn(100);
		var l2Zombie = Lsn(200);
		var l3NewLeader = Lsn(300);

		// Leader token 5 commits L1.
		(await WriteAsync(connId, l1, fencingToken: 5).ConfigureAwait(true)).ShouldBe(1);

		// Zombie (token 3) is fenced out — checkpoint stays at L1.
		(await WriteAsync(connId, l2Zombie, fencingToken: 3).ConfigureAwait(true)).ShouldBe(0);
		Hex(await ReadLsnAsync(connId).ConfigureAwait(true)).ShouldBe(Hex(l1));

		// A newly-elected leader (higher token 7) takes over and advances to L3.
		var rows = await WriteAsync(connId, l3NewLeader, fencingToken: 7).ConfigureAwait(true);
		rows.ShouldBe(1, "a HIGHER fencing token (new leadership tenure) must be admitted.");

		Hex(await ReadLsnAsync(connId).ConfigureAwait(true)).ShouldBe(Hex(l3NewLeader),
			"failover: the new leader's checkpoint must advance from the last committed position.");
	}

	/// <summary>
	/// Pure-unit arm: the exception the checkpoint manager throws when the store reports the stale-token
	/// 0-row case (<see cref="CdcLeadershipSupersededException"/>) is classified <em>fatal</em>
	/// (non-retryable), so a demoted leader STOPS advancing rather than spinning on an identically-rejected
	/// write. Fail-safe fallback classifier (no <c>IMessageFailureClassifier</c>).
	/// </summary>
	[Fact]
	public void Classify_a_superseded_leadership_token_as_fatal_so_the_demoted_leader_stops()
	{
		CdcFatalClassifier.IsFatal(new CdcLeadershipSupersededException(), classifier: null)
			.ShouldBeTrue("a superseded fencing token is terminal — retrying the same stale token is rejected identically.");

		// Liveness pair: an ordinary transient fault must NOT be treated as fatal (the guard is
		// superseded-specific, not blanket-terminate).
		CdcFatalClassifier.IsFatal(new TimeoutException("transient"), classifier: null)
			.ShouldBeFalse("a transient fault must remain retryable — only a superseded token is terminal.");
	}

	// --- helpers ----------------------------------------------------------------------------------------

	private string ConnectionString => _fixture.ConnectionString;

	private static string NewConnId() => $"conn_{Guid.NewGuid():N}";

	private CdcStateStore NewStore() =>
		new(
			new SqlConnection(ConnectionString),
			MsOptions.Create(new SqlServerCdcStateStoreOptions { SchemaName = SchemaName, TableName = _stateTable }));

	private async Task<int> WriteAsync(string connId, byte[] lsn, long fencingToken)
	{
		await using var store = NewStore();
		return await store.UpdateLastProcessedPositionAsync(
			connId,
			DatabaseName,
			TableName,
			lsn,
			sequenceValue: null,
			commitTime: DateTime.UtcNow,
			fencingToken: fencingToken,
			CancellationToken.None).ConfigureAwait(false);
	}

	private async Task<byte[]> ReadLsnAsync(string connId)
	{
		// Fresh store instance — proves the checkpoint survives the store (durability across the boundary).
		await using var store = NewStore();
		var states = await store.GetLastProcessedPositionAsync(connId, DatabaseName, CancellationToken.None)
			.ConfigureAwait(false);

		var row = states.FirstOrDefault(s => s.TableName == TableName);
		row.ShouldNotBeNull("the checkpoint row must exist after a leader's write.");
		return row.LastProcessedLsn;
	}

	// A strictly-increasing SQL Server LSN encoded big-endian into a 10-byte value (matches the conformance
	// fixture's encoding). value 0 would map to an empty LSN, so all callers pass a positive value.
	private static byte[] Lsn(ulong value)
	{
		var lsn = new byte[10];
		for (var i = 0; i < 8; i++)
		{
			lsn[9 - i] = (byte)(value >> (8 * i));
		}

		return lsn;
	}

	private static string Hex(byte[] bytes) => Convert.ToHexString(bytes);

	private async Task EnsureStateTableAsync()
	{
		_fixture.ConnectionString.ShouldNotBeNullOrWhiteSpace(
			"Docker/SQL Server must be available — the CDC fencing checkpoint invariant is a real-infra lock and must never be skipped.");

		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		_ = await connection.ExecuteAsync(
			$"IF SCHEMA_ID('{SchemaName}') IS NULL EXEC('CREATE SCHEMA [{SchemaName}]');").ConfigureAwait(false);

		// Columns + PK mirror the store's MERGE/SELECT against QualifiedTableName ([Cdc].[<table>]).
		// FencingToken BIGINT NULL is the column the non-decreasing CAS guards on.
		_ = await connection.ExecuteAsync($"""
			IF OBJECT_ID('[{SchemaName}].[{_stateTable}]', 'U') IS NULL
			CREATE TABLE [{SchemaName}].[{_stateTable}] (
			    MessageId                    BIGINT IDENTITY(1,1) NOT NULL,
			    DatabaseConnectionIdentifier NVARCHAR(255) NOT NULL,
			    DatabaseName                 NVARCHAR(255) NOT NULL,
			    TableName                    NVARCHAR(255) NOT NULL,
			    LastProcessedLsn             VARBINARY(16) NOT NULL,
			    LastProcessedSequenceValue   VARBINARY(16) NULL,
			    LastCommitTime               DATETIME2     NULL,
			    FencingToken                 BIGINT        NULL,
			    ProcessedAt                  DATETIME2     NOT NULL,
			    CONSTRAINT [PK_{_stateTable}] PRIMARY KEY
			        (DatabaseConnectionIdentifier, DatabaseName, TableName)
			);
			""").ConfigureAwait(false);
	}
}
