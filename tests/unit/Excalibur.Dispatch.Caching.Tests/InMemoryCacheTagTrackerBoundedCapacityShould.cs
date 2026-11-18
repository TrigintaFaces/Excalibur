// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

using Tests.Shared.Helpers;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Rebuilds the bounded-capacity coverage for <see cref="InMemoryCacheTagTracker"/> that was lost when
/// the tracker moved from a key-set contract to per-tag version stamps.
/// </summary>
/// <remarks>
/// <para>
/// The capacity bound exists because tag names may be derived from unbounded input (a per-entity tag
/// such as <c>orders:{id}</c>), so an unbounded map would be a memory-exhaustion vector. The behaviour
/// under the new contract is materially different from the old one and is worth stating precisely,
/// because it is easy to misread as a bug: <b>at capacity the tracker does not throw and does not
/// evict — it hands back a fresh, unmemoized stamp.</b> Caching is cross-cutting infrastructure and
/// must fail open rather than break the request that happens to arrive at the bound.
/// </para>
/// <para>
/// The consequence is worth naming, since no single assertion shows it: an untracked tag at capacity
/// resolves to a DIFFERENT stamp on every call, so an entry written under it can never match on read
/// and is treated as invalidated. That degrades the hit rate to zero for tags past the bound; it never
/// serves a stale entry. Failing in the safe direction is the deliberate choice, and
/// <see cref="AtCapacity_UntrackedTag_ShouldNeverServeAStableStamp"/> pins it so a future change that
/// "fixes" the instability by memoizing past the bound has to argue with a test rather than slip past.
/// </para>
/// <para>
	/// Capacity is checked through the internal tracked count and public stamp behavior. Concurrency
	/// controls exercise admission; stamp controls distinguish rejected admission from dropped invalidation.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCacheTagTrackerBoundedCapacityShould
{
	private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

	private static InMemoryCacheTagTracker CreateTracker(
		int capacity,
		IMeterFactory meterFactory,
		ILogger<InMemoryCacheTagTracker>? logger = null) =>
		new(meterFactory, MsOptions.Create(new CacheOptions { TagTrackerCapacity = capacity }), logger);

	/// <summary>Fills the tracker to exactly <paramref name="capacity"/> distinct admitted tags.</summary>
	private static async Task FillToCapacityAsync(InMemoryCacheTagTracker tracker, int capacity)
	{
		for (var i = 0; i < capacity; i++)
		{
			_ = await tracker.GetOrCreateStampAsync($"fill-{i}", Ct);
		}
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[Trait("Pattern", "Concurrency")]
	public async Task ConcurrentAdmissions_ShouldRespectCapacity(bool sameTag, bool mixedBumps)
	{
		using var meterFactory = new TestMeterFactory();
		for (var trial = 0; trial < 32; trial++)
		{
			var tracker = CreateTracker(capacity: 1, meterFactory);
			using var ready = new CountdownEvent(16);
			using var start = new ManualResetEventSlim(false);
			var callers = Enumerable.Range(0, 16).Select(index => Task.Factory.StartNew(
				async () =>
				{
					ready.Signal();
					start.Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();
					if (mixedBumps && index % 2 == 0)
					{
						await tracker.BumpStampAsync($"bump-{index}", Ct);
					}

					tracker.TrackedTagCount.ShouldBeLessThanOrEqualTo(1);
					return await tracker.GetOrCreateStampAsync(sameTag ? "shared" : $"tag-{index}", Ct);
				}, Ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap()).ToArray();
			try
			{
				ready.Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();
			}
			finally
			{
				start.Set();
				await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(10));
			}

			var stamps = await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(10), Ct);
			tracker.TrackedTagCount.ShouldBe(1);
			stamps.ShouldAllBe(stamp => !string.IsNullOrEmpty(stamp));
			if (sameTag)
			{
				stamps.Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
			}
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[Trait("Pattern", "Concurrency")]
	public async Task CapacityWarning_ShouldAllowAnotherThreadToMutateTracker(bool collapse)
	{
		using var meterFactory = new TestMeterFactory();
		var logger = new CapturingLogger<InMemoryCacheTagTracker>();
		var tracker = CreateTracker(capacity: 1, meterFactory, logger);
		var original = await tracker.GetOrCreateStampAsync("tracked", Ct);
		using var completed = new ManualResetEventSlim(false);
		Task? reentrant = null;
		logger.OnWarning = () =>
		{
			reentrant = Task.Run(async () =>
			{
				try
				{
					await tracker.BumpStampAsync("tracked", Ct);
				}
				finally
				{
					completed.Set();
				}
			}, Ct);
			completed.Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();
		};

		try
		{
			if (collapse)
			{
				await tracker.BumpStampAsync("untracked", Ct);
			}
			else
			{
				_ = await tracker.GetOrCreateStampAsync("untracked", Ct);
			}
		}
		finally
		{
			if (reentrant is not null)
			{
				await reentrant.WaitAsync(TimeSpan.FromSeconds(10));
			}
		}

		_ = reentrant.ShouldNotBeNull();

		logger.Warnings.Count.ShouldBe(1);
		(await tracker.GetOrCreateStampAsync("tracked", Ct)).ShouldNotBe(original);
		tracker.TrackedTagCount.ShouldBe(1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[Trait("Pattern", "Concurrency")]
	public async Task ResolveRacingInvalidation_ShouldRejectTheOldStampAfterCompletion(bool collapse)
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 1, meterFactory);
		var original = await tracker.GetOrCreateStampAsync("tracked", Ct);
		await Task.WhenAll(
			Task.Run(() => tracker.GetOrCreateStampAsync("tracked", Ct), Ct),
			Task.Run(() => tracker.BumpStampAsync(collapse ? "unknown" : "tracked", Ct), Ct));

		(await tracker.GetOrCreateStampAsync("tracked", Ct)).ShouldNotBe(original);
		tracker.TrackedTagCount.ShouldBe(1);
	}

	[Fact]
	[Trait("Pattern", "Concurrency")]
	public async Task CreationMeasurement_ShouldAllowAnotherThreadToMutateTracker()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 1, meterFactory);
		using var completed = new ManualResetEventSlim(false);
		using var listener = new MeterListener();
		Task? reentrant = null;
		listener.InstrumentPublished = (instrument, observer) =>
		{
			if (meterFactory.Owns(instrument.Meter) && instrument.Name == "dispatch.cache.tag_tracker.stamps_created")
			{
				observer.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
		{
			reentrant = Task.Run(async () =>
			{
				try
				{
					await tracker.BumpStampAsync("tracked", Ct);
				}
				finally
				{
					completed.Set();
				}
			}, Ct);
			completed.Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();
		});
		listener.Start();
		string? original = null;
		try
		{
			original = await tracker.GetOrCreateStampAsync("tracked", Ct);
		}
		finally
		{
			if (reentrant is not null)
			{
				await reentrant.WaitAsync(TimeSpan.FromSeconds(10));
			}
		}

		_ = reentrant.ShouldNotBeNull();
		(await tracker.GetOrCreateStampAsync("tracked", Ct)).ShouldNotBe(original);
		tracker.TrackedTagCount.ShouldBe(1);
	}

	// ---- Under the bound: normal memoized behaviour. ----

	[Fact]
	public async Task UnderCapacity_ShouldAdmitTagsAndMemoizeTheirStamps()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 8, meterFactory);

		var first = await tracker.GetOrCreateStampAsync("orders", Ct);
		var second = await tracker.GetOrCreateStampAsync("orders", Ct);

		first.ShouldNotBeNullOrEmpty();
		second.ShouldBe(first, "a tag admitted under the capacity bound must memoize its stamp");
	}

	// ---- At the bound: fail open, never throw. ----

	[Fact]
	public async Task AtCapacity_NewTag_ShouldStillReturnAUsableStampAndNotThrow()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 4, meterFactory);
		await FillToCapacityAsync(tracker, 4);

		var stamp = await tracker.GetOrCreateStampAsync("one-tag-too-many", Ct);

		stamp.ShouldNotBeNullOrEmpty(
			"caching is cross-cutting infrastructure: reaching the capacity bound must degrade the "
			+ "hit rate, never break the request");
	}

	/// <summary>
	/// The deliberate cost of failing open: past the bound a tag has no memoized stamp, so each
	/// resolution mints a new one and every entry written under that tag misses on its next read.
	/// </summary>
	[Fact]
	public async Task AtCapacity_UntrackedTag_ShouldNeverServeAStableStamp()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 4, meterFactory);
		await FillToCapacityAsync(tracker, 4);

		var first = await tracker.GetOrCreateStampAsync("beyond-the-bound", Ct);
		var second = await tracker.GetOrCreateStampAsync("beyond-the-bound", Ct);

		second.ShouldNotBe(first,
			"a tag rejected at the bound is not memoized, so it must resolve to a fresh stamp each "
			+ "time — entries under it miss rather than risk being served after an invalidation the "
			+ "tracker had no room to record");
	}

	/// <summary>
	/// Liveness pair for the arm above: hitting the bound must not corrupt or destabilise the tags
	/// that were admitted before it. Without this, an implementation that simply stopped memoizing
	/// everything once full would pass the safety arm.
	/// </summary>
	[Fact]
	public async Task AtCapacity_AlreadyAdmittedTag_ShouldStillResolveToItsStableStamp()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 4, meterFactory);
		var admitted = await tracker.GetOrCreateStampAsync("fill-0", Ct);
		await FillToCapacityAsync(tracker, 4);

		// Push well past the bound with unique tags.
		for (var i = 0; i < 10; i++)
		{
			_ = await tracker.GetOrCreateStampAsync($"overflow-{i}", Ct);
		}

		var stillAdmitted = await tracker.GetOrCreateStampAsync("fill-0", Ct);

		stillAdmitted.ShouldBe(admitted,
			"pressure from tags past the bound must not disturb a tag already tracked");
	}

	[Fact]
	public async Task AtCapacity_AdmittedTag_ShouldStillBeInvalidatable()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 4, meterFactory);
		await FillToCapacityAsync(tracker, 4);
		var before = await tracker.GetOrCreateStampAsync("fill-1", Ct);

		await tracker.BumpStampAsync("fill-1", Ct);
		var after = await tracker.GetOrCreateStampAsync("fill-1", Ct);

		after.ShouldNotBe(before,
			"invalidation of an already-tracked tag must keep working at the capacity bound — a tag "
			+ "that cannot be invalidated would serve stale entries indefinitely");
	}

	// ---- The one-shot capacity warning. ----

	[Fact]
	public async Task AtCapacity_ShouldWarnExactlyOnce_NoMatterHowManyTagsAreRejected()
	{
		using var meterFactory = new TestMeterFactory();
		var logger = new CapturingLogger<InMemoryCacheTagTracker>();
		var tracker = CreateTracker(capacity: 3, meterFactory, logger);
		await FillToCapacityAsync(tracker, 3);

		for (var i = 0; i < 25; i++)
		{
			_ = await tracker.GetOrCreateStampAsync($"rejected-{i}", Ct);
		}

		logger.Warnings.Count.ShouldBe(1,
			"the capacity warning is one-shot by design: a per-rejection warning would flood the log "
			+ "at exactly the moment the process is already under memory pressure");
		logger.Warnings[0].ShouldContain("3", Case.Sensitive, "the warning should name the capacity it hit");
	}

	[Fact]
	public async Task UnderCapacity_ShouldNotWarnAtAll()
	{
		using var meterFactory = new TestMeterFactory();
		var logger = new CapturingLogger<InMemoryCacheTagTracker>();
		var tracker = CreateTracker(capacity: 100, meterFactory, logger);

		for (var i = 0; i < 20; i++)
		{
			_ = await tracker.GetOrCreateStampAsync($"tag-{i}", Ct);
		}

		logger.Warnings.ShouldBeEmpty("nothing was rejected, so nothing should be warned about");
	}

	[Fact]
	public async Task NullLogger_AtCapacity_ShouldNotThrow()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 2, meterFactory, logger: null);
		await FillToCapacityAsync(tracker, 2);

		var stamp = await tracker.GetOrCreateStampAsync("no-logger", Ct);

		stamp.ShouldNotBeNullOrEmpty("the logger is optional; its absence must not turn the bound into a crash");
	}

	// ---- Degenerate configuration falls back to the default bound. ----

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public async Task NonPositiveConfiguredCapacity_ShouldFallBackToTheDefaultBound(int configuredCapacity)
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(configuredCapacity, meterFactory);

		// A default bound of 10,000 is far above these few tags, so admission (stamp stability) proves
		// the configured value was rejected in favour of the default rather than taken literally — a
		// literal 0 or negative bound would reject the very first tag.
		var first = await tracker.GetOrCreateStampAsync("orders", Ct);
		var second = await tracker.GetOrCreateStampAsync("orders", Ct);

		second.ShouldBe(first,
			$"a configured capacity of {configuredCapacity} is not a real bound and must fall back to "
			+ "the default rather than reject every tag");
	}

	// ---- The bump path at capacity. ----
	//
	// The invalidate-path arms proper -- an untracked bump at capacity going global, an ordinary bump
	// staying per-tag, and the map staying bounded across many bumps -- live in
	// InMemoryCacheTagTrackerEpochCollapseShould, shipped with the fix itself. Not duplicated here.
	// What follows is the one property that file does not assert.

	/// <summary>
	/// <b>Requirement:</b> the collapse must leave the tracker usable.
	/// <b>Predicate tested:</b> after a collapse, a newly admitted tag memoizes a stable stamp again.
	/// </summary>
	/// <remarks>
	/// The liveness pair to the collapse arms next door. Those prove the invalidation is not lost; this
	/// proves the cure is not permanent. A tracker that collapsed and stayed at capacity would re-invalidate
	/// the entire cache on every subsequent bump -- strictly worse than the unbounded growth it replaced,
	/// and invisible to any arm that only checks the collapse happened.
	/// </remarks>
	[Fact]
	public async Task AtCapacity_TheCollapse_ShouldRelievePressureSoTheBoundIsSelfCorrecting()
	{
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 3, meterFactory);
		await FillToCapacityAsync(tracker, 3);

		await tracker.BumpStampAsync("untracked-at-capacity", Ct);

		var first = await tracker.GetOrCreateStampAsync("after-collapse", Ct);
		(await tracker.GetOrCreateStampAsync("after-collapse", Ct)).ShouldBe(first,
			"the collapse must leave the tracker usable -- a store that stayed at capacity would invalidate "
			+ "the entire cache on every subsequent bump, which is a worse failure than the leak it replaced");
	}

	/// <summary>Captures warning-level messages so the one-shot guard can be counted.</summary>
	private sealed class CapturingLogger<T> : ILogger<T>
	{
		private readonly List<string> _warnings = [];

		public IReadOnlyList<string> Warnings => _warnings;

		public Action? OnWarning { get; set; }

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (logLevel == LogLevel.Warning)
			{
				_warnings.Add(formatter(state, exception));
				OnWarning?.Invoke();
			}
		}
	}
}
