// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Exception thrown when the Postgres CDC processor detects a stale WAL position
/// that cannot be recovered automatically.
/// </summary>
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
	public CdcPositionResetEventArgs? EventArgs { get; }

	/// <summary>
	/// Gets the processor ID that detected the stale position.
	/// </summary>
	public string? ProcessorId => EventArgs?.ProcessorId;

	/// <summary>
	/// Gets the reason code for the stale position.
	/// </summary>
	public string? ReasonCode => EventArgs?.ReasonCode;

	/// <summary>
	/// Gets the stale WAL position that was detected.
	/// </summary>
	public PostgresCdcPosition? StalePosition => EventArgs?.StalePosition != null && EventArgs.StalePosition.Length >= 8
		? new PostgresCdcPosition(BitConverter.ToUInt64(EventArgs.StalePosition, 0))
		: null;

	/// <summary>
	/// Gets the name of the affected replication slot.
	/// </summary>
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
