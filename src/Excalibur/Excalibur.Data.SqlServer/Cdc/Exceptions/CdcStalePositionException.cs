// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Cdc;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Exception thrown when the CDC processor encounters a stale position that cannot be recovered.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown when:
/// <list type="bullet">
/// <item><description><see cref="StalePositionRecoveryStrategy.Throw"/> is configured</description></item>
/// <item><description>Recovery attempts are exhausted for any strategy</description></item>
/// <item><description>The callback handler throws an exception during <see cref="StalePositionRecoveryStrategy.InvokeCallback"/></description></item>
/// </list>
/// </para>
/// <para>
/// The exception contains detailed information about the stale position scenario via <see cref="EventArgs"/>.
/// </para>
/// </remarks>
[Serializable]
public sealed class CdcStalePositionException : ApiException
{
	private const int DefaultStatusCode = 500;
	private const string DefaultMessage = "The saved CDC position (LSN) is no longer valid in the database.";

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStalePositionException"/> class.
	/// </summary>
	public CdcStalePositionException()
		: base(DefaultStatusCode, DefaultMessage, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStalePositionException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public CdcStalePositionException(string message)
		: base(DefaultStatusCode, message, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStalePositionException"/> class with a specified error message
	/// and a reference to the inner exception that is the cause of this exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public CdcStalePositionException(string message, Exception? innerException)
		: base(DefaultStatusCode, message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStalePositionException"/> class with stale position event details.
	/// </summary>
	/// <param name="eventArgs">The event arguments containing details about the stale position scenario.</param>
	/// <param name="message">The optional custom error message.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public CdcStalePositionException(
		CdcPositionResetEventArgs eventArgs,
		string? message = null,
		Exception? innerException = null)
		: base(DefaultStatusCode, message ?? CreateMessage(eventArgs), innerException)
	{
		ArgumentNullException.ThrowIfNull(eventArgs);
		EventArgs = eventArgs;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStalePositionException"/> class with status code, message, and inner exception.
	/// </summary>
	/// <param name="statusCode">The HTTP status code associated with the exception.</param>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public CdcStalePositionException(int statusCode, string? message, Exception? innerException)
		: base(statusCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets the stale position event arguments containing detailed information about the scenario.
	/// </summary>
	/// <value>
	/// The event arguments, or <see langword="null"/> if not provided during construction.
	/// </value>
	public CdcPositionResetEventArgs? EventArgs { get; }

	/// <summary>
	/// Gets the stale LSN that was detected as invalid.
	/// </summary>
	/// <value>
	/// The stale LSN as a byte array, or <see langword="null"/> if not available.
	/// </value>
	public byte[]? StalePosition => EventArgs?.StalePosition;

	/// <summary>
	/// Gets the reason code explaining why the position became stale.
	/// </summary>
	/// <value>
	/// The reason code (e.g., "CDC_CLEANUP", "BACKUP_RESTORE"), or <see langword="null"/> if not available.
	/// </value>
	public string? ReasonCode => EventArgs?.ReasonCode;

	/// <summary>
	/// Gets the capture instance (table) affected by the stale position.
	/// </summary>
	/// <value>
	/// The capture instance name, or <see langword="null"/> if not available.
	/// </value>
	public string? CaptureInstance => EventArgs?.CaptureInstance;

	private static string CreateMessage(CdcPositionResetEventArgs eventArgs)
	{
		var stalePositionHex = eventArgs.StalePosition != null
			? $"0x{Convert.ToHexString(eventArgs.StalePosition)}"
			: "unknown";

		return $"The saved CDC position ({stalePositionHex}) is no longer valid. " +
			   $"Reason: {eventArgs.ReasonCode}. " +
			   $"Capture instance: {eventArgs.CaptureInstance ?? "all"}. " +
			   $"Processor: {eventArgs.ProcessorId}.";
	}
}
