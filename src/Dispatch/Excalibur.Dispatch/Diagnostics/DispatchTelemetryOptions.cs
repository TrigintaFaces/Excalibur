// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Configuration options for Dispatch telemetry and observability features. Implements R8.21 comprehensive telemetry and R7.17 performance monitoring.
/// </summary>
/// <remarks>
/// <para>
/// Export/sampling properties are in <see cref="Export"/>.
/// This follows the <c>OtlpExporterOptions</c> pattern of separating export configuration from feature toggles.
/// </para>
/// <para>
/// Controls the collection and emission of telemetry data including:
/// <list type="bullet">
/// <item> OpenTelemetry metrics for performance monitoring </item>
/// <item> Distributed tracing for request flow visibility </item>
/// <item> Custom metrics for business logic monitoring </item>
/// <item> Performance counters for hot-path optimization </item>
/// <item> Health check integration for operational status </item>
/// </list>
/// </para>
/// </remarks>
public sealed class DispatchTelemetryOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether distributed tracing is enabled.
	/// </summary>
	/// <value> True to enable OpenTelemetry ActivitySource tracing; false to disable. Default is true. </value>
	public bool EnableTracing { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether metrics collection is enabled.
	/// </summary>
	/// <value> True to enable OpenTelemetry Meter metrics; false to disable. Default is true. </value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether enhanced store observability is enabled.
	/// </summary>
	/// <value> True to enable detailed metrics from enhanced stores; false for basic metrics only. Default is true. </value>
	public bool EnableEnhancedStoreObservability { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether pipeline observability is enabled.
	/// </summary>
	/// <value> True to enable pipeline stage metrics and tracing; false to disable. Default is true. </value>
	public bool EnablePipelineObservability { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether hot-path performance metrics are enabled.
	/// </summary>
	/// <value> True to enable high-frequency performance counters; false to disable. Default is false. </value>
	public bool EnableHotPathMetrics { get; set; }

	/// <summary>
	/// Gets or sets the service name for telemetry data.
	/// </summary>
	/// <value> The service name used in OpenTelemetry resource attributes. Default is "Excalibur.Dispatch". </value>
	[Required]
	public string ServiceName { get; set; } = "Excalibur.Dispatch";

	/// <summary>
	/// Gets or sets the service version for telemetry data.
	/// </summary>
	/// <value> The service version used in OpenTelemetry resource attributes. Default is "1.0.0". </value>
	[Required]
	public string ServiceVersion { get; set; } = "1.0.0";

	/// <summary>
	/// Gets or sets the threshold for slow operation warnings.
	/// </summary>
	/// <value> Operations taking longer than this threshold will be flagged as slow. Default is 2 seconds. </value>
	public TimeSpan SlowOperationThreshold { get; set; } = TimeSpan.FromSeconds(2);

	/// <summary>
	/// Gets or sets the tags to include with all telemetry data.
	/// </summary>
	/// <value> Key-value pairs added to all traces and metrics. Default is empty. </value>
	public IDictionary<string, string> GlobalTags { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the export and sampling options.
	/// </summary>
	/// <value> The telemetry export options. </value>
	public TelemetryExportOptions Export { get; set; } = new();

	/// <summary>
	/// Creates a configuration optimized for production environments.
	/// </summary>
	/// <returns> Configuration with settings appropriate for production use. </returns>
	public static DispatchTelemetryOptions CreateProductionProfile() =>
		new()
		{
			EnableTracing = true,
			EnableMetrics = true,
			EnableEnhancedStoreObservability = true,
			EnablePipelineObservability = false, // Reduced overhead
			EnableHotPathMetrics = false, // Disabled for performance
			SlowOperationThreshold = TimeSpan.FromSeconds(5),
			Export = new TelemetryExportOptions
			{
				SamplingRatio = 0.01, // 1% sampling
				MetricBatchSize = 1000,
				ExportTimeout = TimeSpan.FromSeconds(10),
			},
		};

	/// <summary>
	/// Creates a configuration optimized for development environments.
	/// </summary>
	/// <returns> Configuration with settings appropriate for development use. </returns>
	public static DispatchTelemetryOptions CreateDevelopmentProfile() =>
		new()
		{
			EnableTracing = true,
			EnableMetrics = true,
			EnableEnhancedStoreObservability = true,
			EnablePipelineObservability = true,
			EnableHotPathMetrics = true, // Enabled for debugging
			SlowOperationThreshold = TimeSpan.FromSeconds(1),
			Export = new TelemetryExportOptions
			{
				SamplingRatio = 1.0, // 100% sampling
				MetricBatchSize = 100,
				ExportTimeout = TimeSpan.FromSeconds(60),
			},
		};

	/// <summary>
	/// Creates a configuration optimized for throughput scenarios with minimal observability overhead.
	/// </summary>
	/// <returns> Configuration with minimal observability overhead. </returns>
	public static DispatchTelemetryOptions CreateThroughputProfile() =>
		new()
		{
			EnableTracing = false, // Disabled for performance
			EnableMetrics = true,
			EnableEnhancedStoreObservability = false, // Disabled for performance
			EnablePipelineObservability = false, // Disabled for performance
			EnableHotPathMetrics = false, // Disabled for performance
			SlowOperationThreshold = TimeSpan.FromSeconds(10),
			Export = new TelemetryExportOptions
			{
				SamplingRatio = 0.001, // 0.1% sampling
				MetricBatchSize = 2000,
				ExportTimeout = TimeSpan.FromSeconds(5),
			},
		};

	/// <summary>
	/// Validates the configuration options and throws an exception if any values are invalid.
	/// </summary>
	/// <exception cref="ArgumentException"> Thrown when configuration values are invalid or inconsistent. </exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ServiceName))
		{
			throw new ArgumentException(
					ErrorMessages.ServiceNameCannotBeNullOrEmpty,
					nameof(ServiceName));
		}

		if (string.IsNullOrWhiteSpace(ServiceVersion))
		{
			throw new ArgumentException(
					ErrorMessages.ServiceVersionCannotBeNullOrEmpty,
					nameof(ServiceVersion));
		}

		if (SlowOperationThreshold <= TimeSpan.Zero)
		{
			throw new ArgumentException(
					ErrorMessages.SlowOperationThresholdMustBePositive,
					nameof(SlowOperationThreshold));
		}

		if (Export.ExportTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException(ErrorMessages.ExportTimeoutMustBePositive, nameof(Export.ExportTimeout));
		}

		if (Export.SamplingRatio is < 0.0 or > 1.0)
		{
			throw new ArgumentException(
					ErrorMessages.SamplingRatioMustBeBetween0And1,
					nameof(Export.SamplingRatio));
		}
	}

	/// <summary>
	/// Copies all properties from this instance to the target instance.
	/// </summary>
	/// <param name="target"> The target instance to copy properties to. </param>
	/// <exception cref="ArgumentNullException"> Thrown when target is null. </exception>
	public void CopyTo(DispatchTelemetryOptions target)
	{
		ArgumentNullException.ThrowIfNull(target);

		target.EnableTracing = EnableTracing;
		target.EnableMetrics = EnableMetrics;
		target.EnableEnhancedStoreObservability = EnableEnhancedStoreObservability;
		target.EnablePipelineObservability = EnablePipelineObservability;
		target.EnableHotPathMetrics = EnableHotPathMetrics;
		target.ServiceName = ServiceName;
		target.ServiceVersion = ServiceVersion;
		target.SlowOperationThreshold = SlowOperationThreshold;
		target.GlobalTags = new Dictionary<string, string>(GlobalTags, StringComparer.Ordinal);
		target.Export = new TelemetryExportOptions
		{
			SamplingRatio = Export.SamplingRatio,
			MaxSpansPerTrace = Export.MaxSpansPerTrace,
			MetricBatchSize = Export.MetricBatchSize,
			ExportTimeout = Export.ExportTimeout,
		};
	}
}
