// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

using Grpc.Core;

namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Detects and classifies stale CDC position errors from Firestore exceptions.
/// </summary>
/// <remarks>
/// <para>
/// Firestore reports stale position scenarios through gRPC status codes:
/// <list type="bullet">
/// <item><description>DEADLINE_EXCEEDED (4) - Listener stream timed out</description></item>
/// <item><description>NOT_FOUND (5) - Collection or document not found</description></item>
/// <item><description>PERMISSION_DENIED (7) - Access denied</description></item>
/// <item><description>RESOURCE_EXHAUSTED (8) - Quota exceeded</description></item>
/// <item><description>ABORTED (10) - Request aborted</description></item>
/// <item><description>UNAVAILABLE (14) - Service unavailable</description></item>
/// </list>
/// </para>
/// </remarks>
public static class FirestoreStalePositionDetector
{
	/// <summary>
	/// gRPC status code for CANCELLED.
	/// </summary>
	public const int GrpcCancelled = 1;

	/// <summary>
	/// gRPC status code for DEADLINE_EXCEEDED.
	/// </summary>
	public const int GrpcDeadlineExceeded = 4;

	/// <summary>
	/// gRPC status code for NOT_FOUND.
	/// </summary>
	public const int GrpcNotFound = 5;

	/// <summary>
	/// gRPC status code for PERMISSION_DENIED.
	/// </summary>
	public const int GrpcPermissionDenied = 7;

	/// <summary>
	/// gRPC status code for RESOURCE_EXHAUSTED.
	/// </summary>
	public const int GrpcResourceExhausted = 8;

	/// <summary>
	/// gRPC status code for ABORTED.
	/// </summary>
	public const int GrpcAborted = 10;

	/// <summary>
	/// gRPC status code for INTERNAL.
	/// </summary>
	public const int GrpcInternal = 13;

	/// <summary>
	/// gRPC status code for UNAVAILABLE.
	/// </summary>
	public const int GrpcUnavailable = 14;

	/// <summary>
	/// Gets the set of gRPC status codes that indicate a stale CDC position.
	/// </summary>
	public static IReadOnlySet<int> StalePositionStatusCodes { get; } = new HashSet<int>
	{
		GrpcCancelled,
		GrpcDeadlineExceeded,
		GrpcNotFound,
		GrpcPermissionDenied,
		GrpcResourceExhausted,
		GrpcAborted,
		GrpcInternal,
		GrpcUnavailable
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
			RpcException rpcEx => IsStalePositionStatusCode((int)rpcEx.StatusCode),
			AggregateException aggEx => aggEx.InnerExceptions.Any(IsStalePositionException),
			_ => IsStalePositionByMessage(exception.Message) ||
				 IsStalePositionException(exception.InnerException)
		};
	}

	/// <summary>
	/// Extracts the gRPC status code that indicates a stale position from the exception.
	/// </summary>
	/// <param name="exception">The exception to analyze.</param>
	/// <returns>
	/// The gRPC status code if found; otherwise, <see langword="null"/>.
	/// </returns>
	public static int? GetStalePositionStatusCode(Exception? exception)
	{
		if (exception == null)
		{
			return null;
		}

		return exception switch
		{
			RpcException rpcEx when IsStalePositionStatusCode((int)rpcEx.StatusCode) =>
				(int)rpcEx.StatusCode,
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
	/// <param name="stalePosition">The position that was detected as stale.</param>
	/// <param name="newPosition">The new position to resume from, if known.</param>
	/// <param name="projectId">The Firestore project ID, if known.</param>
	/// <param name="collectionPath">The affected collection path, if known.</param>
	/// <param name="documentId">The affected document ID, if known.</param>
	/// <returns>A populated <see cref="CdcPositionResetEventArgs"/> instance.</returns>
	public static CdcPositionResetEventArgs CreateEventArgs(
		Exception exception,
		string processorId,
		FirestoreCdcPosition? stalePosition = null,
		FirestoreCdcPosition? newPosition = null,
		string? projectId = null,
		string? collectionPath = null,
		string? documentId = null)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		var statusCode = GetStalePositionStatusCode(exception);
		var reasonCode = statusCode.HasValue
			? FirestoreStalePositionReasonCodes.FromGrpcStatusCode(statusCode.Value)
			: FirestoreStalePositionReasonCodes.FromErrorMessage(exception.Message);

		// Build additional context with Firestore-specific information
		var additionalContext = new Dictionary<string, object>();
		if (statusCode.HasValue)
		{
			additionalContext["GrpcStatusCode"] = statusCode.Value;
		}
		if (!string.IsNullOrEmpty(projectId))
		{
			additionalContext["ProjectId"] = projectId;
		}
		if (!string.IsNullOrEmpty(collectionPath))
		{
			additionalContext["CollectionPath"] = collectionPath;
		}
		if (!string.IsNullOrEmpty(documentId))
		{
			additionalContext["DocumentId"] = documentId;
		}

		return new CdcPositionResetEventArgs
		{
			ProcessorId = processorId,
			ProviderType = "Firestore",
			CaptureInstance = collectionPath ?? string.Empty,
			DatabaseName = projectId ?? string.Empty,
			ReasonCode = reasonCode,
			ReasonMessage = $"Firestore CDC position reset: {reasonCode}",
			StalePosition = stalePosition?.ToBytes(),
			NewPosition = newPosition?.ToBytes(),
			OriginalException = exception,
			DetectedAt = DateTimeOffset.UtcNow,
			AdditionalContext = additionalContext.Count > 0 ? additionalContext : null
		};
	}

	/// <summary>
	/// Determines the appropriate reason code from an RPC exception.
	/// </summary>
	/// <param name="exception">The RPC exception to analyze.</param>
	/// <returns>The reason code string.</returns>
	public static string GetReasonCode(RpcException? exception)
	{
		if (exception == null)
		{
			return FirestoreStalePositionReasonCodes.Unknown;
		}

		return FirestoreStalePositionReasonCodes.FromGrpcStatusCode((int)exception.StatusCode);
	}

	/// <summary>
	/// Determines if a gRPC status code indicates a stale position scenario.
	/// </summary>
	/// <param name="statusCode">The gRPC status code.</param>
	/// <returns><see langword="true"/> if the status code indicates stale position.</returns>
	public static bool IsStalePositionStatusCode(int statusCode) =>
		StalePositionStatusCodes.Contains(statusCode);

	/// <summary>
	/// Determines if a gRPC status code indicates a stale position scenario.
	/// </summary>
	/// <param name="statusCode">The gRPC status code.</param>
	/// <returns><see langword="true"/> if the status code indicates stale position.</returns>
	public static bool IsStalePositionStatusCode(StatusCode statusCode) =>
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

		return message.Contains("DEADLINE", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
			   ((message.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
				 message.Contains("NOTFOUND", StringComparison.OrdinalIgnoreCase)) &&
				(message.Contains("COLLECTION", StringComparison.OrdinalIgnoreCase) ||
				 message.Contains("DOCUMENT", StringComparison.OrdinalIgnoreCase))) ||
			   message.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("DENIED", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("CANCELLED", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("CANCELED", StringComparison.OrdinalIgnoreCase);
	}
}
