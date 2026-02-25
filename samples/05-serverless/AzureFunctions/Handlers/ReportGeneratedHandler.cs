// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using AzureFunctionsSample.Messages;

using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.Logging;

namespace AzureFunctionsSample.Handlers;

/// <summary>
/// Handles report generated events.
/// </summary>
public sealed class ReportGeneratedHandler : IEventHandler<ReportGeneratedEvent>
{
	private readonly ILogger<ReportGeneratedHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ReportGeneratedHandler"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public ReportGeneratedHandler(ILogger<ReportGeneratedHandler> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public Task HandleAsync(ReportGeneratedEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		_logger.LogInformation(
			"Report {ReportId} generated for {ReportDate} at {GeneratedAt}",
			eventMessage.ReportId,
			eventMessage.ReportDate,
			eventMessage.GeneratedAt);

		// In a real application, you might:
		// 1. Store the report in blob storage
		// 2. Send notification to stakeholders
		// 3. Update dashboard

		return Task.CompletedTask;
	}
}
