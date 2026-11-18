// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Caching.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// In-process implementation of <see cref="ICacheTagTracker"/> using a bounded
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> of per-tag version stamps.
/// Thread-safe for single-process scenarios (Memory cache mode, or Distributed/Hybrid mode where
/// cross-instance tag propagation is not required).
/// </summary>
public sealed class InMemoryCacheTagTracker : ICacheTagTracker
{
	/// <summary>
	/// Default upper bound on distinct tags tracked, applied by the constructors that do not receive
	/// <see cref="CacheOptions.TagTrackerCapacity"/>. Prevents unbounded growth when tag names are
	/// derived from unbounded input (e.g. per-entity tags).
	/// </summary>
	/// <remarks>
	/// At capacity the tracker does not drop invalidations: a bump for a tag it cannot record
	/// collapses to a global invalidation instead, so every entry written before that moment is
	/// treated as stale and the cache refills. The observable cost is a miss-rate spike, never
	/// stale data.
	/// </remarks>
	private const int DefaultCapacity = 10_000;

	private readonly ConcurrentDictionary<string, string> _stamps = new(StringComparer.Ordinal);
	private readonly Lock _mutationLock = new();
	private readonly Counter<long> _stampCreationCounter;
	private readonly Counter<long> _stampBumpCounter;
	private readonly Counter<long> _epochCollapseCounter;

