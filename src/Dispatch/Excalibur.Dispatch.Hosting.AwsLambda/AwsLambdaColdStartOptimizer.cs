// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.AwsLambda;

/// <summary>
/// AWS Lambda-specific cold start optimization implementation.
/// </summary>
/// <remarks>
/// Extends <see cref="ColdStartOptimizerBase"/> with AWS-specific SDK warmup
/// (X-Ray tracing, CloudWatch, environment variables).
/// </remarks>
/// <param name="serviceProvider">The service provider for DI container access.</param>
/// <param name="logger">The logger instance.</param>
public partial class AwsLambdaColdStartOptimizer(
	IServiceProvider serviceProvider,
	ILogger<AwsLambdaColdStartOptimizer> logger) : ColdStartOptimizerBase(serviceProvider, logger)
{
	private readonly ILogger<AwsLambdaColdStartOptimizer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public override bool IsEnabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

	/// <inheritdoc />
	protected override string PlatformName => "AWS Lambda";

	/// <inheritdoc />
	protected override Task WarmupPlatformSdkAsync()
	{
		LogAwsSdkWarmupStarting();

		try
		{
			var xrayTraceId = Environment.GetEnvironmentVariable("_X_AMZN_TRACE_ID");
			if (!string.IsNullOrEmpty(xrayTraceId))
			{
				LogXRayTracingEnabled();
			}

			_ = Environment.GetEnvironmentVariable("AWS_REGION");
			_ = Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV");
			_ = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE");

			LogAwsSdkWarmupCompleted();
		}
		catch (Exception ex)
		{
			LogAwsSdkWarmupFailed(ex);
		}

		return Task.CompletedTask;
	}

	[LoggerMessage(AwsLambdaEventId.AwsSdkWarmupStarting, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Starting AWS SDK client warmup")]
	private partial void LogAwsSdkWarmupStarting();

	[LoggerMessage(AwsLambdaEventId.AwsSdkWarmupCompleted, Microsoft.Extensions.Logging.LogLevel.Debug,
		"AWS SDK client warmup completed")]
	private partial void LogAwsSdkWarmupCompleted();

	[LoggerMessage(AwsLambdaEventId.AwsSdkWarmupFailed, Microsoft.Extensions.Logging.LogLevel.Warning,
		"AWS SDK client warmup failed")]
	private partial void LogAwsSdkWarmupFailed(Exception ex);

	[LoggerMessage(AwsLambdaEventId.XRayTracingEnabled, Microsoft.Extensions.Logging.LogLevel.Debug,
		"X-Ray tracing is enabled")]
	private partial void LogXRayTracingEnabled();
}
