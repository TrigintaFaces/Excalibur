// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Handler for SOC 2 compliance alerts.
/// </summary>
/// <remarks>
/// <para>
/// The continuous monitoring service generates alerts for compliance gaps.
/// Implement this interface to receive and process these alerts (e.g., send to SIEM,
/// email notifications, PagerDuty, etc.).
/// </para>
/// </remarks>
public interface IComplianceAlertHandler
{
	/// <summary>
	/// Handles a compliance gap alert.
	/// </summary>
	/// <param name="alert">The compliance gap alert.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task HandleComplianceGapAsync(ComplianceGapAlert alert, CancellationToken cancellationToken);

	/// <summary>
	/// Handles a control validation failure alert.
	/// </summary>
	/// <param name="alert">The validation failure alert.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task HandleValidationFailureAsync(ControlValidationFailureAlert alert, CancellationToken cancellationToken);

	/// <summary>
	/// Handles a compliance status change notification.
	/// </summary>
	/// <param name="notification">The status change notification.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task HandleStatusChangeAsync(ComplianceStatusChangeNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Alert for a detected compliance gap.
/// </summary>
public sealed record ComplianceGapAlert
{
	/// <summary>
	/// Unique identifier for this alert.
	/// </summary>
	public required Guid AlertId { get; init; }

	/// <summary>
	/// The compliance gap that triggered the alert.
	/// </summary>
	public required ComplianceGap Gap { get; init; }

	/// <summary>
	/// When the alert was generated.
	/// </summary>
	public required DateTimeOffset GeneratedAt { get; init; }

	/// <summary>
	/// Optional tenant ID for multi-tenant scenarios.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Whether this is a recurring alert for a persistent gap.
	/// </summary>
	public bool IsRecurring { get; init; }

	/// <summary>
	/// Number of times this gap has been detected.
	/// </summary>
	public int OccurrenceCount { get; init; } = 1;
}

/// <summary>
/// Alert for control validation failures.
/// </summary>
public sealed record ControlValidationFailureAlert
{
	/// <summary>
	/// Unique identifier for this alert.
	/// </summary>
	public required Guid AlertId { get; init; }

	/// <summary>
	/// The control that failed validation.
	/// </summary>
	public required string ControlId { get; init; }

	/// <summary>
	/// The criterion the control is mapped to.
	/// </summary>
	public required TrustServicesCriterion Criterion { get; init; }

	/// <summary>
	/// Error message from validation.
	/// </summary>
	public required string ErrorMessage { get; init; }

	/// <summary>
	/// When the validation failure occurred.
	/// </summary>
	public required DateTimeOffset FailedAt { get; init; }

	/// <summary>
	/// Number of consecutive failures.
	/// </summary>
	public int ConsecutiveFailures { get; init; } = 1;

	/// <summary>
	/// Optional tenant ID for multi-tenant scenarios.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Severity based on consecutive failures.
	/// </summary>
	public GapSeverity Severity => ConsecutiveFailures switch
	{
		>= 5 => GapSeverity.Critical,
		>= 3 => GapSeverity.High,
		>= 1 => GapSeverity.Medium,
		_ => GapSeverity.Low
	};
}

/// <summary>
/// Notification for compliance status changes.
/// </summary>
public sealed record ComplianceStatusChangeNotification
{
	/// <summary>
	/// Unique identifier for this notification.
	/// </summary>
	public required Guid NotificationId { get; init; }

	/// <summary>
	/// The criterion that changed status.
	/// </summary>
	public required TrustServicesCriterion Criterion { get; init; }

	/// <summary>
	/// Previous compliance status.
	/// </summary>
	public required bool WasCompliant { get; init; }

	/// <summary>
	/// Current compliance status.
	/// </summary>
	public required bool IsCompliant { get; init; }

	/// <summary>
	/// When the status change was detected.
	/// </summary>
	public required DateTimeOffset ChangedAt { get; init; }

	/// <summary>
	/// Optional tenant ID for multi-tenant scenarios.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Description of what caused the status change.
	/// </summary>
	public string? Reason { get; init; }
}
