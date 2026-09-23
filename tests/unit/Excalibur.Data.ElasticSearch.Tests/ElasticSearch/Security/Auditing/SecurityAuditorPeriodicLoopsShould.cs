// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.AuditLogging;
using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Time.Testing;

using Tests.Shared.Infrastructure;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

/// <summary>
/// Deterministic locks on the two periodic loops <see cref="SecurityAuditor"/> owns — the audit-queue
/// drain and the daily compliance report — now that both run on <see cref="PeriodicTimer"/> driven by an
/// injected <see cref="TimeProvider"/> rather than raw <see cref="System.Threading.Timer"/> callbacks.
/// </summary>
/// <remarks>
/// Every test advances a <see cref="FakeTimeProvider"/>; none waits real time for a schedule. Each loop is
/// pinned by BOTH arms: a liveness arm (advancing one full interval runs the body) and a safety arm
/// (advancing less than one interval does not), so neither a loop that never fires nor a loop that fires
/// continuously can pass.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Security")]
public sealed class SecurityAuditorPeriodicLoopsShould
{
	private static readonly TimeSpan DrainInterval = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan ComplianceInterval = TimeSpan.FromHours(24);

	[Fact]
	public async Task DrainTheAuditQueue_WhenOneFullDrainIntervalElapses()
	{
		var (auditor, store, time, _) = CreateAuditor();
		await using var sut = auditor.ConfigureAwait(false);

		await auditor.AuditSecurityActivityAsync(
			new SecurityActivityEvent { UserId = "u1", ActivityType = "read", Timestamp = time.GetUtcNow() },
			TestContext.Current.CancellationToken).ConfigureAwait(false);

		time.Advance(DrainInterval);

		// LIVENESS: the drain body actually ran and handed the batch to the store.
		await WaitUntilAsync(() => AppendedEvents(store).Count > 0).ConfigureAwait(false);
		AppendedEvents(store).ShouldHaveSingleItem().UserId.ShouldBe("u1");
	}

	[Fact]
	public async Task NotDrainTheAuditQueue_WhenLessThanOneDrainIntervalElapses()
	{
		var (auditor, store, time, _) = CreateAuditor();
		await using var sut = auditor.ConfigureAwait(false);

		await auditor.AuditSecurityActivityAsync(
			new SecurityActivityEvent { UserId = "u1", ActivityType = "read", Timestamp = time.GetUtcNow() },
			TestContext.Current.CancellationToken).ConfigureAwait(false);

		time.Advance(DrainInterval - TimeSpan.FromMilliseconds(1));

		// SAFETY: give a misbehaving loop a real wall-clock window to fire in. A loop driven by the
		// FakeTimeProvider cannot tick however long we wait, so this can only fail an implementation whose
		// schedule escaped the injected clock -- which is the mutation the arm exists to catch.
		await GiveAWallClockLoopItsChanceAsync().ConfigureAwait(false);
		AppendedEvents(store).ShouldBeEmpty();
	}

	[Fact]
	public async Task GenerateComplianceReports_WhenOneFullComplianceIntervalElapses()
	{
		var (auditor, _, time, log) = CreateAuditor(ComplianceFramework.Gdpr);
		await using var sut = auditor.ConfigureAwait(false);

		time.Advance(ComplianceInterval);

		// LIVENESS: the compliance body ran. The query service logs the framework it is reporting on
		// before it touches Elasticsearch, so this observes the loop body and not the network.
		await WaitUntilAsync(() => log.Mentions("Generating compliance report")).ConfigureAwait(false);
	}

	[Fact]
	public async Task NotGenerateComplianceReports_WhenLessThanOneComplianceIntervalElapses()
	{
		var (auditor, _, time, log) = CreateAuditor(ComplianceFramework.Gdpr);
		await using var sut = auditor.ConfigureAwait(false);

		time.Advance(ComplianceInterval - TimeSpan.FromMinutes(1));

		// SAFETY: 23h59m is not a day, and a loop that ignored the injected clock would have reported
		// several times over inside the window below.
		await GiveAWallClockLoopItsChanceAsync().ConfigureAwait(false);
		log.Mentions("Generating compliance report").ShouldBeFalse();
	}

	[Fact]
	public async Task StopBothLoops_WhenDisposedAsync()
	{
		var (auditor, store, time, _) = CreateAuditor(ComplianceFramework.Gdpr);

		await auditor.DisposeAsync().ConfigureAwait(false);

		// Disposal is idempotent and never throws.
		await auditor.DisposeAsync().ConfigureAwait(false);
		auditor.Dispose();

		// The loops are gone: enqueueing is refused by the completed channel and ticks no longer drain.
		time.Advance(ComplianceInterval);
		await GiveAWallClockLoopItsChanceAsync().ConfigureAwait(false);
		AppendedEvents(store).ShouldBeEmpty();
	}

