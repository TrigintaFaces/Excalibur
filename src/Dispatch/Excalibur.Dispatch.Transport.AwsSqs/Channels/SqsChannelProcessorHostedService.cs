// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Hosted service for SQS channel processor.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="SqsChannelProcessorHostedService" /> class. </remarks>
public partial class SqsChannelProcessorHostedService(
	ISqsChannelProcessor processor,
	ILogger<SqsChannelProcessorHostedService> logger) : BackgroundService
{
	private readonly ISqsChannelProcessor _processor = processor ?? throw new ArgumentNullException(nameof(processor));
	private readonly ILogger<SqsChannelProcessorHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Executes the hosted service.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogStarting();

		try
		{
			await _processor.StartAsync(stoppingToken).ConfigureAwait(false);

			// Keep the service running
			await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// Expected when cancellation is requested
			LogStopped();
		}
		catch (Exception ex)
		{
			LogError(ex);
			throw;
		}
		finally
		{
			await _processor.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(AwsSqsEventId.HostedServiceStarting, LogLevel.Information,
		"Starting SQS channel processor")]
	private partial void LogStarting();

	[LoggerMessage(AwsSqsEventId.HostedServiceStopped, LogLevel.Information,
		"SQS channel processor stopped due to cancellation")]
	private partial void LogStopped();

	[LoggerMessage(AwsSqsEventId.HostedServiceError, LogLevel.Error,
		"Error in SQS channel processor")]
	private partial void LogError(Exception ex);
}
