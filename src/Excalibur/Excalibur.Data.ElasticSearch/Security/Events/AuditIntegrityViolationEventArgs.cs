// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Event arguments for when audit log integrity violations are detected.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuditIntegrityViolationEventArgs" /> class.
/// </remarks>
/// <param name="violationType"> The type of integrity violation detected. </param>
/// <param name="affectedRecords"> The number of audit records affected by the violation. </param>
/// <param name="detectedAt"> The timestamp when the violation was detected. </param>
public sealed class AuditIntegrityViolationEventArgs(string violationType, int affectedRecords, DateTimeOffset detectedAt) : EventArgs
{
	/// <summary>
	/// Gets the type of integrity violation detected.
	/// </summary>
	/// <value> The category or classification of the integrity violation (e.g., "hash_mismatch", "timestamp_anomaly"). </value>
	public string ViolationType { get; } = violationType ?? throw new ArgumentNullException(nameof(violationType));

	/// <summary>
	/// Gets the number of audit records affected by the violation.
	/// </summary>
	/// <value> The count of audit log entries that were impacted by the integrity violation. </value>
	public int AffectedRecords { get; } = affectedRecords;

	/// <summary>
	/// Gets the timestamp when the violation was detected.
	/// </summary>
	/// <value> The UTC timestamp when the audit integrity violation was identified by the monitoring system. </value>
	public DateTimeOffset DetectedAt { get; } = detectedAt;

	/// <summary>
	/// Gets the description of the integrity violation.
	/// </summary>
	/// <value> A detailed description of the integrity violation, or null if no description is provided. </value>
	public string? Description { get; init; }

	/// <summary>
	/// Gets the severity level of the integrity violation.
	/// </summary>
	/// <value> The severity classification of the violation (e.g., "Critical", "High", "Medium"), or null if not specified. </value>
	public string? Severity { get; init; }

	/// <summary>
	/// Gets the audit log range affected by the violation.
	/// </summary>
	/// <value> A string representation of the audit log time range or record IDs affected, or null if not specified. </value>
	public string? AffectedLogRange { get; init; }

	/// <summary>
	/// Gets additional details about the integrity violation.
	/// </summary>
	/// <value>
	/// A dictionary containing additional contextual information about the violation, or null if no additional details are available.
	/// </value>
	public IReadOnlyDictionary<string, object>? Details { get; init; }
}
