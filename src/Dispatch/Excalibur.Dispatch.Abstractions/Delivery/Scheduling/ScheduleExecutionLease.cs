// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Bearer authority identifying one admitted occurrence on one logical store.</summary>
/// <remarks>Providers validate the entire tuple against current stored ownership; constructing a token does not grant authority.</remarks>
public sealed record ScheduleExecutionLease
{
	/// <summary>Initializes a provider-issued ownership tuple.</summary>
	/// <param name="storeId">Logical store identifier.</param>
	/// <param name="scheduleId">Schedule identifier.</param>
	/// <param name="generation">Administrative generation.</param>
	/// <param name="occurrenceUtc">Original admitted occurrence.</param>
	/// <param name="ownerToken">Unique ownership token.</param>
	public ScheduleExecutionLease(Guid storeId, Guid scheduleId, Guid generation, DateTimeOffset occurrenceUtc, Guid ownerToken)
	{
		if (storeId == Guid.Empty)
		{
			throw new ArgumentException("The identifier must be nonempty.", nameof(storeId));
		}
		if (scheduleId == Guid.Empty)
		{
			throw new ArgumentException("The identifier must be nonempty.", nameof(scheduleId));
		}
		if (generation == Guid.Empty)
		{
			throw new ArgumentException("The identifier must be nonempty.", nameof(generation));
		}
		if (ownerToken == Guid.Empty)
		{
			throw new ArgumentException("The identifier must be nonempty.", nameof(ownerToken));
		}
		StoreId = storeId;
		ScheduleId = scheduleId;
		Generation = generation;
		OccurrenceUtc = occurrenceUtc;
		OwnerToken = ownerToken;
	}
	/// <summary>Gets the logical store identifier.</summary>
	public Guid StoreId { get; }
	/// <summary>Gets the schedule identifier.</summary>
	public Guid ScheduleId { get; }
	/// <summary>Gets the administrative generation.</summary>
	public Guid Generation { get; }
	/// <summary>Gets the original admitted occurrence.</summary>
	public DateTimeOffset OccurrenceUtc { get; }
	/// <summary>Gets the unique ownership token.</summary>
	public Guid OwnerToken { get; }

	/// <summary>Gets the one-based count of admissions of this occurrence since its last committed settlement.</summary>
	/// <value>1 on the first admission of an occurrence, incrementing on each subsequent admission of the same one.</value>
	/// <remarks>
	/// <para>
	/// <b>This is history, not authority, and it is deliberately excluded from the ownership tuple.</b>
	/// Providers validate the other five members against current stored ownership; this one is reported
	/// alongside them so a caller can tell a first attempt from a repeated one, and changing it neither
	/// grants nor revokes anything.
	/// </para>
	/// <para>
	/// It counts admissions, not handler invocations, and it resets only when a settlement is COMMITTED —
	/// never when a lease is released or lapses. That is what makes it bound the repeat: an occurrence whose
	/// settlement is refused is admitted again by the next poll, legitimately, because the fence makes a late
	/// write safe and a successor must be able to take over. Without a durable count the repeat is unbounded,
	/// and a count reset by release would never reach a bound at all.
	/// </para>
	/// <para>
	/// Required rather than defaulted on purpose: a provider that omits it would otherwise report a first
	/// attempt forever, which no caller could distinguish from a healthy schedule.
	/// </para>
	/// </remarks>
	public required int AdmissionAttempt { get; init; }
}
