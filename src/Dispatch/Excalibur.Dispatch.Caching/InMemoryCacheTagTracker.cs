// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Caching.Diagnostics;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// In-memory implementation of cache tag tracking using concurrent dictionaries.
/// Thread-safe for single-process scenarios (Memory and Distributed cache modes with in-memory tracking).
/// </summary>
public sealed class InMemoryCacheTagTracker : ICacheTagTracker
{
	private readonly Counter<long> _tagRegistrationCounter;
	private readonly Counter<long> _tagLookupCounter;
	private readonly Counter<long> _tagUnregistrationCounter;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCacheTagTracker"/> class.
	/// </summary>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by the class instance; instruments hold a reference to it")]
	public InMemoryCacheTagTracker()
	{
		var meter = new Meter(DispatchCachingTelemetryConstants.MeterName, DispatchCachingTelemetryConstants.Version);
		_tagRegistrationCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.registrations", "registrations", "Number of cache key-tag registrations");
		_tagLookupCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.lookups", "lookups", "Number of cache tag lookup operations");
		_tagUnregistrationCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.unregistrations", "unregistrations", "Number of cache key-tag unregistrations");
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
		_tagRegistrationCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.registrations", "registrations", "Number of cache key-tag registrations");
		_tagLookupCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.lookups", "lookups", "Number of cache tag lookup operations");
		_tagUnregistrationCounter = meter.CreateCounter<long>("dispatch.cache.tag_tracker.unregistrations", "unregistrations", "Number of cache key-tag unregistrations");
	}

	// Maps tag -> set of keys
	private readonly ConcurrentDictionary<string, HashSet<string>> _tagToKeys = new(StringComparer.Ordinal);

	// Maps key -> set of tags (for cleanup)
	private readonly ConcurrentDictionary<string, HashSet<string>> _keyToTags = new(StringComparer.Ordinal);

	// Lock for atomic updates to HashSets
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	/// <inheritdoc />
	public Task RegisterKeyAsync(string key, string[] tags, CancellationToken cancellationToken)
	{

		if (tags == null || tags.Length == 0)
		{
			return Task.CompletedTask;
		}

		_tagRegistrationCounter.Add(1);

		lock (_lock)
		{
			// Store key -> tags mapping for cleanup
			_keyToTags[key] = [.. tags];

			// Store tag -> key mappings for lookup
			foreach (var tag in tags)
			{
				if (!_tagToKeys.TryGetValue(tag, out var keys))
				{
					keys = [];
					_tagToKeys[tag] = keys;
				}

				_ = keys.Add(key);
			}

		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<HashSet<string>> GetKeysByTagsAsync(string[] tags, CancellationToken cancellationToken)
	{

		var result = new HashSet<string>(StringComparer.Ordinal);

		if (tags == null || tags.Length == 0)
		{
			return Task.FromResult(result);
		}

		_tagLookupCounter.Add(1);

		lock (_lock)
		{
			foreach (var tag in tags)
			{
				if (_tagToKeys.TryGetValue(tag, out var keys))
				{
					result.UnionWith(keys);
				}
				else
				{
				}
			}

		}

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task UnregisterKeyAsync(string key, CancellationToken cancellationToken)
	{
		_tagUnregistrationCounter.Add(1);

		lock (_lock)
		{
			// Get tags for this key
			if (_keyToTags.TryRemove(key, out var tags))
			{
				// Remove key from each tag's key set
				foreach (var tag in tags)
				{
					if (_tagToKeys.TryGetValue(tag, out var keys))
					{
						_ = keys.Remove(key);

						// Clean up empty tag entries
						if (keys.Count == 0)
						{
							_ = _tagToKeys.TryRemove(tag, out _);
						}
					}
				}
			}
		}

		return Task.CompletedTask;
	}
}
