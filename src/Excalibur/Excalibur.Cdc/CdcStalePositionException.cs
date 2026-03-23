// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;

namespace Excalibur.Cdc;

/// <summary>
/// Base exception for all CDC stale position scenarios across providers.
/// </summary>
/// <remarks>
/// <para>
/// Consumers can catch this base type to handle stale position errors generically,
/// regardless of the underlying CDC provider (SqlServer, Postgres, CosmosDB, DynamoDB,
/// Firestore, MongoDB). Provider-specific subclasses contain additional provider-specific
/// recovery information.
/// </para>
/// <para>
/// A stale position occurs when the CDC processor's saved checkpoint (LSN, resume token,
/// sequence number, etc.) is no longer valid in the source database. This typically happens
/// due to log truncation, backup/restore, or stream expiration.
/// </para>
/// </remarks>
public class CdcStalePositionException : ResourceException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStalePositionException"/> class.
	/// </summary>
	public CdcStalePositionException()
		: base("The CDC processor detected a stale position that cannot be recovered.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStalePositionException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public CdcStalePositionException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStalePositionException"/> class
	/// with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public CdcStalePositionException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStalePositionException"/> class
	/// with the specified event arguments.
	/// </summary>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public CdcStalePositionException(CdcPositionResetEventArgs eventArgs)
		: base(FormatMessage(eventArgs), eventArgs?.OriginalException)
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
	public CdcPositionResetEventArgs? EventArgs { get; protected set; }

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
	/// Gets the stale position that was detected.
	/// </summary>
	/// <value>The stale position as bytes, or <see langword="null"/> if not available.</value>
	public byte[]? StalePosition => EventArgs?.StalePosition;

	private static string FormatMessage(CdcPositionResetEventArgs? eventArgs)
	{
		if (eventArgs is null)
		{
			return "The CDC processor detected a stale position that cannot be recovered.";
		}

		var stalePositionStr = eventArgs.StalePosition != null
			? $"0x{Convert.ToHexString(eventArgs.StalePosition)}"
			: "unknown";

		return $"CDC processor '{eventArgs.ProcessorId}' detected stale position. " +
			   $"Reason: {eventArgs.ReasonCode}. Position: {stalePositionStr}. " +
			   $"Detected at: {eventArgs.DetectedAt:O}";
	}
}
