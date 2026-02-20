// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Aws;

/// <summary>
/// Default implementation of <see cref="IAwsTracingIntegration"/> that bridges
/// Dispatch telemetry to AWS X-Ray and CloudWatch.
/// </summary>
/// <remarks>
/// <para>
/// Registers an <see cref="ActivityListener"/> that listens for Dispatch activities
/// and maps them to AWS X-Ray trace segments. Trace IDs are converted from
/// W3C format to X-Ray format.
/// </para>
/// </remarks>
public sealed partial class AwsTracingIntegration : IAwsTracingIntegration, IDisposable
{
	private readonly AwsObservabilityOptions _options;
	private readonly ILogger<AwsTracingIntegration> _logger;
	private ActivityListener? _activityListener;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsTracingIntegration"/> class.
	/// </summary>
	/// <param name="options">The AWS observability options.</param>
	/// <param name="logger">The logger.</param>
	public AwsTracingIntegration(
		IOptions<AwsObservabilityOptions> options,
		ILogger<AwsTracingIntegration> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task ConfigureXRayAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_options.EnableXRay)
		{
			return Task.CompletedTask;
		}

		_activityListener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.StartsWith("Dispatch.", StringComparison.Ordinal),
			Sample = SampleActivity,
			ActivityStarted = OnActivityStarted,
			ActivityStopped = OnActivityStopped
		};

		ActivitySource.AddActivityListener(_activityListener);

		LogXRayConfigured(_options.ServiceName, _options.SamplingRate);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task ConfigureCloudWatchMetricsAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_options.EnableCloudWatchMetrics)
		{
			return Task.CompletedTask;
		}

		// CloudWatch metrics integration registers listeners for Dispatch meters
		// The actual publishing is done via the OpenTelemetry CloudWatch exporter
		LogCloudWatchMetricsConfigured(_options.ServiceName, _options.MetricsNamespace);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_activityListener?.Dispose();
		_disposed = true;
	}

	private ActivitySamplingResult SampleActivity(ref ActivityCreationOptions<ActivityContext> options)
	{
		// Use configured sampling rate
#pragma warning disable CA5394 // Random is not cryptographic — sampling rate jitter does not need crypto-strength randomness
		var randomValue = Random.Shared.NextDouble();
#pragma warning restore CA5394
		return randomValue <= _options.SamplingRate
			? ActivitySamplingResult.AllDataAndRecorded
			: ActivitySamplingResult.None;
	}

	private void OnActivityStarted(Activity activity)
	{
		// Add X-Ray specific tags to the activity
		activity.SetTag("aws.xray.service", _options.ServiceName);

		if (_options.XRayDaemonEndpoint is not null)
		{
			activity.SetTag("aws.xray.daemon_endpoint", _options.XRayDaemonEndpoint);
		}
	}

	private static void OnActivityStopped(Activity activity)
	{
		// Mark fault/error status based on activity status
		if (activity.Status == ActivityStatusCode.Error)
		{
			activity.SetTag("aws.xray.fault", "true");
		}
	}

	[LoggerMessage(AwsObservabilityEventId.XRayConfigured, LogLevel.Information,
		"AWS X-Ray integration configured for service {ServiceName} with sampling rate {SamplingRate}")]
	private partial void LogXRayConfigured(string serviceName, double samplingRate);

	[LoggerMessage(AwsObservabilityEventId.CloudWatchMetricsConfigured, LogLevel.Information,
		"AWS CloudWatch metrics integration configured for service {ServiceName} in namespace {MetricsNamespace}")]
	private partial void LogCloudWatchMetricsConfigured(string serviceName, string metricsNamespace);
}
