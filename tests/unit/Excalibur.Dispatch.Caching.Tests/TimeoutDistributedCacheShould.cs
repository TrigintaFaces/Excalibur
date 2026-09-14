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
using Microsoft.Extensions.Time.Testing;

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
		//
		// The deadline is spent on an INJECTED clock. This arm used to let the backend stall for real and
		// assert the call returned inside Deadline * 10, which measures the host as much as the decorator:
		// under a full-shard run it failed at 2.51s against a 2s bound while the decorator was behaving
		// correctly -- the timeout fired on time and the thread pool simply did not reach the continuation.
		// A timing bound a busy CI host can cross is a red build with no defect behind it, and this shard is
		// release-blocking. The property asserted is unchanged; the two arms below already prove it this way.
		var backend = new ControllableCache { GetDelay = Deadline * 20 };
		backend.Store("k", [1, 2, 3]);
		var timeProvider = new FakeTimeProvider();
		var cache = new TimeoutDistributedCache(
			backend,
			MsOptions.Create(OptionsWith(Deadline)),
			_meterFactory,
			NullLogger<TimeoutDistributedCache>.Instance,
			circuitBreaker: null,
			timeProvider);

		var elapsed = Stopwatch.StartNew();
		var pending = cache.GetAsync("k", CancellationToken.None);

		// Wait for the backend to be ENTERED before advancing: the deadline source is created before the
		// operation is invoked, so entry proves the timer exists and the advance cannot be lost.
		await backend.Entered.Task;
		timeProvider.Advance(Deadline + TimeSpan.FromTicks(1));

		var value = await pending;
		elapsed.Stop();

		value.ShouldBeNull("a backend that outlasts the deadline is reported as a miss, so the handler runs");
		elapsed.Elapsed.ShouldBeLessThan(
			backend.GetDelay,
			"the caller must be released when the DEADLINE elapses, not when the backend finally returns");
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
	public async Task ReportAMiss_WhenTheBackendIgnoresCancellationEntirely()
	{
		// SAFETY, and the one every other timeout arm above cannot prove: every ControllableCache arm
		// passes the CancellationToken to its own Task.Delay, so it correctly unwinds the moment cts.Token
		// fires -- which is NOT how the one distributed-cache backend this framework ships actually
		// behaves. Microsoft.Extensions.Caching.StackExchangeRedis's RedisCache only checks its token
		// BEFORE dispatching the command; StackExchange.Redis's own IDatabase async methods take no
		// CancellationToken at all, so a slow Redis command cannot be aborted once in flight. This fixture
		// (UncooperativeCache) reproduces exactly that: it never observes the token it is handed. Before
		// the WaitAsync fix, a bare `await` on this backend's task would hang for the full delay regardless
		// of the deadline -- this arm is what would have gone RED against that.
		//
		// THE CLOCK IS INJECTED, and that is the whole difference from how this arm used to read. It
		// previously let a real 4-second backend delay race a real 2-second assertion, so a loaded machine
		// failed it -- measured 1 failure in 5 consecutive runs with no code change between them. The
		// property under test was never the wall clock; it is that the caller is released by the DEADLINE
		// rather than by the backend. Advancing a fake clock asserts exactly that and cannot be perturbed
		// by load, which also stops the arm passing for the wrong reason on a fast machine.
		var backend = new UncooperativeCache { GetDelay = Deadline * 20 };
		backend.Store("k", [1, 2, 3]);
		var timeProvider = new FakeTimeProvider();
		var cache = new TimeoutDistributedCache(
			backend,
			MsOptions.Create(OptionsWith(Deadline)),
			_meterFactory,
			NullLogger<TimeoutDistributedCache>.Instance,
			circuitBreaker: null,
			timeProvider);

		var elapsed = Stopwatch.StartNew();
		var pending = cache.GetAsync("k", CancellationToken.None);

		// Wait for the backend to be ENTERED before advancing: the deadline source is created before the
		// operation is invoked, so entry proves the timer exists and the advance cannot be lost.
		await backend.Entered.Task;
		timeProvider.Advance(Deadline + TimeSpan.FromTicks(1));

		var value = await pending;
		elapsed.Stop();

		value.ShouldBeNull("a backend that ignores its own cancellation token must still be bounded by the caller-side deadline");
		elapsed.Elapsed.ShouldBeLessThan(
			backend.GetDelay,
			"the caller must be released when the DEADLINE elapses, not when the uncancellable backend finally returns");
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

	[Fact]
	public async Task ReleaseTheCallerOnAFakeClockWithoutSpendingTheDeadline()
	{
		// EVERY other arm in this class pays its deadline in wall-clock time, which is why the whole class
		// is environment-sensitive: the margin between a 200ms deadline and a slower-than-expected machine
		// is what decides the result. This arm asserts the same timeout behaviour with the clock injected,
		// so it cannot be affected by machine load at all.
		var backend = new NeverCompletingCache();
		var timeProvider = new FakeTimeProvider();
		var cache = new TimeoutDistributedCache(
			backend,
			MsOptions.Create(OptionsWith(Deadline)),
			_meterFactory,
			NullLogger<TimeoutDistributedCache>.Instance,
			circuitBreaker: null,
			timeProvider);

		var elapsed = Stopwatch.StartNew();
		var pending = cache.GetAsync("k", CancellationToken.None);

		// Wait for the backend to be ENTERED rather than advancing blind: the deadline source is created
		// before the operation is invoked, so entry proves the timer exists and the advance cannot be lost.
		await backend.Entered.Task;
		timeProvider.Advance(Deadline + TimeSpan.FromTicks(1));

		var value = await pending;
		elapsed.Stop();

		value.ShouldBeNull("the deadline elapsed on the injected clock, so the call must degrade to a miss");
		elapsed.Elapsed.ShouldBeLessThan(
			Deadline,
			"the deadline was spent on the fake clock, so this arm must not consume it in wall-clock time");
	}

	/// <summary>A backend whose operation never completes, so only the deadline can end the call.</summary>
	private sealed class NeverCompletingCache : IDistributedCache
	{
		public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public byte[]? Get(string key) => null;

		public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
		{
			_ = Entered.TrySetResult();
			return new TaskCompletionSource<byte[]?>().Task;
		}

		public void Refresh(string key)
		{
		}

		public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

		public void Remove(string key)
		{
		}

		public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;

		public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
		{
		}

		public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
			=> Task.CompletedTask;
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

		/// <summary>
		/// Completes when the backend has been entered, so a test driving an injected clock can advance it
		/// only once the deadline timer provably exists. Mirrors the signal <see cref="NeverCompletingCache"/>
		/// and <see cref="UncooperativeCache"/> already expose.
		/// </summary>
		public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
			_ = Entered.TrySetResult();

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

	/// <summary>
	/// A backend that NEVER observes the <see cref="CancellationToken"/> it is handed, matching how the real
	/// shipped Redis backend (<c>Microsoft.Extensions.Caching.StackExchangeRedis</c>) behaves once a command
	/// is in flight -- unlike <see cref="ControllableCache"/>, whose delay is itself cancellable and so
	/// cannot expose a decorator that only works when the backend cooperates.
	/// </summary>
	private sealed class UncooperativeCache : IDistributedCache
	{
		private readonly Dictionary<string, byte[]> _store = [];

		/// <summary>Signals that the backend call has been entered, so a fake-clock advance cannot be lost.</summary>
		public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public TimeSpan GetDelay { get; init; }

		public void Store(string key, byte[] value)
		{
			lock (_store)
			{
				_store[key] = value;
			}
		}

		public byte[]? Get(string key)
		{
			lock (_store)
			{
				return _store.TryGetValue(key, out var v) ? v : null;
			}
		}

		public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
		{
			_ = Entered.TrySetResult();

			// Deliberately CancellationToken.None: the delay itself is not cancellable, reproducing a
			// command already dispatched to a server that does not accept a token.
			if (GetDelay > TimeSpan.Zero)
			{
				// delay-ok: SIMULATED WORK in a fake backend, and being uncancellable IS the property under
				// test -- this reproduces a Redis command already dispatched to a server that accepts no
				// token. The duration is expressed relative to the deadline (Deadline * 20), never as an
				// absolute wall-clock number, and the arm that uses it injects a FakeTimeProvider: the
				// decorator's deadline fires on the fake clock, so the test never waits for this to elapse
				// and load cannot perturb it. The two cancellable siblings on this same fixture pattern are
				// already acknowledged in task-delay-syncwait.baseline.txt.
				await Task.Delay(GetDelay, CancellationToken.None).ConfigureAwait(false); // delay-ok: see above
			}

			return Get(key);
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

		public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
		{
			Store(key, value);
			return Task.CompletedTask;
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
		//
		// Polled, not asserted immediately: the caller is now released via WaitAsync's own abandon race
		// against the SAME cts.Token the backend observes, so for a token-cooperative backend (this fake)
		// the caller can resume before ExecuteAsync's catch block finishes recording the failure. The
		// abandoned execution still runs to completion and reports its real outcome -- it is eventually
		// consistent, not lost -- which is exactly the tradeoff the fix documents on TimeoutDistributedCache.
		var breaker = new RecordingCircuitBreaker();
		var backend = new ControllableCache { GetDelay = Deadline * 20 };
		var cache = CreateWithBreaker(backend, breaker);

		_ = await cache.GetAsync("k", CancellationToken.None);

		var recorded = await WaitHelpers.WaitUntilAsync(() => breaker.Failures == 1, TestTimeouts.Scale(TimeSpan.FromSeconds(2)));
		recorded.ShouldBeTrue(
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
	/// <summary>
	/// A breaker that counts what it was asked to execute.
	/// </summary>
	/// <remarks>
	/// This fixture used to do the opposite: it counted out-of-band outcome reports and threw from
	/// ExecuteAsync, which locked in a decorator that reported outcomes beside the breaker instead of
	/// running through it. That is the shape that left the Polly-backed breaker unable to open. The
	/// contract now carries no out-of-band recorder at all, so execution is the only thing to count.
	/// </remarks>
	private sealed class RecordingCircuitBreaker : ICircuitBreakerPolicy
	{
		public int Successes { get; private set; }

		public int Failures { get; private set; }

		public CircuitState State => CircuitState.Closed;

		public Task ResetAsync(CancellationToken cancellationToken)
		{
			Successes = 0;
			Failures = 0;
			return Task.CompletedTask;
		}


		public async Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> operation,
			Func<TResult, bool> isFailure,
			CancellationToken cancellationToken)
		{
			try
			{
				var result = await operation(cancellationToken).ConfigureAwait(false);

				if (isFailure(result))
				{
					Failures++;
				}
				else
				{
					Successes++;
				}

				return result;
			}
			catch
			{
				Failures++;
				throw;
			}
		}

		public async Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> operation,
			CancellationToken cancellationToken)
		{
			try
			{
				var result = await operation(cancellationToken).ConfigureAwait(false);
				Successes++;
				return result;
			}
			catch
			{
				Failures++;
				throw;
			}
		}
	}

}
