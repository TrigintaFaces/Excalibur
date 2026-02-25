// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MsOptions = Microsoft.Extensions.Options.Options;
using Excalibur.EventSourcing.Observability;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Test fixture for EventSourcing telemetry integration testing.
/// Provides Activity and Meter listeners for capturing telemetry data during tests.
/// </summary>
public sealed class EventSourcingTelemetryTestFixture : IDisposable
{
	private readonly ActivityListener _activityListener;
	private readonly MeterListener _meterListener;
	private readonly List<Activity> _recordedActivities = [];
	private readonly List<Measurement<long>> _recordedLongMetrics = [];
	private readonly List<Measurement<double>> _recordedDoubleMetrics = [];
	private readonly List<Measurement<int>> _recordedIntMetrics = [];
	private readonly DateTimeOffset _creationTime = DateTimeOffset.UtcNow;
	private bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventSourcingTelemetryTestFixture"/> class.
	/// Sets up listeners for activities and metrics from EventSourcing components.
	/// </summary>
	public EventSourcingTelemetryTestFixture()
	{
		ClearRecordedData();

		_activityListener = new ActivityListener
		{
			ShouldListenTo = source =>
				source.Name == EventSourcingActivitySource.Name ||
				source.Name.StartsWith("Excalibur.", StringComparison.Ordinal) ||
				source.Name.StartsWith("Test.", StringComparison.Ordinal) ||
				source.Name == "Test.Parent" ||
				source.Name == "Test.Outer",

			Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
				ActivitySamplingResult.AllDataAndRecorded,

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

		_meterListener = new MeterListener
		{
			InstrumentPublished = (instrument, listener) =>
			{
				if (instrument.Meter.Name.StartsWith("Excalibur.", StringComparison.Ordinal) ||
					instrument.Meter.Name.StartsWith("Test.", StringComparison.Ordinal))
				{
					listener.EnableMeasurementEvents(instrument);
				}
			},
		};

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
	public IReadOnlyList<Activity> GetRecordedActivities()
	{
		lock (_recordedActivities)
		{
			return _recordedActivities.ToList().AsReadOnly();
		}
	}

	/// <summary>
	/// Gets all long (Int64) metrics recorded since fixture creation or last <see cref="ClearRecordedData"/> call.
	/// </summary>
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
	public IReadOnlyList<Measurement<int>> GetRecordedIntMetrics()
	{
		lock (_recordedIntMetrics)
		{
			return _recordedIntMetrics.ToList().AsReadOnly();
		}
	}

	/// <summary>
	/// Clears all recorded telemetry data (activities and metrics).
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
