// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net;
using System.Text;

using Excalibur.Cdc;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Detects and classifies stale CDC position errors from CosmosDB exceptions.
/// </summary>
/// <remarks>
/// <para>
/// CosmosDB reports stale position scenarios through specific HTTP status codes:
/// <list type="bullet">
/// <item><description>410 (Gone) - Continuation token expired beyond 7-day retention</description></item>
/// <item><description>404 (NotFound) - Partition or container not found</description></item>
/// <item><description>412 (PreconditionFailed) - ETag mismatch due to concurrent modification</description></item>
/// </list>
/// </para>
/// </remarks>
public static class CosmosDbStalePositionDetector
{
	/// <summary>
	/// HTTP status code for Gone (continuation token expired).
	/// </summary>
	public const int HttpGone = 410;

	/// <summary>
	/// HTTP status code for NotFound (partition/container not found).
	/// </summary>
	public const int HttpNotFound = 404;

	/// <summary>
	/// HTTP status code for PreconditionFailed (ETag mismatch).
	/// </summary>
	public const int HttpPreconditionFailed = 412;

	/// <summary>
	/// Gets the set of HTTP status codes that indicate a stale CDC position.
	/// </summary>
	public static IReadOnlySet<int> StalePositionStatusCodes { get; } = new HashSet<int>
	{
		HttpGone,
		HttpNotFound,
		HttpPreconditionFailed
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
			CosmosException cosmosEx => IsStalePositionStatusCode((int)cosmosEx.StatusCode),
			AggregateException aggEx => aggEx.InnerExceptions.Any(IsStalePositionException),
			_ => IsStalePositionByMessage(exception.Message) ||
				 IsStalePositionException(exception.InnerException)
		};
	}

	/// <summary>
	/// Extracts the HTTP status code that indicates a stale position from the exception.
	/// </summary>
	/// <param name="exception">The exception to analyze.</param>
	/// <returns>
	/// The HTTP status code if found; otherwise, <see langword="null"/>.
	/// </returns>
	public static int? GetStalePositionStatusCode(Exception? exception)
	{
		if (exception == null)
		{
			return null;
		}

		return exception switch
		{
			CosmosException cosmosEx when IsStalePositionStatusCode((int)cosmosEx.StatusCode) =>
				(int)cosmosEx.StatusCode,
			AggregateException aggEx => aggEx.InnerExceptions
				.Select(GetStalePositionStatusCode)
				.FirstOrDefault(c => c.HasValue),
			_ => GetStalePositionStatusCode(exception.InnerException)
		};
	}

	/// <summary>
	/// Creates a <see cref="CdcPositionResetEventArgs"/> from an exception and context.
	/// </summary>
	/// <param name="exception">The exception that was caught.</param>
	/// <param name="processorId">The identifier of the CDC processor.</param>
	/// <param name="stalePosition">The continuation token position that was detected as stale.</param>
	/// <param name="newPosition">The new position to resume from, if known.</param>
	/// <param name="databaseName">The affected database name, if known.</param>
	/// <param name="containerName">The affected container name, if known.</param>
	/// <param name="partitionKeyRangeId">The affected partition key range ID, if known.</param>
	/// <returns>A populated <see cref="CdcPositionResetEventArgs"/> instance.</returns>
	public static CdcPositionResetEventArgs CreateEventArgs(
		Exception exception,
		string processorId,
		CosmosDbCdcPosition? stalePosition = null,
		CosmosDbCdcPosition? newPosition = null,
		string? databaseName = null,
		string? containerName = null,
		string? partitionKeyRangeId = null)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		var statusCode = GetStalePositionStatusCode(exception);
		var reasonCode = statusCode.HasValue
			? CosmosDbStalePositionReasonCodes.FromStatusCode(statusCode.Value)
			: CosmosDbStalePositionReasonCodes.FromErrorMessage(exception.Message);

		// Build the capture instance from database/container info
		var captureInstance = !string.IsNullOrEmpty(databaseName) && !string.IsNullOrEmpty(containerName)
			? $"{databaseName}/{containerName}"
			: databaseName ?? containerName ?? string.Empty;

		// Build additional context with CosmosDB-specific fields
		var additionalContext = new Dictionary<string, object>();
		if (statusCode.HasValue)
		{
			additionalContext["HttpStatusCode"] = statusCode.Value;
		}
		if (!string.IsNullOrEmpty(partitionKeyRangeId))
		{
			additionalContext["PartitionKeyRangeId"] = partitionKeyRangeId;
		}
		if (!string.IsNullOrEmpty(containerName))
		{
			additionalContext["ContainerName"] = containerName;
		}

		return new CdcPositionResetEventArgs
		{
			ProcessorId = processorId,
			ProviderType = "CosmosDB",
			ReasonCode = reasonCode,
			CaptureInstance = captureInstance,
			DatabaseName = databaseName ?? string.Empty,
			StalePosition = stalePosition != null ? Encoding.UTF8.GetBytes(stalePosition.ToBase64()) : null,
			NewPosition = newPosition != null ? Encoding.UTF8.GetBytes(newPosition.ToBase64()) : null,
			OriginalException = exception,
			DetectedAt = DateTimeOffset.UtcNow,
			AdditionalContext = additionalContext.Count > 0 ? additionalContext : null
		};
	}

	/// <summary>
	/// Determines the appropriate reason code from a CosmosDB exception.
	/// </summary>
	/// <param name="exception">The CosmosDB exception to analyze.</param>
	/// <returns>The reason code string.</returns>
	public static string GetReasonCode(CosmosException? exception)
	{
		if (exception == null)
		{
			return CosmosDbStalePositionReasonCodes.Unknown;
		}

		return CosmosDbStalePositionReasonCodes.FromStatusCode((int)exception.StatusCode);
	}

	/// <summary>
	/// Determines if an HTTP status code indicates a stale position scenario.
	/// </summary>
	/// <param name="statusCode">The HTTP status code.</param>
	/// <returns><see langword="true"/> if the status code indicates stale position.</returns>
	public static bool IsStalePositionStatusCode(int statusCode) =>
		StalePositionStatusCodes.Contains(statusCode);

	/// <summary>
	/// Determines if an HTTP status code indicates a stale position scenario.
	/// </summary>
	/// <param name="statusCode">The HTTP status code.</param>
	/// <returns><see langword="true"/> if the status code indicates stale position.</returns>
	public static bool IsStalePositionStatusCode(HttpStatusCode statusCode) =>
		IsStalePositionStatusCode((int)statusCode);

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

		return (message.Contains("CONTINUATION", StringComparison.OrdinalIgnoreCase) &&
			   (message.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("GONE", StringComparison.OrdinalIgnoreCase))) ||
			   (message.Contains("PARTITION", StringComparison.OrdinalIgnoreCase) &&
			   (message.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("SPLIT", StringComparison.OrdinalIgnoreCase))) ||
			   (message.Contains("CONTAINER", StringComparison.OrdinalIgnoreCase) &&
			   (message.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("DELETED", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("DOES NOT EXIST", StringComparison.OrdinalIgnoreCase))) ||
			   message.Contains("ETAG", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("PRECONDITION", StringComparison.OrdinalIgnoreCase);
	}
}
