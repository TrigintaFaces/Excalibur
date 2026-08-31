// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Represents the result of an audit log integrity verification.
/// </summary>
/// <remarks>
/// <para>
/// Audit logs are hash-chained for tamper detection. Regular integrity verification should be performed as
/// part of ongoing compliance monitoring.
/// </para>
/// <para>
/// Read <see cref="Outcome"/> to interpret the result. It carries three states, because verification has
/// three: the chain was checked and held, the chain was checked and did not hold, or there was nothing in
/// the window to check. The third case establishes nothing and must not be reported as a pass. A
/// verification that examined no events is not evidence that the audit log is intact.
/// </para>
/// <para>
/// Instances are produced only by <see cref="Verified"/>, <see cref="ViolationsDetected"/>, and
/// <see cref="NoEventsInScope"/>. Those three cover every representable result, and each rejects the
/// arguments that would describe an impossible one, notably a claim of successful verification over zero
/// events, which no caller can construct.
/// </para>
/// </remarks>
public sealed record AuditIntegrityResult
{
	// Construction is closed. The three factories are the only way to obtain an instance, so every
	// representable result has been through the guard that rejects an impossible one -- most importantly a
	// claim of successful verification over zero events, which is what this type exists to prevent.
	private AuditIntegrityResult()
	{
	}

	/// <summary>
	/// Gets what the verification established.
	/// </summary>
	/// <value>
	/// <see cref="AuditIntegrityOutcome.Verified"/> when every event in the window was examined and the
	/// chain held; <see cref="AuditIntegrityOutcome.ViolationsDetected"/> when at least one event failed;
	/// <see cref="AuditIntegrityOutcome.NoEventsInScope"/> when the window contained no events at all.
	/// </value>
	public AuditIntegrityOutcome Outcome { get; private init; }

	/// <summary>
	/// Gets the total number of events examined.
	/// </summary>
	/// <value>
	/// The number of audit events read and checked. Always zero when <see cref="Outcome"/> is
	/// <see cref="AuditIntegrityOutcome.NoEventsInScope"/>, and always greater than zero otherwise.
	/// </value>
	public long EventsVerified { get; private init; }

	/// <summary>
	/// Gets the start of the verification period.
	/// </summary>
	/// <value>The inclusive start of the window that was examined.</value>
	public DateTimeOffset StartDate { get; private init; }

	/// <summary>
	/// Gets the end of the verification period.
	/// </summary>
	/// <value>The inclusive end of the window that was examined.</value>
	public DateTimeOffset EndDate { get; private init; }

	/// <summary>
	/// Gets the timestamp when verification was performed.
	/// </summary>
	/// <value>The instant the verification ran.</value>
	public DateTimeOffset VerifiedAt { get; private init; }

	/// <summary>
	/// Gets the first event ID with an integrity violation.
	/// </summary>
	/// <value>
	/// The identifier of the earliest failing event, or <see langword="null"/> unless <see cref="Outcome"/>
	/// is <see cref="AuditIntegrityOutcome.ViolationsDetected"/>.
	/// </value>
	public string? FirstViolationEventId { get; private init; }

	/// <summary>
	/// Gets a description of the integrity violation.
	/// </summary>
	/// <value>
	/// A description of the earliest failure, or <see langword="null"/> unless <see cref="Outcome"/> is
	/// <see cref="AuditIntegrityOutcome.ViolationsDetected"/>.
	/// </value>
	public string? ViolationDescription { get; private init; }

	/// <summary>
	/// Gets the number of the store's chaining units that failed verification.
	/// </summary>
	/// <value>
	/// The count of compromised chains, or zero unless <see cref="Outcome"/> is
	/// <see cref="AuditIntegrityOutcome.ViolationsDetected"/>.
	/// </value>
	/// <remarks>
	/// A chaining unit is the grouping the store's write path chains over, and each compromised unit counts
	/// once however many of its records are affected: once a chain breaks, the records after the break are
	/// unverifiable rather than independently sound, so counting them would report the consequences of a
	/// single break as separate findings. When <see cref="IsHashChained"/> is <see langword="false"/> the
	/// store chains nothing and each record is its own unit, so this is a count of records that failed
	/// content verification. Read it together with <see cref="IsHashChained"/>, which says which of the two
	/// it is.
	/// </remarks>
	public int CompromisedChainCount { get; private init; }

	/// <summary>
	/// Gets a value indicating whether the store's write path hash-chained the records that were verified.
	/// </summary>
	/// <value>
	/// <see langword="true"/> when the records were chained, so deletion, insertion and reordering were
	/// tested; otherwise <see langword="false"/>, meaning only each record's own content integrity was
	/// tested and deletion, insertion and reordering were not.
	/// </value>
	/// <remarks>
	/// This is what makes <see cref="CompromisedChainCount"/> readable. Without it the same field carries
	/// two different units depending on a store setting the reader cannot see.
	/// </remarks>
	public bool IsHashChained { get; private init; }

