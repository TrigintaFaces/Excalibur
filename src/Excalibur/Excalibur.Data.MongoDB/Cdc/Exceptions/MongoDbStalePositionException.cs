// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Exception thrown when the MongoDB CDC processor detects a stale resume token position
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
public sealed class MongoDbStalePositionException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbStalePositionException"/> class.
	/// </summary>
	public MongoDbStalePositionException()
		: base("The MongoDB CDC processor detected a stale resume token position that cannot be recovered.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbStalePositionException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public MongoDbStalePositionException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbStalePositionException"/> class
	/// with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public MongoDbStalePositionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbStalePositionException"/> class
	/// with the specified event arguments.
	/// </summary>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public MongoDbStalePositionException(CdcPositionResetEventArgs eventArgs)
		: base(FormatMessage(eventArgs), eventArgs?.OriginalException)
	{
		EventArgs = eventArgs;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbStalePositionException"/> class
	/// with a custom message and event arguments.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public MongoDbStalePositionException(string message, CdcPositionResetEventArgs eventArgs)
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
	/// Gets the stale resume token position that was detected (as bytes).
	/// </summary>
	/// <value>The stale position bytes, or <see langword="null"/> if not available.</value>
	public byte[]? StalePosition => EventArgs?.StalePosition;

	/// <summary>
	/// Gets the name of the affected database.
	/// </summary>
	/// <value>The database name, or <see langword="null"/> if not available.</value>
	public string? DatabaseName => EventArgs?.DatabaseName;

	/// <summary>
	/// Gets the name of the affected collection.
	/// </summary>
	/// <value>The collection name, or <see langword="null"/> if not available.</value>
	public string? CollectionName => EventArgs?.AdditionalContext?.TryGetValue("CollectionName", out var value) == true
		? value as string
		: null;

	private static string FormatMessage(CdcPositionResetEventArgs? eventArgs)
	{
		if (eventArgs == null)
		{
			return "The MongoDB CDC processor detected a stale resume token position that cannot be recovered.";
		}

		var stalePositionStr = eventArgs.StalePosition is not null
			? $"0x{Convert.ToHexString(eventArgs.StalePosition)}"
			: "unknown";

		var collectionName = eventArgs.AdditionalContext?.TryGetValue("CollectionName", out var value) == true
			? value as string
			: null;

		var namespaceInfo = !string.IsNullOrEmpty(eventArgs.DatabaseName) && !string.IsNullOrEmpty(collectionName)
			? $" for namespace '{eventArgs.DatabaseName}.{collectionName}'"
			: !string.IsNullOrEmpty(eventArgs.DatabaseName)
				? $" for database '{eventArgs.DatabaseName}'"
				: string.Empty;

		return $"MongoDB CDC processor '{eventArgs.ProcessorId}' detected stale position{namespaceInfo}. " +
			   $"Reason: {eventArgs.ReasonCode}. Position: {stalePositionStr}. " +
			   $"Detected at: {eventArgs.DetectedAt:O}";
	}
}
