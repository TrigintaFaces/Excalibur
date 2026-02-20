// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Observability;

/// <summary>
/// Test fixture for OpenTelemetry integration testing.
/// Provides Activity and Meter listeners for capturing telemetry data during tests.
/// </summary>
/// <remarks>
/// This fixture sets up infrastructure for observability validation tests, ensuring that:
/// - Activities (distributed tracing) are captured for all Dispatch operations
/// - Metrics (measurements) are recorded for performance monitoring
/// - Telemetry data can be queried and validated in assertions
///
/// Usage:
/// <code>
/// public class MyObservabilityTests : IDisposable
/// {
///     private readonly OpenTelemetryTestFixture _fixture;
///
///     public MyObservabilityTests()
///     {
///         _fixture = new OpenTelemetryTestFixture();
///     }
///
///     [Fact]
///     public void Should_Record_Activity()
///     {
///         // Act: Perform operation that should create telemetry
///
///         // Assert
///         var activities = _fixture.GetRecordedActivities();
///         activities.ShouldContain(a => a.OperationName == "MyOperation");
///     }
///
///     public void Dispose() => _fixture?.Dispose();
/// }
/// </code>
/// </remarks>
public sealed class OpenTelemetryTestFixture : IDisposable
{
	private readonly ActivityListener _activityListener;
	private readonly MeterListener _meterListener;
	private readonly List<Activity> _recordedActivities = [];
	private readonly List<Measurement<long>> _recordedLongMetrics = [];
	private readonly List<Measurement<double>> _recordedDoubleMetrics = [];
	private readonly List<Measurement<int>> _recordedIntMetrics = [];

	// Track fixture creation time to filter out activities from other tests
	private readonly DateTimeOffset _creationTime = DateTimeOffset.UtcNow;

