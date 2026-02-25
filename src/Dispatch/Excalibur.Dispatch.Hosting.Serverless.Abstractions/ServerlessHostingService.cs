// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Hosted service for serverless hosting lifecycle management.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ServerlessHostingService" /> class. </remarks>
/// <param name="provider"> The serverless host provider. </param>
/// <param name="logger"> The logger instance. </param>
internal sealed partial class ServerlessHostingService(
	IServerlessHostProvider provider,
	ILogger<ServerlessHostingService> logger) : IHostedService
{
	private readonly IServerlessHostProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
	private readonly ILogger<ServerlessHostingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		LogStarting(_provider.Platform.ToString());

		if (!_provider.IsAvailable)
		{
			LogProviderUnavailable(_provider.Platform.ToString());
		}
		else
		{
			LogProviderReady(_provider.Platform.ToString());
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken)
	{
		LogStopping(_provider.Platform.ToString());
		return Task.CompletedTask;
	}

	// Source-generated logging methods (Sprint 368 - EventId migration)
	[LoggerMessage(ServerlessEventId.HostingServiceStarting, LogLevel.Information,
		"Starting serverless hosting service for platform {Platform}")]
	private partial void LogStarting(string platform);

	[LoggerMessage(ServerlessEventId.ProviderUnavailable, LogLevel.Warning,
		"Serverless provider {Platform} is not available in the current environment")]
	private partial void LogProviderUnavailable(string platform);

	[LoggerMessage(ServerlessEventId.ProviderReady, LogLevel.Information,
		"Serverless provider {Platform} is ready")]
	private partial void LogProviderReady(string platform);

	[LoggerMessage(ServerlessEventId.HostingServiceStopping, LogLevel.Information,
		"Stopping serverless hosting service for platform {Platform}")]
	private partial void LogStopping(string platform);
}
