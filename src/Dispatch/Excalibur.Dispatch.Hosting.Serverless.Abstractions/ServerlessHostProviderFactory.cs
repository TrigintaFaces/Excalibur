// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Default implementation of the serverless host provider factory.
/// </summary>
public partial class ServerlessHostProviderFactory : IServerlessHostProviderFactory
{
	private readonly ILogger<ServerlessHostProviderFactory> _logger;
	private readonly ConcurrentDictionary<ServerlessPlatform, IServerlessHostProvider> _providers;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServerlessHostProviderFactory" /> class.
	/// </summary>
	/// <param name="logger"> The logger. </param>
	/// <param name="providers"> Optional collection of providers to register. </param>
	public ServerlessHostProviderFactory(
		ILogger<ServerlessHostProviderFactory> logger,
		IEnumerable<IServerlessHostProvider>? providers = null)
	{
		_logger = logger;
		_providers = new ConcurrentDictionary<ServerlessPlatform, IServerlessHostProvider>();

		// Register provided providers
		if (providers != null)
		{
			foreach (var provider in providers)
			{
				RegisterProvider(provider);
			}
		}
	}

	/// <inheritdoc />
	public IEnumerable<IServerlessHostProvider> AvailableProviders => _providers.Values.Where(static p => p.IsAvailable);

	/// <inheritdoc />
	public IServerlessHostProvider CreateProvider(ServerlessPlatform? preferredPlatform = null)
	{
		var targetPlatform = preferredPlatform ?? DetectPlatform();

		if (_providers.TryGetValue(targetPlatform, out var provider) && provider.IsAvailable)
		{
			LogSelectedPlatform(_logger, targetPlatform);
			return provider;
		}

		// Fall back to any available provider
		var availableProvider = AvailableProviders.FirstOrDefault();
		if (availableProvider != null)
		{
			LogPlatformFallback(_logger, targetPlatform, availableProvider.Platform);
			return availableProvider;
		}

		throw new InvalidOperationException(
			"No serverless providers are available. Ensure the appropriate serverless runtime libraries are installed.");
	}

	/// <inheritdoc />
	public IServerlessHostProvider GetProvider(ServerlessPlatform platform)
	{
		if (_providers.TryGetValue(platform, out var provider))
		{
			if (provider.IsAvailable)
			{
				return provider;
			}

			throw new InvalidOperationException(
				$"Provider for {platform} is registered but not available in the current environment.");
		}

		throw new ArgumentException($"No provider registered for platform {platform}.", nameof(platform));
	}

	/// <inheritdoc />
	public ServerlessPlatform DetectPlatform()
	{
		// AWS Lambda detection
		if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")) ||
			!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")))
		{
			return ServerlessPlatform.AwsLambda;
		}

		// Azure Functions detection
		if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")) ||
			!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
		{
			return ServerlessPlatform.AzureFunctions;
		}

		// Google Cloud Functions detection
		if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTION_NAME")) ||
			!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("K_SERVICE")))
		{
			return ServerlessPlatform.GoogleCloudFunctions;
		}

		LogUnableToDetectPlatform(_logger);
		return ServerlessPlatform.Unknown;
	}

	/// <inheritdoc />
	public void RegisterProvider(IServerlessHostProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		_providers[provider.Platform] = provider;
		LogRegisteredProvider(_logger, provider.Platform);
	}

	// Source-generated logging methods (Sprint 368 - EventId migration)
	[LoggerMessage(ServerlessEventId.PlatformSelected, LogLevel.Information, "Selected {Platform} provider for serverless hosting")]
	private static partial void LogSelectedPlatform(ILogger logger, ServerlessPlatform platform);

	[LoggerMessage(ServerlessEventId.PlatformFallback, LogLevel.Warning, "Preferred platform {PreferredPlatform} not available, falling back to {ActualPlatform}")]
	private static partial void LogPlatformFallback(ILogger logger, ServerlessPlatform preferredPlatform, ServerlessPlatform actualPlatform);

	[LoggerMessage(ServerlessEventId.UnableToDetectPlatform, LogLevel.Warning, "Unable to detect serverless platform from environment variables")]
	private static partial void LogUnableToDetectPlatform(ILogger logger);

	[LoggerMessage(ServerlessEventId.ProviderRegistered, LogLevel.Debug, "Registered provider for platform {Platform}")]
	private static partial void LogRegisteredProvider(ILogger logger, ServerlessPlatform platform);
}
