// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using GoogleApi = Google.Api;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Exports metrics to Google Cloud Monitoring (formerly Stackdriver).
/// </summary>
public sealed class CloudMonitoringExporter : IDisposable
{
	private readonly GooglePubSubOptions _options;
	private readonly ILogger<CloudMonitoringExporter> _logger;
	private readonly MetricServiceClient? _metricClient;
	private readonly ProjectName? _projectName;
	private readonly Timer? _exportTimer;
	private readonly Dictionary<string, GoogleApi.MetricDescriptor> _metricDescriptors;
	private readonly MeterListener? _meterListener;
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else
	private readonly object _lock = new();

#endif

	/// <summary>
	/// Buffered metric points for batch export.
	/// </summary>
	private readonly Dictionary<string, List<Point>> _bufferedPoints;

	/// <summary>
	/// Resource for all metrics.
	/// </summary>
	private readonly GoogleApi.MonitoredResource? _resource;

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudMonitoringExporter" /> class.
	/// </summary>
	public CloudMonitoringExporter(
		IOptions<GooglePubSubOptions> options,
		ILogger<CloudMonitoringExporter> logger)
	{
		_options = options.Value;
		_logger = logger;
		_metricDescriptors = [];
		_bufferedPoints = [];

		if (!_options.ExportToCloudMonitoring)
		{
			_logger.LogInformation("Cloud Monitoring export is disabled");
			return;
		}

		_metricClient = MetricServiceClient.Create();
		_projectName = ProjectName.FromProject(_options.ProjectId);

		// Configure resource labels
		_resource = new GoogleApi.MonitoredResource { Type = "global", Labels = { ["project_id"] = _options.ProjectId } };

		// Add custom resource labels if configured
		foreach (var label in _options.TelemetryResourceLabels)
		{
			_resource.Labels[label.Key] = label.Value;
		}

		// Setup meter listener for OpenTelemetry metrics
		_meterListener = new MeterListener();
		_meterListener.InstrumentPublished = (instrument, listener) =>
		{
			if (string.Equals(instrument.Meter.Name, GooglePubSubTelemetryConstants.MeterName, StringComparison.Ordinal))
			{
				// Subscribe to this instrument
				listener.EnableMeasurementEvents(instrument);

				// Create metric descriptor if needed
				CreateMetricDescriptorIfNeeded(instrument);
			}
		};

		// Set measurement callbacks
		_meterListener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
		_meterListener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);
		_meterListener.SetMeasurementEventCallback<int>(OnMeasurementRecorded);

		_meterListener.Start();

		// Setup export timer
		var exportInterval = TimeSpan.FromSeconds(_options.TelemetryExportIntervalSeconds);
		_exportTimer = new Timer(ExportMetrics, state: null, exportInterval, exportInterval);

		_logger.LogInformation(
			"Cloud Monitoring exporter initialized for project {ProjectId} with {Interval}s export interval",
			_options.ProjectId,
			_options.TelemetryExportIntervalSeconds);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_exportTimer?.Dispose();

		// Export any remaining metrics
		ExportMetrics(state: null);

