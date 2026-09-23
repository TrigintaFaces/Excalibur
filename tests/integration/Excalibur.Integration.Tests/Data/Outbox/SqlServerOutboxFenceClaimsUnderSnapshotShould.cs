// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Integration.Tests.Data.Migrations;
using Tests.Shared.Helpers;
using Excalibur.Data;
using Excalibur.Outbox.SqlServer;
using Excalibur.Outbox.SqlServer.Requests;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-SQL-Server lock on the fenced mark-sent under SNAPSHOT-BASED READ COMMITTED: a leader superseded
/// while its own mark-sent is in flight must not apply that write.
/// </summary>
/// <remarks>
/// <para>
/// <b>Requirement:</b> the fence step claims — it advances the high-water and the guarded write applies only
/// if that advance accepted the presented token. <b>Predicate this arm tests:</b> with a fresher tenure's
/// advance committing while a superseded tenure's mark-sent is in flight, the message does NOT reach Sent.
/// Stated separately because they are different claims: the second is observable from outside the store, the
/// first is the mechanism that produces it, and an arm pinned to the mechanism would go green on any
/// rearrangement that still let the write land.
/// </para>
/// <para>
/// <b>Why this needs its own database.</b> <c>READ_COMMITTED_SNAPSHOT</c> is a database-level setting and is
/// OFF by default on a stock container, while it is ON by default on some hosted SQL Server offerings — so
/// the defect is invisible on the default test server and live for a large share of real deployments. The
/// arm therefore creates a scratch database and turns it ON, which is also why it cannot share the suite's
/// shared database: the ALTER requires no other sessions.
/// </para>
/// <para>
/// <b>Why the interleaving is deterministic rather than raced.</b> A held-open transaction is a stable stand-in
/// for "an advance commits during the statement": the fresher tenure advances the high-water and does not
/// commit, so the advance is real and pending for as long as the arm needs. Under snapshot-based read
/// committed a lock-free guard does not block on it and resolves against the older committed version — which
/// is precisely the window. A claiming guard takes the range lock instead, blocks until the fresher advance
/// commits, then observes it. No sleeps, no timing assumptions.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> restore the guard to a scalar subquery read of the fence table inside the UPDATE's
/// own WHERE and this arm observes the message at Sent → RED.
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxFenceClaimsUnderSnapshotShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const long SupersededToken = 5;
	private const long WinningToken = 9;

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerOutboxFenceClaimsUnderSnapshotShould(SqlServerOutboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	[Fact]
	public async Task RefuseTheMarkSentOfATenureSupersededWhileTheStatementIsInFlight()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a superseded leader completing a message it no longer owns is a fencing safety failure — "
			+ "this real-SQL-Server lock must never be skipped");

		var cancellationToken = TestContext.Current.CancellationToken;

		await using var scratch = await SqlServerScratchDatabase
			.CreateAsync(_fixture.ConnectionString, "u9zatx_fence_claim", cancellationToken)
			.ConfigureAwait(false);

		// The setting under which the defect exists at all.
		await ExecuteOnAdminAsync(
			$"ALTER DATABASE [{DatabaseNameOf(scratch.ConnectionString)}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE",
			cancellationToken).ConfigureAwait(false);

		// The schema the package SHIPS, applied as a consumer applies it -- a hand-written copy in a test
		// is free to drift from the real one.
		await using (var schemaConnection = new SqlConnection(scratch.ConnectionString))
		{
			await schemaConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
			foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches(
				"src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql"))
			{
				// CA2100: the text is a batch of the package's own shipped schema script read from the repo,
				// not input. Same convention as the suite's fixture.
#pragma warning disable CA2100
				await using var command = new SqlCommand(batch, schemaConnection);
#pragma warning restore CA2100
				_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		var store = NewStore(scratch.ConnectionString);
		var message = new OutboundMessage("test.message", [1], "dest");
		await store.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);

		var claimed = (await store.GetUnsentMessagesAsync(10, SupersededToken, cancellationToken)
			.ConfigureAwait(false)).ToList();
		claimed.Count.ShouldBe(1, "the tenure must hold a claim before it can be superseded mid-mark");

		// A fresher tenure advances the high-water and HOLDS the transaction open, so the advance is pending
		// for the whole of the mark-sent below.
		await using var fresher = new SqlConnection(scratch.ConnectionString);
		await fresher.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var fresherTransaction = (SqlTransaction)await fresher
			.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
		await using (var advance = fresher.CreateCommand())
		{
			advance.Transaction = fresherTransaction;
			advance.CommandText =
				"UPDATE [dbo].[OutboxFence] SET HighWaterToken = @Token WHERE OutboxTable = @Scope;" +
				"IF @@ROWCOUNT = 0 INSERT INTO [dbo].[OutboxFence] (OutboxTable, HighWaterToken) VALUES (@Scope, @Token);";
			_ = advance.Parameters.AddWithValue("@Token", WinningToken);
			_ = advance.Parameters.AddWithValue("@Scope", "[dbo].[OutboxMessages]");
			_ = await advance.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		// The superseded tenure's GUARDED WRITE, driven directly rather than through the store's
		// MarkSentAsync. That is deliberate and it is the difference between an arm that discriminates and
		// one that does not: the store advances the fence in a SEPARATE call before this request runs, and
		// that advance takes the same row lock, so it blocks on the pending advance above and the request
		// never sees the window at all. Going through the store therefore refuses for a reason that has
		// nothing to do with the guard under test, and passes identically whether the guard claims or reads.
		// The residual this bead names lives in the request, so the request is what the arm drives.
		var markSent = Task.Run(
			async () =>
			{
				await using var writer = new SqlConnection(scratch.ConnectionString);
				await writer.OpenAsync(CancellationToken.None).ConfigureAwait(false);
				return await writer.ResolveAsync(
						new MarkMessageSentRequest(
							"[dbo].[OutboxMessages]",
							claimed[0].Id,
							30,
							SupersededToken,
							"[dbo].[OutboxFence]",
							"[dbo].[OutboxMessages]",
							CancellationToken.None))
					.ConfigureAwait(false);
			},
			cancellationToken);

		// THE BARRIER. Without it this arm passes on BOTH implementations, measured: the commit below
		// raced the write above and usually won, so the write refused because the advance was already
		// VISIBLE -- which any guard does, claiming or reading. That is the same wrong-reason refusal the
		// comment above avoids at the STORE level, surviving one level down at the statement.
		//
		// The wait is bounded and its two outcomes are the two implementations, which is why this is a
		// discriminator and not a sleep:
		//   READING guard  -- does not block on the pending advance (that is the defect), so it COMPLETES
		//                     inside the window, reads the older committed high-water, and applies. -> 1 row
		//   CLAIMING guard -- takes the range lock, BLOCKS on the pending advance, and is still running
		//                     when the window elapses. -> we commit, it observes the advance, refuses. -> 0
		// Only the claiming guard can exhaust the window, so a slow machine cannot turn a RED into a GREEN;
		// it can only delay the GREEN. Exceeding it is the PASS condition, never the fail condition.
		var completedWhileAdvancePending =
			await Task.WhenAny(markSent, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken))
				.ConfigureAwait(false) == markSent;

		await fresherTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		var rowsAffected = await markSent.ConfigureAwait(false);

		completedWhileAdvancePending.ShouldBeFalse(
			"the guarded write ran to completion while a fresher tenure's advance was pending and "
			+ "uncommitted, which means it never took the range lock: it resolved the high-water against "
			+ "the older committed version instead of claiming. That is the snapshot-read window this "
			+ "fence is supposed to close.");

		// UpdatedCount, not the value itself: MarkMessageSentRequest used to return a bare rowcount and now
		// returns MarkSentMutationResult, because the statement also has to hand back the high-water it
		// used and whether the row existed — all three decided inside the one mutating transaction. The
		// assertion is unchanged in meaning; only the member carrying the rowcount moved. Strengthened, not
		// relaxed, per the F-5 rule: this still asserts ZERO rows, it does not weaken to "not null".
		rowsAffected.UpdatedCount.ShouldBe(
			0,
			"the superseded tenure's guarded write must match no rows once the fresher advance is visible");

		var status = await ReadStatusAsync(scratch.ConnectionString, claimed[0].Id, cancellationToken)
			.ConfigureAwait(false);
		status.ShouldNotBe(
			2,
			"a tenure superseded while its mark-sent was in flight completed the message anyway: the message "
			+ "is recorded Sent by a leader that no longer owned it, and the live leader will never deliver "
			+ "it. Status 2 is Sent.");
	}

	private static string DatabaseNameOf(string connectionString) =>
		new SqlConnectionStringBuilder(connectionString).InitialCatalog;

	private async Task ExecuteOnAdminAsync(string sql, CancellationToken cancellationToken)
	{
		await using var admin = new SqlConnection(_fixture.ConnectionString);
		await admin.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = admin.CreateCommand();
		// CA2100: the only interpolated value is a database name this arm generated and bracket-quoted.
#pragma warning disable CA2100
		command.CommandText = sql;
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task<int?> ReadStatusAsync(
		string connectionString,
		string messageId,
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT Status FROM [dbo].[OutboxMessages] WHERE Id = @Id";
		_ = command.Parameters.AddWithValue("@Id", messageId);
		var raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return raw is null or DBNull ? null : Convert.ToInt32(raw, System.Globalization.CultureInfo.InvariantCulture);
	}

	private static SqlServerOutboxStore NewStore(string connectionString)
	{
		var options = Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = connectionString,
			Processing = { CommandTimeoutSeconds = 30 },
		});
		return new SqlServerOutboxStore(options, NullLogger<SqlServerOutboxStore>.Instance);
	}
}
