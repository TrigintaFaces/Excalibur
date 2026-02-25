// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Excalibur.Cdc;

namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Detects and classifies stale CDC position errors from DynamoDB Streams exceptions.
/// </summary>
/// <remarks>
/// <para>
/// DynamoDB Streams reports stale position scenarios through specific exception types:
/// <list type="bullet">
/// <item><description>ExpiredIteratorException - Iterator expired (15-minute timeout)</description></item>
/// <item><description>TrimmedDataAccessException - Data beyond 24-hour trim horizon</description></item>
/// <item><description>ResourceNotFoundException - Stream/shard not found</description></item>
/// </list>
/// </para>
/// </remarks>
public static class DynamoDbStalePositionDetector
{
	/// <summary>
	/// Gets the set of AWS exception type names that indicate a stale CDC position.
	/// </summary>
	public static IReadOnlySet<string> StalePositionExceptionTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
	{
		"ExpiredIteratorException",
		"TrimmedDataAccessException",
		"ResourceNotFoundException"
	};

	/// <summary>
	/// Determines whether the specified exception indicates a stale CDC position.
	/// </summary>
	/// <param name="exception">The exception to analyze.</param>
	/// <returns>
	/// <see langword="true"/> if the exception indicates a stale position; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsStalePositionException(Exception? exception)
	{
		if (exception == null)
		{
			return false;
		}

		return exception switch
		{
			ExpiredIteratorException => true,
			TrimmedDataAccessException => true,
			ResourceNotFoundException => true,
			AmazonDynamoDBException dynamoEx => IsStalePositionByMessage(dynamoEx.Message),
			AggregateException aggEx => aggEx.InnerExceptions.Any(IsStalePositionException),
			_ => IsStalePositionByMessage(exception.Message) ||
				 IsStalePositionException(exception.InnerException)
		};
	}

	/// <summary>
	/// Extracts the AWS exception type name that indicates a stale position from the exception.
	/// </summary>
	/// <param name="exception">The exception to analyze.</param>
	/// <returns>
	/// The exception type name if found; otherwise, <see langword="null"/>.
	/// </returns>
	public static string? GetStalePositionExceptionType(Exception? exception)
	{
		if (exception == null)
		{
			return null;
		}

		return exception switch
		{
			ExpiredIteratorException => "ExpiredIteratorException",
			TrimmedDataAccessException => "TrimmedDataAccessException",
			ResourceNotFoundException => "ResourceNotFoundException",
			AggregateException aggEx => aggEx.InnerExceptions
				.Select(GetStalePositionExceptionType)
				.FirstOrDefault(t => t != null),
			_ => GetStalePositionExceptionType(exception.InnerException)
		};
	}

	/// <summary>
	/// Creates a <see cref="CdcPositionResetEventArgs"/> from an exception and context.
	/// </summary>
	/// <param name="exception">The exception that was caught.</param>
	/// <param name="processorId">The identifier of the CDC processor.</param>
	/// <param name="stalePosition">The sequence number position that was detected as stale.</param>
	/// <param name="newPosition">The new position to resume from, if known.</param>
	/// <param name="streamArn">The Stream ARN, if known.</param>
	/// <param name="tableName">The table name, if known.</param>
	/// <param name="shardId">The affected shard ID, if known.</param>
	/// <param name="sequenceNumber">The stale sequence number, if known.</param>
	/// <returns>A populated <see cref="CdcPositionResetEventArgs"/> instance.</returns>
	public static CdcPositionResetEventArgs CreateEventArgs(
		Exception exception,
		string processorId,
		DynamoDbCdcPosition? stalePosition = null,
		DynamoDbCdcPosition? newPosition = null,
		string? streamArn = null,
		string? tableName = null,
		string? shardId = null,
		string? sequenceNumber = null)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		var exceptionType = GetStalePositionExceptionType(exception);
		var reasonCode = exceptionType != null
			? DynamoDbStalePositionReasonCodes.FromExceptionType(exceptionType)
			: DynamoDbStalePositionReasonCodes.FromErrorMessage(exception.Message);

		var additionalContext = new Dictionary<string, object>();
		if (exceptionType != null)
		{
			additionalContext["ExceptionType"] = exceptionType;
		}
		if (streamArn != null)
		{
			additionalContext["StreamArn"] = streamArn;
		}
		if (tableName != null)
		{
			additionalContext["TableName"] = tableName;
		}
		if (shardId != null)
		{
			additionalContext["ShardId"] = shardId;
		}
		if (sequenceNumber != null)
		{
			additionalContext["SequenceNumber"] = sequenceNumber;
		}

		return new CdcPositionResetEventArgs
		{
			ProcessorId = processorId,
			ProviderType = "DynamoDB",
			CaptureInstance = tableName ?? streamArn ?? string.Empty,
			DatabaseName = string.Empty,
			ReasonCode = reasonCode,
			ReasonMessage = exception.Message,
			StalePosition = stalePosition?.ToBytes(),
			NewPosition = newPosition?.ToBytes(),
			EarliestAvailablePosition = null,
			LatestAvailablePosition = null,
			OriginalException = exception,
			DetectedAt = DateTimeOffset.UtcNow,
			AdditionalContext = additionalContext.Count > 0 ? additionalContext : null
		};
	}

	/// <summary>
	/// Determines the appropriate reason code from a DynamoDB exception.
	/// </summary>
	/// <param name="exception">The DynamoDB exception to analyze.</param>
	/// <returns>The reason code string.</returns>
	public static string GetReasonCode(Exception? exception)
	{
		if (exception == null)
		{
			return DynamoDbStalePositionReasonCodes.Unknown;
		}

		var exceptionType = GetStalePositionExceptionType(exception);
		return exceptionType != null
			? DynamoDbStalePositionReasonCodes.FromExceptionType(exceptionType)
			: DynamoDbStalePositionReasonCodes.FromErrorMessage(exception.Message);
	}

	/// <summary>
	/// Determines if an error message indicates a stale position scenario.
	/// </summary>
	/// <param name="message">The error message to analyze.</param>
	/// <returns><see langword="true"/> if the message indicates stale position.</returns>
	private static bool IsStalePositionByMessage(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return false;
		}

		return (message.Contains("ITERATOR", StringComparison.OrdinalIgnoreCase) &&
				(message.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase) ||
				 message.Contains("INVALID", StringComparison.OrdinalIgnoreCase))) ||
			   (message.Contains("TRIM", StringComparison.OrdinalIgnoreCase) &&
				message.Contains("HORIZON", StringComparison.OrdinalIgnoreCase)) ||
			   message.Contains("TRIMMED", StringComparison.OrdinalIgnoreCase) ||
			   (message.Contains("SHARD", StringComparison.OrdinalIgnoreCase) &&
				(message.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
				 message.Contains("CLOSED", StringComparison.OrdinalIgnoreCase))) ||
			   (message.Contains("STREAM", StringComparison.OrdinalIgnoreCase) &&
				(message.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
				 message.Contains("DISABLED", StringComparison.OrdinalIgnoreCase)));
	}
}
