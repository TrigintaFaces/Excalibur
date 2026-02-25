// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Exception thrown when the Postgres CDC processor detects a stale WAL position
/// that cannot be recovered automatically.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown in the following scenarios:
/// <list type="bullet">
/// <item><description>The recovery strategy is <see cref="StalePositionRecoveryStrategy.Throw"/></description></item>
/// <item><description>The maximum recovery attempts have been exhausted</description></item>
/// <item><description>The callback strategy throws or fails to provide a valid new position</description></item>
/// </list>
/// </para>
/// <para>
/// The <see cref="EventArgs"/> property contains detailed information about the stale position,
/// including the reason code, affected positions, and original exception.
/// </para>
/// </remarks>
public sealed class PostgresStalePositionException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresStalePositionException"/> class.
	/// </summary>
	public PostgresStalePositionException()
		: base("The Postgres CDC processor detected a stale WAL position that cannot be recovered.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresStalePositionException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public PostgresStalePositionException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresStalePositionException"/> class
	/// with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public PostgresStalePositionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresStalePositionException"/> class
	/// with the specified event arguments.
	/// </summary>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public PostgresStalePositionException(CdcPositionResetEventArgs eventArgs)
		: base(FormatMessage(eventArgs), eventArgs?.OriginalException)
	{
		EventArgs = eventArgs;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresStalePositionException"/> class
	/// with a custom message and event arguments.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public PostgresStalePositionException(string message, CdcPositionResetEventArgs eventArgs)
		: base(message, eventArgs?.OriginalException)
	{
		EventArgs = eventArgs;
	}

	/// <summary>
	/// Gets the event arguments containing details about the stale position scenario.
	/// </summary>
	/// <value>
	/// The <see cref="CdcPositionResetEventArgs"/> with detailed information,
	/// or <see langword="null"/> if not provided.
	/// </value>
	public CdcPositionResetEventArgs? EventArgs { get; }

	/// <summary>
	/// Gets the processor ID that detected the stale position.
	/// </summary>
	/// <value>The processor ID, or <see langword="null"/> if not available.</value>
	public string? ProcessorId => EventArgs?.ProcessorId;

	/// <summary>
	/// Gets the reason code for the stale position.
	/// </summary>
	/// <value>The reason code, or <see langword="null"/> if not available.</value>
	public string? ReasonCode => EventArgs?.ReasonCode;

	/// <summary>
	/// Gets the stale WAL position that was detected.
	/// </summary>
	/// <value>The stale position, or <see langword="null"/> if not available.</value>
	public PostgresCdcPosition? StalePosition => EventArgs?.StalePosition != null && EventArgs.StalePosition.Length >= 8
		? new PostgresCdcPosition(BitConverter.ToUInt64(EventArgs.StalePosition, 0))
		: null;

	/// <summary>
	/// Gets the name of the affected replication slot.
	/// </summary>
	/// <value>The replication slot name, or <see langword="null"/> if not available.</value>
	public string? ReplicationSlotName => EventArgs?.AdditionalContext?.TryGetValue("ReplicationSlotName", out var slot) == true
		? slot as string
		: EventArgs?.CaptureInstance;

	private static string FormatMessage(CdcPositionResetEventArgs? eventArgs)
	{
		if (eventArgs == null)
		{
			return "The Postgres CDC processor detected a stale WAL position that cannot be recovered.";
		}

		var stalePositionStr = eventArgs.StalePosition != null && eventArgs.StalePosition.Length >= 8
			? new PostgresCdcPosition(BitConverter.ToUInt64(eventArgs.StalePosition, 0)).ToString()
			: "unknown";

		var slotName = eventArgs.AdditionalContext?.TryGetValue("ReplicationSlotName", out var slot) == true
			? slot as string
			: eventArgs.CaptureInstance;

		var slotInfo = !string.IsNullOrEmpty(slotName)
			? $" for slot '{slotName}'"
			: string.Empty;

		return $"Postgres CDC processor '{eventArgs.ProcessorId}' detected stale position{slotInfo}. " +
			   $"Reason: {eventArgs.ReasonCode}. Position: {stalePositionStr}. " +
			   $"Detected at: {eventArgs.DetectedAt:O}";
	}
}
