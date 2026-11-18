// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.Data.DeadLetter;

/// <summary>
/// Verifies that a SQL dead-letter replay CLAIMS the entry before the effect, so the claim decides who
/// may deliver rather than recording that somebody did.
/// </summary>
/// <remarks>
/// The claim is a conditional UPDATE whose rowcount is the decision. These arms bind the three properties
/// that follow from it — one delivery under concurrency, a declined second replay, and no re-delivery
/// through a batch filter that omits the replayed flag — plus the liveness half that keeps a failed
/// replay retryable, and the two reachable release-failure paths.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerDeadLetterTestCollection.CollectionName)]
public sealed class SqlServerDeadLetterReplayClaimShould(SqlServerContainerFixture fixture) : IAsyncLifetime
{
	private const string StoredTenant = "replay-claim-tenant";
	private readonly string _tableName = "DeadLetterClaim_" + Guid.NewGuid().ToString("N");
	private volatile bool _failConnections;

	/// <summary>
	/// SAFETY. Two concurrent replays of ONE entry deliver exactly once.
	/// </summary>
	/// <remarks>
	/// The discriminating fixture state is a SINGLE entry that EXISTS with IsReplayed = 0, reached by two
	/// callers — one entry per caller would make this arm vacuous and it would pass either way. Both
	/// replay tasks target that same entry, and the handler simulates in-flight work. The semaphore below
	/// is not a two-caller admission barrier and no deterministic read/claim interleaving is asserted.
	/// Under the claim, the conditional UPDATE admits exactly one caller however the two interleave.
	/// </remarks>
	[Fact]
	public async Task DeliverOnceWhenTwoReplaysRaceTheSameEntry()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);

		var deliveries = 0;
		using var bothCallersReady = new SemaphoreSlim(0);

		Task Handler(object _)
		{
			_ = Interlocked.Increment(ref deliveries);

			// Simulate in-flight work so replay tasks can overlap; this is not deterministic mutant calibration.
			return Task.Delay(TimeSpan.FromMilliseconds(250)); // delay-ok: Simulate in-flight replay handler work; this delay is not a two-caller barrier or deterministic interleaving proof.
		}

		async Task<bool> Replay()
		{
			_ = bothCallersReady.Release();
			await bothCallersReady.WaitAsync(TestTimeout).ConfigureAwait(false);
			return await Queue(Handler).ReplayAsync(id, CancellationToken.None).ConfigureAwait(false);
		}

		var outcomes = await Task.WhenAll(Replay(), Replay()).ConfigureAwait(false);

		Volatile.Read(ref deliveries).ShouldBe(1, "two concurrent redrives of one entry must deliver it once");
		outcomes.Count(replayed => replayed).ShouldBe(1, "exactly one caller may report that it replayed the entry");

		var row = await ReadRowAsync(id).ConfigureAwait(false);
		row.IsReplayed.ShouldBeTrue();
		row.ReplayedAt.ShouldNotBeNull();
	}

	/// <summary>Replaying an already-replayed entry declines without invoking the handler again.</summary>
	[Fact]
	public async Task DeclineASecondReplayOfAnAlreadyReplayedEntry()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		var deliveries = 0;
		var queue = Queue(_ =>
		{
			deliveries++;
			return Task.CompletedTask;
		});

		(await queue.ReplayAsync(id, CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
		(await queue.ReplayAsync(id, CancellationToken.None).ConfigureAwait(false)).ShouldBeFalse();

		deliveries.ShouldBe(1, "the entry was already replayed, so the handler must not run a second time");
	}

	/// <summary>
	/// The claim is observable to the handler: it has already been taken when the effect runs.
	/// </summary>
	/// <remarks>
	/// The liveness counterpart to the arms above, and the positive statement of the ordering they rely
	/// on. An implementation that recorded the replay AFTER the handler would satisfy every "delivered
	/// once" assertion in a single-caller test while leaving the double delivery open under concurrency.
	/// </remarks>
	[Fact]
	public async Task HoldTheClaimAlreadyWhileTheHandlerRuns()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		var observed = false;

		(await Queue(async _ =>
		{
			var during = await ReadRowAsync(id).ConfigureAwait(false);
			during.IsReplayed.ShouldBeTrue("the claim authorises the handler, so it is taken before the effect");
			during.ReplayedAt.ShouldNotBeNull();
			observed = true;
		}).ReplayAsync(id, CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();

		observed.ShouldBeTrue();
	}

	/// <summary>
	/// A batch whose filter OMITS the replayed flag must not re-deliver entries already replayed.
	/// </summary>
	/// <remarks>
	/// This is the reachable operator harm: the batch surface takes its filter from the caller, so a
	/// filter that says nothing about the replayed flag enumerates every entry. Enumeration is expected
	/// to include the replayed entry; DELIVERY is not. Both halves are asserted, because an arm that only
	/// counted deliveries would also pass for a batch that enumerated nothing at all.
	/// </remarks>
	[Fact]
	public async Task NotRedeliverReplayedEntriesInABatchWhoseFilterOmitsTheReplayedFlag()
	{
		var alreadyReplayed = await EnqueueAsync().ConfigureAwait(false);
		var stillPending = await EnqueueAsync().ConfigureAwait(false);

		(await Queue(_ => Task.CompletedTask).ReplayAsync(alreadyReplayed, CancellationToken.None)
			.ConfigureAwait(false)).ShouldBeTrue();

		var batchDeliveries = 0;
		var result = await Queue(_ =>
		{
			batchDeliveries++;
			return Task.CompletedTask;
		}).ReplayBatchAsync(new DeadLetterQueryFilter(), 10, CancellationToken.None).ConfigureAwait(false);

		batchDeliveries.ShouldBe(1, "the already-replayed entry must not be delivered a second time by the batch");
		result.Enumerated.ShouldBe(2, "a filter that omits the replayed flag enumerates both entries");
		result.Replayed.ShouldBe(1, "only the entry that was still pending may be delivered");
		result.Truncated.ShouldBeFalse();

		(await ReadRowAsync(stillPending).ConfigureAwait(false)).IsReplayed.ShouldBeTrue();
	}

	/// <summary>
	/// LIVENESS. A throwing handler releases the claim, so the entry stays pending, stays selectable by
	/// PendingOnly(), and a later replay with a working handler delivers it.
	/// </summary>
	/// <remarks>
	/// Without the release, claiming first would convert a double-delivery defect into an entry-burning
	/// one: an ordinary transient downstream failure would consume the entry and PendingOnly() would hide
	/// it from the operator who needs to retry. All three properties are asserted, because the stored flag
	/// alone would not reveal that the operator's own query had stopped returning it.
	/// </remarks>
	[Fact]
	public async Task LeaveAThrowingReplayPendingSelectableAndReplayableByALaterCall()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		var fault = new InvalidOperationException("downstream briefly unavailable");

		var observed = await Should.ThrowAsync<InvalidOperationException>(
			() => Queue(_ => Task.FromException(fault)).ReplayAsync(id, CancellationToken.None));
		observed.ShouldBeSameAs(fault);

		var afterFailure = await ReadRowAsync(id).ConfigureAwait(false);
		afterFailure.IsReplayed.ShouldBeFalse("a failed replay must release its claim or the entry is burned");
		afterFailure.ReplayedAt.ShouldBeNull();

		var pending = await Queue().GetEntriesAsync(
			CancellationToken.None, DeadLetterQueryFilter.PendingOnly()).ConfigureAwait(false);
		pending.Select(entry => entry.Id).ShouldContain(id, "the operator must still be able to find the entry");

		var deliveries = 0;
		(await Queue(_ =>
		{
			deliveries++;
			return Task.CompletedTask;
		}).ReplayAsync(id, CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();

		deliveries.ShouldBe(1);
		(await ReadRowAsync(id).ConfigureAwait(false)).IsReplayed.ShouldBeTrue();
	}

	/// <summary>
	/// The accepted residual: when the release matches no row the entry stays marked replayed and
	/// undelivered, and the warning naming the remedy is the operator's only signal.
	/// </summary>
	/// <remarks>
	/// Reached by removing the row inside the handler and then throwing, so the release's conditional
	/// UPDATE matches nothing. The state it leaves is indistinguishable from a replay that succeeded,
	/// which is why the log line is asserted rather than the row.
	/// </remarks>
	[Fact]
	public async Task WarnWhenTheClaimReleaseMatchesNoRow()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		var logger = new CapturingLogger();

		_ = await Should.ThrowAsync<InvalidOperationException>(() => Queue(async _ =>
		{
			await DeleteRowAsync(id).ConfigureAwait(false);
			throw new InvalidOperationException("replay failed after the row went away");
		}, logger).ReplayAsync(id, CancellationToken.None));

		logger.Captured(LogLevel.Warning)
			.ShouldContain(message => message.Contains("could not be released", StringComparison.Ordinal)
				&& message.Contains("Reset IsReplayed", StringComparison.Ordinal),
				"the warning is the only signal an operator gets for this state, and it must name the remedy");
	}

	/// <summary>
	/// When the release itself fails, the caller still sees the replay's own fault, and the failure is
	/// reported rather than swallowed.
	/// </summary>
	[Fact]
	public async Task ReportTheOriginalFaultWhenTheClaimReleaseItselfFails()
	{
		var id = await EnqueueAsync().ConfigureAwait(false);
		var logger = new CapturingLogger();
		var fault = new InvalidOperationException("replay failed");

		var observed = await Should.ThrowAsync<InvalidOperationException>(() => Queue(_ =>
		{
			// Armed only after the claim has been taken, so the next factory call is the release's.
			_failConnections = true;
			return Task.FromException(fault);
		}, logger).ReplayAsync(id, CancellationToken.None));

		_failConnections = false;

		observed.ShouldBeSameAs(fault, "the bookkeeping failure must not displace the real one");
		logger.Captured(LogLevel.Error)
			.ShouldContain(message => message.Contains("releasing its claim also failed", StringComparison.Ordinal),
				"an operator must be told the entry remains marked replayed and undelivered");

		(await ReadRowAsync(id).ConfigureAwait(false)).IsReplayed
			.ShouldBeTrue("a release that could not run leaves the claim in place — the documented residual");
	}

	public async ValueTask InitializeAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue(
			"replay claim arms require a real SQL Server; infrastructure absence is a failure, never a skip");
		await using var connection = new SqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		_ = await connection.ExecuteAsync($"""
			CREATE TABLE [dbo].[{_tableName}] (
			    Id UNIQUEIDENTIFIER NOT NULL,
			    TenantId NVARCHAR(255) NOT NULL,
			    MessageType NVARCHAR(500) NOT NULL,
			    Payload VARBINARY(MAX) NOT NULL,
			    Reason INT NOT NULL,
			    ExceptionMessage NVARCHAR(MAX) NULL,
			    ExceptionStackTrace NVARCHAR(MAX) NULL,
			    EnqueuedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
			    OriginalAttempts INT NOT NULL DEFAULT 0,
			    Metadata NVARCHAR(MAX) NULL,
			    CorrelationId NVARCHAR(255) NULL,
			    CausationId NVARCHAR(255) NULL,
			    SourceQueue NVARCHAR(255) NULL,
			    IsReplayed BIT NOT NULL DEFAULT 0,
			    ReplayedAt DATETIMEOFFSET NULL,
			    PRIMARY KEY (Id, TenantId)
			);
			""").ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		_failConnections = false;
		await using var connection = new SqlConnection(fixture.ConnectionString);
		_ = await connection.ExecuteAsync($"DROP TABLE IF EXISTS [dbo].[{_tableName}]").ConfigureAwait(false);
	}

	private static TimeSpan TestTimeout => TimeSpan.FromSeconds(30);

	private SqlServerDeadLetterQueue Queue(Func<object, Task>? handler = null, ILogger<SqlServerDeadLetterQueue>? logger = null) => new(
		() => _failConnections
			? throw new InvalidOperationException("the store is unreachable")
			: new SqlConnection(fixture.ConnectionString),
		new SqlServerDeadLetterQueueOptions { TableName = _tableName },
		logger ?? new CapturingLogger(),
		new FixedTenantContext(), handler);

	private Task<Guid> EnqueueAsync() => Queue().EnqueueAsync(
		new OrderPayload("order-claim"), DeadLetterReason.MaxRetriesExceeded, CancellationToken.None);

	private async Task<ClaimRow> ReadRowAsync(Guid id)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		return await connection.QuerySingleAsync<ClaimRow>(
			$"SELECT IsReplayed, ReplayedAt FROM [dbo].[{_tableName}] WHERE Id = @Id AND TenantId = @TenantId",
			new { Id = id, TenantId = StoredTenant }).ConfigureAwait(false);
	}

	private async Task DeleteRowAsync(Guid id)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		(await connection.ExecuteAsync(
			$"DELETE FROM [dbo].[{_tableName}] WHERE Id = @Id AND TenantId = @TenantId",
			new { Id = id, TenantId = StoredTenant }).ConfigureAwait(false)).ShouldBe(1);
	}

	private sealed class FixedTenantContext : ITenantContext
	{
		public string? TenantId => StoredTenant;
		public bool HasTenant => true;
	}

	private sealed class ClaimRow
	{
		public bool IsReplayed { get; set; }
		public DateTimeOffset? ReplayedAt { get; set; }
	}

	private sealed class CapturingLogger : ILogger<SqlServerDeadLetterQueue>
	{
		private readonly List<(LogLevel Level, string Message)> _entries = [];

		public IReadOnlyList<string> Captured(LogLevel level)
		{
			lock (_entries)
			{
				return _entries.Where(entry => entry.Level == level).Select(entry => entry.Message).ToList();
			}
		}

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			ArgumentNullException.ThrowIfNull(formatter);
			lock (_entries)
			{
				_entries.Add((logLevel, formatter(state, exception)));
			}
		}
	}

	private sealed record OrderPayload(string OrderId);
}
