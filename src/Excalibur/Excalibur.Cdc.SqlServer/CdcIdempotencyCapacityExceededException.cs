// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Transient exception thrown by an in-memory CDC idempotency filter when its tracking store is at
/// capacity and a not-yet-seen event cannot be evaluated without silently degrading deduplication.
/// </summary>
/// <remarks>
/// This is a <b>fail-closed</b> signal raised <i>before</i> the event handler runs: when the filter is
/// saturated, a previously-unseen CDC event cannot be reliably deduplicated, so the filter throws rather
/// than admit an un-trackable event (which would risk processing the same change twice on redelivery).
/// The batch is not acknowledged and the event is redelivered once capacity is reclaimed. Treat it as
/// transient and retry-able. Initializes a new instance of the
/// <see cref="CdcIdempotencyCapacityExceededException"/> class.
/// </remarks>
/// <param name="message">The exception message.</param>
public sealed class CdcIdempotencyCapacityExceededException(string message) : Exception(message)
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CdcIdempotencyCapacityExceededException"/> class.
	/// </summary>
	public CdcIdempotencyCapacityExceededException()
		: this("The CDC idempotency filter is at capacity.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcIdempotencyCapacityExceededException"/> class for
	/// the given capacity.
	/// </summary>
	/// <param name="maxTrackedEvents">The configured maximum number of tracked events that was reached.</param>
	public CdcIdempotencyCapacityExceededException(int maxTrackedEvents)
		: this($"The CDC idempotency filter is at capacity ({maxTrackedEvents} events); the operation failed closed and the event should be redelivered.") =>
		MaxTrackedEvents = maxTrackedEvents;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcIdempotencyCapacityExceededException"/> class with a
	/// message and inner exception.
	/// </summary>
	/// <param name="message">The exception message.</param>
	/// <param name="innerException">The inner exception.</param>
	/// <remarks>
	/// The <paramref name="innerException"/> parameter cannot be forwarded to the base
	/// <see cref="Exception(string, System.Exception)"/> constructor because the primary constructor locks
	/// the base call to <c>Exception(message)</c>. This overload exists to satisfy the standard exception
	/// constructor pattern (CA1032).
	/// </remarks>
#pragma warning disable IDE0060 // innerException cannot be forwarded — primary constructor limits base call to Exception(message)
	public CdcIdempotencyCapacityExceededException(string? message, Exception? innerException)
		: this(message ?? string.Empty)
	{
	}
#pragma warning restore IDE0060

	/// <summary>
	/// Gets the configured maximum number of tracked events that was reached, when known.
	/// </summary>
	/// <value>The configured capacity cap, or <see langword="null"/> when not specified.</value>
	public int? MaxTrackedEvents { get; }
}
