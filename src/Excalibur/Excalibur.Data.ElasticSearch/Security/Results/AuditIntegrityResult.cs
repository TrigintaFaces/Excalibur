// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the result of an audit log integrity validation operation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuditIntegrityResult" /> class.
/// </remarks>
/// <param name="validationId"> The unique identifier for this validation operation. </param>
/// <param name="isValid"> Whether the audit logs passed integrity validation. </param>
/// <param name="totalEventsValidated"> The total number of events validated. </param>
/// <param name="corruptedEvents"> The number of corrupted events found. </param>
public sealed class AuditIntegrityResult(Guid validationId, bool isValid, long totalEventsValidated, long corruptedEvents)
{
	/// <summary>
	/// Gets the unique identifier for this validation operation.
	/// </summary>
	/// <value>
	/// The unique identifier for this validation operation.
	/// </value>
	public Guid ValidationId { get; } = validationId;

	/// <summary>
	/// Gets a value indicating whether the audit logs passed integrity validation.
	/// </summary>
	/// <value>
	/// A value indicating whether the audit logs passed integrity validation.
	/// </value>
	public bool IsValid { get; } = isValid;

	/// <summary>
	/// Gets the total number of events validated.
	/// </summary>
	/// <value>
	/// The total number of events validated.
	/// </value>
	public long TotalEventsValidated { get; } = totalEventsValidated;

	/// <summary>
	/// Gets the number of corrupted events found.
	/// </summary>
	/// <value>
	/// The number of corrupted events found.
	/// </value>
	public long CorruptedEvents { get; } = corruptedEvents;

	/// <summary>
	/// Gets the timestamp when this validation was performed.
	/// </summary>
	/// <value>
	/// The timestamp when this validation was performed.
	/// </value>
	public DateTimeOffset ValidatedAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the validation execution time in milliseconds.
	/// </summary>
	/// <value>
	/// The validation execution time in milliseconds.
	/// </value>
	public long ExecutionTimeMs { get; init; }

	/// <summary>
	/// Gets the detailed validation message.
	/// </summary>
	/// <value>
	/// The detailed validation message.
	/// </value>
	public string? Message { get; init; }

	/// <summary>
	/// Gets the list of corrupted event IDs.
	/// </summary>
	/// <value>
	/// The list of corrupted event IDs.
	/// </value>
	public IReadOnlyList<string> CorruptedEventIds { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Gets additional validation details.
	/// </summary>
	/// <value>
	/// Additional validation details.
	/// </value>
	public IReadOnlyDictionary<string, object> Details { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets any errors encountered during validation.
	/// </summary>
	/// <value>
	/// Any errors encountered during validation.
	/// </value>
	public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
