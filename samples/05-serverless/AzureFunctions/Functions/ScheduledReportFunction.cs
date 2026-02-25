// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using AzureFunctionsSample.Messages;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureFunctionsSample.Functions;

/// <summary>
/// Timer-triggered Azure Function for scheduled tasks.
/// Demonstrates Dispatch messaging integration with scheduled execution.
/// </summary>
public sealed class ScheduledReportFunction
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<ScheduledReportFunction> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ScheduledReportFunction"/> class.
	/// </summary>
	/// <param name="dispatcher">The Dispatch dispatcher.</param>
	/// <param name="logger">The logger instance.</param>
	public ScheduledReportFunction(IDispatcher dispatcher, ILogger<ScheduledReportFunction> logger)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Generates daily sales report on schedule.
	/// </summary>
	/// <param name="timerInfo">The timer trigger information.</param>
	/// <remarks>
	/// <para>
	/// This function runs daily at midnight UTC (0 0 0 * * *).
	/// Adjust the CRON expression for different schedules:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Every hour: 0 0 * * * *</description></item>
	///   <item><description>Every 5 minutes: 0 */5 * * * *</description></item>
	///   <item><description>Weekdays at 9am: 0 0 9 * * 1-5</description></item>
	/// </list>
	/// </remarks>
	[Function("GenerateDailySalesReport")]
	public async Task GenerateDailySalesReportAsync(
		[TimerTrigger("0 0 0 * * *", RunOnStartup = false)] TimerInfo timerInfo)
	{
		_logger.LogInformation(
			"Timer trigger: GenerateDailySalesReport invoked at {UtcNow}",
			DateTimeOffset.UtcNow);

		if (timerInfo.IsPastDue)
		{
			_logger.LogWarning("Timer trigger is running late (past due)");
		}

		// Generate report for yesterday's date
		var reportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

		// Create the report generated event
		var reportEvent = new ReportGeneratedEvent(
			$"RPT-{reportDate:yyyyMMdd}",
			reportDate,
			DateTimeOffset.UtcNow);

		// Create dispatch context
		var context = DispatchContextInitializer.CreateDefaultContext();

		// Dispatch the event using Excalibur messaging
		_ = await _dispatcher.DispatchAsync(reportEvent, context, cancellationToken: default).ConfigureAwait(false);

		_logger.LogInformation(
			"Daily sales report generation initiated for {ReportDate}",
			reportDate);

		// Log next scheduled run
		if (timerInfo.ScheduleStatus?.Next is not null)
		{
			_logger.LogInformation(
				"Next scheduled run: {NextRun}",
				timerInfo.ScheduleStatus.Next);
		}
	}

	/// <summary>
	/// Performs hourly health check and cleanup.
	/// </summary>
	/// <param name="timerInfo">The timer trigger information.</param>
	[Function("HourlyHealthCheck")]
	public Task HourlyHealthCheckAsync(
		[TimerTrigger("0 0 * * * *", RunOnStartup = false)] TimerInfo timerInfo)
	{
		_logger.LogInformation(
			"Timer trigger: HourlyHealthCheck at {UtcNow}, " +
			"Last run: {LastRun}, Next run: {NextRun}",
			DateTimeOffset.UtcNow,
			timerInfo.ScheduleStatus?.Last,
			timerInfo.ScheduleStatus?.Next);

		// In production:
		// 1. Check database connectivity
		// 2. Verify external service health
		// 3. Clean up stale data
		// 4. Report metrics

		return Task.CompletedTask;
	}
}
