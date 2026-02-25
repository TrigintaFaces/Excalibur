// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

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
			Status = _options.Value.AutoFulfill
				? SubjectAccessRequestStatus.Fulfilled
				: SubjectAccessRequestStatus.Pending,
			Deadline = deadline,
			FulfilledAt = _options.Value.AutoFulfill ? DateTimeOffset.UtcNow : null
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
