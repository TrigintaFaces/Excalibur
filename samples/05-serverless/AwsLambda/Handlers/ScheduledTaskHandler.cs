// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using AwsLambdaSample.Messages;

using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.Logging;

namespace AwsLambdaSample.Handlers;

/// <summary>
/// Handles scheduled task events.
/// </summary>
public sealed class ScheduledTaskHandler : IEventHandler<ScheduledTaskEvent>
{
	private readonly ILogger<ScheduledTaskHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ScheduledTaskHandler"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public ScheduledTaskHandler(ILogger<ScheduledTaskHandler> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public Task HandleAsync(ScheduledTaskEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		_logger.LogInformation(
			"Scheduled task {TaskId} '{TaskName}' executed at {ExecutedAt}",
			eventMessage.TaskId,
			eventMessage.TaskName,
			eventMessage.ExecutedAt);

		// In a real application, you might:
		// 1. Generate reports
		// 2. Clean up expired data
		// 3. Trigger batch processing

		return Task.CompletedTask;
	}
}
