// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Cdc;
using Excalibur.Data.CosmosDb.Resources;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Exception thrown when the CosmosDB CDC processor detects a stale continuation token position
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
public sealed class CosmosDbStalePositionException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbStalePositionException"/> class.
	/// </summary>
	public CosmosDbStalePositionException()
		: base(ErrorMessages.StaleContinuationTokenDetected)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbStalePositionException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public CosmosDbStalePositionException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbStalePositionException"/> class
	/// with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public CosmosDbStalePositionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbStalePositionException"/> class
	/// with the specified event arguments.
	/// </summary>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public CosmosDbStalePositionException(CdcPositionResetEventArgs eventArgs)
		: base(FormatMessage(eventArgs), eventArgs?.OriginalException)
	{
		EventArgs = eventArgs;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbStalePositionException"/> class
	/// with a custom message and event arguments.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public CosmosDbStalePositionException(string message, CdcPositionResetEventArgs eventArgs)
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
	/// Gets the stale continuation token position that was detected (as raw bytes).
	/// </summary>
	/// <value>The stale position bytes, or <see langword="null"/> if not available.</value>
	public byte[]? StalePosition => EventArgs?.StalePosition;

	/// <summary>
	/// Gets the HTTP status code that indicated the stale position.
	/// </summary>
	/// <value>The HTTP status code, or <see langword="null"/> if not available.</value>
	public int? HttpStatusCode => EventArgs?.AdditionalContext?.TryGetValue("HttpStatusCode", out var code) == true ? (int?)code : null;

	/// <summary>
	/// Gets the name of the affected database.
	/// </summary>
	/// <value>The database name, or <see langword="null"/> if not available.</value>
	public string? DatabaseName => EventArgs?.DatabaseName;

	/// <summary>
	/// Gets the name of the affected container.
	/// </summary>
	/// <value>The container name, or <see langword="null"/> if not available.</value>
	public string? ContainerName => EventArgs?.AdditionalContext?.TryGetValue("ContainerName", out var name) == true ? name as string : null;

	/// <summary>
	/// Gets the partition key range ID that was affected.
	/// </summary>
	/// <value>The partition key range ID, or <see langword="null"/> if not available.</value>
	public string? PartitionKeyRangeId => EventArgs?.AdditionalContext?.TryGetValue("PartitionKeyRangeId", out var id) == true ? id as string : null;

	private static string FormatMessage(CdcPositionResetEventArgs? eventArgs)
	{
		if (eventArgs == null)
		{
			return ErrorMessages.StaleContinuationTokenDetected;
		}

		var stalePositionStr = eventArgs.StalePosition != null
			? $"0x{Convert.ToHexString(eventArgs.StalePosition)}"
			: "unknown";

		// Extract container name from additional context or capture instance
		var containerName = eventArgs.AdditionalContext?.TryGetValue("ContainerName", out var name) == true
			? name as string
			: null;

		var namespaceInfo = !string.IsNullOrEmpty(eventArgs.DatabaseName) && !string.IsNullOrEmpty(containerName)
			? $" for container '{eventArgs.DatabaseName}/{containerName}'"
			: !string.IsNullOrEmpty(eventArgs.DatabaseName)
				? $" for database '{eventArgs.DatabaseName}'"
				: !string.IsNullOrEmpty(eventArgs.CaptureInstance)
					? $" for capture instance '{eventArgs.CaptureInstance}'"
					: string.Empty;

		var httpStatusCode = eventArgs.AdditionalContext?.TryGetValue("HttpStatusCode", out var code) == true
			? (int?)code
			: null;
		var httpStatusInfo = httpStatusCode.HasValue
			? $" HTTP {httpStatusCode.Value}."
			: string.Empty;

		return $"CosmosDB CDC processor '{eventArgs.ProcessorId}' detected stale position{namespaceInfo}. " +
			   $"Reason: {eventArgs.ReasonCode}.{httpStatusInfo} Position: {stalePositionStr}. " +
			   $"Detected at: {eventArgs.DetectedAt:O}";
	}
}
