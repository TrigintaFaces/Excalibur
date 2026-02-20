// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides GDPR Article 15 subject access request (SAR) capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This service manages the lifecycle of subject access requests, including creation,
/// status tracking, and fulfillment. SARs support access, rectification, and erasure
/// request types per GDPR Articles 15-17.
/// </para>
/// </remarks>
public interface ISubjectAccessService
{
	/// <summary>
	/// Creates a new subject access request.
	/// </summary>
	/// <param name="request">The subject access request details.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result containing the request tracking identifier.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
	Task<SubjectAccessResult> CreateRequestAsync(
		SubjectAccessRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current status of a subject access request.
	/// </summary>
	/// <param name="requestId">The request tracking identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The request result, or null if not found.</returns>
	Task<SubjectAccessResult?> GetRequestStatusAsync(
		string requestId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Fulfills a subject access request by collecting and preparing the requested data.
	/// </summary>
	/// <param name="requestId">The request tracking identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The fulfillment result.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the request has already been fulfilled or cannot be processed.</exception>
	Task<SubjectAccessResult> FulfillRequestAsync(
		string requestId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents a subject access request under GDPR.
/// </summary>
public sealed record SubjectAccessRequest
{
	/// <summary>
	/// Gets the data subject identifier.
	/// </summary>
	public required string SubjectId { get; init; }

	/// <summary>
	/// Gets the type of request.
	/// </summary>
	public required SubjectAccessRequestType RequestType { get; init; }

	/// <summary>
	/// Gets the timestamp when the request was made.
	/// </summary>
	public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Types of subject access requests under GDPR.
/// </summary>
public enum SubjectAccessRequestType
{
	/// <summary>
	/// GDPR Article 15: Right of access to personal data.
	/// </summary>
	Access = 0,

	/// <summary>
	/// GDPR Article 16: Right to rectification of inaccurate personal data.
	/// </summary>
	Rectification = 1,

	/// <summary>
	/// GDPR Article 17: Right to erasure (right to be forgotten).
	/// </summary>
	Erasure = 2
}

/// <summary>
/// Result of a subject access request operation.
/// </summary>
public sealed record SubjectAccessResult
{
	/// <summary>
	/// Gets the unique tracking identifier for this request.
	/// </summary>
	public required string RequestId { get; init; }

	/// <summary>
	/// Gets the current status of the request.
	/// </summary>
	public required SubjectAccessRequestStatus Status { get; init; }

	/// <summary>
	/// Gets the deadline by which the request must be fulfilled.
	/// </summary>
	public DateTimeOffset? Deadline { get; init; }

	/// <summary>
	/// Gets the timestamp when the request was fulfilled, if applicable.
	/// </summary>
	public DateTimeOffset? FulfilledAt { get; init; }
}

/// <summary>
/// Status of a subject access request.
/// </summary>
public enum SubjectAccessRequestStatus
{
	/// <summary>
	/// The request has been received and is pending processing.
	/// </summary>
	Pending = 0,

	/// <summary>
	/// The request is being processed.
	/// </summary>
	InProgress = 1,

	/// <summary>
	/// The request has been fulfilled.
	/// </summary>
	Fulfilled = 2,

	/// <summary>
	/// The request was rejected (e.g., identity verification failed).
	/// </summary>
	Rejected = 3
}

/// <summary>
/// Configuration options for subject access request processing.
/// </summary>
public sealed class SubjectAccessOptions
{
	/// <summary>
	/// Gets or sets the response deadline in days from request creation.
	/// Default: 30 days (per GDPR Article 12(3)).
	/// </summary>
	public int ResponseDeadlineDays { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether requests should be automatically fulfilled.
	/// Default: false.
	/// </summary>
	public bool AutoFulfill { get; set; }
}