	/// <summary>
	/// Creates a result recording that every event in the window was examined and the hash chain held.
	/// </summary>
	/// <param name="eventsVerified"> The number of events examined. Must be greater than zero. </param>
	/// <param name="startDate"> The start of the verification period. </param>
	/// <param name="endDate"> The end of the verification period. </param>
	/// <param name="isHashChained"> Whether the store's write path chained the records that were verified. </param>
	/// <returns> A result whose <see cref="Outcome"/> is <see cref="AuditIntegrityOutcome.Verified"/>. </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="eventsVerified"/> is less than one. Verification that examined no events
	/// establishes nothing and cannot be reported as a successful verification; use
	/// <see cref="NoEventsInScope"/> for an empty window.
	/// </exception>
	public static AuditIntegrityResult Verified(
		long eventsVerified,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		bool isHashChained)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(eventsVerified, 1L);

		return Create(
			AuditIntegrityOutcome.Verified,
			eventsVerified,
			startDate,
			endDate,
			firstViolationEventId: null,
			violationDescription: null,
			compromisedChainCount: 0,
			isHashChained);
	}

	/// <summary>
	/// Creates a result recording that the window contained no audit events, so integrity was not exercised.
	/// </summary>
	/// <param name="startDate"> The start of the verification period. </param>
	/// <param name="endDate"> The end of the verification period. </param>
	/// <returns>
	/// A result whose <see cref="Outcome"/> is <see cref="AuditIntegrityOutcome.NoEventsInScope"/> and whose
	/// <see cref="EventsVerified"/> is zero.
	/// </returns>
	/// <remarks>
	/// This is not a pass. Callers producing compliance evidence must report it as an unexamined window
	/// rather than as a successful verification.
	/// </remarks>
	public static AuditIntegrityResult NoEventsInScope(
		DateTimeOffset startDate,
		DateTimeOffset endDate) =>
		Create(
			AuditIntegrityOutcome.NoEventsInScope,
			eventsVerified: 0,
			startDate,
			endDate,
			firstViolationEventId: null,
			violationDescription: null,
			compromisedChainCount: 0,
			isHashChained: false);

	/// <summary>
	/// Creates a result recording that at least one event in the window failed verification.
	/// </summary>
	/// <param name="eventsVerified"> The number of events examined. Must be greater than zero. </param>
	/// <param name="startDate"> The start of the verification period. </param>
	/// <param name="endDate"> The end of the verification period. </param>
	/// <param name="firstViolationEventId"> The identifier of the earliest failing event. </param>
	/// <param name="violationDescription"> A description of the earliest failure. </param>
	/// <param name="compromisedChainCount">
	/// The number of the store's chaining units that failed. Must be greater than zero.
	/// </param>
	/// <param name="isHashChained"> Whether the store's write path chained the records that were verified. </param>
	/// <returns> A result whose <see cref="Outcome"/> is <see cref="AuditIntegrityOutcome.ViolationsDetected"/>. </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="eventsVerified"/> or <paramref name="compromisedChainCount"/> is less than one.
	/// A violation cannot be detected in an event that was never examined.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="firstViolationEventId"/> or <paramref name="violationDescription"/> is
	/// null, empty, or whitespace. A reported violation must identify and describe itself.
	/// </exception>
	public static AuditIntegrityResult ViolationsDetected(
		long eventsVerified,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		string firstViolationEventId,
		string violationDescription,
		int compromisedChainCount,
		bool isHashChained)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(eventsVerified, 1L);
		ArgumentException.ThrowIfNullOrWhiteSpace(firstViolationEventId);
		ArgumentException.ThrowIfNullOrWhiteSpace(violationDescription);
		ArgumentOutOfRangeException.ThrowIfLessThan(compromisedChainCount, 1);

		return Create(
			AuditIntegrityOutcome.ViolationsDetected,
			eventsVerified,
			startDate,
			endDate,
			firstViolationEventId,
			violationDescription,
			compromisedChainCount,
			isHashChained);
	}

	private static AuditIntegrityResult Create(
		AuditIntegrityOutcome outcome,
		long eventsVerified,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		string? firstViolationEventId,
		string? violationDescription,
		int compromisedChainCount,
		bool isHashChained) =>
		new()
		{
			Outcome = outcome,
			EventsVerified = eventsVerified,
			StartDate = startDate,
			EndDate = endDate,
			VerifiedAt = DateTimeOffset.UtcNow,
			FirstViolationEventId = firstViolationEventId,
			ViolationDescription = violationDescription,
			CompromisedChainCount = compromisedChainCount,
			IsHashChained = isHashChained
		};
}
