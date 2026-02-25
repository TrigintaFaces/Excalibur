// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Compliance.Diagnostics;

/// <summary>
/// Caps the cardinality of a metric tag dimension by mapping values beyond a configured limit
/// to an overflow sentinel. Prevents unbounded tag value growth (e.g., from key IDs, tenant IDs,
/// or event types) that would cause O(n) metric scrape cost.
/// </summary>
/// <remarks>
/// <para>
/// Intentional copy of <c>Excalibur.Dispatch.Observability.Diagnostics.TagCardinalityGuard</c>.
/// Kept separate due to dependency isolation: <c>Excalibur.Dispatch.Observability</c> must not be
/// pulled into <c>Excalibur.Dispatch.Compliance</c>.
/// </para>
/// <para>
/// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>. O(1) amortized per call.
/// </para>
/// </remarks>
internal sealed class TagCardinalityGuard
{
	private readonly ConcurrentDictionary<string, byte> _knownValues;
	private readonly int _maxCardinality;
	private readonly string _overflowValue;

	/// <summary>
	/// Initializes a new instance of the <see cref="TagCardinalityGuard"/> class.
	/// </summary>
	/// <param name="maxCardinality">Maximum number of distinct tag values to track before overflow.</param>
	/// <param name="overflowValue">The sentinel value returned for values that exceed the cardinality limit.</param>
	public TagCardinalityGuard(int maxCardinality = 100, string overflowValue = "__other__")
	{
		_maxCardinality = maxCardinality;
		_overflowValue = overflowValue;
		_knownValues = new ConcurrentDictionary<string, byte>(
			concurrencyLevel: Environment.ProcessorCount,
			capacity: Math.Min(maxCardinality, 1024));
	}

	/// <summary>
	/// Returns the tag value if it is within the cardinality limit, or the overflow sentinel otherwise.
	/// </summary>
	/// <param name="tagValue">The raw tag value to guard.</param>
	/// <returns>The original value if within limits; otherwise the overflow sentinel.</returns>
	public string Guard(string? tagValue)
	{
		if (tagValue is null)
		{
			return _overflowValue;
		}

		// Fast path: already tracked — O(1) lookup, no race
		if (_knownValues.ContainsKey(tagValue))
		{
			return tagValue;
		}

		// Check cardinality limit before attempting to add.
		// Minor overshoot is acceptable — cardinality guards are approximate by design.
		if (_knownValues.Count >= _maxCardinality)
		{
			return _overflowValue;
		}

		// TryAdd is atomic — concurrent adds may slightly exceed _maxCardinality
		// but will not corrupt state. This is the standard ConcurrentDictionary pattern.
		_knownValues.TryAdd(tagValue, 0);
		return tagValue;
	}
}