		_meterListener?.Dispose();
	}

	private void CreateMetricDescriptorIfNeeded(Instrument instrument)
	{
		var metricType = GetMetricType(instrument.Name);

		lock (_lock)
		{
			if (_metricDescriptors.ContainsKey(metricType))
			{
				return;
			}

			try
			{
				var descriptor = new GoogleApi.MetricDescriptor
				{
					Type = metricType,
					DisplayName = instrument.Name,
					Description = instrument.Description ?? string.Empty,
					MetricKind = GetMetricKind(instrument),
					ValueType = GetValueType(instrument),
					Unit = instrument.Unit ?? "1",
				};

				// Add labels from common tags
				descriptor.Labels.Add(new GoogleApi.LabelDescriptor
				{
					Key = "subscription",
					ValueType = GoogleApi.LabelDescriptor.Types.ValueType.String,
					Description = "Pub/Sub subscription name",
				});

				descriptor.Labels.Add(new GoogleApi.LabelDescriptor
				{
					Key = "topic",
					ValueType = GoogleApi.LabelDescriptor.Types.ValueType.String,
					Description = "Pub/Sub topic name",
				});

				descriptor.Labels.Add(new GoogleApi.LabelDescriptor
				{
					Key = "project",
					ValueType = GoogleApi.LabelDescriptor.Types.ValueType.String,
					Description = "GCP project ID",
				});

				// Create the descriptor in Cloud Monitoring
				var request = new CreateMetricDescriptorRequest
				{
					Name = _projectName?.ToString() ?? string.Empty,
					MetricDescriptor = descriptor,
				};
				var createdDescriptor = _metricClient?.CreateMetricDescriptor(request);

				if (createdDescriptor != null)
				{
					_metricDescriptors[metricType] = createdDescriptor;
				}

				_logger.LogDebug(
					"Created metric descriptor for {MetricType}",
					metricType);
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Failed to create metric descriptor for {MetricType}",
					metricType);
			}
		}
	}

	private void OnMeasurementRecorded<T>(
		Instrument instrument,
		T measurement,
		ReadOnlySpan<KeyValuePair<string, object?>> tags,
		object? state)
	{
		if (!_options.ExportToCloudMonitoring)
		{
			return;
		}

		var metricType = GetMetricType(instrument.Name);
		var timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);

		// Create metric labels from tags
		var metricLabels = new Dictionary<string, string>
			(StringComparer.Ordinal)
		{
			["subscription"] = _options.SubscriptionId,
			["topic"] = _options.TopicId,
			["project"] = _options.ProjectId,
		};

		foreach (var tag in tags)
		{
			if (tag.Value != null)
			{
				metricLabels[tag.Key] = tag.Value.ToString() ?? string.Empty;
			}
		}

		// Create the point
		var point = new Point { Interval = new TimeInterval { EndTime = timestamp } };

		// Set the value based on type
		if (typeof(T) == typeof(long))
		{
			point.Value = new TypedValue { Int64Value = Convert.ToInt64(measurement) };
		}
		else if (typeof(T) == typeof(double))
		{
			point.Value = new TypedValue { DoubleValue = Convert.ToDouble(measurement) };
		}
		else if (typeof(T) == typeof(int))
		{
			point.Value = new TypedValue { Int64Value = Convert.ToInt64(measurement) };
		}

		// Buffer the point for batch export
		lock (_lock)
		{
			var key = $"{metricType}:{string.Join(',', metricLabels.Select(static kv => $"{kv.Key}={kv.Value}"))}";

			if (!_bufferedPoints.TryGetValue(key, out var value))
			{
				value = [];
				_bufferedPoints[key] = value;
			}

			value.Add(point);
		}
	}

	private void ExportMetrics(object? state)
	{
		if (!_options.ExportToCloudMonitoring)
		{
			return;
		}

		Dictionary<string, List<Point>> pointsToExport;

		lock (_lock)
		{
			if (_bufferedPoints.Count == 0)
			{
				return;
			}

			// Swap buffers
			pointsToExport = new Dictionary<string, List<Point>>(_bufferedPoints, StringComparer.Ordinal);
			_bufferedPoints.Clear();
		}

		// Create time series for export
		var timeSeries = new List<TimeSeries>();

		foreach (var kvp in pointsToExport)
		{
			var parts = kvp.Key.Split(':', 2);
			if (parts.Length != 2)
			{
				continue;
			}

			var metricType = parts[0];
			var labelString = parts[1];

			// Parse labels
			var metric = new GoogleApi.Metric { Type = metricType };

			foreach (var labelPair in labelString.Split(','))
			{
				var labelParts = labelPair.Split('=', 2);
				if (labelParts.Length == 2)
				{
					metric.Labels[labelParts[0]] = labelParts[1];
				}
			}

			var series = new TimeSeries { Metric = metric, Resource = _resource, Points = { kvp.Value } };

			timeSeries.Add(series);
		}

		if (timeSeries.Count == 0)
		{
			return;
		}

		try
		{
			// Export in batches of 200 (Cloud Monitoring limit)
			const int batchSize = 200;
			for (var i = 0; i < timeSeries.Count; i += batchSize)
			{
				var batch = timeSeries.Skip(i).Take(batchSize).ToList();

				if (_projectName != null)
				{
					_metricClient?.CreateTimeSeries(
						_projectName,
						batch);
				}
			}

			_logger.LogDebug(
				"Exported {Count} time series to Cloud Monitoring",
				timeSeries.Count);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to export metrics to Cloud Monitoring");
		}
	}

	private static string GetMetricType(string instrumentName)
	{
		// Convert to Cloud Monitoring metric type format
		var sanitized = instrumentName.Replace('.', '_').Replace('-', '_');
		return $"custom.googleapis.com/pubsub/{sanitized}";
	}

	private static GoogleApi.MetricDescriptor.Types.MetricKind GetMetricKind(Instrument instrument) =>
		instrument switch
		{
			Counter<long> => GoogleApi.MetricDescriptor.Types.MetricKind.Cumulative,
			Counter<double> => GoogleApi.MetricDescriptor.Types.MetricKind.Cumulative,
			Counter<int> => GoogleApi.MetricDescriptor.Types.MetricKind.Cumulative,
			Histogram<long> => GoogleApi.MetricDescriptor.Types.MetricKind.Gauge,
			Histogram<double> => GoogleApi.MetricDescriptor.Types.MetricKind.Gauge,
			ObservableGauge<int> => GoogleApi.MetricDescriptor.Types.MetricKind.Gauge,
			ObservableGauge<long> => GoogleApi.MetricDescriptor.Types.MetricKind.Gauge,
			_ => GoogleApi.MetricDescriptor.Types.MetricKind.Unspecified,
		};

	private static GoogleApi.MetricDescriptor.Types.ValueType GetValueType(Instrument instrument) =>
		instrument switch
		{
			Counter<long> => GoogleApi.MetricDescriptor.Types.ValueType.Int64,
			Counter<int> => GoogleApi.MetricDescriptor.Types.ValueType.Int64,
			Counter<double> => GoogleApi.MetricDescriptor.Types.ValueType.Double,
			Histogram<long> => GoogleApi.MetricDescriptor.Types.ValueType.Distribution,
			Histogram<double> => GoogleApi.MetricDescriptor.Types.ValueType.Distribution,
			ObservableGauge<int> => GoogleApi.MetricDescriptor.Types.ValueType.Int64,
			ObservableGauge<long> => GoogleApi.MetricDescriptor.Types.ValueType.Int64,
			_ => GoogleApi.MetricDescriptor.Types.ValueType.Int64,
		};
}
