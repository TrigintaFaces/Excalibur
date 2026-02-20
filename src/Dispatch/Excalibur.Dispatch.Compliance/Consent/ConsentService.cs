// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implementation of <see cref="IConsentService"/> providing GDPR Article 7
/// consent management capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This in-memory implementation is suitable for development and testing.
/// Production deployments should use a persistent store via <see cref="IComplianceStore"/>.
/// </para>
/// </remarks>
public sealed partial class ConsentService : IConsentService
{
	private readonly ConcurrentDictionary<string, ConsentRecord> _consents = new(StringComparer.OrdinalIgnoreCase);
	private readonly IOptions<ConsentOptions> _options;
	private readonly ILogger<ConsentService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConsentService"/> class.
	/// </summary>
	/// <param name="options">The consent options.</param>
	/// <param name="logger">The logger.</param>
	public ConsentService(
		IOptions<ConsentOptions> options,
		ILogger<ConsentService> logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task RecordConsentAsync(
		ConsentRecord record,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);

		var opts = _options.Value;
		var effectiveRecord = record;

		// Apply default expiration if configured and not already set
		if (opts.DefaultExpirationDays > 0 && record.ExpiresAt is null)
		{
			effectiveRecord = record with
			{
				ExpiresAt = record.GrantedAt.AddDays(opts.DefaultExpirationDays)
			};
		}

		var key = BuildKey(record.SubjectId, record.Purpose);
		_consents[key] = effectiveRecord;

		LogConsentRecorded(record.SubjectId, record.Purpose, record.LegalBasis);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<ConsentRecord?> GetConsentAsync(
		string subjectId,
		string purpose,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
		ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

		var key = BuildKey(subjectId, purpose);

		if (!_consents.TryGetValue(key, out var record))
		{
			return Task.FromResult<ConsentRecord?>(null);
		}

		// Check if consent has expired
		if (record.ExpiresAt.HasValue && DateTimeOffset.UtcNow > record.ExpiresAt)
		{
			return Task.FromResult<ConsentRecord?>(null);
		}

		// Check if consent has been withdrawn
		if (record.IsWithdrawn)
		{
			return Task.FromResult<ConsentRecord?>(null);
		}

		return Task.FromResult<ConsentRecord?>(record);
	}

	/// <inheritdoc />
	public Task<bool> WithdrawConsentAsync(
		string subjectId,
		string purpose,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
		ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

		var key = BuildKey(subjectId, purpose);

		if (!_consents.TryGetValue(key, out var existing))
		{
			return Task.FromResult(false);
		}

		if (existing.IsWithdrawn)
		{
			return Task.FromResult(false);
		}

		var withdrawn = existing with
		{
			IsWithdrawn = true,
			WithdrawnAt = DateTimeOffset.UtcNow
		};

		_consents[key] = withdrawn;

		LogConsentWithdrawn(subjectId, purpose);

		return Task.FromResult(true);
	}

	private static string BuildKey(string subjectId, string purpose) =>
		$"{subjectId}|{purpose}";

	[LoggerMessage(
		ComplianceEventId.ConsentRecorded,
		LogLevel.Information,
		"Consent recorded for subject {SubjectId}, purpose {Purpose}, legal basis {LegalBasis}")]
	private partial void LogConsentRecorded(string subjectId, string purpose, LegalBasis legalBasis);

	[LoggerMessage(
		ComplianceEventId.ConsentWithdrawn,
		LogLevel.Information,
		"Consent withdrawn for subject {SubjectId}, purpose {Purpose}")]
	private partial void LogConsentWithdrawn(string subjectId, string purpose);

	[LoggerMessage(
		ComplianceEventId.ConsentOperationFailed,
		LogLevel.Error,
		"Consent operation failed for subject {SubjectId}")]
	private partial void LogConsentOperationFailed(string subjectId, Exception exception);
}
