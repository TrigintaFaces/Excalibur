// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Excalibur.EventSourcing.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for materialized view operations.
/// </summary>
/// <remarks>
/// <para>
/// Provides the following metrics:
/// <list type="bullet">
/// <item><c>materialized_view.refresh.duration</c> - Histogram of refresh operation durations</item>
/// <item><c>materialized_view.staleness</c> - Gauge of view staleness in seconds</item>
/// <item><c>materialized_view.refresh.failures</c> - Counter of refresh failures</item>
/// <item><c>materialized_view.state</c> - Gauge of view state (1=healthy, 0=unhealthy)</item>
/// </list>
/// </para>
/// <para>
/// All metrics use the <c>Excalibur.EventSourcing.MaterializedViews</c> meter.
/// </para>
/// </remarks>
public sealed class MaterializedViewMetrics : IDisposable
{
	/// <summary>
	/// The meter name for materialized view metrics.
	/// </summary>
	public const string MeterName = "Excalibur.EventSourcing.MaterializedViews";

	private readonly Meter _meter;
	private readonly Histogram<double> _refreshDuration;
	private readonly Counter<long> _refreshFailures;

	private readonly TagCardinalityGuard _viewNameGuard = new(maxCardinality: 128);
	private readonly TagCardinalityGuard _errorTypeGuard = new(maxCardinality: 50);

	private readonly object _stateLock = new();
	private readonly Dictionary<string, ViewState> _viewStates = new();
	private long _totalRefreshAttempts;
	private long _totalRefreshFailures;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MaterializedViewMetrics"/> class.
	/// </summary>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by this class and disposed in Dispose()")]
	public MaterializedViewMetrics()
		: this(new Meter(MeterName))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MaterializedViewMetrics"/> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="meterFactory"> The meter factory for DI-managed meter lifecycle. </param>
	public MaterializedViewMetrics(IMeterFactory meterFactory)
		: this(meterFactory?.Create(MeterName) ?? throw new ArgumentNullException(nameof(meterFactory)))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MaterializedViewMetrics"/> class with a custom meter.
	/// </summary>
	/// <param name="meter">The meter to use for metrics.</param>
	internal MaterializedViewMetrics(Meter meter)
	{
		_meter = meter ?? throw new ArgumentNullException(nameof(meter));

		_refreshDuration = _meter.CreateHistogram<double>(
			"materialized_view.refresh.duration",
			unit: "s",
			description: "Duration of materialized view refresh operations");

		_refreshFailures = _meter.CreateCounter<long>(
			"materialized_view.refresh.failures",
			unit: "{failure}",
			description: "Number of materialized view refresh failures");

		// Create observable gauges
		_ = _meter.CreateObservableGauge(
			"materialized_view.staleness",
			observeValues: GetStalenessObservations,
			unit: "s",
			description: "Time since last successful refresh for each view");

		_ = _meter.CreateObservableGauge(
			"materialized_view.state",
			observeValues: GetStateObservations,
			unit: "{state}",
			description: "Health state of materialized views (1=healthy, 0=unhealthy)");
	}

