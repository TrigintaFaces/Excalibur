// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using System.Reflection;
using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.TieredStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using IEventStore = Excalibur.EventSourcing.IEventStore;
using StoredEvent = Excalibur.EventSourcing.StoredEvent;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

/// <summary>
/// Gap-fill tests for <see cref="EventArchiveService"/> -- archive cycle logic,
/// best-effort per aggregate, skip when no policy, cold write + hot delete ordering.
/// Tests invoke RunArchiveCycleAsync directly (via reflection) for deterministic execution.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class EventArchiveServiceShould
{
	/// <summary>The tenant every candidate in this fixture belongs to; the archive service must carry it
	/// from the candidate through to BOTH the cold write and the hot delete.</summary>
	private static readonly KeyedTenantPartition TestTenant = KeyedTenantPartition.Scoped("tenant-a");

	private readonly IEventStoreArchive _archiveSource = A.Fake<IEventStoreArchive>();
	private readonly IEventStoreArchiveReader _hotStore = A.Fake<IEventStoreArchiveReader>();
	private readonly IColdEventStore _coldStore = A.Fake<IColdEventStore>();

	[Fact]
	public async Task ContinueArchivingAfterThePreviousCycleLeftHotMarkers()
	{
		var original = CreateEvents("agg-1", 0, 1);
		var cold = new List<StoredEvent>();
		var submissions = new List<long[]>();
		var ceiling = 0;
		A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new List<ArchiveCandidate> { new(TestTenant, "agg-1", "Order", ceiling, ceiling) });
		A.CallTo(() => _coldStore.WriteAsync(TestTenant, "agg-1", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.ReturnsLazily((KeyedTenantPartition tenant, string id, string aggregateType, IReadOnlyList<StoredEvent> batch, CancellationToken _) =>
			{
				submissions.Add(batch.Select(e => e.Version).ToArray());
				cold.AddRange(ColdArchiveBatch.GetAdditions(tenant, id, cold, batch));
				return Task.FromResult(ColdArchiveBatch.ContiguousDurablePrefix(cold));
			});
		A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "agg-1", "Order", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { original[0] });
		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) });
		await InvokeArchiveCycleAsync(service);
		cold.Select(e => e.Version).ShouldBe([0L]);
		A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(TestTenant, "agg-1", "Order", 0, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		ceiling = 1;
		A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "agg-1", "Order", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { original[0] with { EventData = null, ArchivedAt = DateTimeOffset.UnixEpoch }, original[1] });
		await InvokeArchiveCycleAsync(service);

		submissions.Count.ShouldBe(2);
		submissions[0].ShouldBe([0L]);
		submissions[1].ShouldBe([1L]);
		cold.Select(e => e.Version).ShouldBe([0L, 1L]);
		cold.ShouldAllBe(e => e.EventData != null);
		A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(TestTenant, "agg-1", "Order", 1, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RefuseErasedEventsEvenWhenTheyHaveAnArchiveStampOrReplacementPayload(bool replacementPayload)
	{
		A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(new List<ArchiveCandidate> { new(TestTenant, "agg-1", "Order", 1, 1) });
		var original = CreateEvents("agg-1", 0, 1);
		A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "agg-1", "Order", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { original[0] with { EventType = "$erased", EventData = replacementPayload ? [0] : null, ArchivedAt = DateTimeOffset.UnixEpoch }, original[1] });
		await InvokeArchiveCycleAsync(CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) }));
		A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, A<string>._, "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, A<string>._, A<string>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task DistinguishPreviouslyArchivedMarkersFromUnresolvedPayloads(bool archived)
	{
		A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(new List<ArchiveCandidate> { new(TestTenant, "agg-1", "Order", 1, 1) });
		var original = CreateEvents("agg-1", 0, 1);
		A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "agg-1", "Order", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { original[0] with { EventData = null, ArchivedAt = archived ? DateTimeOffset.UnixEpoch : null }, original[1] });
		A.CallTo(() => _coldStore.WriteAsync(TestTenant, "agg-1", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._)).Returns(1L);
		await InvokeArchiveCycleAsync(CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) }));
		if (archived)
		{
			A.CallTo(() => _coldStore.WriteAsync(TestTenant, "agg-1", "Order",
				A<IReadOnlyList<StoredEvent>>.That.Matches(events => events.Count == 1 && events[0].Version == 1 && events[0].EventData != null),
				A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(TestTenant, "agg-1", "Order", 1, A<CancellationToken>._))
				.MustHaveHappenedOnceExactly();
		}
		else
		{
			A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, A<string>._, "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._)).MustNotHaveHappened();
			A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, A<string>._, A<string>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
	}

	[Fact]
	public async Task ArchiveEventsFromHotToCold()
	{
		var candidates = new List<ArchiveCandidate> { new(TestTenant, "agg-1", "Order", 5, 5) };
		_ = A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(candidates);

		var events = CreateEvents("agg-1", 1, 2, 3, 4, 5);
		_ = A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "agg-1", "Order", A<long>._, A<CancellationToken>._))
			.Returns(events);
		// Cold store confirms the full range durable (watermark = 5), so hot delete is authorized up to 5.
		_ = A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "agg-1", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.Returns(5L);
		_ = A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-1", "Order", 5, A<CancellationToken>._))
			.Returns(5);

		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) });

		// Act -- invoke cycle directly (deterministic, no timing)
		await InvokeArchiveCycleAsync(service);

		// Assert
		A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "agg-1", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-1", "Order", 5, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteOnlyUpToTheDurableColdWatermark()
	{
		// SAFETY: cold store durably confirmed only versions <= 3 (a partial/deferred write of a 1..5 batch),
		// so the hot delete MUST be bounded to 3 — never the submitted max of 5 — or events 4,5 would be
		// destroyed while their only durable copy is not yet in cold.
		var candidates = new List<ArchiveCandidate> { new(TestTenant, "agg-p", "Order", 5, 5) };
		_ = A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(candidates);

		var events = CreateEvents("agg-p", 1, 2, 3, 4, 5);
		_ = A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "agg-p", "Order", A<long>._, A<CancellationToken>._))
			.Returns(events);
		_ = A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "agg-p", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.Returns(3L);
		_ = A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-p", "Order", A<long>._, A<CancellationToken>._))
			.Returns(3);

		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) });

		await InvokeArchiveCycleAsync(service);

		// LIVENESS: the confirmed prefix (<= 3) IS deleted — the archive still makes progress.
		A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-p", "Order", 3, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		// SAFETY: nothing is deleted beyond the durable watermark.
		A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-p", "Order", 5, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task NotDeleteWhenNothingDurablyArchived()
	{
		// SAFETY: cold store confirms no durable watermark at/above the first candidate version (returns -1,
		// e.g. a buffering writer that only enqueued) — the hot delete MUST NOT run at all.
		var candidates = new List<ArchiveCandidate> { new(TestTenant, "agg-n", "Order", 3, 3) };
		_ = A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(candidates);

		var events = CreateEvents("agg-n", 1, 2, 3);
		_ = A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "agg-n", "Order", A<long>._, A<CancellationToken>._))
			.Returns(events);
		_ = A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "agg-n", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.Returns(-1L);

		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) });

		await InvokeArchiveCycleAsync(service);

		A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-n", "Order", A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReportTheRowsTheHotDeleteActuallyRemoved()
	{
		// The load-bearing property is the OUTCOME the operator sees, not that the delete was called:
		// a hot delete that removes zero rows leaves the events in both tiers, so the cycle must NOT
		// report an archive. RED before the fix, where 3030 was logged unconditionally.
		var candidates = new List<ArchiveCandidate> { new(TestTenant, "agg-z", "Order", 3, 3) };
		_ = A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(candidates);
		_ = A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "agg-z", "Order", A<long>._, A<CancellationToken>._))
			.Returns(CreateEvents("agg-z", 1, 2, 3));
		_ = A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "agg-z", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.Returns(3L);
		_ = A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-z", "Order", A<long>._, A<CancellationToken>._))
			.Returns(0);

		var logger = new CapturingLogger();
		await InvokeArchiveCycleAsync(CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) }, logger));

		logger.Events.ShouldContain(3031);   // warned that the hot tier still holds the events
		logger.Events.ShouldNotContain(3030); // and did NOT report an archive
	}

	[Fact]
	public async Task ReportAnArchiveWhenTheHotDeleteRemovedRows()
	{
		// LIVENESS: the success path still reports 3030, so the safety arm above cannot pass vacuously.
		var candidates = new List<ArchiveCandidate> { new(TestTenant, "agg-y", "Order", 3, 3) };
		_ = A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(candidates);
		_ = A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "agg-y", "Order", A<long>._, A<CancellationToken>._))
			.Returns(CreateEvents("agg-y", 1, 2, 3));
		_ = A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "agg-y", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.Returns(3L);
		_ = A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-y", "Order", A<long>._, A<CancellationToken>._))
			.Returns(3);

		var logger = new CapturingLogger();
		await InvokeArchiveCycleAsync(CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) }, logger));

		logger.Events.ShouldContain(3030);
		logger.Events.ShouldNotContain(3031);
	}

	[Fact]
	public async Task SkipCycleWhenNoPolicyCriteriaConfigured()
	{
		var service = CreateService(new ArchivePolicy());

		await InvokeArchiveCycleAsync(service);

		A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ContinueOnPerAggregateFailure()
	{
		var candidates = new List<ArchiveCandidate>
		{
			new(TestTenant, "fail-agg", "Order", 3, 3),
			new(TestTenant, "ok-agg", "Order", 2, 2)
		};
		_ = A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(candidates);

		_ = A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "fail-agg", "Order", A<long>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("DB unavailable"));

		var events = CreateEvents("ok-agg", 1, 2);
		_ = A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "ok-agg", "Order", A<long>._, A<CancellationToken>._))
			.Returns(events);
		_ = A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "ok-agg", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.Returns(2L);
		_ = A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, "ok-agg", "Order", 2, A<CancellationToken>._))
			.Returns(2);

		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(1) });

		await InvokeArchiveCycleAsync(service);

		A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "ok-agg", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipWhenNoCandidatesFound()
	{
		_ = A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(new List<ArchiveCandidate>());

		var service = CreateService(new ArchivePolicy { RetainRecentCount = 100 });

		await InvokeArchiveCycleAsync(service);

		A.CallTo(() => _coldStore.WriteAsync(
			A<KeyedTenantPartition>._, A<string>._, "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void ThrowOnNullArchiveSource()
	{
		var pm = new OptionsMonitorWrapper<ArchivePolicy>(new ArchivePolicy());
		var om = new OptionsMonitorWrapper<EventArchiveServiceOptions>(new EventArchiveServiceOptions());
		Should.Throw<ArgumentNullException>(() => new EventArchiveService(
			null!, _hotStore, new SinglePageScanner(_archiveSource), _coldStore, pm, om, NullLogger<EventArchiveService>.Instance));
	}

	[Fact]
	public void ThrowOnNullHotStore()
	{
		var pm = new OptionsMonitorWrapper<ArchivePolicy>(new ArchivePolicy());
		var om = new OptionsMonitorWrapper<EventArchiveServiceOptions>(new EventArchiveServiceOptions());
		Should.Throw<ArgumentNullException>(() => new EventArchiveService(
			_archiveSource, null!, new SinglePageScanner(_archiveSource), _coldStore, pm, om, NullLogger<EventArchiveService>.Instance));
	}

	[Fact]
	public void ThrowOnNullColdStore()
	{
		var pm = new OptionsMonitorWrapper<ArchivePolicy>(new ArchivePolicy());
		var om = new OptionsMonitorWrapper<EventArchiveServiceOptions>(new EventArchiveServiceOptions());
		Should.Throw<ArgumentNullException>(() => new EventArchiveService(
			_archiveSource, _hotStore, new SinglePageScanner(_archiveSource), null!, pm, om, NullLogger<EventArchiveService>.Instance));
	}

	[Fact]
	public async Task AdvancePastPoisonAndEmptyPagesThenRetryOnTheNextRound()
	{
		var scanner = A.Fake<IEventStoreArchiveScanner>();
		var first = new TestCursor();
		var second = new TestCursor();
		var poison = new ArchiveCandidate(TestTenant, "poison", "Order", 0, 1);
		var healthy = new ArchiveCandidate(TestTenant, "healthy", "Order", 0, 1);
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, null, A<CancellationToken>._))
			.Returns(new ArchiveScanPage([poison], first));
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, first, A<CancellationToken>._))
			.Returns(new ArchiveScanPage([], second));
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, second, A<CancellationToken>._))
			.Returns(new ArchiveScanPage([healthy], null));
		A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "poison", "Order", 0, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Permanent poison stream"));
		A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "healthy", "Order", 0, A<CancellationToken>._))
			.Returns(CreateEvents("healthy", 0));
		A.CallTo(() => _coldStore.WriteAsync(TestTenant, "healthy", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._)).Returns(0L);
		A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(TestTenant, "healthy", "Order", 0, A<CancellationToken>._)).Returns(1);
		var service = CreateService(new ArchivePolicy { RetainRecentCount = 1 }, NullLogger<EventArchiveService>.Instance, scanner);
		await InvokeArchiveCycleAsync(service);
		await InvokeArchiveCycleAsync(service);
		await InvokeArchiveCycleAsync(service);
		await InvokeArchiveCycleAsync(service);
		A.CallTo(() => _archiveSource.TombstoneArchivedEventsUpToVersionAsync(TestTenant, "healthy", "Order", 0, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "poison", "Order", 0, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task RetainThePreviousContinuationWhenCancelledInsideAPage()
	{
		var scanner = A.Fake<IEventStoreArchiveScanner>();
		var previous = new TestCursor();
		var cursor = new TestCursor();
		var candidates = new[] { new ArchiveCandidate(TestTenant, "first", "Order", 0, 1), new ArchiveCandidate(TestTenant, "second", "Order", 0, 1) };
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, null, A<CancellationToken>._))
			.Returns(new ArchiveScanPage([], previous));
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, previous, A<CancellationToken>._))
			.Returns(new ArchiveScanPage(candidates, cursor));
		using var cancellation = new CancellationTokenSource();
		A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "first", "Order", 0, cancellation.Token))
			.Invokes(() => cancellation.Cancel()).Throws(new OperationCanceledException(cancellation.Token));
		var service = CreateService(new ArchivePolicy { RetainRecentCount = 1 }, NullLogger<EventArchiveService>.Instance, scanner);
		await InvokeArchiveCycleAsync(service);
		await Should.ThrowAsync<OperationCanceledException>(() => InvokeArchiveCycleAsync(service, cancellation.Token));
		A.CallTo(() => _hotStore.LoadArchiveEventsAsync(TestTenant, "second", "Order", 0, A<CancellationToken>._)).MustNotHaveHappened();
		await InvokeArchiveCycleAsync(service);
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, previous, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, cursor, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ScheduleContinuationImmediatelyButDelayFailedFetchAndNewRound()
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
		var clock = new ObservedClock();
		var scanner = A.Fake<IEventStoreArchiveScanner>();
		var cursor = new TestCursor();
		var calls = Channel.CreateUnbounded<ArchiveScanCursor?>();
		var count = 0;
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, A<ArchiveScanCursor?>._, A<CancellationToken>._))
			.ReturnsLazily((ArchivePolicy _, int _, ArchiveScanCursor? continuation, CancellationToken _) =>
			{
				calls.Writer.TryWrite(continuation).ShouldBeTrue();
				var call = Interlocked.Increment(ref count);
				if (call == 2)
				{
					throw new InvalidOperationException("Transient fetch failure");
				}
				return new ValueTask<ArchiveScanPage>(new ArchiveScanPage([], call == 1 ? cursor : null));
			});
		using var service = CreateService(new ArchivePolicy { RetainRecentCount = 1 }, NullLogger<EventArchiveService>.Instance, scanner, clock);
		await service.StartAsync(timeout.Token);
		(await clock.Delays.Reader.ReadAsync(timeout.Token)).ShouldBe(TimeSpan.FromHours(1));
		clock.Advance(TimeSpan.FromHours(1));
		(await calls.Reader.ReadAsync(timeout.Token)).ShouldBeNull();
		(await calls.Reader.ReadAsync(timeout.Token)).ShouldBeSameAs(cursor);
		// No second clock advance was needed to fetch the next page, but failure now installs a delay.
		(await clock.Delays.Reader.ReadAsync(timeout.Token)).ShouldBe(TimeSpan.FromHours(1));
		Volatile.Read(ref count).ShouldBe(2);
		clock.Advance(TimeSpan.FromHours(1));
		(await calls.Reader.ReadAsync(timeout.Token)).ShouldBeSameAs(cursor);
		(await clock.Delays.Reader.ReadAsync(timeout.Token)).ShouldBe(TimeSpan.FromHours(1));
		Volatile.Read(ref count).ShouldBe(3);
		await service.StopAsync(timeout.Token);
	}

	[Fact]
	public async Task StopBeforeAnotherPageWhenCancellationArrivesDuringFetch()
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
		using var lifetime = new CancellationTokenSource();
		var clock = new ObservedClock();
		var scanner = A.Fake<IEventStoreArchiveScanner>();
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, A<ArchiveScanCursor?>._, A<CancellationToken>._))
			.Invokes(() => lifetime.Cancel()).Returns(new ArchiveScanPage([], new TestCursor()));
		using var service = CreateService(new ArchivePolicy { RetainRecentCount = 1 }, NullLogger<EventArchiveService>.Instance, scanner, clock);
		await service.StartAsync(lifetime.Token);
		await clock.Delays.Reader.ReadAsync(timeout.Token);
		clock.Advance(TimeSpan.FromHours(1));
		await service.ExecuteTask.ShouldNotBeNull().WaitAsync(timeout.Token);
		A.CallTo(() => scanner.ScanArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, A<ArchiveScanCursor?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private sealed class ObservedClock : TimeProvider
	{
		private readonly FakeTimeProvider _clock = new();
		internal Channel<TimeSpan> Delays { get; } = Channel.CreateUnbounded<TimeSpan>();
		internal void Advance(TimeSpan interval) => _clock.Advance(interval);
		public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
		{
			var timer = _clock.CreateTimer(callback, state, dueTime, period);
			Delays.Writer.TryWrite(dueTime).ShouldBeTrue();
			return timer;
		}
	}

	private sealed class TestCursor : ArchiveScanCursor;

	// Existing payload tests use a deliberately single-page fixture; fairness tests supply their own scanner.
	private sealed class SinglePageScanner(IEventStoreArchive source) : IEventStoreArchiveScanner
	{
		public async ValueTask<ArchiveScanPage> ScanArchiveCandidatesAsync(ArchivePolicy policy, int scanSize,
			ArchiveScanCursor? continuation, CancellationToken cancellationToken) =>
			new(await source.GetArchiveCandidatesAsync(policy, scanSize, cancellationToken), null);
	}

	// --- Helpers ---

	/// <summary>
	/// Invokes RunArchiveCycleAsync directly via reflection for deterministic testing.
	/// </summary>
	private static async Task InvokeArchiveCycleAsync(EventArchiveService service, CancellationToken cancellationToken = default)
	{
		var method = typeof(EventArchiveService).GetMethod(
			"RunArchiveCycleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
		var task = (Task)method!.Invoke(service, [cancellationToken])!;
		await task.ConfigureAwait(false);
	}

	private EventArchiveService CreateService(ArchivePolicy policy) =>
		CreateService(policy, NullLogger<EventArchiveService>.Instance);

	private EventArchiveService CreateService(ArchivePolicy policy, ILogger<EventArchiveService> logger, IEventStoreArchiveScanner? scanner = null, TimeProvider? clock = null)
	{
		var pm = new OptionsMonitorWrapper<ArchivePolicy>(policy);
		var om = new OptionsMonitorWrapper<EventArchiveServiceOptions>(
			new EventArchiveServiceOptions { ArchiveInterval = TimeSpan.FromHours(1) });
		return new EventArchiveService(_archiveSource, _hotStore, scanner ?? new SinglePageScanner(_archiveSource), _coldStore, pm, om, logger)
		{ TimeProvider = clock ?? TimeProvider.System };
	}

	/// <summary>Records the event ids the service logged, so a test can assert the reported outcome.</summary>
	private sealed class CapturingLogger : ILogger<EventArchiveService>
	{
		public List<int> Events { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			Events.Add(eventId.Id);
	}

	private static List<StoredEvent> CreateEvents(string aggregateId, params long[] versions)
	{
		return versions.Select(v => new StoredEvent(
			Guid.NewGuid().ToString(), aggregateId, "Order", "TestEvent",
			Array.Empty<byte>(), null, v, DateTimeOffset.UtcNow)).ToList();
	}

	private sealed class OptionsMonitorWrapper<T> : IOptionsMonitor<T>
	{
		public OptionsMonitorWrapper(T value) => CurrentValue = value;
		public T CurrentValue { get; }
		public T Get(string? name) => CurrentValue;
		public IDisposable? OnChange(Action<T, string?> listener) => null;
	}
}
