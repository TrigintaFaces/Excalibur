// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>The durable outcome of one idempotent event append.</summary>
/// <param name="AggregateId">The original aggregate identifier.</param>
/// <param name="AggregateType">The original aggregate type.</param>
/// <param name="NextExpectedVersion">The committed, zero-based stream version.</param>
/// <param name="FirstEventPosition">The first committed global position.</param>
/// <param name="LastEventPosition">The last committed global position, usable as a read session token.</param>
public sealed record SqlServerOperationReceipt(string AggregateId, string AggregateType,
	long NextExpectedVersion, long FirstEventPosition, long LastEventPosition);

/// <summary>The outcome of an operation; a no-event operation has no receipt.</summary>
/// <param name="Replayed">Whether a prior committed receipt prevented another append.</param>
/// <param name="Receipt">The durable append outcome, if any.</param>
public sealed record SqlServerOperationResult(bool Replayed, SqlServerOperationReceipt? Receipt);

/// <summary>An operation identity was reused with different immutable input.</summary>
public sealed class SqlServerOperationConflictException : InvalidOperationException
{
	/// <summary>Initializes a conflict exception with the default explanation.</summary>
	public SqlServerOperationConflictException() : base("The idempotency key was already committed with different input.") { }

	/// <summary>Initializes a conflict exception with a caller-supplied explanation.</summary>
	/// <param name="message">The explanation. Supply one when you can name the operation or the input that differed.</param>
	public SqlServerOperationConflictException(string message) : base(message) { }

	/// <summary>Initializes a conflict exception that preserves the failure beneath it.</summary>
	/// <param name="message">The explanation.</param>
	/// <param name="innerException">The underlying failure, kept so a consumer wrapping this does not lose it.</param>
	public SqlServerOperationConflictException(string message, Exception innerException) : base(message, innerException) { }
}

