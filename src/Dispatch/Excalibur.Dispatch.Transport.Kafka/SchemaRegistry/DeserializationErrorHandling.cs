// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Specifies how deserialization errors should be handled during message consumption.
/// </summary>
/// <remarks>
/// <para>
/// This enum allows configuration of error handling strategy at the consumer level,
/// enabling different behaviors based on application requirements.
/// </para>
/// </remarks>
public enum DeserializationErrorHandling
{
	/// <summary>
	/// Skip the message and continue consuming.
	/// </summary>
	/// <remarks>
	/// Use this for fire-and-forget scenarios where message loss is acceptable.
	/// The error is logged as a warning but processing continues.
	/// </remarks>
	Skip = 0,

	/// <summary>
	/// Send the message to a dead letter queue and continue consuming.
	/// </summary>
	/// <remarks>
	/// This is the safest default for production systems. Failed messages are
	/// preserved for later inspection and reprocessing while allowing the
	/// consumer to continue processing other messages.
	/// </remarks>
	DeadLetter = 1,

	/// <summary>
	/// Throw an exception and stop consumption.
	/// </summary>
	/// <remarks>
	/// Use this for strict consistency requirements where message loss or
	/// reordering cannot be tolerated. The consumer will stop and require
	/// manual intervention.
	/// </remarks>
	Throw = 2
}
