// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

namespace Excalibur.Outbox.Oracle.Tests;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
[Trait("Database", "Oracle")]
[Collection(OracleOutboxCollection.Name)]
public sealed class OracleStrongClaimCompletionShould(OracleOutboxStoreContainerFixture fixture) : IAsyncLifetime
{
	private const string MessageId = "owned-message";
	private static readonly byte[] Body = [0, 255, 128, 1];
	private static readonly DateTimeOffset Created = new(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
	private static readonly DateTimeOffset DispatcherNow = new(2035, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private ServiceProvider? _callbackServices;

	public async ValueTask InitializeAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue("these are real Oracle controls and must never skip");
		await fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		if (_callbackServices is not null) { await _callbackServices.DisposeAsync().ConfigureAwait(false); }
		await fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	[Theory]
	[InlineData(false, 0)]
	[InlineData(false, 1)]
	[InlineData(false, 2)]
	[InlineData(false, 3)]
	[InlineData(true, 0)]
	[InlineData(true, 1)]
	[InlineData(true, 2)]
	[InlineData(true, 3)]
	public async Task RejectThePreviousAcquisitionButApplyTheCurrentHolderSelectedTransition(bool fenced, int operation)
	{
		using var store = Store();
		await StageAsync(store, MessageId).ConfigureAwait(false);
		var first = await ClaimAsync(store, fenced).ConfigureAwait(false);
		await SqlAsync($"UPDATE {Table} SET dispatcher_timeout=SYSTIMESTAMP-NUMTODSINTERVAL(1,'SECOND') WHERE message_id=:Id",
			new { Id = MessageId }).ConfigureAwait(false);
		var successor = await ClaimAsync(store, fenced).ConfigureAwait(false);
		successor.DispatcherId.ShouldNotBe(first.DispatcherId);
		(await CompleteAsync(store, fenced, operation, first.DispatcherId!)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
		(await CountAsync($"SELECT COUNT(*) FROM {Table} WHERE message_id=:Id AND dispatcher_id=:Owner AND attempts=0 AND error_message IS NULL",
			new { Id = MessageId, Owner = successor.DispatcherId })).ShouldBe(1);
		var before = await ServerNowAsync().ConfigureAwait(false);
		(await CompleteAsync(store, fenced, operation, successor.DispatcherId!)).ShouldBe(OutboxCompletionOutcome.Applied);
		var after = await ServerNowAsync().ConfigureAwait(false);
		await VerifySelectedTransitionAsync(operation, before, after).ConfigureAwait(false);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseAnExpiredClaimEvenWithoutAReclaimer(bool fenced)
	{
		using var store = Store();
		await StageAsync(store, MessageId).ConfigureAwait(false);
		var first = await ClaimAsync(store, fenced).ConfigureAwait(false);
		await SqlAsync($"UPDATE {Table} SET dispatcher_timeout=SYSTIMESTAMP-NUMTODSINTERVAL(1,'SECOND') WHERE message_id=:Id",
			new { Id = MessageId }).ConfigureAwait(false);
		(await CompleteAsync(store, fenced, 0, first.DispatcherId!)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
		(await CountAsync($"SELECT COUNT(*) FROM {Table} WHERE message_id=:Id", new { Id = MessageId })).ShouldBe(1);
	}

	[Theory]
	[InlineData("Owner", "owner")]
	[InlineData("café", "cafe")]
	[InlineData("é", "e\u0301")]
	[InlineData("\U00010000", "\uE000")]
	[InlineData("claim ", "claim")]
	public async Task CompareOpaqueClaimsByExactBytesUnderLinguisticSessionSettings(string current, string different)
	{
		using var store = Store(linguistic: true);
		await StageAsync(store, MessageId).ConfigureAwait(false);
		_ = await ClaimAsync(store, fenced: true).ConfigureAwait(false);
		await SqlAsync($"UPDATE {Table} SET dispatcher_id=:Owner WHERE message_id=:Id",
			new { Owner = current, Id = MessageId }).ConfigureAwait(false);
		(await store.MarkSentAsync(MessageId, new OutboxWriteAuthority(7, different), CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.ClaimLost);
		(await store.MarkSentAsync(MessageId, new OutboxWriteAuthority(7, current), CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.Applied, "the exact non-ASCII/current identity must still progress");
		(await CountAsync($"SELECT COUNT(*) FROM {Table} WHERE message_id=:Id", new { Id = MessageId })).ShouldBe(0);
	}

	[Fact]
	public async Task JudgeFenceBeforeMissingRowAndRetainAnAcceptedNewerFenceOnRowRefusal()
	{
		using var store = Store();
		(await store.MarkSentAsync("absent", new OutboxWriteAuthority(8, "new"), CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.MessageNotFound);
		(await store.MarkSentAsync("absent", new OutboxWriteAuthority(7, "old"), CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.FenceRefused);
		(await store.GetFencingHighWaterAsync(CancellationToken.None)).ShouldBe(8);
	}

	[Fact]
	public async Task UseOneFenceForCaseAliasesOfTheSameUnquotedPhysicalTable()
	{
		using var upper = Store(tableName: Table.ToUpperInvariant());
#pragma warning disable CA1308 // Deliberate lowercase unquoted alias is the independent half of the physical-table case-alias control.
		using var lower = Store(tableName: Table.ToLowerInvariant());
#pragma warning restore CA1308
		(await upper.MarkSentAsync("absent", new OutboxWriteAuthority(8, "new"), CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.MessageNotFound);
		(await lower.MarkSentAsync("absent", new OutboxWriteAuthority(7, "old"), CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.FenceRefused);
		(await lower.GetFencingHighWaterAsync(CancellationToken.None)).ShouldBe(8);
		(await CountAsync($"SELECT COUNT(*) FROM {fixture.FenceTableName}")).ShouldBe(1);
	}

	[Fact]
	public async Task ShareThePhysicalFenceAcrossBareAndExplicitCurrentSchemaNames()
	{
		await using var connection = fixture.CreateConnection();
		var owner = await connection.ExecuteScalarAsync<string>("SELECT SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM dual").ConfigureAwait(false);
		using var bare = Store(schemaName: "");
		using var qualified = Store(schemaName: owner);
		(await bare.MarkSentAsync("absent", new OutboxWriteAuthority(8, "new"), CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.MessageNotFound);
		(await qualified.MarkSentAsync("absent", new OutboxWriteAuthority(7, "old"), CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.FenceRefused);
		(await qualified.GetFencingHighWaterAsync(CancellationToken.None)).ShouldBe(8);
		await qualified.ResetFencingHighWaterAsync(9, false, CancellationToken.None).ConfigureAwait(false);
		(await bare.GetFencingHighWaterAsync(CancellationToken.None)).ShouldBe(9);
		(await CountAsync($"SELECT COUNT(*) FROM {fixture.FenceTableName}")).ShouldBe(1);
	}

	[Fact]
	public async Task ShareThePhysicalFenceThroughALocalSynonym()
	{
		var alias = "OWNED_ALIAS_" + Guid.NewGuid().ToString("N")[..12];
		var created = false;
		try
		{
			await SqlAsync($"CREATE SYNONYM {alias} FOR {Table}").ConfigureAwait(false);
			created = true;
			using var physical = Store();
			using var synonym = Store(tableName: alias);
			await physical.ResetFencingHighWaterAsync(8, false, CancellationToken.None).ConfigureAwait(false);
			(await synonym.MarkSentAsync("absent", new OutboxWriteAuthority(7, "old"), CancellationToken.None))
				.ShouldBe(OutboxCompletionOutcome.FenceRefused);
			(await synonym.GetFencingHighWaterAsync(CancellationToken.None)).ShouldBe(8);
		}
		finally { if (created) { await SqlAsync($"DROP SYNONYM {alias}").ConfigureAwait(false); } }
	}

	[Fact]
	public async Task KeepDistinctPhysicalNamesSeparateWhenTheirBase64ScopesCollate()
	{
		// AAA and AA[ have collating Base64 forms QUFB/QUFb under BINARY_AI.
		// Valid ASCII aliases exercise returned identifiers broader than configuration validation.
		var prefix = "OWNED_" + Guid.NewGuid().ToString("N")[..8] + "_";
		var firstName = prefix + "AAA";
		var secondName = prefix + "AA[";
		var firstAlias = prefix + "A";
		var secondAlias = prefix + "B";
		var created = new List<string>();
		try
		{
			foreach (var name in new[] { firstName, secondName })
			{
				await SqlAsync($"CREATE TABLE \"{name}\" AS SELECT * FROM {Table} WHERE 1=0").ConfigureAwait(false);
				created.Add($"DROP TABLE \"{name}\"");
			}
			await SqlAsync($"CREATE SYNONYM {firstAlias} FOR \"{firstName}\"").ConfigureAwait(false);
			created.Add($"DROP SYNONYM {firstAlias}");
			await SqlAsync($"CREATE SYNONYM {secondAlias} FOR \"{secondName}\"").ConfigureAwait(false);
			created.Add($"DROP SYNONYM {secondAlias}");
			using var first = Store(linguistic: true, tableName: firstAlias);
			using var second = Store(linguistic: true, tableName: secondAlias);
			await first.ResetFencingHighWaterAsync(11, false, CancellationToken.None).ConfigureAwait(false);
			await second.ResetFencingHighWaterAsync(3, false, CancellationToken.None).ConfigureAwait(false);
			(await first.GetFencingHighWaterAsync(CancellationToken.None)).ShouldBe(11);
			(await second.GetFencingHighWaterAsync(CancellationToken.None)).ShouldBe(3);
			(await CountAsync($"SELECT COUNT(*) FROM {fixture.FenceTableName}")).ShouldBe(2);
			await AssertDistinctScopesHaveOneExactLinguisticSortKeyAsync().ConfigureAwait(false);
		}
		finally { foreach (var drop in created.AsEnumerable().Reverse()) { await SqlAsync(drop).ConfigureAwait(false); } }
	}

	[Fact]
	public async Task RefuseAViewBeforeCreatingAnyFenceAuthority()
	{
		var name = "OWNED_VIEW_" + Guid.NewGuid().ToString("N")[..12];
		var created = false;
		try
		{
			await SqlAsync($"CREATE VIEW {name} AS SELECT * FROM {Table}").ConfigureAwait(false);
			created = true;
			using var store = Store(tableName: name);
			await Should.ThrowAsync<InvalidOperationException>(async () =>
				await store.MarkSentAsync("absent", new OutboxWriteAuthority(7, "claim"), CancellationToken.None));
			(await CountAsync($"SELECT COUNT(*) FROM {fixture.FenceTableName}")).ShouldBe(0);
		}
		finally { if (created) { await SqlAsync($"DROP VIEW {name}").ConfigureAwait(false); } }
	}

	[Fact]
	public async Task RefuseAFenceTableWithoutAnExactSingleColumnUniqueKey()
	{
		var name = "OWNED_NO_KEY_" + Guid.NewGuid().ToString("N")[..10];
		var created = false;
		try
		{
			await SqlAsync($"CREATE TABLE {name} AS SELECT * FROM {fixture.FenceTableName} WHERE 1=0").ConfigureAwait(false);
			created = true;
			using var store = Store(fenceTableName: name);
			await Should.ThrowAsync<InvalidOperationException>(async () =>
				await store.MarkSentAsync("absent", new OutboxWriteAuthority(7, "claim"), CancellationToken.None));
			(await CountAsync($"SELECT COUNT(*) FROM {name}")).ShouldBe(0);
		}
		finally { if (created) { await SqlAsync($"DROP TABLE {name}").ConfigureAwait(false); } }
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReturnOnlyTheExactAcquisitionInTheFullLinguisticBatch(bool fenced)
	{
		using var store = Store();
		await StageAsync(store, "exact").ConfigureAwait(false);
		await StageAsync(store, "lookalike").ConfigureAwait(false);
		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await connection.ExecuteAsync("ALTER SESSION SET NLS_COMP=LINGUISTIC").ConfigureAwait(false);
		await connection.ExecuteAsync("ALTER SESSION SET NLS_SORT=BINARY_AI").ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
		var options = new OracleOutboxStoreOptions { OutboxTableName=Table, DeadLetterTableName=DeadTable, FenceTableName=fixture.FenceTableName };
		var scope = await OraclePhysicalOutboxScope.ResolveAsync(connection, transaction, options, CancellationToken.None).ConfigureAwait(false);
		var plain = fenced ? null : new ReserveOutboxMessages("Owner", 2, 300, scope.OutboxTable, 30, transaction, CancellationToken.None);
		var strict = fenced ? new FencedReserveOutboxMessages("Owner", 2, 300, 7, scope.OutboxTable, scope.FenceTable, 30, transaction, scope.Key, CancellationToken.None) : null;
		var identity = plain?.ClaimIdentity ?? strict!.ClaimIdentity;
		var lookalike = identity.ToUpperInvariant();
		lookalike.ShouldNotBe(identity);
		_ = await connection.ExecuteAsync($"UPDATE {scope.OutboxTable} SET dispatcher_id=:Owner,dispatcher_timeout=SYSTIMESTAMP+NUMTODSINTERVAL(300,'SECOND') WHERE message_id='lookalike'",
			new { Owner=lookalike }, transaction).ConfigureAwait(false);
		var rows = plain is not null ? await connection.ResolveAsync(plain).ConfigureAwait(false) : await connection.ResolveAsync(strict!).ConfigureAwait(false);
		var exact = rows.Single();
		exact.MessageId.ShouldBe("exact");
		exact.DispatcherId.ShouldBe(identity);
		await transaction.CommitAsync().ConfigureAwait(false);
	}

	[Theory]
	[InlineData("insert")]
	[InlineData("delete")]
	public async Task RollBackSourceAndArchiveTogetherWhenAMutationFaults(string phase)
	{
		using var store = Store();
		await StageAsync(store, MessageId).ConfigureAwait(false);
		var claim = await ClaimAsync(store, fenced: true).ConfigureAwait(false);
		var target = phase == "insert" ? DeadTable : Table;
		var verb = phase == "insert" ? "INSERT" : "DELETE";
		var trigger = "OUTBOX_OWNED_FAULT_" + Guid.NewGuid().ToString("N")[..12];
		var created = false;
		try
		{
			await SqlAsync($"CREATE TRIGGER {trigger} BEFORE {verb} ON {target} FOR EACH ROW BEGIN RAISE_APPLICATION_ERROR(-20042,'arranged fault'); END;").ConfigureAwait(false);
			created = true;
			var fault = await Should.ThrowAsync<OperationFailedException>(async () =>
				await store.MarkDeadLetteredAsync(MessageId, "failure", 4,
					new OutboxWriteAuthority(7, claim.DispatcherId!), CancellationToken.None));
			fault.InnerException.ShouldBeOfType<OracleException>().Errors.Cast<OracleError>().Any(error => error.Number == 20042).ShouldBeTrue();
			(await CountAsync($"SELECT COUNT(*) FROM {Table} WHERE message_id=:Id AND dispatcher_id=:Owner",
				new { Id = MessageId, Owner = claim.DispatcherId })).ShouldBe(1);
			(await CountAsync($"SELECT COUNT(*) FROM {DeadTable} WHERE message_id=:Id", new { Id = MessageId })).ShouldBe(0);
		}
		finally { if (created) { await SqlAsync($"DROP TRIGGER {trigger}").ConfigureAwait(false); } }
		(await store.MarkDeadLetteredAsync(MessageId, "failure", 4,
			new OutboxWriteAuthority(7, claim.DispatcherId!), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
		await VerifySelectedTransitionAsync(3).ConfigureAwait(false);
	}

	// The observer requires test-user SELECT on V_$SESSION; fail if infrastructure is not provisioned.
	// It never calls a merely pending Task proof that the intended lock was reached.
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DoNotCommitRawBorrowedWorkAndJudgeExpiryAfterTheActualRowLock(bool expireWhileBlocked)
	{
		var tag = "owned-" + Guid.NewGuid().ToString("N");
		using var store = Store(clientId: tag);
		await StageAsync(store, MessageId).ConfigureAwait(false);
		await StageAsync(store, "sentinel", scheduled: true).ConfigureAwait(false);
		var claim = await ClaimAsync(store, fenced: true).ConfigureAwait(false);
		await using var blocker = fixture.CreateConnection();
		await blocker.OpenAsync().ConfigureAwait(false);
		await using var blockTransaction = await blocker.BeginTransactionAsync().ConfigureAwait(false);
		_ = await blocker.ExecuteScalarAsync<string>($"SELECT message_id FROM {Table} WHERE message_id=:Id FOR UPDATE",
			new { Id = MessageId }, blockTransaction).ConfigureAwait(false);
		using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var completion = store.MarkSentAsync(MessageId, new OutboxWriteAuthority(7, claim.DispatcherId!), stop.Token).AsTask();
		var released = false;
		Exception? primary = null;
		try
		{
			await WaitForBlockedSessionAsync(tag, stop.Token).ConfigureAwait(false);
			await using var rawBorrowed = fixture.CreateConnection();
			await rawBorrowed.OpenAsync(stop.Token).ConfigureAwait(false);
			await using var callerTransaction = await rawBorrowed.BeginTransactionAsync(stop.Token).ConfigureAwait(false);
			_ = await rawBorrowed.ExecuteAsync($"UPDATE {Table} SET error_message='caller sentinel' WHERE message_id='sentinel'",
				transaction: callerTransaction).ConfigureAwait(false);
			if (expireWhileBlocked)
			{
				_ = await blocker.ExecuteAsync($"UPDATE {Table} SET dispatcher_timeout=SYSTIMESTAMP WHERE message_id=:Id",
					new { Id = MessageId }, blockTransaction).ConfigureAwait(false);
			}
			await blockTransaction.CommitAsync(stop.Token).ConfigureAwait(false);
			released = true;
			(await completion.ConfigureAwait(false)).ShouldBe(expireWhileBlocked
				? OutboxCompletionOutcome.ClaimLost : OutboxCompletionOutcome.Applied);
			(await CountAsync($"SELECT COUNT(*) FROM {Table} WHERE message_id='sentinel' AND DBMS_LOB.GETLENGTH(error_message)=15 AND DBMS_LOB.COMPARE(error_message, TO_CLOB('caller sentinel'))=0"))
				.ShouldBe(0, "the private worker commit must not commit unrelated raw borrowed-connection work");
			(await rawBorrowed.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {Table} WHERE message_id='sentinel' AND DBMS_LOB.GETLENGTH(error_message)=15 AND DBMS_LOB.COMPARE(error_message, TO_CLOB('caller sentinel'))=0",
				transaction: callerTransaction).ConfigureAwait(false)).ShouldBe(1);
			await callerTransaction.RollbackAsync(stop.Token).ConfigureAwait(false);
		}
		catch (Exception error) { primary = error; throw; }
		finally
		{
			await stop.CancelAsync().ConfigureAwait(false);
			Exception? cleanupFailure = null;
			if (!released)
			{
				try
				{
					using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
					await blockTransaction.RollbackAsync(cleanup.Token).ConfigureAwait(false);
				}
				catch (Exception error) { cleanupFailure = error; }
			}
			try { await completion.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false); }
			catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
			catch (Exception error) { cleanupFailure ??= error; }
			if (primary is null && cleanupFailure is not null) { ExceptionDispatchInfo.Capture(cleanupFailure).Throw(); }
		}
	}

	private async Task WaitForBlockedSessionAsync(string tag, CancellationToken token)
	{
		var start = Stopwatch.GetTimestamp();
		while (Stopwatch.GetElapsedTime(start) < TimeSpan.FromSeconds(20))
		{
			await using var observer = fixture.CreateConnection();
			var count = await observer.ExecuteScalarAsync<int>(new CommandDefinition(
				"SELECT COUNT(*) FROM v$session WHERE client_identifier=:Tag AND blocking_session IS NOT NULL",
				new { Tag = tag }, cancellationToken: token)).ConfigureAwait(false);
			if (count == 1) { return; }
			await Task.Delay(25, token).ConfigureAwait(false); // delay-ok: Poll the exact owned Oracle session row-lock wait under the existing stopwatch before cancellation.
		}
		throw new TimeoutException("The exact owned Oracle worker session never reached a database row-lock wait.");
	}

	private string Table => fixture.OutboxTableName;
	private string DeadTable => fixture.DeadLetterTableName;

	[SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "StateChange is a synchronous test-only callback configuring the fresh session before BeginTransaction, without async-void or global options.")]
	private OracleOutboxStore Store(bool linguistic = false, string? clientId = null, string? tableName = null, string? schemaName = null, string? fenceTableName = null)
	{
		var borrowed = A.Fake<IDb>();
		A.CallTo(() => borrowed.Connection).ReturnsLazily(() => fixture.CreateConnection());
		_callbackServices ??= new ServiceCollection().BuildServiceProvider();
		var source = OracleWorkerConnectionSource.FromFactory(
			_callbackServices.GetRequiredService<IServiceScopeFactory>(), (_, _) =>
			{
				var connection = fixture.CreateConnection();
				if (linguistic || clientId is not null)
				{
					connection.StateChange += (_, change) =>
					{
						if (change.CurrentState != ConnectionState.Open) { return; }
						if (clientId is not null) { connection.ClientId = clientId; }
						if (!linguistic) { return; }
						using var command = connection.CreateCommand();
						command.CommandText = "ALTER SESSION SET NLS_COMP=LINGUISTIC";
						_ = command.ExecuteNonQuery();
						command.CommandText = "ALTER SESSION SET NLS_SORT=BINARY_AI";
						_ = command.ExecuteNonQuery();
					};
				}
				return ValueTask.FromResult(connection);
			});
		return new OracleOutboxStore(borrowed, Options.Create(new OracleOutboxStoreOptions
		{
			SchemaName = schemaName ?? fixture.SchemaName, OutboxTableName = tableName ?? Table, DeadLetterTableName = DeadTable,
			FenceTableName = fenceTableName ?? fixture.FenceTableName, ReservationTimeout = 300, FailureBackoffFloorSeconds = 30,
		}), NullLogger<OracleOutboxStore>.Instance, timeProvider: new DispatcherClock(), workerConnections: source);
	}

	private static async Task StageAsync(IOutboxStore store, string id, bool scheduled = false) =>
		await store.StageMessageAsync(new OutboundMessage("type", Body, "destination")
		{
			Id = id, TenantId = "actual-tenant", CreatedAt = Created,
			ScheduledAt = scheduled ? DateTimeOffset.UtcNow.AddYears(1) : null,
		}, CancellationToken.None);

	private static async Task<OutboundMessage> ClaimAsync(OracleOutboxStore store, bool fenced) =>
		(fenced ? await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)
			: await store.GetUnsentMessagesAsync(1, CancellationToken.None)).Single();

	private static ValueTask<OutboxCompletionOutcome> CompleteAsync(OracleOutboxStore store, bool fenced, int operation, string owner) =>
		(fenced, operation) switch
		{
			(true, 0) => store.MarkSentAsync(MessageId, new OutboxWriteAuthority(7, owner), CancellationToken.None),
			(true, 1) => store.MarkFailedAsync(MessageId, "failure", 4, null, new OutboxWriteAuthority(7, owner), CancellationToken.None),
			(true, 2) => store.MarkFailedAsync(MessageId, "failure", 4, DispatcherNow.AddMinutes(2), new OutboxWriteAuthority(7, owner), CancellationToken.None),
			(true, _) => store.MarkDeadLetteredAsync(MessageId, "failure", 4, new OutboxWriteAuthority(7, owner), CancellationToken.None),
			(false, 0) => store.MarkSentAsync(MessageId, owner, CancellationToken.None),
			(false, 1) => store.MarkFailedAsync(MessageId, "failure", 4, owner, CancellationToken.None),
			(false, 2) => store.MarkFailedWithBackoffAsync(MessageId, "failure", 4, DispatcherNow.AddMinutes(2), owner, CancellationToken.None),
			_ => store.MarkDeadLetteredAsync(MessageId, "failure", 4, owner, CancellationToken.None),
		};

	private async Task VerifySelectedTransitionAsync(int operation, DateTimeOffset? before = null, DateTimeOffset? after = null)
	{
		if (operation is 0 or 3)
		{
			(await CountAsync($"SELECT COUNT(*) FROM {Table} WHERE message_id=:Id", new { Id = MessageId })).ShouldBe(0);
			(await CountAsync($"SELECT COUNT(*) FROM {DeadTable} WHERE message_id=:Id", new { Id = MessageId })).ShouldBe(operation == 3 ? 1 : 0);
			if (operation == 3)
			{
				(await CountAsync($"SELECT COUNT(*) FROM {DeadTable} WHERE message_id=:Id AND tenant_id='actual-tenant' AND attempts=4 AND DBMS_LOB.GETLENGTH(error_message)=7 AND DBMS_LOB.COMPARE(error_message, TO_CLOB('failure'))=0",
					new { Id = MessageId })).ShouldBe(1);
				await using var read = fixture.CreateConnection();
				(await read.ExecuteScalarAsync<string>($"SELECT RAWTOHEX(DBMS_LOB.SUBSTR(message_body,100,1)) FROM {DeadTable} WHERE message_id=:Id",
					new { Id = MessageId }).ConfigureAwait(false)).ShouldBe(Convert.ToHexString(Body));
			}
		}
		else
		{
			var lower = before ?? throw new InvalidOperationException("Missing server failure-window lower bound.");
			var upper = after ?? throw new InvalidOperationException("Missing server failure-window upper bound.");
			(await CountAsync($"SELECT COUNT(*) FROM {Table} WHERE message_id=:Id AND attempts=4 AND DBMS_LOB.GETLENGTH(error_message)=7 AND DBMS_LOB.COMPARE(error_message, TO_CLOB('failure'))=0 AND dispatcher_id IS NULL AND dispatcher_timeout IS NULL AND next_attempt_at>=:Before+NUMTODSINTERVAL(:Delay,'SECOND') AND next_attempt_at<=:After+NUMTODSINTERVAL(:Delay,'SECOND')",
				new { Id = MessageId, Before = lower, After = upper, Delay = operation == 2 ? 120 : 30 })).ShouldBe(1);
		}
	}

	private async Task AssertDistinctScopesHaveOneExactLinguisticSortKeyAsync()
	{
		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Fixed fixture table identifier; the actual stored keys are read, never supplied as SQL text.
		command.CommandText = $"SELECT scope_key, RAWTOHEX(NLSSORT(scope_key,'NLS_SORT=BINARY_AI')), UTL_RAW.LENGTH(NLSSORT(scope_key,'NLS_SORT=BINARY_AI')) FROM {fixture.FenceTableName}";
#pragma warning restore CA2100
		var scopes = new List<string>();
		var keys = new List<byte[]>();
		await using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
		{
			while (await reader.ReadAsync().ConfigureAwait(false))
			{
				(await reader.IsDBNullAsync(0).ConfigureAwait(false)).ShouldBeFalse();
				(await reader.IsDBNullAsync(1).ConfigureAwait(false)).ShouldBeFalse("the measured collision must have a real nonnull RAW key");
				(await reader.IsDBNullAsync(2).ConfigureAwait(false)).ShouldBeFalse();
				var scope = reader.GetString(0);
				scope.ShouldNotBeNullOrWhiteSpace();
				var key = Convert.FromHexString(reader.GetString(1));
				key.ShouldNotBeEmpty();
				reader.GetDecimal(2).ShouldBe(key.Length);
				scopes.Add(scope);
				keys.Add(key);
			}
		}
		scopes.Count.ShouldBe(2);
		string.Equals(scopes[0], scopes[1], StringComparison.Ordinal).ShouldBeFalse("the two persisted scope identities must remain byte-distinct");
		keys[1].ShouldBe(keys[0], "the actual nonempty server sort keys must be exactly equal, proving one linguistic equivalence class");

		// Independent literal control on the same server/connection; do not replace the persisted-row proof.
		command.CommandText = "SELECT RAWTOHEX(NLSSORT('QUFB','NLS_SORT=BINARY_AI')), RAWTOHEX(NLSSORT('QUFb','NLS_SORT=BINARY_AI')) FROM dual";
		await using var literal = await command.ExecuteReaderAsync().ConfigureAwait(false);
		(await literal.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
		(await literal.IsDBNullAsync(0).ConfigureAwait(false)).ShouldBeFalse();
		(await literal.IsDBNullAsync(1).ConfigureAwait(false)).ShouldBeFalse();
		var first = Convert.FromHexString(literal.GetString(0));
		var second = Convert.FromHexString(literal.GetString(1));
		first.ShouldNotBeEmpty();
		second.ShouldNotBeEmpty();
		second.ShouldBe(first);
		(await literal.ReadAsync().ConfigureAwait(false)).ShouldBeFalse();
	}

	private async Task<DateTimeOffset> ServerNowAsync()
	{
		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT SYSTIMESTAMP FROM dual";
		await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
		(await reader.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
		return ((OracleDataReader)reader).GetDateTimeOffset(0);
	}

	private sealed class DispatcherClock : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => DispatcherNow;
	}

	private async Task<int> CountAsync(string sql, object? parameters = null)
	{
		await using var connection = fixture.CreateConnection();
		return await connection.ExecuteScalarAsync<int>(sql, parameters).ConfigureAwait(false);
	}

	private async Task SqlAsync(string sql, object? parameters = null)
	{
		await using var connection = fixture.CreateConnection();
		_ = await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
	}
}