	/// <summary>
	/// Records a successful refresh operation.
	/// </summary>
	/// <param name="viewName">The name of the view that was refreshed.</param>
	/// <param name="duration">The duration of the refresh operation.</param>
	/// <param name="eventsProcessed">The number of events processed.</param>
	public void RecordRefreshSuccess(string viewName, TimeSpan duration, int eventsProcessed)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);

		var guardedViewName = _viewNameGuard.Guard(viewName);
		var tags = new TagList
		{
			{ "view_name", guardedViewName },
			{ "status", "success" }
		};

		_refreshDuration.Record(duration.TotalSeconds, tags);

		lock (_stateLock)
		{
			_totalRefreshAttempts++;
			_viewStates[viewName] = new ViewState(
				IsHealthy: true,
				LastRefreshUtc: DateTimeOffset.UtcNow,
				EventsProcessed: eventsProcessed);
		}
	}

	/// <summary>
	/// Records a failed refresh operation.
	/// </summary>
	/// <param name="viewName">The name of the view that failed to refresh.</param>
	/// <param name="duration">The duration before the failure.</param>
	/// <param name="errorType">Optional error type classification.</param>
	public void RecordRefreshFailure(string viewName, TimeSpan duration, string? errorType = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);

		var guardedViewName = _viewNameGuard.Guard(viewName);
		var tags = new TagList
		{
			{ "view_name", guardedViewName },
			{ "status", "failure" }
		};

		if (!string.IsNullOrEmpty(errorType))
		{
			tags.Add("error_type", _errorTypeGuard.Guard(errorType));
		}

		_refreshDuration.Record(duration.TotalSeconds, tags);
		_refreshFailures.Add(1, tags);

		lock (_stateLock)
		{
			_totalRefreshAttempts++;
			_totalRefreshFailures++;

			if (_viewStates.TryGetValue(viewName, out var existingState))
			{
				// Keep last successful refresh time but mark as unhealthy
				_viewStates[viewName] = existingState with { IsHealthy = false };
			}
			else
			{
				_viewStates[viewName] = new ViewState(
					IsHealthy: false,
					LastRefreshUtc: null,
					EventsProcessed: 0);
			}
		}
	}

	/// <summary>
	/// Records the start of a refresh operation.
	/// </summary>
	/// <param name="viewName">The name of the view being refreshed.</param>
	/// <returns>A stopwatch for timing the operation.</returns>
	public Stopwatch StartRefresh(string viewName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
#pragma warning disable RS0030 // Preserve public API contract returning Stopwatch for callers/tests
		return Stopwatch.StartNew();
#pragma warning restore RS0030
	}

	/// <summary>
	/// Gets the maximum staleness across all views in seconds.
	/// </summary>
	/// <returns>The maximum staleness in seconds, or 0 if no views have been refreshed.</returns>
	public double GetMaxStalenessSeconds()
	{
		lock (_stateLock)
		{
			if (_viewStates.Count == 0)
			{
				return 0;
			}

			var now = DateTimeOffset.UtcNow;
			double maxStaleness = 0;

			foreach (var state in _viewStates.Values)
			{
				if (state.LastRefreshUtc.HasValue)
				{
					var staleness = (now - state.LastRefreshUtc.Value).TotalSeconds;
					maxStaleness = Math.Max(maxStaleness, staleness);
				}
				else
				{
					// Never refreshed - consider infinitely stale
					return double.MaxValue;
				}
			}

			return maxStaleness;
		}
	}

	/// <summary>
	/// Gets the current failure rate as a percentage.
	/// </summary>
	/// <returns>The failure rate percentage (0-100).</returns>
	public double GetFailureRatePercent()
	{
		lock (_stateLock)
		{
			if (_totalRefreshAttempts == 0)
			{
				return 0;
			}

			return _totalRefreshFailures * 100.0 / _totalRefreshAttempts;
		}
	}

	/// <summary>
	/// Resets the failure tracking counters.
	/// </summary>
	/// <remarks>
	/// Call this after recovering from a failure state to reset failure rate calculations.
	/// </remarks>
	public void ResetFailureTracking()
	{
		lock (_stateLock)
		{
			_totalRefreshAttempts = 0;
			_totalRefreshFailures = 0;
		}
	}

	private IEnumerable<Measurement<double>> GetStalenessObservations()
	{
		var now = DateTimeOffset.UtcNow;

		lock (_stateLock)
		{
			foreach (var (viewName, state) in _viewStates)
			{
				var staleness = state.LastRefreshUtc.HasValue
					? (now - state.LastRefreshUtc.Value).TotalSeconds
					: -1; // -1 indicates never refreshed

				yield return new Measurement<double>(staleness, new TagList { { "view_name", _viewNameGuard.Guard(viewName) } });
			}
		}
	}

	private IEnumerable<Measurement<int>> GetStateObservations()
	{
		lock (_stateLock)
		{
			foreach (var (viewName, state) in _viewStates)
			{
				yield return new Measurement<int>(
					state.IsHealthy ? 1 : 0,
					new TagList { { "view_name", _viewNameGuard.Guard(viewName) } });
			}
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_meter.Dispose();
	}

	private sealed record ViewState(bool IsHealthy, DateTimeOffset? LastRefreshUtc, int EventsProcessed);
}
