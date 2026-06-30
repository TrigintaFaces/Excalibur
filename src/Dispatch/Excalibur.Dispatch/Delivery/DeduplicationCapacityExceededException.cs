// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Transient exception thrown by an in-memory deduplicator when its tracking store is at capacity and a
/// record-producing operation cannot be honored without silently skipping deduplication.
/// </summary>
/// <remarks>
/// This is a <b>fail-closed</b> signal: rather than admit an un-trackable message (which would risk
/// dropping a real message or missing a later duplicate), the deduplicator throws so the message is not
/// acknowledged and is redelivered once capacity is reclaimed by expiry or cleanup. Treat it as transient
/// and retry-able. Initializes a new instance of the <see cref="DeduplicationCapacityExceededException"/> class.
/// </remarks>
/// <param name="message">The exception message.</param>
public sealed class DeduplicationCapacityExceededException(string message) : Exception(message)
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeduplicationCapacityExceededException"/> class.
	/// </summary>
	public DeduplicationCapacityExceededException() : this("The deduplication tracking store is at capacity.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DeduplicationCapacityExceededException"/> class for the
	/// given capacity.
	/// </summary>
	/// <param name="maxTrackedEntries">The configured maximum number of tracked entries that was reached.</param>
	public DeduplicationCapacityExceededException(int maxTrackedEntries)
		: this($"The deduplication tracking store is at capacity ({maxTrackedEntries} entries); the operation failed closed and the message should be redelivered.") =>
		MaxTrackedEntries = maxTrackedEntries;

	/// <summary>
	/// Initializes a new instance of the <see cref="DeduplicationCapacityExceededException"/> class with a
	/// message and inner exception.
	/// </summary>
	/// <param name="message">The exception message.</param>
	/// <param name="innerException">The inner exception.</param>
	/// <remarks>
	/// The <paramref name="innerException"/> parameter cannot be forwarded to the base
	/// <see cref="Exception(string, System.Exception)"/> constructor because the primary constructor locks the
	/// base call to <c>Exception(message)</c>. This overload exists to satisfy the standard exception
	/// constructor pattern (CA1032).
	/// </remarks>
#pragma warning disable IDE0060 // innerException cannot be forwarded — primary constructor limits base call to Exception(message)
	public DeduplicationCapacityExceededException(string? message, Exception? innerException) : this(message ?? string.Empty)
	{
	}
#pragma warning restore IDE0060

	/// <summary>
	/// Gets the configured maximum number of tracked entries that was reached, when known.
	/// </summary>
	/// <value>The configured capacity cap, or <see langword="null"/> when not specified.</value>
	public int? MaxTrackedEntries { get; }
}