	[Fact]
	public void ExposeNoRawThreadingTimerField()
	{
		// The migration is structural, not incidental: a re-introduced raw Timer would restore the
		// wall-clock schedule and the unobserved void callback this class was moved off.
		typeof(SecurityAuditor)
			.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
			.ShouldNotContain(f => f.FieldType == typeof(System.Threading.Timer));
	}

	[Fact]
	public async Task NotDropRecords_WhenTheFlushTargetThrows()
	{
		// SAFETY (never-drop): a record handed to the auditor is not lost because the flush target
		// failed. The auditor holds un-flushed events in its retry buffer and re-attempts them on the
		// next drain tick, so the proof is that the EXACT set reappears once the target recovers.
		var (auditor, store, time, _) = CreateAuditor();
		await using var sut = auditor.ConfigureAwait(false);

		store.ThrowOnFlush = true;

		foreach (var user in new[] { "u1", "u2", "u3" })
		{
			await auditor.AuditSecurityActivityAsync(
				new SecurityActivityEvent { UserId = user, ActivityType = "read", Timestamp = time.GetUtcNow() },
				TestContext.Current.CancellationToken).ConfigureAwait(false);
		}

		time.Advance(DrainInterval);
		await WaitUntilAsync(() => store.FlushAttempts > 0).ConfigureAwait(false);
		AppendedEvents(store).ShouldBeEmpty();

		// The target recovers. Every record must arrive — asserted as the exact set, so a store that
		// kept only the last batch, or only one record, cannot pass on a non-zero count.
		store.ThrowOnFlush = false;
		time.Advance(DrainInterval);

		await WaitUntilAsync(() => AppendedEvents(store).Count >= 3).ConfigureAwait(false);
		AppendedEvents(store).Select(static e => e.UserId).OrderBy(static u => u, StringComparer.Ordinal)
			.ShouldBe(["u1", "u2", "u3"]);
	}

	[Fact]
	public async Task NotDeadlock_AfterTheFlushTargetThrows()
	{
		// SAFETY (never-deadlock): a throwing flush must not wedge the drain loop or a caller. Every
		// wait here is bounded by WaitUntilAsync, which FAILS rather than hangs, so a wedge is a red
		// test and never a hung run.
		var (auditor, store, time, _) = CreateAuditor();
		await using var sut = auditor.ConfigureAwait(false);

		store.ThrowOnFlush = true;
		await auditor.AuditSecurityActivityAsync(
			new SecurityActivityEvent { UserId = "before", ActivityType = "read", Timestamp = time.GetUtcNow() },
			TestContext.Current.CancellationToken).ConfigureAwait(false);

		time.Advance(DrainInterval);
		await WaitUntilAsync(() => store.FlushAttempts > 0).ConfigureAwait(false);

		// A caller submitting AFTER the failed flush still completes — this await is the assertion.
		await auditor.AuditSecurityActivityAsync(
			new SecurityActivityEvent { UserId = "after", ActivityType = "read", Timestamp = time.GetUtcNow() },
			TestContext.Current.CancellationToken).ConfigureAwait(false);

		// And the loop still makes progress rather than having stopped on the exception.
		var attemptsBefore = store.FlushAttempts;
		time.Advance(DrainInterval);
		await WaitUntilAsync(() => store.FlushAttempts > attemptsBefore).ConfigureAwait(false);

		store.ThrowOnFlush = false;
		time.Advance(DrainInterval);
		await WaitUntilAsync(() => AppendedEvents(store).Count >= 2).ConfigureAwait(false);
		AppendedEvents(store).Select(static e => e.UserId).OrderBy(static u => u, StringComparer.Ordinal)
			.ShouldBe(["after", "before"]);
	}

	[Fact]
	public async Task DeliverRecordsToTheSink_WhenTheFlushTargetSucceeds()
	{
		// LIVENESS for the two arms above. Without this, an auditor that silently discarded everything
		// would never "drop" observably and never deadlock, and would satisfy both safety arms while
		// delivering nothing.
		var (auditor, store, time, _) = CreateAuditor();
		await using var sut = auditor.ConfigureAwait(false);

		foreach (var user in new[] { "a", "b" })
		{
			await auditor.AuditSecurityActivityAsync(
				new SecurityActivityEvent { UserId = user, ActivityType = "read", Timestamp = time.GetUtcNow() },
				TestContext.Current.CancellationToken).ConfigureAwait(false);
		}

		time.Advance(DrainInterval);

		await WaitUntilAsync(() => AppendedEvents(store).Count >= 2).ConfigureAwait(false);
		AppendedEvents(store).Select(static e => e.UserId).ShouldBe(["a", "b"]);
	}

