// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Provides standardized reason codes for DynamoDB Streams stale position scenarios.
/// </summary>
/// <remarks>
/// <para>
/// These codes categorize why a DynamoDB Stream position became invalid.
/// DynamoDB Streams retain data for 24 hours. Common stale position scenarios include:
/// <list type="bullet">
/// <item><description>Iterator expiry (iterators expire after 15 minutes of inactivity)</description></item>
/// <item><description>Sequence number beyond the trim horizon (data older than 24 hours)</description></item>
/// <item><description>Shard closure due to splits or merges</description></item>
/// <item><description>Stream disabled or table deleted</description></item>
/// </list>
/// </para>
/// <para>
/// These codes are used in <see cref="Excalibur.Cdc.CdcPositionResetEventArgs.ReasonCode"/> to enable consistent
/// logging, alerting, and handling across different stale position scenarios.
/// </para>
/// </remarks>
public static class DynamoDbStalePositionReasonCodes
{
	/// <summary>
	/// The shard iterator has expired (15 minutes without use).
	/// </summary>
	/// <remarks>
	/// <para>
	/// DynamoDB shard iterators are valid for up to 15 minutes. If the iterator is not used
	/// within this window, it expires and a new iterator must be obtained.
	/// AWS Exception: <c>ExpiredIteratorException</c>.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Consumer paused or stopped for more than 15 minutes</description></item>
	/// <item><description>Network interruption causing iterator timeout</description></item>
	/// <item><description>Processing delay exceeding iterator lifetime</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string IteratorExpired = "DYNAMODB_ITERATOR_EXPIRED";

	/// <summary>
	/// The sequence number is beyond the stream's trim horizon (older than 24 hours).
	/// </summary>
	/// <remarks>
	/// <para>
	/// DynamoDB Streams retain records for 24 hours. If a consumer falls behind
	/// by more than 24 hours, the sequence number is no longer available.
	/// AWS Exception: <c>TrimmedDataAccessException</c>.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Consumer offline for more than 24 hours</description></item>
	/// <item><description>Processing lag exceeding retention window</description></item>
	/// <item><description>Saved checkpoint older than 24 hours</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string TrimmedData = "DYNAMODB_TRIMMED_DATA";

	/// <summary>
	/// The shard has been closed and is no longer available.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Shards can close when they split, merge, or when the stream is disabled.
	/// A closed shard should be read to completion, then replaced with child shards.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Shard split due to increased write throughput</description></item>
	/// <item><description>Shard merge during table capacity reduction</description></item>
	/// <item><description>Stream disabled on the table</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string ShardClosed = "DYNAMODB_SHARD_CLOSED";

	/// <summary>
	/// The shard was not found, possibly due to split or stream recreation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when attempting to access a shard that no longer exists.
	/// The shard may have split into child shards or been deleted.
	/// AWS Exception: <c>ResourceNotFoundException</c>.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Shard split created new child shards</description></item>
	/// <item><description>Stream was disabled and re-enabled</description></item>
	/// <item><description>Invalid or corrupted shard ID</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string ShardNotFound = "DYNAMODB_SHARD_NOT_FOUND";

	/// <summary>
	/// The stream was not found, possibly disabled or table deleted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the stream is no longer available for the table.
	/// AWS Exception: <c>ResourceNotFoundException</c>.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Stream disabled on the DynamoDB table</description></item>
	/// <item><description>Table deleted</description></item>
	/// <item><description>Stream ARN is invalid or from a different region</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string StreamNotFound = "DYNAMODB_STREAM_NOT_FOUND";

	/// <summary>
	/// The stream has been disabled on the table.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When a stream is disabled, it can no longer be read.
	/// Re-enabling streams creates a new stream with a new ARN.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Stream explicitly disabled via console or API</description></item>
	/// <item><description>Table configuration changed</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string StreamDisabled = "DYNAMODB_STREAM_DISABLED";

	/// <summary>
	/// The reason for the stale position could not be determined.
	/// </summary>
	/// <remarks>
	/// This is used when the specific cause cannot be identified from the AWS error
	/// or when an unexpected error occurs during position validation.
	/// </remarks>
	public const string Unknown = "DYNAMODB_UNKNOWN";

	/// <summary>
	/// Determines the reason code from an AWS exception type name.
	/// </summary>
	/// <param name="exceptionTypeName">The name of the AWS exception type.</param>
	/// <returns>The corresponding reason code.</returns>
	/// <remarks>
	/// <para>
	/// AWS DynamoDB Streams exception types relevant to stale positions:
	/// <list type="bullet">
	/// <item><description>ExpiredIteratorException - Iterator expired</description></item>
	/// <item><description>TrimmedDataAccessException - Data beyond trim horizon</description></item>
	/// <item><description>ResourceNotFoundException - Stream/shard not found</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static string FromExceptionType(string? exceptionTypeName)
	{
		if (string.IsNullOrWhiteSpace(exceptionTypeName))
		{
			return Unknown;
		}

		return exceptionTypeName switch
		{
			"ExpiredIteratorException" => IteratorExpired,
			"TrimmedDataAccessException" => TrimmedData,
			"ResourceNotFoundException" => ShardNotFound,
			_ when exceptionTypeName.Contains("Expired", StringComparison.OrdinalIgnoreCase) => IteratorExpired,
			_ when exceptionTypeName.Contains("Trimmed", StringComparison.OrdinalIgnoreCase) => TrimmedData,
			_ when exceptionTypeName.Contains("NotFound", StringComparison.OrdinalIgnoreCase) => ShardNotFound,
			_ => Unknown
		};
	}

	/// <summary>
	/// Determines the reason code from a DynamoDB Streams error message pattern.
	/// </summary>
	/// <param name="errorMessage">The DynamoDB Streams error message.</param>
	/// <returns>The corresponding reason code.</returns>
	/// <remarks>
	/// This method provides fallback detection when exception types are not available
	/// by analyzing common error message patterns.
	/// </remarks>
	public static string FromErrorMessage(string? errorMessage)
	{
		if (string.IsNullOrWhiteSpace(errorMessage))
		{
			return Unknown;
		}

		if (errorMessage.Contains("ITERATOR", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("INVALID", StringComparison.OrdinalIgnoreCase)))
		{
			return IteratorExpired;
		}

		if ((errorMessage.Contains("TRIM", StringComparison.OrdinalIgnoreCase) &&
			 errorMessage.Contains("HORIZON", StringComparison.OrdinalIgnoreCase)) ||
			errorMessage.Contains("TRIMMED", StringComparison.OrdinalIgnoreCase))
		{
			return TrimmedData;
		}

		if (errorMessage.Contains("SHARD", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("CLOSED", StringComparison.OrdinalIgnoreCase)))
		{
			return errorMessage.Contains("CLOSED", StringComparison.OrdinalIgnoreCase)
				? ShardClosed
				: ShardNotFound;
		}

		if (errorMessage.Contains("STREAM", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("DISABLED", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("DOES NOT EXIST", StringComparison.OrdinalIgnoreCase)))
		{
			return errorMessage.Contains("DISABLED", StringComparison.OrdinalIgnoreCase)
				? StreamDisabled
				: StreamNotFound;
		}

		if (errorMessage.Contains("SEQUENCE", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("OUT OF RANGE", StringComparison.OrdinalIgnoreCase)))
		{
			return TrimmedData;
		}

		return Unknown;
	}
}
