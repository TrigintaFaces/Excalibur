// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;

using Excalibur.Compliance.Diagnostics;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Tests.Shared.Helpers;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the sweep's behaviour when a legal hold is extended in the window between the sweep reading it
/// and writing its release.
/// </summary>
/// <remarks>
/// <para>
/// A legal hold is the authority that stops an erasure destroying records a controller is legally obliged
/// to keep. The sweep is a read-modify-write: it reads the expired holds, then writes each one back
/// deactivated. Written blindly, an operator extending the expiry inside that window loses the extension —
/// the hold is released, the next erasure for that subject proceeds, and nothing reports the lost update:
/// the released record carries a plausible reason, so an auditor sees a legitimate auto-release.
/// </para>
/// <para>
/// These arms run against the REAL in-memory store rather than a fake, because the property under test is
/// the store's compare-and-set. A fake that returns <see langword="true"/> would report PASS with no
/// concurrency control present at all.
/// </para>
/// <para>
/// The interleaving is produced deterministically by <see cref="ExtendOnFirstUpdateStore"/> rather than by
/// racing two tasks, so the arm cannot flake and cannot pass by accident of timing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ExtendingAHoldDuringTheExpirationSweepShould
{
	private const string CaseReference = "CASE-RETENTION-EXTENDED";

	/// <summary>
	/// The RED arm. Fails against a blind whole-record write: the sweep's stale copy lands, the hold is
	/// released, and the extension is gone.
	/// </summary>
	[Fact]
	public async Task Leave_the_hold_active_with_its_extended_expiry_and_report_the_conflict()
	{
		var extendedExpiry = DateTimeOffset.UtcNow.AddYears(5);
		var inner = NewStore();
		var hold = ExpiredHold();

		await inner.SaveHoldAsync(hold, TestContext.Current.CancellationToken).ConfigureAwait(false);

		// The concurrent writer lands between the sweep's read and its write.
		var store = new ExtendOnFirstUpdateStore(inner, hold.HoldId, extendedExpiry);
		var logger = new CapturingLogger<LegalHoldExpirationService>();

		using var outcomeMeter = new OutcomeCounterProbe();

		await RunOneSweepCycleAsync(store, logger).ConfigureAwait(false);

		var outcomes = outcomeMeter.Counts;

		store.ExtensionApplied.ShouldBeTrue(
			"the arm is vacuous unless the concurrent extension actually landed before the sweep's write");

		// A log line is read by somebody already looking. This is the instrument an operator alerts on, so
		// the outcome must be distinguishable without parsing text.
		outcomes.ShouldContainKey(
			"extended",
			"the sweep must COUNT the conflict under its own outcome, not only log it -- a hold whose "
			+ "release keeps losing is exactly the condition nobody is reading log lines for");

		var stored = await inner.GetHoldAsync(hold.HoldId, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		stored.ShouldNotBeNull();
		stored.IsActive.ShouldBeTrue(
			"the hold was extended while the sweep was releasing it, so it must REMAIN ACTIVE -- releasing "
			+ "it lets the next erasure destroy the records it was extended to preserve");
		stored.ExpiresAt.ShouldBe(extendedExpiry,
			"the extension must survive: a sweep must not write its stale copy over a newer one");
		stored.ReleasedBy.ShouldBeNull("nothing was released, so nothing may be stamped as released");
		stored.ReleaseReason.ShouldBeNull();

		// Reported, not absorbed. A sweep that silently skips a hold it could not release is the same
		// silence one layer up.
		var conflictReports = logger.Entries
			.Where(e => e.EventId.Id == ComplianceEventId.LegalHoldExpirationConflictHoldExtended)
			.ToList();

		conflictReports.ShouldNotBeEmpty(
			"the sweep must report the conflict at a level an operator sees, not absorb it");
		conflictReports.ShouldAllBe(e => e.Level >= Microsoft.Extensions.Logging.LogLevel.Warning);
		conflictReports[0].Message.ShouldContain(CaseReference);

		logger.Entries.ShouldContain(
			e => e.EventId.Id == ComplianceEventId.LegalHoldExpirationCycleConflicts,
			"the cycle must report how many holds it could not release, so a persistently conflicting hold "
			+ "is visible rather than being one line lost among many cycles");

		logger.Entries.ShouldNotContain(
			e => e.EventId.Id == ComplianceEventId.LegalHoldExpirationAutoReleased,
			"a hold that was not released must not be reported as released");
	}

	/// <summary>
	/// The liveness arm. Without it, a sweep that releases NOTHING passes the arm above and looks
	/// identical to a correct one — until a retention outlives its statutory period.
	/// </summary>
	[Fact]
	public async Task Still_release_a_genuinely_expired_hold_when_no_writer_contends()
	{
		var store = NewStore();
		var hold = ExpiredHold();

		await store.SaveHoldAsync(hold, TestContext.Current.CancellationToken).ConfigureAwait(false);

		var logger = new CapturingLogger<LegalHoldExpirationService>();

		using var outcomeMeter = new OutcomeCounterProbe();

		await RunOneSweepCycleAsync(store, logger).ConfigureAwait(false);

		outcomeMeter.Counts.ShouldContainKey(
			"released",
			"the liveness arm must also prove the counter is not stuck reporting conflicts -- an instrument "
			+ "that only ever emits the alerting value is as useless as one that never emits it");

		var stored = await store.GetHoldAsync(hold.HoldId, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		stored.ShouldNotBeNull();
		stored.IsActive.ShouldBeFalse("an expired hold with no concurrent writer must still be released");
		stored.ReleasedBy.ShouldNotBeNullOrWhiteSpace();
		stored.ReleaseReason.ShouldNotBeNullOrWhiteSpace();
		stored.Version.ShouldBe(hold.Version + 1, "the successful release must advance the version");

		logger.Entries.ShouldContain(
			e => e.EventId.Id == ComplianceEventId.LegalHoldExpirationAutoReleased);
		logger.Entries.ShouldNotContain(
			e => e.EventId.Id == ComplianceEventId.LegalHoldExpirationCycleConflicts);
	}

	private static InMemoryLegalHoldStore NewStore() =>
		new(UntenantedContext.Instance,
			Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	private static LegalHold ExpiredHold() => new()
	{
		HoldId = Guid.NewGuid(),
		DataSubjectIdHash = "hash-1",
		IdType = DataSubjectIdType.UserId,
		Basis = LegalHoldBasis.LegalObligation,
		CaseReference = CaseReference,
		Description = "Records a statute requires be kept",
		IsActive = true,
		ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
		CreatedBy = "legal@test",
		CreatedAt = DateTimeOffset.UtcNow.AddYears(-1)
	};

	/// <summary>
	/// Runs the background service until it has completed one sweep cycle, then stops it.
	/// </summary>
	private static async Task RunOneSweepCycleAsync(
		ILegalHoldStore store,
		CapturingLogger<LegalHoldExpirationService> logger)
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(ILegalHoldStore))).Returns(store);

		var scope = A.Fake<IServiceScope>();
		A.CallTo(() => scope.ServiceProvider).Returns(serviceProvider);

		var scopeFactory = A.Fake<IServiceScopeFactory>();
		A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);

		var sut = new LegalHoldExpirationService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(new LegalHoldExpirationOptions
			{
				Enabled = true,

				// Long enough that the assertions observe exactly one cycle. Polling, not sleeping, is what
				// makes the wait deterministic.
				PollingInterval = TimeSpan.FromMinutes(10)
			}),
			logger);

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);

		var cycleObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => logger.Entries.Any(e =>
				e.EventId.Id == ComplianceEventId.LegalHoldExpirationAutoReleased
				|| e.EventId.Id == ComplianceEventId.LegalHoldExpirationCycleConflicts
				|| e.EventId.Id == ComplianceEventId.LegalHoldExpirationTargetMissing
				|| e.EventId.Id == ComplianceEventId.LegalHoldExpirationReleaseFailed),
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);

		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		cycleObserved.ShouldBeTrue("the sweep must complete one cycle and say what it did");
	}

	/// <summary>
	/// Collects the expiration sweep's outcome counter, keyed by its <c>outcome</c> tag.
	/// </summary>
	/// <remarks>
	/// The counter is a process-lifetime static, so this only ever asserts that an outcome WAS recorded --
	/// never that one was absent. Another arm running in parallel can add to a series; it cannot remove
	/// from one, so a presence assertion stays deterministic while an absence assertion would not.
	/// </remarks>
	private sealed class OutcomeCounterProbe : IDisposable
	{
		private readonly MeterListener _listener = new();
		private readonly Dictionary<string, long> _counts = [];
		private readonly object _gate = new();

		public OutcomeCounterProbe()
		{
			_listener.InstrumentPublished = (instrument, listener) =>
			{
				if (instrument.Meter.Name == ErasureTelemetryConstants.MeterName
					&& instrument.Name == ErasureTelemetryConstants.MetricNames.LegalHoldExpirationOutcomes)
				{
					listener.EnableMeasurementEvents(instrument);
				}
			};

			_listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
			{
				foreach (var tag in tags)
				{
					if (tag.Key != "outcome" || tag.Value is not string outcome)
					{
						continue;
					}

					lock (_gate)
					{
						_counts[outcome] = _counts.TryGetValue(outcome, out var existing)
							? existing + measurement
							: measurement;
					}
				}
			});

			_listener.Start();
		}

		public IReadOnlyDictionary<string, long> Counts
		{
			get
			{
				lock (_gate)
				{
					return new Dictionary<string, long>(_counts);
				}
			}
		}

		public void Dispose() => _listener.Dispose();
	}

	/// <summary>
	/// A decorator that applies a competing expiry extension to the inner store immediately before
	/// forwarding the first update, reproducing the read-modify-write window without racing.
	/// </summary>
	private sealed class ExtendOnFirstUpdateStore : ILegalHoldStore
	{
		private readonly InMemoryLegalHoldStore _inner;
		private readonly Guid _holdId;
		private readonly DateTimeOffset _extendedExpiry;
		private bool _extended;

		public ExtendOnFirstUpdateStore(
			InMemoryLegalHoldStore inner,
			Guid holdId,
			DateTimeOffset extendedExpiry)
		{
			_inner = inner;
			_holdId = holdId;
			_extendedExpiry = extendedExpiry;
		}

		public bool ExtensionApplied => _extended;

		public Task SaveHoldAsync(LegalHold hold, CancellationToken cancellationToken) =>
			_inner.SaveHoldAsync(hold, cancellationToken);

		public Task<LegalHold?> GetHoldAsync(Guid holdId, CancellationToken cancellationToken) =>
			_inner.GetHoldAsync(holdId, cancellationToken);

		public async Task<bool> UpdateHoldAsync(LegalHold hold, CancellationToken cancellationToken)
		{
			if (!_extended && hold.HoldId == _holdId)
			{
				var current = await _inner.GetHoldAsync(_holdId, cancellationToken).ConfigureAwait(false)
					?? throw new InvalidOperationException("The hold under test must exist.");

				var extended = await _inner.UpdateHoldAsync(
					current with { ExpiresAt = _extendedExpiry },
					cancellationToken).ConfigureAwait(false);

				if (!extended)
				{
					throw new InvalidOperationException("The competing extension must have been applied.");
				}

				_extended = true;
			}

			// The sweep's own write, still carrying the version it read.
			return await _inner.UpdateHoldAsync(hold, cancellationToken).ConfigureAwait(false);
		}

		public object? GetService(Type serviceType) => _inner.GetService(serviceType);
	}
}
