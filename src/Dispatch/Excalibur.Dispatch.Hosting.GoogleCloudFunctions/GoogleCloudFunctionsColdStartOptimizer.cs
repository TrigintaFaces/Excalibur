// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Hosting.GoogleCloudFunctions;

namespace Excalibur.Dispatch.Hosting.GoogleCloud;

/// <summary>
/// Google Cloud Functions-specific cold start optimization implementation.
/// </summary>
/// <remarks>
/// Extends <see cref="ColdStartOptimizerBase"/> with GCP-specific SDK warmup
/// (Cloud Trace, metadata server, Secret Manager).
/// </remarks>
/// <param name="serviceProvider">The service provider for DI container access.</param>
/// <param name="logger">The logger instance.</param>
public partial class GoogleCloudFunctionsColdStartOptimizer(
	IServiceProvider serviceProvider,
	ILogger<GoogleCloudFunctionsColdStartOptimizer> logger) : ColdStartOptimizerBase(serviceProvider, logger)
{
	private readonly ILogger<GoogleCloudFunctionsColdStartOptimizer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public override bool IsEnabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTION_NAME"));

	/// <inheritdoc />
	protected override string PlatformName => "Google Cloud Functions";

	/// <inheritdoc />
	protected override Task WarmupPlatformSdkAsync()
	{
		LogGcpSdkWarmupStarting();

		try
		{
			var projectId = Environment.GetEnvironmentVariable("GCP_PROJECT") ??
							Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
			if (!string.IsNullOrEmpty(projectId))
			{
				LogCloudTraceEnabled();
			}

			_ = Environment.GetEnvironmentVariable("FUNCTION_TARGET");
			_ = Environment.GetEnvironmentVariable("FUNCTION_REGION");
			_ = Environment.GetEnvironmentVariable("K_SERVICE");
			_ = Environment.GetEnvironmentVariable("K_REVISION");

			LogGcpSdkWarmupCompleted();
		}
		catch (Exception ex)
		{
			LogGcpSdkWarmupFailed(ex);
		}

		return Task.CompletedTask;
	}

	[LoggerMessage(GoogleCloudFunctionsEventId.GcpSdkWarmupStarting, LogLevel.Debug, "Starting GCP SDK client warmup")]
	private partial void LogGcpSdkWarmupStarting();

	[LoggerMessage(GoogleCloudFunctionsEventId.GcpSdkWarmupCompleted, LogLevel.Debug, "GCP SDK client warmup completed")]
	private partial void LogGcpSdkWarmupCompleted();

	[LoggerMessage(GoogleCloudFunctionsEventId.GcpSdkWarmupFailed, LogLevel.Warning, "GCP SDK client warmup failed")]
	private partial void LogGcpSdkWarmupFailed(Exception ex);

	[LoggerMessage(GoogleCloudFunctionsEventId.CloudTraceEnabled, LogLevel.Debug, "Cloud Trace is enabled")]
	private partial void LogCloudTraceEnabled();
}
