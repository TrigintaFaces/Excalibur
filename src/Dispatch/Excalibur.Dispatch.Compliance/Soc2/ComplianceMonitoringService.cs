// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Background service for continuous SOC 2 compliance monitoring.
/// </summary>
/// <remarks>
/// <para>
/// This service:
/// </para>
/// <list type="bullet">
/// <item><description>Runs control validation on a configurable interval</description></item>
/// <item><description>Detects compliance gaps and status changes</description></item>
/// <item><description>Generates alerts for gaps exceeding severity threshold</description></item>
/// <item><description>Maintains state to detect recurring and new gaps</description></item>
/// </list>
/// </remarks>
public partial class ComplianceMonitoringService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<Soc2Options> _options;
	private readonly ILogger<ComplianceMonitoringService> _logger;

	// Track previous state to detect changes
	private readonly ConcurrentDictionary<TrustServicesCriterion, bool> _previousCriterionStatus = new();

	private readonly ConcurrentDictionary<string, int> _gapOccurrences = new();
	private readonly ConcurrentDictionary<string, int> _validationFailures = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ComplianceMonitoringService"/> class.
	/// </summary>
	public ComplianceMonitoringService(
		IServiceScopeFactory scopeFactory,
		IOptions<Soc2Options> options,
		ILogger<ComplianceMonitoringService> logger)
	{
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Value.EnableContinuousMonitoring)
		{
			LogMonitoringDisabled();
			return;
		}

		LogMonitoringStarting(_options.Value.MonitoringInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await RunMonitoringCycleAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogErrorInCycle(ex);
			}

			try
			{
				await Task.Delay(_options.Value.MonitoringInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogMonitoringStopped();
	}

	private static async Task NotifyComplianceGapAsync(
		IReadOnlyList<IComplianceAlertHandler> handlers,
		ComplianceGapAlert alert,
		CancellationToken cancellationToken)
	{
		foreach (var handler in handlers)
		{
			try
			{
				await handler.HandleComplianceGapAsync(alert, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Continue to next handler - logging is done at the handler level
			}
		}
	}

	private static async Task NotifyValidationFailureAsync(
		IReadOnlyList<IComplianceAlertHandler> handlers,
		ControlValidationFailureAlert alert,
		CancellationToken cancellationToken)
	{
		foreach (var handler in handlers)
		{
			try
			{
				await handler.HandleValidationFailureAsync(alert, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Continue to next handler - logging is done at the handler level
			}
		}
	}

	private static async Task NotifyStatusChangeAsync(
		IReadOnlyList<IComplianceAlertHandler> handlers,
		ComplianceStatusChangeNotification notification,
		CancellationToken cancellationToken)
	{
		foreach (var handler in handlers)
		{
			try
			{
				await handler.HandleStatusChangeAsync(notification, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Continue to next handler - logging is done at the handler level
			}
		}
	}

	[LoggerMessage(LogLevel.Information, "Continuous compliance monitoring is disabled")]
	private partial void LogMonitoringDisabled();

	[LoggerMessage(LogLevel.Information,
		"Starting continuous compliance monitoring with interval {Interval}")]
	private partial void LogMonitoringStarting(TimeSpan interval);

	[LoggerMessage(LogLevel.Error, "Error in compliance monitoring cycle")]
	private partial void LogErrorInCycle(Exception ex);

	[LoggerMessage(LogLevel.Information, "Compliance monitoring service stopped")]
	private partial void LogMonitoringStopped();

	[LoggerMessage(LogLevel.Debug, "Starting compliance monitoring cycle")]
	private partial void LogCycleStarting();

	[LoggerMessage(LogLevel.Error, "Failed to get compliance status during monitoring cycle")]
	private partial void LogStatusFetchFailed(Exception ex);

	[LoggerMessage(LogLevel.Debug,
		"Completed compliance monitoring cycle. Overall level: {Level}, Gaps: {GapCount}")]
	private partial void LogCycleCompleted(ComplianceLevel level, int gapCount);

	[LoggerMessage(LogLevel.Information,
		"Compliance status change detected for {Criterion}: {PreviousStatus} --> {CurrentStatus}")]
	private partial void LogStatusChangeDetected(
		TrustServicesCriterion criterion,
		string previousStatus,
		string currentStatus);

	[LoggerMessage(LogLevel.Warning,
		"Compliance gap alert: {GapId} - {Description} (Severity: {Severity}, Occurrences: {Occurrences})")]
	private partial void LogGapAlert(string gapId, string description, GapSeverity severity, int occurrences);

	[LoggerMessage(LogLevel.Error,
		"Control validation failure: {ControlId} - {ErrorMessage} (Consecutive failures: {Failures})")]
	private partial void LogControlValidationFailure(string controlId, string errorMessage, int failures);

	private async Task RunMonitoringCycleAsync(CancellationToken cancellationToken)
	{
		LogCycleStarting();

		await using var scope = _scopeFactory.CreateAsyncScope();

		var complianceService = scope.ServiceProvider.GetRequiredService<ISoc2ComplianceService>();
		var alertHandlers = scope.ServiceProvider.GetServices<IComplianceAlertHandler>().ToList();

		// Get current compliance status
		ComplianceStatus status;
		try
		{
			status = await complianceService.GetComplianceStatusAsync(tenantId: null, cancellationToken: cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogStatusFetchFailed(ex);

			// Generate validation failure alerts for each enabled category
			if (_options.Value.EnableAlerts)
			{
				foreach (var category in _options.Value.EnabledCategories)
				{
					await HandleValidationErrorAsync(
						alertHandlers,
						$"CAT-{category}",
						category.GetCriteria().First(),
						ex.Message,
						cancellationToken).ConfigureAwait(false);
				}
			}

			return;
		}

		// Check for status changes and gaps
		await ProcessStatusChangesAsync(status, alertHandlers, cancellationToken).ConfigureAwait(false);
		await ProcessComplianceGapsAsync(status, alertHandlers, cancellationToken).ConfigureAwait(false);

		// Reset validation failure counters for successful validations
		ResetSuccessfulValidations(status);

		var isFullyCompliant = status.OverallLevel == ComplianceLevel.FullyCompliant;
		LogCycleCompleted(status.OverallLevel, status.ActiveGaps.Count);
	}

	private async Task ProcessStatusChangesAsync(
		ComplianceStatus status,
		IReadOnlyList<IComplianceAlertHandler> alertHandlers,
		CancellationToken cancellationToken)
	{
		foreach (var (criterion, criterionStatus) in status.CriterionStatuses)
		{
			var isCompliant = criterionStatus.IsMet;

			if (_previousCriterionStatus.TryGetValue(criterion, out var wasCompliant))
			{
				if (wasCompliant != isCompliant)
				{
					LogStatusChangeDetected(
						criterion,
						wasCompliant ? "Compliant" : "Non-Compliant",
						isCompliant ? "Compliant" : "Non-Compliant");

					if (_options.Value.EnableAlerts)
					{
						var notification = new ComplianceStatusChangeNotification
						{
							NotificationId = Guid.NewGuid(),
							Criterion = criterion,
							WasCompliant = wasCompliant,
							IsCompliant = isCompliant,
							ChangedAt = DateTimeOffset.UtcNow,
							Reason = isCompliant
								? "All controls now passing validation"
								: $"Control validation failures detected. Effectiveness score: {criterionStatus.EffectivenessScore}%"
						};

						await NotifyStatusChangeAsync(alertHandlers, notification, cancellationToken)
							.ConfigureAwait(false);
					}
				}
			}

			_previousCriterionStatus[criterion] = isCompliant;
		}
	}

	private async Task ProcessComplianceGapsAsync(
		ComplianceStatus status,
		IReadOnlyList<IComplianceAlertHandler> alertHandlers,
		CancellationToken cancellationToken)
	{
		if (!_options.Value.EnableAlerts)
		{
			return;
		}

		foreach (var gap in status.ActiveGaps)
		{
			// Check severity threshold
			if (gap.Severity < _options.Value.AlertThreshold)
			{
				continue;
			}

			var gapKey = $"{gap.GapId}:{gap.Criterion}";
			var occurrences = _gapOccurrences.AddOrUpdate(gapKey, 1, (_, count) => count + 1);

			var alert = new ComplianceGapAlert
			{
				AlertId = Guid.NewGuid(),
				Gap = gap,
				GeneratedAt = DateTimeOffset.UtcNow,
				IsRecurring = occurrences > 1,
				OccurrenceCount = occurrences
			};

			LogGapAlert(
				gap.GapId,
				gap.Description,
				gap.Severity,
				occurrences);

			await NotifyComplianceGapAsync(alertHandlers, alert, cancellationToken).ConfigureAwait(false);
		}

		// Clear gaps that are no longer present
		var currentGapKeys = status.ActiveGaps
			.Select(g => $"{g.GapId}:{g.Criterion}")
			.ToHashSet(StringComparer.Ordinal);

		foreach (var key in _gapOccurrences.Keys.ToArray())
		{
			if (!currentGapKeys.Contains(key))
			{
				_ = _gapOccurrences.TryRemove(key, out _);
			}
		}
	}

	private async Task HandleValidationErrorAsync(
		IReadOnlyList<IComplianceAlertHandler> alertHandlers,
		string controlId,
		TrustServicesCriterion criterion,
		string errorMessage,
		CancellationToken cancellationToken)
	{
		var failures = _validationFailures.AddOrUpdate(controlId, 1, (_, count) => count + 1);

		var alert = new ControlValidationFailureAlert
		{
			AlertId = Guid.NewGuid(),
			ControlId = controlId,
			Criterion = criterion,
			ErrorMessage = errorMessage,
			FailedAt = DateTimeOffset.UtcNow,
			ConsecutiveFailures = failures
		};

		LogControlValidationFailure(controlId, errorMessage, failures);

		await NotifyValidationFailureAsync(alertHandlers, alert, cancellationToken).ConfigureAwait(false);
	}

	private void ResetSuccessfulValidations(ComplianceStatus status)
	{
		// Reset failure counters for controls that are now passing
		foreach (var (criterion, criterionStatus) in status.CriterionStatuses)
		{
			if (criterionStatus.IsMet)
			{
				var categoryKey = $"CAT-{criterion.GetCategory()}";
				_ = _validationFailures.TryRemove(categoryKey, out _);
			}
		}
	}
}
