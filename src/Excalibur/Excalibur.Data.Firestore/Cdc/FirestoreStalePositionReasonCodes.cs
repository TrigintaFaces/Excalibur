// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Provides standardized reason codes for Firestore CDC stale position scenarios.
/// </summary>
/// <remarks>
/// <para>
/// These codes categorize why a Firestore CDC position became invalid.
/// Firestore uses gRPC status codes for error reporting. Common stale position scenarios include:
/// <list type="bullet">
/// <item><description>Listener stream timeout (gRPC DEADLINE_EXCEEDED)</description></item>
/// <item><description>Collection or document not found (gRPC NOT_FOUND)</description></item>
/// <item><description>Permission denied (gRPC PERMISSION_DENIED)</description></item>
/// <item><description>Database deleted or unavailable (gRPC UNAVAILABLE)</description></item>
/// </list>
/// </para>
/// <para>
/// These codes are used in <c>CdcPositionResetEventArgs.ReasonCode</c> to enable consistent
/// logging, alerting, and handling across different stale position scenarios.
/// </para>
/// </remarks>
public static class FirestoreStalePositionReasonCodes
{
	/// <summary>
	/// The listener stream timed out due to deadline exceeded.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Firestore listener connections have timeout limits. If a listener is idle too long
	/// or the server takes too long to respond, the stream may time out.
	/// gRPC status code: DEADLINE_EXCEEDED (4).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Network latency or congestion</description></item>
	/// <item><description>Server overload</description></item>
	/// <item><description>Long-running queries timing out</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string DeadlineExceeded = "FIRESTORE_DEADLINE_EXCEEDED";

	/// <summary>
	/// The collection or document was not found.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the watched collection or document no longer exists.
	/// gRPC status code: NOT_FOUND (5).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Collection deleted</description></item>
	/// <item><description>Document deleted</description></item>
	/// <item><description>Invalid collection path</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string NotFound = "FIRESTORE_NOT_FOUND";

	/// <summary>
	/// Permission was denied to access the collection or document.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when security rules prevent access to the watched collection.
	/// gRPC status code: PERMISSION_DENIED (7).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Security rules changed</description></item>
	/// <item><description>Service account permissions revoked</description></item>
	/// <item><description>Token expired or invalidated</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string PermissionDenied = "FIRESTORE_PERMISSION_DENIED";

	/// <summary>
	/// The service was unavailable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the Firestore service is temporarily unavailable.
	/// gRPC status code: UNAVAILABLE (14).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Firestore service outage</description></item>
	/// <item><description>Network connectivity issues</description></item>
	/// <item><description>Regional availability issues</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string Unavailable = "FIRESTORE_UNAVAILABLE";

	/// <summary>
	/// The request was cancelled.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the listener request is cancelled by the client or server.
	/// gRPC status code: CANCELLED (1).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Client initiated cancellation</description></item>
	/// <item><description>Server-side cancellation</description></item>
	/// <item><description>Resource cleanup during shutdown</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string Cancelled = "FIRESTORE_CANCELLED";

	/// <summary>
	/// Resource was exhausted (quota exceeded).
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when Firestore quota limits are exceeded.
	/// gRPC status code: RESOURCE_EXHAUSTED (8).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Too many concurrent listeners</description></item>
	/// <item><description>Rate limit exceeded</description></item>
	/// <item><description>Project quota exhausted</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string ResourceExhausted = "FIRESTORE_RESOURCE_EXHAUSTED";

	/// <summary>
	/// The request was aborted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when a request is aborted, typically due to a conflict.
	/// gRPC status code: ABORTED (10).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Conflict with another operation</description></item>
	/// <item><description>Transaction aborted</description></item>
	/// <item><description>Optimistic locking failure</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string Aborted = "FIRESTORE_ABORTED";

	/// <summary>
	/// Internal server error occurred.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This indicates an internal Firestore error.
	/// gRPC status code: INTERNAL (13).
	/// </para>
	/// </remarks>
	public const string Internal = "FIRESTORE_INTERNAL";

	/// <summary>
	/// The reason for the stale position could not be determined.
	/// </summary>
	/// <remarks>
	/// This is used when the specific cause cannot be identified from the Firestore error
	/// or when an unexpected error occurs during position validation.
	/// </remarks>
	public const string Unknown = "FIRESTORE_UNKNOWN";

	/// <summary>
	/// Determines the reason code from a gRPC status code.
	/// </summary>
	/// <param name="statusCode">The gRPC status code.</param>
	/// <returns>The corresponding reason code.</returns>
	/// <remarks>
	/// <para>
	/// gRPC status codes relevant to stale positions:
	/// <list type="bullet">
	/// <item><description>1 - CANCELLED</description></item>
	/// <item><description>4 - DEADLINE_EXCEEDED</description></item>
	/// <item><description>5 - NOT_FOUND</description></item>
	/// <item><description>7 - PERMISSION_DENIED</description></item>
	/// <item><description>8 - RESOURCE_EXHAUSTED</description></item>
	/// <item><description>10 - ABORTED</description></item>
	/// <item><description>13 - INTERNAL</description></item>
	/// <item><description>14 - UNAVAILABLE</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static string FromGrpcStatusCode(int statusCode) =>
		statusCode switch
		{
			1 => Cancelled,
			4 => DeadlineExceeded,
			5 => NotFound,
			7 => PermissionDenied,
			8 => ResourceExhausted,
			10 => Aborted,
			13 => Internal,
			14 => Unavailable,
			_ => Unknown
		};

	/// <summary>
	/// Determines the reason code from a Firestore error message pattern.
	/// </summary>
	/// <param name="errorMessage">The Firestore error message.</param>
	/// <returns>The corresponding reason code.</returns>
	/// <remarks>
	/// This method provides fallback detection when gRPC status codes are not available
	/// by analyzing common error message patterns.
	/// </remarks>
	public static string FromErrorMessage(string? errorMessage)
	{
		if (string.IsNullOrWhiteSpace(errorMessage))
		{
			return Unknown;
		}

		if (errorMessage.Contains("DEADLINE", StringComparison.OrdinalIgnoreCase) ||
			(errorMessage.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase) &&
			 !errorMessage.Contains("CONNECTION", StringComparison.OrdinalIgnoreCase)))
		{
			return DeadlineExceeded;
		}

		if ((errorMessage.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("NOTFOUND", StringComparison.OrdinalIgnoreCase)) &&
			(errorMessage.Contains("COLLECTION", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("DOCUMENT", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("PATH", StringComparison.OrdinalIgnoreCase)))
		{
			return NotFound;
		}

		if (errorMessage.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("DENIED", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase))
		{
			return PermissionDenied;
		}

		if (errorMessage.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase) ||
			(errorMessage.Contains("SERVICE", StringComparison.OrdinalIgnoreCase) &&
			 errorMessage.Contains("DOWN", StringComparison.OrdinalIgnoreCase)))
		{
			return Unavailable;
		}

		if (errorMessage.Contains("CANCELLED", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("CANCELED", StringComparison.OrdinalIgnoreCase))
		{
			return Cancelled;
		}

		if (errorMessage.Contains("QUOTA", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("RATE LIMIT", StringComparison.OrdinalIgnoreCase))
		{
			return ResourceExhausted;
		}

		if (errorMessage.Contains("ABORTED", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
		{
			return Aborted;
		}

		if (errorMessage.Contains("INTERNAL", StringComparison.OrdinalIgnoreCase))
		{
			return Internal;
		}

		return Unknown;
	}
}
