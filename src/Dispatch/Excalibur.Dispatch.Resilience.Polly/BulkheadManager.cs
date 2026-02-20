// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Bulkhead manager for managing multiple bulkheads.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="BulkheadManager" /> class. </remarks>
/// <param name="logger">The logger instance for logging bulkhead operations.</param>
public sealed class BulkheadManager(ILogger<BulkheadManager> logger)
{
	private readonly Dictionary<string, IBulkheadPolicy> _bulkheads = [];
	private readonly ILogger<BulkheadManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	/// <summary>
	/// Gets or creates a bulkhead for the specified resource.
	/// </summary>
	/// <param name="resourceName">The name of the resource guarded by the bulkhead.</param>
	/// <param name="options">Optional configuration for the bulkhead.</param>
	/// <returns>The resolved bulkhead policy.</returns>
	public IBulkheadPolicy GetOrCreateBulkhead(string resourceName, BulkheadOptions? options = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

		lock (_lock)
		{
			if (!_bulkheads.TryGetValue(resourceName, out var bulkhead))
			{
				options ??= new BulkheadOptions();
				bulkhead = new BulkheadPolicy(resourceName, options, _logger);
				_bulkheads[resourceName] = bulkhead;
			}

			return bulkhead;
		}
	}

	/// <summary>
	/// Gets metrics for all managed bulkheads.
	/// </summary>
	/// <returns>A map from resource name to bulkhead metrics.</returns>
	public IReadOnlyDictionary<string, BulkheadMetrics> GetAllMetrics()
	{
		lock (_lock)
		{
			return _bulkheads.ToDictionary(
				static kvp => kvp.Key,
				static kvp => kvp.Value.GetMetrics(),
				StringComparer.Ordinal);
		}
	}

	/// <summary>
	/// Removes a bulkhead from management.
	/// </summary>
	/// <param name="resourceName">The name of the resource whose bulkhead should be removed.</param>
	/// <returns>True if the bulkhead was successfully removed; otherwise, false.</returns>
	public bool RemoveBulkhead(string resourceName)
	{
		lock (_lock)
		{
			if (_bulkheads.TryGetValue(resourceName, out var bulkhead))
			{
				if (bulkhead is IDisposable disposable)
				{
					disposable.Dispose();
				}

				return _bulkheads.Remove(resourceName);
			}

			return false;
		}
	}
}
