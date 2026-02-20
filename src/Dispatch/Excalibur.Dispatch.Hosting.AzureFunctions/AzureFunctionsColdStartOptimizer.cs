// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.AzureFunctions;

/// <summary>
/// Azure Functions-specific cold start optimization implementation.
/// </summary>
/// <remarks>
/// Extends <see cref="ColdStartOptimizerBase"/> with Azure-specific SDK warmup
/// (Application Insights, Key Vault, configuration providers).
/// </remarks>
/// <param name="serviceProvider">The service provider for DI container access.</param>
/// <param name="logger">The logger instance.</param>
public partial class AzureFunctionsColdStartOptimizer(
	IServiceProvider serviceProvider,
	ILogger<AzureFunctionsColdStartOptimizer> logger) : ColdStartOptimizerBase(serviceProvider, logger)
{
	private readonly ILogger<AzureFunctionsColdStartOptimizer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public override bool IsEnabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"));

	/// <inheritdoc />
	protected override string PlatformName => "Azure Functions";

	/// <inheritdoc />
	protected override Task WarmupPlatformSdkAsync()
	{
		LogAzureSdkWarmupStarting();

		try
		{
			var appInsightsKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
			var appInsightsConnection = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
			if (!string.IsNullOrEmpty(appInsightsKey) || !string.IsNullOrEmpty(appInsightsConnection))
			{
				LogAppInsightsEnabled();
			}

			_ = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
			_ = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
			_ = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");
			_ = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

			LogAzureSdkWarmupCompleted();
		}
		catch (Exception ex)
		{
			LogAzureSdkWarmupFailed(ex);
		}

		return Task.CompletedTask;
	}

	[LoggerMessage(AzureFunctionsEventId.AzureSdkWarmupStarting, LogLevel.Debug, "Starting Azure SDK client warmup")]
	private partial void LogAzureSdkWarmupStarting();

	[LoggerMessage(AzureFunctionsEventId.AzureSdkWarmupCompleted, LogLevel.Debug, "Azure SDK client warmup completed")]
	private partial void LogAzureSdkWarmupCompleted();

	[LoggerMessage(AzureFunctionsEventId.AzureSdkWarmupFailed, LogLevel.Warning, "Azure SDK client warmup failed")]
	private partial void LogAzureSdkWarmupFailed(Exception ex);

	[LoggerMessage(AzureFunctionsEventId.AppInsightsEnabled, LogLevel.Debug, "Application Insights is enabled")]
	private partial void LogAppInsightsEnabled();
}
