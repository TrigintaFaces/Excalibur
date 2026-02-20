// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents the result of an audit log integrity verification.
/// </summary>
/// <remarks>
/// Audit logs are hash-chained for tamper detection. Regular integrity verification should be performed as part of SOC2 compliance.
/// </remarks>
public sealed record AuditIntegrityResult
{
	/// <summary>
	/// Gets a value indicating whether the audit log integrity is valid.
	/// </summary>
	public required bool IsValid { get; init; }

	/// <summary>
	/// Gets the total number of events verified.
	/// </summary>
	public required long EventsVerified { get; init; }

	/// <summary>
	/// Gets the start of the verification period.
	/// </summary>
	public required DateTimeOffset StartDate { get; init; }

	/// <summary>
	/// Gets the end of the verification period.
	/// </summary>
	public required DateTimeOffset EndDate { get; init; }

	/// <summary>
	/// Gets the timestamp when verification was performed.
	/// </summary>
	public required DateTimeOffset VerifiedAt { get; init; }

	/// <summary>
	/// Gets the first event ID with an integrity violation. Null if no violations found.
	/// </summary>
	public string? FirstViolationEventId { get; init; }

	/// <summary>
	/// Gets a description of the integrity violation if any.
	/// </summary>
	public string? ViolationDescription { get; init; }

	/// <summary>
	/// Gets the number of events with integrity violations.
	/// </summary>
	public int ViolationCount { get; init; }

	/// <summary>
	/// Creates a successful integrity result.
	/// </summary>
	public static AuditIntegrityResult Valid(
		long eventsVerified,
		DateTimeOffset startDate,
		DateTimeOffset endDate) =>
		new()
		{
			IsValid = true,
			EventsVerified = eventsVerified,
			StartDate = startDate,
			EndDate = endDate,
			VerifiedAt = DateTimeOffset.UtcNow
		};

	/// <summary>
	/// Creates a failed integrity result.
	/// </summary>
	public static AuditIntegrityResult Invalid(
		long eventsVerified,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		string firstViolationEventId,
		string violationDescription,
		int violationCount = 1) =>
		new()
		{
			IsValid = false,
			EventsVerified = eventsVerified,
			StartDate = startDate,
			EndDate = endDate,
			VerifiedAt = DateTimeOffset.UtcNow,
			FirstViolationEventId = firstViolationEventId,
			ViolationDescription = violationDescription,
			ViolationCount = violationCount
		};
}