	private bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenTelemetryTestFixture"/> class.
	/// Sets up listeners for activities and metrics from Dispatch and Excalibur components.
	/// </summary>
	public OpenTelemetryTestFixture()
	{
		// Clear any stale data from previous test runs to prevent test interference
		ClearRecordedData();
		// Configure Activity listener for distributed tracing
		_activityListener = new ActivityListener
		{
			// Listen to all Excalibur.Dispatch.* and Excalibur.* activity sources, plus Test.* for unit tests
			ShouldListenTo = source =>
				source.Name.StartsWith("Excalibur.Dispatch.", StringComparison.Ordinal) ||
				source.Name.StartsWith("Excalibur.", StringComparison.Ordinal) ||
				source.Name == "Test" || // For unit tests using _testActivitySource
				source.Name.StartsWith("Test.", StringComparison.Ordinal),

			// Record all activities with full data
			Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
				ActivitySamplingResult.AllDataAndRecorded,
			SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
				ActivitySamplingResult.AllDataAndRecorded,

			// Capture activity when it completes (has full timing data)
			// Only capture activities that started after this fixture was created
			// to avoid capturing activities from concurrent tests
			ActivityStopped = activity =>
			{
				if (activity != null && activity.StartTimeUtc >= _creationTime.UtcDateTime)
				{
					lock (_recordedActivities)
					{
						_recordedActivities.Add(activity);
					}
				}
			},
		};

		ActivitySource.AddActivityListener(_activityListener);

		// Configure Meter listener for metrics collection
		_meterListener = new MeterListener
		{
			// Publish all instruments from Excalibur.*, and Test.* meters
			InstrumentPublished = (instrument, listener) =>
			{
				if (instrument.Meter.Name.StartsWith("Excalibur.", StringComparison.Ordinal) ||
					instrument.Meter.Name.StartsWith("Test.", StringComparison.Ordinal) ||
					instrument.Meter.Name == "Test")
				{
					listener.EnableMeasurementEvents(instrument);
				}
			},
		};

		// Subscribe to measurements for different numeric types
		_meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			lock (_recordedLongMetrics)
			{
				_recordedLongMetrics.Add(new Measurement<long>(measurement, tags));
			}
		});

		_meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
		{
			lock (_recordedDoubleMetrics)
			{
				_recordedDoubleMetrics.Add(new Measurement<double>(measurement, tags));
			}
		});

		_meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
		{
			lock (_recordedIntMetrics)
			{
				_recordedIntMetrics.Add(new Measurement<int>(measurement, tags));
			}
		});

		_meterListener.Start();
	}

	/// <summary>
	/// Gets all activities recorded since fixture creation or last <see cref="ClearRecordedData"/> call.
	/// </summary>
	/// <returns>Read-only list of recorded activities.</returns>
	public IReadOnlyList<Activity> GetRecordedActivities()
	{
		lock (_recordedActivities)
		{
			return _recordedActivities.ToList().AsReadOnly();
		}
	}

	/// <summary>
	/// Waits for activities matching the specified predicate to be recorded.
	/// </summary>
	/// <param name="count">Minimum number of matching activities required.</param>
	/// <param name="predicate">Optional filter for activities to wait for.</param>
	/// <param name="timeout">Maximum time to wait.</param>
	/// <returns>The matching activities, or throws TimeoutException if not found.</returns>
	public async Task<IReadOnlyList<Activity>> WaitForActivitiesAsync(
		int count = 1,
		Func<Activity, bool>? predicate = null,
		TimeSpan? timeout = null)
	{
		var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
		var deadline = DateTime.UtcNow.Add(effectiveTimeout);
		var pollInterval = TimeSpan.FromMilliseconds(50);

		while (DateTime.UtcNow < deadline)
		{
			var activities = GetRecordedActivities();
			var matching = predicate != null
				? activities.Where(predicate).ToList()
				: [.. activities];

			if (matching.Count >= count)
			{
				return matching.AsReadOnly();
			}

			await Task.Delay(pollInterval).ConfigureAwait(false);
		}

		// Timeout - return what we have for diagnostic purposes
		var finalActivities = GetRecordedActivities();
		var finalMatching = predicate != null
			? finalActivities.Where(predicate).ToList()
			: [.. finalActivities];

		throw new TimeoutException(
			$"Timed out waiting for {count} activities. " +
			$"Found {finalMatching.Count} matching activities out of {finalActivities.Count} total. " +
			$"Activity sources present: {string.Join(", ", finalActivities.Select(a => a.Source.Name).Distinct())}");
	}

	/// <summary>
	/// Waits for activities from a specific source to be recorded.
	/// </summary>
	/// <param name="sourceName">The activity source name to wait for.</param>
	/// <param name="count">Minimum number of activities required.</param>
	/// <param name="timeout">Maximum time to wait.</param>
	/// <returns>The matching activities.</returns>
	public Task<IReadOnlyList<Activity>> WaitForActivitiesFromSourceAsync(
		string sourceName,
		int count = 1,
		TimeSpan? timeout = null)
	{
		return WaitForActivitiesAsync(
			count,
			a => a.Source.Name.Equals(sourceName, StringComparison.Ordinal),
			timeout);
	}

	/// <summary>
	/// Gets all long (Int64) metrics recorded since fixture creation or last <see cref="ClearRecordedData"/> call.
	/// </summary>
	/// <returns>Read-only list of recorded long measurements.</returns>
	public IReadOnlyList<Measurement<long>> GetRecordedLongMetrics()
	{
		lock (_recordedLongMetrics)
		{
			return _recordedLongMetrics.ToList().AsReadOnly();
		}
	}

	/// <summary>
	/// Gets all double metrics recorded since fixture creation or last <see cref="ClearRecordedData"/> call.
	/// </summary>
	/// <returns>Read-only list of recorded double measurements.</returns>
	public IReadOnlyList<Measurement<double>> GetRecordedDoubleMetrics()
	{
		lock (_recordedDoubleMetrics)
		{
			return _recordedDoubleMetrics.ToList().AsReadOnly();
		}
	}

	/// <summary>
	/// Gets all integer (Int32) metrics recorded since fixture creation or last <see cref="ClearRecordedData"/> call.
	/// </summary>
	/// <returns>Read-only list of recorded integer measurements.</returns>
	public IReadOnlyList<Measurement<int>> GetRecordedIntMetrics()
	{
		lock (_recordedIntMetrics)
		{
			return _recordedIntMetrics.ToList().AsReadOnly();
		}
	}

	/// <summary>
	/// Clears all recorded telemetry data (activities and metrics).
	/// Useful for isolating test assertions or reducing memory usage in long-running test suites.
	/// </summary>
	public void ClearRecordedData()
	{
		lock (_recordedActivities)
		{
			_recordedActivities.Clear();
		}

		lock (_recordedLongMetrics)
		{
			_recordedLongMetrics.Clear();
		}

		lock (_recordedDoubleMetrics)
		{
			_recordedDoubleMetrics.Clear();
		}

		lock (_recordedIntMetrics)
		{
			_recordedIntMetrics.Clear();
		}
	}

	/// <summary>
	/// Disposes the fixture, cleaning up activity and meter listeners.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_activityListener?.Dispose();
		_meterListener?.Dispose();

		ClearRecordedData();

		_disposed = true;
	}
}
