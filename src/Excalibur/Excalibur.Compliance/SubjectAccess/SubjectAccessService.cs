// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.SubjectAccess;

/// <summary>
/// Implementation of <see cref="ISubjectAccessService"/> providing GDPR Article 15
/// subject access request processing capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This in-memory implementation is suitable for development and testing.
/// Production deployments should use a persistent store-backed implementation.
/// </para>
/// </remarks>
public sealed partial class SubjectAccessService : ISubjectAccessService
{
	private readonly ConcurrentDictionary<string, SubjectAccessResult> _requests = new(StringComparer.OrdinalIgnoreCase);
	private readonly IOptions<SubjectAccessOptions> _options;
	private readonly ILogger<SubjectAccessService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SubjectAccessService"/> class.
	/// </summary>
	/// <param name="options">The subject access options.</param>
	/// <param name="logger">The logger.</param>
	public SubjectAccessService(
		IOptions<SubjectAccessOptions> options,
		ILogger<SubjectAccessService> logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task<SubjectAccessResult> CreateRequestAsync(
		SubjectAccessRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		var requestId = Guid.NewGuid().ToString("N");
		var deadline = request.RequestedAt.AddDays(_options.Value.ResponseDeadlineDays);

		var result = new SubjectAccessResult
		{
			RequestId = requestId,
			// A request is PENDING when it is created. Always.
			//
			// This used to read the AutoFulfill option and, when set, report Fulfilled with FulfilledAt =
			// UtcNow at the instant of creation -- nothing gathered, nothing sent, an Article 15 obligation
			// declared discharged by a configuration flag. The audience for that answer is a regulator.
			//
			// Fulfilment is an ACT, and the consumer performs it: they gather the data, send it, and then
			// call FulfillRequestAsync to record that it happened. That method already exists and already
			// refuses to fulfil twice, so nothing is lost by removing the flag -- only the ability to claim
			// an outcome nobody produced.
			Status = SubjectAccessRequestStatus.Pending,
			Deadline = deadline,
			FulfilledAt = null
		};

		_requests[requestId] = result;

		LogSubjectAccessRequestCreated(requestId, request.SubjectId, request.RequestType);

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<SubjectAccessResult?> GetRequestStatusAsync(
		string requestId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

		_requests.TryGetValue(requestId, out var result);
		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<SubjectAccessResult> FulfillRequestAsync(
		string requestId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

		if (!_requests.TryGetValue(requestId, out var existing))
		{
			throw new InvalidOperationException($"Subject access request '{requestId}' not found.");
		}

		if (existing.Status == SubjectAccessRequestStatus.Fulfilled)
		{
			throw new InvalidOperationException($"Subject access request '{requestId}' has already been fulfilled.");
		}

		var fulfilled = existing with
		{
			Status = SubjectAccessRequestStatus.Fulfilled,
			FulfilledAt = DateTimeOffset.UtcNow
		};

		_requests[requestId] = fulfilled;

		LogSubjectAccessRequestFulfilled(requestId);

		return Task.FromResult(fulfilled);
	}

	[LoggerMessage(
		ComplianceEventId.SubjectAccessRequestCreated,
		LogLevel.Information,
		"Subject access request {RequestId} created for subject {SubjectId}, type {RequestType}")]
	private partial void LogSubjectAccessRequestCreated(string requestId, string subjectId, SubjectAccessRequestType requestType);

	[LoggerMessage(
		ComplianceEventId.SubjectAccessRequestFulfilled,
		LogLevel.Information,
		"Subject access request {RequestId} fulfilled")]
	private partial void LogSubjectAccessRequestFulfilled(string requestId);

	[LoggerMessage(
		ComplianceEventId.SubjectAccessRequestFailed,
		LogLevel.Error,
		"Subject access request {RequestId} failed")]
	private partial void LogSubjectAccessRequestFailed(string requestId, Exception exception);
}