	// ponytail: one global generation counter, so a bump that cannot be recorded per-tag invalidates EVERY entry
	// rather than only the tag it named. Blunt under sustained tag pressure -- the whole cache refills
	// each time capacity is touched. Upgrade path is a bounded per-tag structure, and only if a
	// measurement shows the bluntness costs more than it saves.
	private long _epoch;   // reported in the collapse log so an operator can count generations
	private readonly int _capacity;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCacheTagTracker"/> class.
	/// </summary>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by the class instance; instruments hold a reference to it")]
	public InMemoryCacheTagTracker()
	{
		var meter = new Meter(DispatchCachingTelemetryConstants.MeterName, DispatchCachingTelemetryConstants.Version);
		_stampCreationCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.stamps_created", "{stamps}", "Number of tag version stamps created");
		_stampBumpCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.stamps_bumped", "{stamps}", "Number of tag version stamps bumped (invalidated)");
		_epochCollapseCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.epoch_collapses", "{collapses}", "Number of global invalidations caused by a bump at capacity");
		_capacity = DefaultCapacity;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCacheTagTracker"/> class using an <see cref="IMeterFactory"/>.
	/// </summary>
	/// <param name="meterFactory"> The meter factory for DI-managed meter lifecycle. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory; instruments hold a reference to it")]
	public InMemoryCacheTagTracker(IMeterFactory meterFactory)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);
		var meter = meterFactory.Create(DispatchCachingTelemetryConstants.MeterName);
		_stampCreationCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.stamps_created", "{stamps}", "Number of tag version stamps created");
		_stampBumpCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.stamps_bumped", "{stamps}", "Number of tag version stamps bumped (invalidated)");
		_epochCollapseCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.epoch_collapses", "{collapses}", "Number of global invalidations caused by a bump at capacity");
		_capacity = DefaultCapacity;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCacheTagTracker"/> class with bounded capacity.
	/// </summary>
	/// <param name="meterFactory"> The meter factory for DI-managed meter lifecycle. </param>
	/// <param name="options"> Cache options providing <see cref="CacheOptions.TagTrackerCapacity"/>. </param>
	/// <param name="logger"> Optional logger for capacity warnings. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory; instruments hold a reference to it")]
	public InMemoryCacheTagTracker(
		IMeterFactory meterFactory,
		IOptions<CacheOptions> options,
		ILogger<InMemoryCacheTagTracker>? logger = null)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(options);
		var meter = meterFactory.Create(DispatchCachingTelemetryConstants.MeterName);
		_stampCreationCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.stamps_created", "{stamps}", "Number of tag version stamps created");
		_stampBumpCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.stamps_bumped", "{stamps}", "Number of tag version stamps bumped (invalidated)");
		_epochCollapseCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.epoch_collapses", "{collapses}", "Number of global invalidations caused by a bump at capacity");
		_capacity = options.Value.TagTrackerCapacity > 0 ? options.Value.TagTrackerCapacity : DefaultCapacity;
		_logger = logger;
	}

	private readonly ILogger<InMemoryCacheTagTracker>? _logger;
	private int _capacityWarningEmitted;

	/// <summary>
	/// Number of tags currently memoized. Exposed to this assembly's test friends so the capacity
	/// bound can be asserted directly rather than inferred from stamp stability, which is a proxy that
	/// cannot distinguish a bounded map from a dropped invalidation.
	/// </summary>
	internal int TrackedTagCount => _stamps.Count;

	/// <inheritdoc />
	public Task<string> GetOrCreateStampAsync(string tag, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(tag);

		if (_stamps.TryGetValue(tag, out var existing))
		{
			return Task.FromResult(existing);
		}

		string? created = null;
		var warnAtCapacity = false;
		lock (_mutationLock)
		{
			// Every writer uses this lock, making admission and capacity one decision.
			if (_stamps.TryGetValue(tag, out existing))
			{
				return Task.FromResult(existing);
			}

			if (_stamps.Count < _capacity)
			{
				created = CacheTagStamp.CreateNew();
				_stamps[tag] = created;
			}
			else
			{
				warnAtCapacity = Interlocked.CompareExchange(ref _capacityWarningEmitted, 1, 0) == 0;
			}
		}

		// Instrumentation can invoke consumer callbacks; never call it under the mutation lock.
		if (created is not null)
		{
			_stampCreationCounter.Add(1);
			return Task.FromResult(created);
		}

		if (warnAtCapacity)
		{
			_logger?.LogWarning(
				"InMemoryCacheTagTracker capacity ({Capacity}) reached. New tags will be tracked without a stable stamp.",
				_capacity);
		}

		return Task.FromResult(CacheTagStamp.CreateNew());
	}

	/// <inheritdoc />
	public Task BumpStampAsync(string tag, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(tag);

		// Overwrite in place when the tag is tracked, and admit a new one only while there is room.
		// The indexer alone would ADD at capacity, letting the invalidate path grow past the bound the
		// resolve path enforces.
		long? collapsedTo = null;
		lock (_mutationLock)
		{
			if (_stamps.ContainsKey(tag) || _stamps.Count < _capacity)
			{
				_stamps[tag] = CacheTagStamp.CreateNew();
			}
			else
			{
				_stamps.Clear();
				collapsedTo = Interlocked.Increment(ref _epoch);
			}
		}

		if (collapsedTo is null)
		{
			_stampBumpCounter.Add(1);
			return Task.CompletedTask;
		}

		// At capacity with an untracked tag there is nowhere to record this invalidation, so it becomes
		// a global one: clearing the map makes subsequent resolutions generate fresh opaque stamps.
		// Previously recorded stamps then compare unequal; the cost is
		// a miss-rate spike, which is observable, bounded and self-correcting.
		// Discard every memoized stamp. Each tag then resolves to a fresh random stamp, so every entry
		// written before this compares unequal and is treated as stale -- the global invalidation --
		// while the stamps themselves stay opaque and separator-free, which they must be because they
		// are concatenated into cache keys. Clearing also RELIEVES the pressure that forced the
		// collapse, so the bound is self-correcting rather than permanently collapsed.
		_epochCollapseCounter.Add(1);
		_logger?.LogWarning(
			"InMemoryCacheTagTracker at capacity ({Capacity}): a bump for an untracked tag collapsed to a "
			+ "global invalidation (epoch {Epoch}). Every cached entry written before this is now treated as "
			+ "stale. Expect a cache-miss spike; raise TagTrackerCapacity if this repeats.",
			_capacity,
			collapsedTo);

		_stampBumpCounter.Add(1);
		return Task.CompletedTask;
	}
}
