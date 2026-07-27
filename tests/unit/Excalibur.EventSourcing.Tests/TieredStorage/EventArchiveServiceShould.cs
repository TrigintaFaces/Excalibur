// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using System.Reflection;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.TieredStorage;
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
public sealed class EventArchiveServiceShould
{
	/// <summary>The tenant every candidate in this fixture belongs to; the archive service must carry it
	/// from the candidate through to BOTH the cold write and the hot delete.</summary>
	private static readonly KeyedTenantPartition TestTenant = KeyedTenantPartition.Scoped("tenant-a");

	private readonly IEventStoreArchive _archiveSource = A.Fake<IEventStoreArchive>();
	private readonly IEventStore _hotStore = A.Fake<IEventStore>();
	private readonly IColdEventStore _coldStore = A.Fake<IColdEventStore>();

	[Fact]
	public async Task ArchiveEventsFromHotToCold()
	{
		var candidates = new List<ArchiveCandidate> { new(TestTenant, "agg-1", "Order", 5, 5) };
		_ = A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(candidates);

		var events = CreateEvents("agg-1", 1, 2, 3, 4, 5);
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(events);
		// Cold store confirms the full range durable (watermark = 5), so hot delete is authorized up to 5.
		_ = A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "agg-1", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.Returns(5L);
		_ = A.CallTo(() => _archiveSource.DeleteEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-1", "Order", 5, A<CancellationToken>._))
			.Returns(5);

		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) });

		// Act -- invoke cycle directly (deterministic, no timing)
		await InvokeArchiveCycleAsync(service);

		// Assert
		A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "agg-1", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _archiveSource.DeleteEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-1", "Order", 5, A<CancellationToken>._))
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
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-p", "Order", A<CancellationToken>._))
			.Returns(events);
		_ = A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "agg-p", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.Returns(3L);
		_ = A.CallTo(() => _archiveSource.DeleteEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-p", "Order", A<long>._, A<CancellationToken>._))
			.Returns(3);

		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) });

		await InvokeArchiveCycleAsync(service);

		// LIVENESS: the confirmed prefix (<= 3) IS deleted — the archive still makes progress.
		A.CallTo(() => _archiveSource.DeleteEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-p", "Order", 3, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		// SAFETY: nothing is deleted beyond the durable watermark.
		A.CallTo(() => _archiveSource.DeleteEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-p", "Order", 5, A<CancellationToken>._))
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
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-n", "Order", A<CancellationToken>._))
			.Returns(events);
		_ = A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "agg-n", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.Returns(-1L);

		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) });

		await InvokeArchiveCycleAsync(service);

		A.CallTo(() => _archiveSource.DeleteEventsUpToVersionAsync(A<KeyedTenantPartition>._, "agg-n", "Order", A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
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

		_ = A.CallTo(() => _hotStore.LoadAsync("fail-agg", "Order", A<CancellationToken>._))
			.Throws(new InvalidOperationException("DB unavailable"));

		var events = CreateEvents("ok-agg", 1, 2);
		_ = A.CallTo(() => _hotStore.LoadAsync("ok-agg", "Order", A<CancellationToken>._))
			.Returns(events);
		_ = A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "ok-agg", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.Returns(2L);
		_ = A.CallTo(() => _archiveSource.DeleteEventsUpToVersionAsync(A<KeyedTenantPartition>._, "ok-agg", "Order", 2, A<CancellationToken>._))
			.Returns(2);

		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(1) });

		await InvokeArchiveCycleAsync(service);

		A.CallTo(() => _coldStore.WriteAsync(A<KeyedTenantPartition>._, "ok-agg", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
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
			A<KeyedTenantPartition>._, A<string>._, A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void ThrowOnNullArchiveSource()
	{
		var pm = new OptionsMonitorWrapper<ArchivePolicy>(new ArchivePolicy());
		var om = new OptionsMonitorWrapper<EventArchiveServiceOptions>(new EventArchiveServiceOptions());
		Should.Throw<ArgumentNullException>(() => new EventArchiveService(
			null!, _hotStore, _coldStore, pm, om, NullLogger<EventArchiveService>.Instance));
	}

	[Fact]
	public void ThrowOnNullHotStore()
	{
		var pm = new OptionsMonitorWrapper<ArchivePolicy>(new ArchivePolicy());
		var om = new OptionsMonitorWrapper<EventArchiveServiceOptions>(new EventArchiveServiceOptions());
		Should.Throw<ArgumentNullException>(() => new EventArchiveService(
			_archiveSource, null!, _coldStore, pm, om, NullLogger<EventArchiveService>.Instance));
	}

	[Fact]
	public void ThrowOnNullColdStore()
	{
		var pm = new OptionsMonitorWrapper<ArchivePolicy>(new ArchivePolicy());
		var om = new OptionsMonitorWrapper<EventArchiveServiceOptions>(new EventArchiveServiceOptions());
		Should.Throw<ArgumentNullException>(() => new EventArchiveService(
			_archiveSource, _hotStore, null!, pm, om, NullLogger<EventArchiveService>.Instance));
	}

	// --- Helpers ---

	/// <summary>
	/// Invokes RunArchiveCycleAsync directly via reflection for deterministic testing.
	/// </summary>
	private static async Task InvokeArchiveCycleAsync(EventArchiveService service)
	{
		var method = typeof(EventArchiveService).GetMethod(
			"RunArchiveCycleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
		var task = (Task)method!.Invoke(service, [CancellationToken.None])!;
		await task.ConfigureAwait(false);
	}

	private EventArchiveService CreateService(ArchivePolicy policy)
	{
		var pm = new OptionsMonitorWrapper<ArchivePolicy>(policy);
		var om = new OptionsMonitorWrapper<EventArchiveServiceOptions>(
			new EventArchiveServiceOptions { ArchiveInterval = TimeSpan.FromHours(1) });
		return new EventArchiveService(
			_archiveSource, _hotStore, _coldStore, pm, om,
			NullLogger<EventArchiveService>.Instance);
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
