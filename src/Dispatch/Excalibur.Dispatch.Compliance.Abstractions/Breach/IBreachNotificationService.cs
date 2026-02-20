// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides GDPR Article 33/34 breach notification capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This service manages the lifecycle of data breach notifications including
/// initial reporting, status tracking, and notification of affected data subjects.
/// GDPR requires breach notification to supervisory authorities within 72 hours.
/// </para>
/// </remarks>
public interface IBreachNotificationService
{
	/// <summary>
	/// Reports a data breach for investigation and notification.
	/// </summary>
	/// <param name="report">The breach report details.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result containing the breach tracking identifier and status.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="report"/> is null.</exception>
	Task<BreachNotificationResult> ReportBreachAsync(
		BreachReport report,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current status of a breach notification.
	/// </summary>
	/// <param name="breachId">The breach tracking identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The breach notification result, or null if not found.</returns>
	Task<BreachNotificationResult?> GetBreachStatusAsync(
		string breachId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Notifies affected data subjects about a breach per GDPR Article 34.
	/// </summary>
	/// <param name="breachId">The breach tracking identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The updated breach notification result after subject notification.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the breach has not been reported or subjects have already been notified.</exception>
	Task<BreachNotificationResult> NotifyAffectedSubjectsAsync(
		string breachId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents a data breach report for GDPR Article 33 notification.
/// </summary>
public sealed record BreachReport
{
	/// <summary>
	/// Gets the unique identifier for this breach.
	/// </summary>
	public required string BreachId { get; init; }

	/// <summary>
	/// Gets the description of the breach.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Gets the timestamp when the breach was detected.
	/// </summary>
	public required DateTimeOffset DetectedAt { get; init; }

	/// <summary>
	/// Gets the estimated number of affected data subjects.
	/// </summary>
	public int AffectedSubjectCount { get; init; }

	/// <summary>
	/// Gets the categories of personal data affected by the breach.
	/// </summary>
	public IReadOnlyList<string> DataCategories { get; init; } = [];
}

/// <summary>
/// Result of a breach notification operation.
/// </summary>
public sealed record BreachNotificationResult
{
	/// <summary>
	/// Gets the breach tracking identifier.
	/// </summary>
	public required string BreachId { get; init; }

	/// <summary>
	/// Gets the current status of the breach notification.
	/// </summary>
	public required BreachNotificationStatus Status { get; init; }

	/// <summary>
	/// Gets the timestamp when the breach was reported.
	/// </summary>
	public DateTimeOffset? ReportedAt { get; init; }

	/// <summary>
	/// Gets the deadline for authority notification (72 hours from detection per GDPR).
	/// </summary>
	public DateTimeOffset? NotificationDeadline { get; init; }

	/// <summary>
	/// Gets the timestamp when affected subjects were notified, if applicable.
	/// </summary>
	public DateTimeOffset? SubjectsNotifiedAt { get; init; }
}

/// <summary>
/// Status of a breach notification process.
/// </summary>
public enum BreachNotificationStatus
{
	/// <summary>
	/// The breach has been reported and is under investigation.
	/// </summary>
	Reported = 0,

	/// <summary>
	/// The supervisory authority has been notified.
	/// </summary>
	AuthorityNotified = 1,

	/// <summary>
	/// Affected data subjects have been notified.
	/// </summary>
	SubjectsNotified = 2,

	/// <summary>
	/// The breach notification process is complete.
	/// </summary>
	Resolved = 3
}

/// <summary>
/// Configuration options for breach notification.
/// </summary>
public sealed class BreachNotificationOptions
{
	/// <summary>
	/// Gets or sets the deadline in hours for notifying the supervisory authority.
	/// Default: 72 hours (per GDPR Article 33).
	/// </summary>
	public int NotificationDeadlineHours { get; set; } = 72;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically notify affected subjects.
	/// Default: false.
	/// </summary>
	public bool AutoNotify { get; set; }
}
