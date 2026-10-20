// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Reflection;

using Excalibur.Caching.AdaptiveTtl;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Caching.AdaptiveTtl;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Concurrency")]
public sealed class AdaptiveTtlCacheHistoryShould
{
	[Fact]
	public void SupplyWindowedFrequencyToTheStrategyWithoutEvictingTheValue()
	{
		using var clock = new ManualClock();
		var inner = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		var strategy = A.Fake<IAdaptiveTtlStrategy>();
		AdaptiveTtlContext? observed = null;
		A.CallTo(() => strategy.CalculateTtl(A<AdaptiveTtlContext>._))
			.Invokes((AdaptiveTtlContext context) => observed = context)
			.Returns(TimeSpan.FromMinutes(20));
		using var cache = new AdaptiveTtlCache(inner, strategy, NullLogger<AdaptiveTtlCache>.Instance,
			A.Fake<ISystemLoadMonitor>(), clock);
		cache.Set("key", [1, 2, 3], new DistributedCacheEntryOptions());
		for (var i = 0; i < 120; i++)
		{
			cache.Get("key").ShouldBe([1, 2, 3]);
		}
		cache.Set("key", [1, 2, 3], new DistributedCacheEntryOptions());
		observed.ShouldNotBeNull().AccessFrequency.ShouldBe(2);
		clock.Minute = 61;
		cache.GetMetrics().CustomMetrics["AccessFrequencyPerMin"].ShouldBe(0);
		inner.Get("key").ShouldBe([1, 2, 3]);
		cache.Set("key", [4], new DistributedCacheEntryOptions());
		observed.ShouldNotBeNull().AccessFrequency.ShouldBe(0);
		inner.Get("key").ShouldBe([4]);
	}

	[Fact]
	public void BoundHistoryIndependentlyOfAccessRate()
	{
		using var clock = new ManualClock();
		var history = new History(clock);
		for (var i = 0; i < 1_000_000; i++)
		{
			history.Record(i % 4 != 0);
		}

		history.Frequency().ShouldBe(1_000_000 / 60.0);
		history.HitRate().ShouldBe(0.75);
		history.CompletedBuckets.ShouldBe(0);
		for (var minute = 1; minute <= 180; minute++)
		{
			clock.Minute = minute;
			history.Record(true);
			history.CompletedBuckets.ShouldBeLessThanOrEqualTo(60);
		}

		history.Frequency().ShouldBe(61 / 60.0);
	}

	[Theory]
	[InlineData(59, 1)]
	[InlineData(60, 1)]
	[InlineData(61, 0)]
	[InlineData(10000, 0)]
	public void ExpireHistoryOnIdleReadsWithoutChangingLifetimeHits(int minute, int expected)
	{
		using var clock = new ManualClock();
		var history = new History(clock);
		history.Record(true);
		clock.Minute = minute;
		history.Frequency().ShouldBe(expected / 60.0);
		history.HitRate().ShouldBe(1);
		history.CompletedBuckets.ShouldBeLessThanOrEqualTo(1);
	}

	[Fact]
	public void DropBothQueuedAndCurrentCountsAfterALargeJump()
	{
		using var clock = new ManualClock();
		var history = new History(clock);
		history.Record(true);
		clock.Minute = 1;
		history.Record(false);
		clock.Minute = 1000;
		history.Record(true);
		history.Frequency().ShouldBe(1 / 60.0);
		history.CompletedBuckets.ShouldBe(0);
		history.HitRate().ShouldBe(2 / 3.0);
	}

	[Fact]
	public void ClampBackwardSamplesWithoutRewindingTheWindow()
	{
		using var clock = new ManualClock { Minute = 100 };
		var history = new History(clock);
		history.Record(true);
		clock.Minute = 20;
		history.Record(false);
		history.Frequency().ShouldBe(2 / 60.0);
		clock.Minute = 160;
		history.Frequency().ShouldBe(2 / 60.0);
		clock.Minute = 161;
		history.Frequency().ShouldBe(0);
		history.HitRate().ShouldBe(0.5);
	}

	[Fact]
	public async Task AttributeADelayedSampleToTheCurrentWindow()
	{
		using var clock = new ManualClock();
		var history = new History(clock);
		clock.DelayNextSample = true;
		var delayed = Task.Run(() => history.Record(false));
		try
		{
			clock.SampleTaken.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
			clock.Minute = 100;
			history.Record(true);
		}
		finally
		{
			clock.ReleaseSample.Set();
		}

		await delayed.WaitAsync(TimeSpan.FromSeconds(10));
		history.Frequency().ShouldBe(2 / 60.0);
		clock.Minute = 161;
		history.Frequency().ShouldBe(0);
	}

	[Fact]
	public void CountEveryConcurrentRecordAcrossRotation()
	{
		using var clock = new ManualClock();
		var history = new History(clock);
		Parallel.For(0, 100_000, i =>
		{
			if (i == 50_000)
			{
				clock.Minute = 1;
			}
			history.Record(i % 2 == 0);
			if (i % 100 == 0)
			{
				_ = history.Frequency();
			}
		});

		history.Frequency().ShouldBe(100_000 / 60.0);
		history.HitRate().ShouldBe(0.5);
		history.CompletedBuckets.ShouldBeLessThanOrEqualTo(1);
	}

	// Inspect the private retention bound while exercising its production methods.
	// Public cache delegation/TTL behavior is covered by the existing cache suites.
	private sealed class History
	{
		private static readonly Type MetadataType = typeof(AdaptiveTtlCache)
			.GetNestedType("CacheEntryMetadata", BindingFlags.NonPublic)!;
		private readonly object _metadata;

		public History(TimeProvider clock)
		{
			_metadata = Activator.CreateInstance(MetadataType, clock)!;
			Record = MetadataType.GetMethod("RecordAccess")!.CreateDelegate<Action<bool>>(_metadata);
			Frequency = MetadataType.GetMethod("GetAccessFrequency")!.CreateDelegate<Func<double>>(_metadata);
			HitRate = MetadataType.GetMethod("GetHitRate")!.CreateDelegate<Func<double>>(_metadata);
		}

		public Action<bool> Record { get; }
		public Func<double> Frequency { get; }
		public Func<double> HitRate { get; }
		public int CompletedBuckets => ((ICollection)MetadataType
			.GetField("_recentAccesses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_metadata)!).Count;
	}

	private sealed class ManualClock : TimeProvider, IDisposable
	{
		private long _minute;
		private int _delayNextSample;

		public long Minute { set => Interlocked.Exchange(ref _minute, value); }
		public bool DelayNextSample { set => Interlocked.Exchange(ref _delayNextSample, value ? 1 : 0); }
		public ManualResetEventSlim SampleTaken { get; } = new();
		public ManualResetEventSlim ReleaseSample { get; } = new();

		public void Dispose()
		{
			SampleTaken.Dispose();
			ReleaseSample.Dispose();
		}

		public override DateTimeOffset GetUtcNow()
		{
			var sample = DateTimeOffset.UnixEpoch.AddMinutes(Interlocked.Read(ref _minute));
			if (Interlocked.Exchange(ref _delayNextSample, 0) == 1)
			{
				SampleTaken.Set();
				if (!ReleaseSample.Wait(TimeSpan.FromSeconds(10)))
				{
					throw new TimeoutException("Delayed clock sample was not released.");
				}
			}
			return sample;
		}
	}
}
