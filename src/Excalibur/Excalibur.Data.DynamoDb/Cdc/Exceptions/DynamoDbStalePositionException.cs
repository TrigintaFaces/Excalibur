// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Exception thrown when the DynamoDB Streams CDC processor detects a stale sequence number position
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
public sealed class DynamoDbStalePositionException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbStalePositionException"/> class.
	/// </summary>
	public DynamoDbStalePositionException()
		: base("The DynamoDB Streams CDC processor detected a stale sequence number position that cannot be recovered.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbStalePositionException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public DynamoDbStalePositionException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbStalePositionException"/> class
	/// with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public DynamoDbStalePositionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbStalePositionException"/> class
	/// with the specified event arguments.
	/// </summary>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public DynamoDbStalePositionException(CdcPositionResetEventArgs eventArgs)
		: base(FormatMessage(eventArgs), eventArgs?.OriginalException)
	{
		EventArgs = eventArgs;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbStalePositionException"/> class
	/// with a custom message and event arguments.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="eventArgs">The stale position event arguments.</param>
	public DynamoDbStalePositionException(string message, CdcPositionResetEventArgs eventArgs)
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
	/// Gets the stale sequence number position that was detected.
	/// </summary>
	/// <value>The stale position as bytes, or <see langword="null"/> if not available.</value>
	public byte[]? StalePosition => EventArgs?.StalePosition;

	/// <summary>
	/// Gets the Stream ARN that was affected (from AdditionalContext).
	/// </summary>
	/// <value>The Stream ARN, or <see langword="null"/> if not available.</value>
	public string? StreamArn => EventArgs?.AdditionalContext?.TryGetValue("StreamArn", out var arn) == true ? arn?.ToString() : null;

	/// <summary>
	/// Gets the table name that was affected (from CaptureInstance or AdditionalContext).
	/// </summary>
	/// <value>The table name, or <see langword="null"/> if not available.</value>
	public string? TableName => !string.IsNullOrEmpty(EventArgs?.CaptureInstance)
		? EventArgs.CaptureInstance
		: EventArgs?.AdditionalContext?.TryGetValue("TableName", out var table) == true ? table?.ToString() : null;

	/// <summary>
	/// Gets the shard ID that was affected (from AdditionalContext).
	/// </summary>
	/// <value>The shard ID, or <see langword="null"/> if not available.</value>
	public string? ShardId => EventArgs?.AdditionalContext?.TryGetValue("ShardId", out var shard) == true ? shard?.ToString() : null;

	/// <summary>
	/// Gets the sequence number that was stale (from AdditionalContext).
	/// </summary>
	/// <value>The sequence number, or <see langword="null"/> if not available.</value>
	public string? SequenceNumber => EventArgs?.AdditionalContext?.TryGetValue("SequenceNumber", out var seq) == true ? seq?.ToString() : null;

	private static string FormatMessage(CdcPositionResetEventArgs? eventArgs)
	{
		if (eventArgs == null)
		{
			return "The DynamoDB Streams CDC processor detected a stale sequence number position that cannot be recovered.";
		}

		var stalePositionStr = eventArgs.StalePosition != null
			? $"0x{Convert.ToHexString(eventArgs.StalePosition)}"
			: "unknown";
		var tableName = eventArgs.AdditionalContext?.TryGetValue("TableName", out var table) == true ? table?.ToString() : null;
		var streamArn = eventArgs.AdditionalContext?.TryGetValue("StreamArn", out var arn) == true ? arn?.ToString() : null;
		var shardId = eventArgs.AdditionalContext?.TryGetValue("ShardId", out var shard) == true ? shard?.ToString() : null;

		var streamInfo = !string.IsNullOrEmpty(tableName)
			? $" for table '{tableName}'"
			: !string.IsNullOrEmpty(streamArn)
				? $" for stream '{streamArn}'"
				: !string.IsNullOrEmpty(eventArgs.CaptureInstance)
					? $" for '{eventArgs.CaptureInstance}'"
					: string.Empty;
		var shardInfo = !string.IsNullOrEmpty(shardId)
			? $" Shard: {shardId}."
			: string.Empty;

		return $"DynamoDB CDC processor '{eventArgs.ProcessorId}' detected stale position{streamInfo}. " +
			   $"Reason: {eventArgs.ReasonCode}.{shardInfo} Position: {stalePositionStr}. " +
			   $"Detected at: {eventArgs.DetectedAt:O}";
	}
}
