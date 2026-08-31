// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Resilience;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Infrastructure;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Verifies that the distributed cache backend is bounded by <see cref="CacheBehaviorOptions.CacheTimeout"/>
/// and that the bound degrades to a miss rather than to a failure.
/// </summary>
/// <remarks>
/// Every arm is paired. A decorator that abandoned every call would satisfy the safety assertions alone
/// while being completely useless, so each "slow call is abandoned" arm has a "healthy call still returns
/// its value" partner.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "Resilience")]
public sealed class TimeoutDistributedCacheShould : UnitTestBase
{
	private static readonly TimeSpan Deadline = TestTimeouts.Scale(TimeSpan.FromMilliseconds(200));

	private readonly TestMeterFactory _meterFactory = new();

	/// <inheritdoc />
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_meterFactory.Dispose();
		}

		base.Dispose(disposing);
	}

	[Fact]
	public async Task ReturnTheStoredValue_WhenTheBackendReadIsFastEnough()
	{
		// LIVENESS. A decorator that always reported a miss would pass every safety arm below.
		var backend = new ControllableCache();
		backend.Store("k", [1, 2, 3]);
		var cache = Create(backend);

		var value = await cache.GetAsync("k", CancellationToken.None);

		value.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public async Task ReportAMiss_WhenTheBackendReadOutlastsTheDeadline()
	{
		// SAFETY. A stalled backend must cost the deadline, not the length of the stall.
		var backend = new ControllableCache { GetDelay = Deadline * 20 };
		backend.Store("k", [1, 2, 3]);
		var cache = Create(backend);

		var elapsed = Stopwatch.StartNew();
		var value = await cache.GetAsync("k", CancellationToken.None);
		elapsed.Stop();

		value.ShouldBeNull("a backend that outlasts the deadline is reported as a miss, so the handler runs");
		elapsed.Elapsed.ShouldBeLessThan(
			Deadline * 10,
			"the caller must be released at the deadline rather than waiting out the backend");
	}

	[Fact]
	public async Task CompleteTheWrite_WhenTheBackendWriteIsFastEnough()
	{
		// LIVENESS partner for the dropped-write arm.
		var backend = new ControllableCache();
		var cache = Create(backend);

		await cache.SetAsync("k", [7], new DistributedCacheEntryOptions(), CancellationToken.None);

		backend.Read("k").ShouldBe([7]);
	}

	[Fact]
	public async Task DropTheWrite_WhenTheBackendWriteOutlastsTheDeadline()
	{
		// SAFETY. A cache write that cannot complete costs a later re-execution; it must never surface as
		// an application failure.
		var backend = new ControllableCache { SetDelay = Deadline * 20 };
		var cache = Create(backend);

		await Should.NotThrowAsync(
			async () => await cache.SetAsync("k", [7], new DistributedCacheEntryOptions(), CancellationToken.None));

		backend.Read("k").ShouldBeNull();
	}

	[Fact]
	public async Task PropagateCancellation_WhenTheCallerCancels()
	{
		// The deadline must not swallow a real caller cancellation, only its own.
		var backend = new ControllableCache { GetDelay = Deadline * 20 };
		var cache = Create(backend);

		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		_ = await Should.ThrowAsync<OperationCanceledException>(
			async () => await cache.GetAsync("k", cts.Token));
	}

	[Fact]
	public async Task NotBoundTheBackend_WhenTheDeadlineIsNotPositive()
	{
		// A non-positive deadline means "no bound", not "abandon everything immediately".
		var backend = new ControllableCache { GetDelay = TestTimeouts.Scale(TimeSpan.FromMilliseconds(50)) };
		backend.Store("k", [9]);
		var cache = Create(backend, TimeSpan.Zero);

		var value = await cache.GetAsync("k", CancellationToken.None);

		value.ShouldBe([9]);
	}

	[Fact]
	public async Task PreserveTheBufferSurface_WhenTheBackendSupportsIt()
	{
		// LIVENESS. HybridCache prefers the buffer path when the backend offers it; bounding the backend
		// must not cost that path, or bounding latency would silently cost allocations on every read.
		var backend = new ControllableBufferCache();
		backend.Store("k", [4, 5]);
		var cache = new BufferTimeoutDistributedCache(
			backend, MsOptions.Create(OptionsWith(Deadline)), _meterFactory, NullLogger<TimeoutDistributedCache>.Instance);

		var writer = new ArrayBufferWriter<byte>();
		var found = await cache.TryGetAsync("k", writer, CancellationToken.None);

		found.ShouldBeTrue();
		writer.WrittenSpan.ToArray().ShouldBe([4, 5]);
	}

	[Fact]
	public async Task ReportABufferMiss_WhenTheBufferReadOutlastsTheDeadline()
	{
		// SAFETY partner for the arm above.
		var backend = new ControllableBufferCache { GetDelay = Deadline * 20 };
		backend.Store("k", [4, 5]);
		var cache = new BufferTimeoutDistributedCache(
			backend, MsOptions.Create(OptionsWith(Deadline)), _meterFactory, NullLogger<TimeoutDistributedCache>.Instance);

		var found = await cache.TryGetAsync("k", new ArrayBufferWriter<byte>(), CancellationToken.None);

		found.ShouldBeFalse();
	}

	[Fact]
	public void BeRegisteredAroundAConsumerSuppliedBackend()
	{
		// The decorator is worthless if composition never applies it.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddSingleton<IDistributedCache>(new ControllableCache());
		_ = services.AddDispatchCaching(o => o.CacheMode = CacheMode.Distributed);

		using var provider = services.BuildServiceProvider();

		_ = provider.GetRequiredService<IDistributedCache>().ShouldBeOfType<TimeoutDistributedCache>();
	}

	[Fact]
	public void NotStackDecorators_WhenCachingIsRegisteredTwice()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddSingleton<IDistributedCache>(new ControllableCache());
		_ = services.AddDispatchCaching(o => o.CacheMode = CacheMode.Distributed);
		_ = services.AddDispatchCaching(o => o.CacheMode = CacheMode.Distributed);

		using var provider = services.BuildServiceProvider();

		var resolved = provider.GetRequiredService<IDistributedCache>().ShouldBeOfType<TimeoutDistributedCache>();
		resolved.ShouldNotBeNull();
	}

	private static CacheOptions OptionsWith(TimeSpan deadline)
		=> new() { Enabled = true, Behavior = { CacheTimeout = deadline } };

	private TimeoutDistributedCache Create(IDistributedCache backend, TimeSpan? deadline = null)
		=> new(
			backend,
			MsOptions.Create(OptionsWith(deadline ?? Deadline)),
			_meterFactory,
			NullLogger<TimeoutDistributedCache>.Instance);

	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters)
			{
				meter.Dispose();
			}

			_meters.Clear();
		}
	}

	/// <summary>A backend whose latency the test controls.</summary>
	private class ControllableCache : IDistributedCache
	{
		private readonly Dictionary<string, byte[]> _store = [];

		public TimeSpan GetDelay { get; init; }

		public TimeSpan SetDelay { get; init; }

		public void Store(string key, byte[] value)
		{
			lock (_store)
			{
				_store[key] = value;
			}
		}

		public byte[]? Read(string key)
		{
			lock (_store)
			{
				return _store.TryGetValue(key, out var v) ? v : null;
			}
		}

		public byte[]? Get(string key) => Read(key);

		public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
		{
			if (GetDelay > TimeSpan.Zero)
			{
				await Task.Delay(GetDelay, token).ConfigureAwait(false);
			}

			return Read(key);
		}

		public void Refresh(string key)
		{
		}

		public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

		public void Remove(string key)
		{
			lock (_store)
			{
				_ = _store.Remove(key);
			}
		}

		public Task RemoveAsync(string key, CancellationToken token = default)
		{
			Remove(key);
			return Task.CompletedTask;
		}

		public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Store(key, value);

		public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
		{
			if (SetDelay > TimeSpan.Zero)
			{
				await Task.Delay(SetDelay, token).ConfigureAwait(false);
			}

			Store(key, value);
		}
	}

	/// <summary>A backend that also offers the buffer surface.</summary>
	private sealed class ControllableBufferCache : ControllableCache, IBufferDistributedCache
	{
		public bool TryGet(string key, IBufferWriter<byte> destination)
		{
			var value = Read(key);
			if (value is null)
			{
				return false;
			}

			destination.Write(value);
			return true;
		}

		public async ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token = default)
		{
			if (GetDelay > TimeSpan.Zero)
			{
				await Task.Delay(GetDelay, token).ConfigureAwait(false);
			}

			return TryGet(key, destination);
		}

		public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options)
			=> Store(key, value.ToArray());

		public async ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default)
		{
			if (SetDelay > TimeSpan.Zero)
			{
				await Task.Delay(SetDelay, token).ConfigureAwait(false);
			}

			Store(key, value.ToArray());
		}
	}
	[Fact]
	public async Task ReportABackendTimeoutToTheCircuitBreaker()
	{
		// SAFETY, and the reason this arm exists: bounding a backend call converts a slow backend into an
		// ordinary cache miss, which is INVISIBLE above this decorator. GetOrCreateAsync returns normally,
		// so the middleware cannot tell a healthy backend from a dead one. If the decorator does not report
		// the timeout, nothing does: the breaker stays closed forever, every request pays the deadline on
		// both the read and the write, and nothing is ever cached.
		var breaker = new RecordingCircuitBreaker();
		var backend = new ControllableCache { GetDelay = Deadline * 20 };
		var cache = CreateWithBreaker(backend, breaker);

		_ = await cache.GetAsync("k", CancellationToken.None);

		breaker.Failures.ShouldBe(
			1,
			"a backend that missed its deadline is unhealthy, and this decorator is the only component that "
			+ "can observe it -- above here the timeout looks like a cache miss");
		breaker.Successes.ShouldBe(
			0,
			"a timed-out operation must never be reported as healthy; doing so holds the breaker closed "
			+ "against a backend that is failing every request");
	}

	[Fact]
	public async Task ReportAHealthyBackendToTheCircuitBreaker()
	{
		// LIVENESS. Without this arm, a decorator that reported failure unconditionally -- or never
		// reported success -- would satisfy the safety arm above while latching the breaker open and
		// disabling caching permanently.
		var breaker = new RecordingCircuitBreaker();
		var backend = new ControllableCache();
		var cache = CreateWithBreaker(backend, breaker);

		_ = await cache.GetAsync("k", CancellationToken.None);

		breaker.Successes.ShouldBe(1, "a backend that answered within its deadline is healthy");
		breaker.Failures.ShouldBe(0, "a healthy backend must not be reported as failing");
	}

	[Fact]
	public async Task NotReportToTheCircuitBreakerWhenItIsDisabled()
	{
		// The breaker is opt-in. A composition that has not enabled it must not be driven by cache traffic.
		var breaker = new RecordingCircuitBreaker();
		var backend = new ControllableCache { GetDelay = Deadline * 20 };
		var options = OptionsWith(Deadline);
		options.Resilience.CircuitBreaker.Enabled = false;
		var cache = new TimeoutDistributedCache(
			backend, MsOptions.Create(options), _meterFactory, NullLogger<TimeoutDistributedCache>.Instance, breaker);

		_ = await cache.GetAsync("k", CancellationToken.None);

		breaker.Failures.ShouldBe(0, "the breaker is disabled, so nothing may be recorded against it");
		breaker.Successes.ShouldBe(0, "the breaker is disabled, so nothing may be recorded against it");
	}

	private TimeoutDistributedCache CreateWithBreaker(IDistributedCache backend, ICircuitBreakerPolicy breaker)
	{
		var options = OptionsWith(Deadline);
		options.Resilience.CircuitBreaker.Enabled = true;
		return new TimeoutDistributedCache(
			backend, MsOptions.Create(options), _meterFactory, NullLogger<TimeoutDistributedCache>.Instance, breaker);
	}

	/// <summary>A backend that always misses the deadline, for the registration-wiring arm.</summary>
	private sealed class SlowBackendForWiring : ControllableCache
	{
		public SlowBackendForWiring() => GetDelay = TestTimeouts.Scale(TimeSpan.FromSeconds(30));
	}

	/// <summary>Records what the decorator reports, so the reporting itself can be asserted.</summary>
	private sealed class RecordingCircuitBreaker : ICircuitBreakerPolicy
	{
		public int Successes { get; private set; }

		public int Failures { get; private set; }

		public CircuitState State => CircuitState.Closed;

		public void RecordSuccess() => Successes++;

		public void RecordFailure(Exception? exception = null) => Failures++;

		public void Reset()
		{
			Successes = 0;
			Failures = 0;
		}

		public Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> operation,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException(
				"The decorator reports outcomes to the breaker; it never delegates execution to it. If this "
				+ "throws, the decorator started routing work through the breaker and these arms no longer "
				+ "describe what it does.");
	}

	[Fact]
	public async Task HandTheCircuitBreakerToTheDecoratorItRegisters()
	{
		// WIRING. The arms above construct the decorator directly, so they prove it REPORTS backend health;
		// they say nothing about whether the decorator the container builds ever receives a breaker. A
		// decorator that reports to a breaker it was never given is the advertised-but-unwired shape: every
		// unit test above stays green while a dead backend silently never opens the breaker in a real app.
		var breaker = new RecordingCircuitBreaker();

		var services = new ServiceCollection();
		_ = services.AddSingleton(new DispatchJsonSerializer());
		_ = services.AddSingleton<ICircuitBreakerPolicy>(breaker);
		_ = services.AddDispatchDistributedCaching<SlowBackendForWiring>(o =>
		{
			o.Enabled = true;
			o.Behavior.CacheTimeout = Deadline;
			o.Resilience.CircuitBreaker.Enabled = true;
		});

		await using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IDistributedCache>();

		_ = resolved.ShouldBeAssignableTo<TimeoutDistributedCache>(
			"registration must decorate the distributed cache, or nothing bounds backend latency at all");

		_ = await resolved.GetAsync("k", CancellationToken.None);

		breaker.Failures.ShouldBe(
			1,
			"the decorator the CONTAINER built must report the timeout -- if registration forgets to pass "
			+ "the breaker, a chronically slow backend never opens it and every request pays the deadline "
			+ "twice forever");
	}

}