	private static IReadOnlyList<SecurityAuditEvent> AppendedEvents(RecordingAuditStore store) => store.Appended;

	/// <summary>
	/// Waits long enough for a loop still bound to the wall clock to have ticked many times over.
	/// </summary>
	/// <remarks>
	/// A correct implementation is driven entirely by the injected <see cref="FakeTimeProvider"/> and cannot
	/// tick during this window no matter how long it is, so the wait adds no flakiness -- it only gives the
	/// SAFETY arms something a continuously-running loop can actually fail.
	/// </remarks>
	private static Task GiveAWallClockLoopItsChanceAsync() =>
		// delay-ok: the DURATION is the semantic. These are SAFETY arms asserting that NOTHING happened,
		// and a negative cannot be polled for -- the window IS the assertion. A correct implementation is
		// driven entirely by the injected FakeTimeProvider and provably cannot tick during it however long
		// it runs, so this adds no flakiness in either direction; it exists solely so a loop whose schedule
		// escaped the injected clock has real wall-clock time in which to fire and go red.
		Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken); // delay-ok: see above

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		// Bounds a scheduling delay only — the SCHEDULE itself is driven by FakeTimeProvider, so this
		// never waits for wall-clock time to pass, only for an already-triggered continuation to run.
		var observed = await WaitHelpers.WaitUntilAsync(
			condition,
			TimeSpan.FromSeconds(30),
			TimeSpan.FromMilliseconds(10),
			TestContext.Current.CancellationToken).ConfigureAwait(false);

		observed.ShouldBeTrue("the periodic loop body did not run within the scheduling budget");
	}

	private static (SecurityAuditor Auditor, RecordingAuditStore Store, FakeTimeProvider Time, RecordingLogger Log) CreateAuditor(
		params ComplianceFramework[] frameworks)
	{
		// Nothing is listening, and the compliance body reaches the client. Pin a short timeout and no
		// retries so the failing call returns promptly: the SCHEDULE is what these locks are about, and a
		// default multi-minute client timeout would make disposal wait on it.
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://localhost:9200"))
			.RequestTimeout(TimeSpan.FromMilliseconds(250))
			.MaximumRetries(0));
		var store = new RecordingAuditStore();
		var time = new FakeTimeProvider();
		var log = new RecordingLogger();

		var options = Options.Create(new AuditOptions
		{
			Enabled = true,
			EnsureLogIntegrity = false,
			MaskPiiInAuditEvents = false,
			ComplianceFrameworks = [.. frameworks],
		});

		var auditor = new SecurityAuditor(
			client,
			store,
			options,
			Options.Create(new SecurityMonitoringOptions()),
			A.Fake<IAuditIntegrityStrategy>(),
			sanitizer: null,
			time,
			log);

		return (auditor, store, time, log);
	}

	private sealed class RecordingAuditStore : ISecurityAuditStore
	{
		private readonly ConcurrentQueue<SecurityAuditEvent> _appended = new();
		private int _flushAttempts;

		public IReadOnlyList<SecurityAuditEvent> Appended => [.. _appended];

		public Task<bool> EnsureAuditIndexTemplateAsync(CancellationToken cancellationToken) => Task.FromResult(false);

		/// <summary>
		/// When set, the flush throws instead of appending. Off by default, so the arms that predate
		/// the never-drop locks are unaffected.
		/// </summary>
		public bool ThrowOnFlush { get; set; }

		/// <summary>
		/// Counts flush attempts including failed ones, so a test can wait for the drain loop to have
		/// REACHED the sink rather than waiting for records to arrive — the distinction the never-drop
		/// and never-deadlock arms turn on.
		/// </summary>
		public int FlushAttempts => Volatile.Read(ref _flushAttempts);

		public Task<AuditBulkAppendResult> BulkAppendEventsAsync(
			IReadOnlyList<SecurityAuditEvent> events,
			CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref _flushAttempts);

			if (ThrowOnFlush)
			{
				throw new InvalidOperationException("audit sink is unavailable");
			}

			foreach (var e in events)
			{
				_appended.Enqueue(e);
			}

			return Task.FromResult(new AuditBulkAppendResult(true, null, events.Count));
		}
	}

	private sealed class RecordingLogger : ILogger<SecurityAuditor>
	{
		private readonly ConcurrentQueue<string> _messages = new();

		public bool Mentions(string fragment) => _messages.Any(m => m.Contains(fragment, StringComparison.Ordinal));

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter) => _messages.Enqueue(formatter(state, exception));
	}
}
