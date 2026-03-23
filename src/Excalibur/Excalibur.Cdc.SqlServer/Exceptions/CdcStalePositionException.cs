// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Exception thrown when the SQL Server CDC processor encounters a stale LSN position that cannot be recovered.
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
/// Inherits from <see cref="Excalibur.Cdc.CdcStalePositionException"/> so consumers can catch all CDC stale position
/// errors generically.
/// </para>
/// </remarks>
public sealed class SqlServerCdcStalePositionException : Excalibur.Cdc.CdcStalePositionException
{
	private const string DefaultMessage = "The saved CDC position (LSN) is no longer valid in the database.";

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcStalePositionException"/> class.
	/// </summary>
	public SqlServerCdcStalePositionException()
		: base(DefaultMessage)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcStalePositionException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public SqlServerCdcStalePositionException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcStalePositionException"/> class with a specified error message
	/// and a reference to the inner exception that is the cause of this exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public SqlServerCdcStalePositionException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcStalePositionException"/> class with stale position event details.
	/// </summary>
	/// <param name="eventArgs">The event arguments containing details about the stale position scenario.</param>
	/// <param name="message">The optional custom error message.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public SqlServerCdcStalePositionException(
		CdcPositionResetEventArgs eventArgs,
		string? message = null,
		Exception? innerException = null)
		: base(message ?? CreateMessage(eventArgs), innerException)
	{
		ArgumentNullException.ThrowIfNull(eventArgs);
		EventArgs = eventArgs;
	}

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
