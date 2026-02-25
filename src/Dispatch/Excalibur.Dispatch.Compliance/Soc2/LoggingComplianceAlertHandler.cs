// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Default compliance alert handler that logs alerts using ILogger.
/// </summary>
/// <remarks>
/// <para>
/// This handler logs all compliance alerts to the configured logging infrastructure.
/// For production use, implement <see cref="IComplianceAlertHandler"/> to send alerts
/// to your preferred notification system (SIEM, PagerDuty, email, Slack, etc.).
/// </para>
/// </remarks>
public sealed partial class LoggingComplianceAlertHandler : IComplianceAlertHandler
{
	private readonly ILogger<LoggingComplianceAlertHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="LoggingComplianceAlertHandler"/> class.
	/// </summary>
	public LoggingComplianceAlertHandler(ILogger<LoggingComplianceAlertHandler> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task HandleComplianceGapAsync(ComplianceGapAlert alert, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(alert);

		if (alert.Gap.Severity == GapSeverity.Critical)
		{
			LogComplianceGapCritical(
							alert.AlertId,
							alert.Gap.GapId,
							alert.Gap.Description,
							alert.Gap.Criterion,
							alert.Gap.Severity,
							alert.IsRecurring,
							alert.OccurrenceCount);
		}
		else if (alert.Gap.Severity == GapSeverity.High)
		{
			LogComplianceGapHigh(
							alert.AlertId,
							alert.Gap.GapId,
							alert.Gap.Description,
							alert.Gap.Criterion,
							alert.Gap.Severity,
							alert.IsRecurring,
							alert.OccurrenceCount);
		}
		else if (alert.Gap.Severity == GapSeverity.Medium)
		{
			LogComplianceGapMedium(
							alert.AlertId,
							alert.Gap.GapId,
							alert.Gap.Description,
							alert.Gap.Criterion,
							alert.Gap.Severity,
							alert.IsRecurring,
							alert.OccurrenceCount);
		}
		else if (alert.Gap.Severity == GapSeverity.Low)
		{
			LogComplianceGapLow(
							alert.AlertId,
							alert.Gap.GapId,
							alert.Gap.Description,
							alert.Gap.Criterion,
							alert.Gap.Severity,
							alert.IsRecurring,
							alert.OccurrenceCount);
		}

		if (!string.IsNullOrEmpty(alert.Gap.Remediation))
		{
			LogComplianceGapRemediation(alert.Gap.GapId, alert.Gap.Remediation);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task HandleValidationFailureAsync(ControlValidationFailureAlert alert, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(alert);

		if (alert.Severity == GapSeverity.Critical)
		{
			LogControlValidationFailureCritical(
							alert.AlertId,
							alert.ControlId,
							alert.ErrorMessage,
							alert.Criterion,
							alert.ConsecutiveFailures);
		}
		else if (alert.Severity == GapSeverity.High)
		{
			LogControlValidationFailureHigh(
							alert.AlertId,
							alert.ControlId,
							alert.ErrorMessage,
							alert.Criterion,
							alert.ConsecutiveFailures);
		}
		else if (alert.Severity == GapSeverity.Medium)
		{
			LogControlValidationFailureMedium(
							alert.AlertId,
							alert.ControlId,
							alert.ErrorMessage,
							alert.Criterion,
							alert.ConsecutiveFailures);
		}
		else if (alert.Severity == GapSeverity.Low)
		{
			LogControlValidationFailureLow(
							alert.AlertId,
							alert.ControlId,
							alert.ErrorMessage,
							alert.Criterion,
							alert.ConsecutiveFailures);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task HandleStatusChangeAsync(ComplianceStatusChangeNotification notification, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(notification);

		if (notification.IsCompliant)
		{
			LogComplianceRestored(
					notification.NotificationId,
					notification.Criterion,
					notification.Reason);
		}
		else
		{
			LogComplianceLost(
					notification.NotificationId,
					notification.Criterion,
					notification.Reason);
		}

		return Task.CompletedTask;
	}

	[LoggerMessage(
			ComplianceEventId.ComplianceGapAlertCritical,
			LogLevel.Critical,
			"COMPLIANCE GAP ALERT [{AlertId}]: {GapId} - {Description}. Criterion: {Criterion}, Severity: {Severity}, Recurring: {IsRecurring}, Occurrences: {Occurrences}")]
	private partial void LogComplianceGapCritical(
			Guid alertId,
			string gapId,
			string description,
			TrustServicesCriterion criterion,
			GapSeverity severity,
			bool isRecurring,
			int occurrences);

	[LoggerMessage(
			ComplianceEventId.ComplianceGapAlertHigh,
			LogLevel.Error,
			"COMPLIANCE GAP ALERT [{AlertId}]: {GapId} - {Description}. Criterion: {Criterion}, Severity: {Severity}, Recurring: {IsRecurring}, Occurrences: {Occurrences}")]
	private partial void LogComplianceGapHigh(
			Guid alertId,
			string gapId,
			string description,
			TrustServicesCriterion criterion,
			GapSeverity severity,
			bool isRecurring,
			int occurrences);

	[LoggerMessage(
			ComplianceEventId.ComplianceGapAlertMedium,
			LogLevel.Warning,
			"COMPLIANCE GAP ALERT [{AlertId}]: {GapId} - {Description}. Criterion: {Criterion}, Severity: {Severity}, Recurring: {IsRecurring}, Occurrences: {Occurrences}")]
	private partial void LogComplianceGapMedium(
			Guid alertId,
			string gapId,
			string description,
			TrustServicesCriterion criterion,
			GapSeverity severity,
			bool isRecurring,
			int occurrences);

	[LoggerMessage(
			ComplianceEventId.ComplianceGapAlertLow,
			LogLevel.Information,
			"COMPLIANCE GAP ALERT [{AlertId}]: {GapId} - {Description}. Criterion: {Criterion}, Severity: {Severity}, Recurring: {IsRecurring}, Occurrences: {Occurrences}")]
	private partial void LogComplianceGapLow(
			Guid alertId,
			string gapId,
			string description,
			TrustServicesCriterion criterion,
			GapSeverity severity,
			bool isRecurring,
			int occurrences);

	[LoggerMessage(
			ComplianceEventId.ComplianceGapRemediationGuidance,
			LogLevel.Information,
			"Remediation guidance for {GapId}: {Guidance}")]
	private partial void LogComplianceGapRemediation(string gapId, string guidance);

	[LoggerMessage(
			ComplianceEventId.ControlValidationFailureCritical,
			LogLevel.Critical,
			"CONTROL VALIDATION FAILURE [{AlertId}]: {ControlId} - {ErrorMessage}. Criterion: {Criterion}, Consecutive failures: {Failures}")]
	private partial void LogControlValidationFailureCritical(
			Guid alertId,
			string controlId,
			string errorMessage,
			TrustServicesCriterion criterion,
			int failures);

	[LoggerMessage(
			ComplianceEventId.ControlValidationFailureHigh,
			LogLevel.Error,
			"CONTROL VALIDATION FAILURE [{AlertId}]: {ControlId} - {ErrorMessage}. Criterion: {Criterion}, Consecutive failures: {Failures}")]
	private partial void LogControlValidationFailureHigh(
			Guid alertId,
			string controlId,
			string errorMessage,
			TrustServicesCriterion criterion,
			int failures);

	[LoggerMessage(
			ComplianceEventId.ControlValidationFailureMedium,
			LogLevel.Warning,
			"CONTROL VALIDATION FAILURE [{AlertId}]: {ControlId} - {ErrorMessage}. Criterion: {Criterion}, Consecutive failures: {Failures}")]
	private partial void LogControlValidationFailureMedium(
			Guid alertId,
			string controlId,
			string errorMessage,
			TrustServicesCriterion criterion,
			int failures);

	[LoggerMessage(
			ComplianceEventId.ControlValidationFailureLow,
			LogLevel.Information,
			"CONTROL VALIDATION FAILURE [{AlertId}]: {ControlId} - {ErrorMessage}. Criterion: {Criterion}, Consecutive failures: {Failures}")]
	private partial void LogControlValidationFailureLow(
			Guid alertId,
			string controlId,
			string errorMessage,
			TrustServicesCriterion criterion,
			int failures);

	[LoggerMessage(
			ComplianceEventId.ComplianceRestored,
			LogLevel.Information,
			"COMPLIANCE RESTORED [{NotificationId}]: {Criterion} is now compliant. Reason: {Reason}")]
	private partial void LogComplianceRestored(
				Guid notificationId,
				TrustServicesCriterion criterion,
				string? reason);

	[LoggerMessage(
			ComplianceEventId.ComplianceLost,
			LogLevel.Warning,
			"COMPLIANCE LOST [{NotificationId}]: {Criterion} is no longer compliant. Reason: {Reason}")]
	private partial void LogComplianceLost(
				Guid notificationId,
				TrustServicesCriterion criterion,
				string? reason);
}
