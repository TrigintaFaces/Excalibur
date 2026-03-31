// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.TieredStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using IEventStore = Excalibur.EventSourcing.Abstractions.IEventStore;
using StoredEvent = Excalibur.EventSourcing.Abstractions.StoredEvent;

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
	private readonly IEventStoreArchive _archiveSource = A.Fake<IEventStoreArchive>();
	private readonly IEventStore _hotStore = A.Fake<IEventStore>();
	private readonly IColdEventStore _coldStore = A.Fake<IColdEventStore>();

	[Fact]
	public async Task ArchiveEventsFromHotToCold()
	{
		var candidates = new List<ArchiveCandidate> { new("agg-1", "Order", 5, 5) };
		_ = A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(candidates);

		var events = CreateEvents("agg-1", 1, 2, 3, 4, 5);
		_ = A.CallTo(() => _hotStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(events);
		_ = A.CallTo(() => _archiveSource.DeleteEventsUpToVersionAsync(
			"agg-1", "Order", 5, A<CancellationToken>._))
			.Returns(5);

		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) });

		// Act -- invoke cycle directly (deterministic, no timing)
		await InvokeArchiveCycleAsync(service);

		// Assert
		A.CallTo(() => _coldStore.WriteAsync("agg-1", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _archiveSource.DeleteEventsUpToVersionAsync("agg-1", "Order", 5, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
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
			new("fail-agg", "Order", 3, 3),
			new("ok-agg", "Order", 2, 2)
		};
		_ = A.CallTo(() => _archiveSource.GetArchiveCandidatesAsync(
			A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(candidates);

		_ = A.CallTo(() => _hotStore.LoadAsync("fail-agg", "Order", A<CancellationToken>._))
			.Throws(new InvalidOperationException("DB unavailable"));

		var events = CreateEvents("ok-agg", 1, 2);
		_ = A.CallTo(() => _hotStore.LoadAsync("ok-agg", "Order", A<CancellationToken>._))
			.Returns(events);
		_ = A.CallTo(() => _archiveSource.DeleteEventsUpToVersionAsync(
			"ok-agg", "Order", 2, A<CancellationToken>._))
			.Returns(2);

		var service = CreateService(new ArchivePolicy { MaxAge = TimeSpan.FromDays(1) });

		await InvokeArchiveCycleAsync(service);

		A.CallTo(() => _coldStore.WriteAsync("ok-agg", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
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

		A.CallTo(() => _coldStore.WriteAsync(A<string>._, A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._))
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
