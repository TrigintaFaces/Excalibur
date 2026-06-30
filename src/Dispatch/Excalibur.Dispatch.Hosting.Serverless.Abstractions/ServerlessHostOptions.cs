// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Telemetry-related configuration options for serverless hosting.
/// </summary>
/// <remarks>
/// On AWS Lambda (and the other serverless platforms), distributed tracing and metrics are
/// <strong>platform-provisioned</strong>: the cloud runtime supplies X-Ray active tracing
/// and CloudWatch metrics when the operator enables them at the function/platform level. The host
/// providers therefore do <em>not</em> wire in-process exporters for these flags — enabling them
/// neither fails nor fabricates a degraded substitute (fail-open).
/// </remarks>
public sealed class ServerlessTelemetryOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable distributed tracing.
	/// </summary>
	/// <value><see langword="true"/> to enable distributed tracing; otherwise, <see langword="false"/>.</value>
	/// <remarks>
	/// Distributed tracing is platform-provisioned (e.g. AWS Lambda X-Ray active tracing); the host
	/// providers do not register in-process exporters for this flag.
	/// </remarks>
	public bool EnableDistributedTracing { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection.
	/// </summary>
	/// <value><see langword="true"/> to enable metrics collection; otherwise, <see langword="false"/>.</value>
	/// <remarks>
	/// Metrics are platform-provisioned (e.g. AWS Lambda CloudWatch metrics); the host providers do
	/// not register in-process exporters for this flag.
	/// </remarks>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable structured logging.
	/// </summary>
	/// <value><see langword="true"/> to enable structured logging; otherwise, <see langword="false"/>.</value>
	public bool EnableStructuredLogging { get; set; } = true;
}

/// <summary>
/// Configuration options for serverless hosting.
/// </summary>
public sealed class ServerlessHostOptions
{
	/// <summary>
	/// The cleanup headroom reserved before the platform's hard timeout, subtracted from the remaining
	/// execution time so handlers are cancelled with enough margin to flush/dispose before the cloud
	/// forcibly terminates the invocation. Shared across all serverless host providers (AWS Lambda,
	/// Azure Functions, Google Cloud Functions) so timeout behavior is identical and not accidentally
	/// platform-divergent; platform-mandated divergence, if ever needed, must be explicit.
	/// </summary>
	/// <value>500 milliseconds.</value>
	internal static readonly TimeSpan DefaultCleanupReserve = TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// Computes the execution-timeout budget for a serverless invocation: the platform-reported
	/// remaining time less <see cref="DefaultCleanupReserve"/>, floored at <see cref="TimeSpan.Zero"/>.
	/// A zero budget means the invocation is already within (or past) the cleanup reserve and MUST be
	/// cancelled immediately (fail-closed) — the handler is never left to run unbounded. Centralizing
	/// the clamp here makes the fail-open branch (skipping cancellation when the budget is non-positive)
	/// structurally inexpressible at every call site.
	/// </summary>
	/// <param name="remainingTime">The platform-reported remaining execution time.</param>
	/// <returns>
	/// A non-negative timeout; <see cref="TimeSpan.Zero"/> when already within the cleanup reserve,
	/// which schedules immediate cancellation.
	/// </returns>
	internal static TimeSpan ComputeExecutionTimeout(TimeSpan remainingTime)
	{
		var executionTimeout = remainingTime - DefaultCleanupReserve;
		return executionTimeout > TimeSpan.Zero ? executionTimeout : TimeSpan.Zero;
	}

	/// <summary>
	/// Gets or sets the preferred serverless platform. If null, auto-detection will be used.
	/// </summary>
	/// <value>The preferred serverless platform, or <see langword="null"/> to use auto-detection.</value>
	public ServerlessPlatform? PreferredPlatform { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable cold start optimization.
	/// </summary>
	/// <value><see langword="true"/> to enable cold start optimization; otherwise, <see langword="false"/>.</value>
	public bool EnableColdStartOptimization { get; set; } = true;

	/// <summary>
	/// Gets or sets the telemetry options for serverless hosting.
	/// </summary>
	/// <value>The telemetry options controlling tracing, metrics, and logging.</value>
	public ServerlessTelemetryOptions Telemetry { get; set; } = new();

	/// <summary>
	/// Gets or sets the timeout for function execution.
	/// </summary>
	/// <value>The timeout for function execution, or <see langword="null"/> to use the default timeout.</value>
	public TimeSpan? ExecutionTimeout { get; set; }

	/// <summary>
	/// Gets or sets the memory limit in MB.
	/// </summary>
	/// <value>The memory limit in MB, or <see langword="null"/> to use the default limit.</value>
	[Range(1, int.MaxValue)]
	public int? MemoryLimitMB { get; set; }

	/// <summary>
	/// Gets custom environment variables.
	/// </summary>
	/// <value>The custom environment variables.</value>
	public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
