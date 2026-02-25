// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implementation of <see cref="IBreachNotificationService"/> providing GDPR Article 33/34
/// breach notification capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This in-memory implementation tracks breach reports and notification status.
/// Production deployments should use a persistent store-backed implementation.
/// </para>
/// </remarks>
public sealed partial class BreachNotificationService : IBreachNotificationService
{
	private readonly ConcurrentDictionary<string, BreachNotificationResult> _breaches = new(StringComparer.OrdinalIgnoreCase);
	private readonly IOptions<BreachNotificationOptions> _options;
	private readonly ILogger<BreachNotificationService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="BreachNotificationService"/> class.
	/// </summary>
	/// <param name="options">The breach notification options.</param>
	/// <param name="logger">The logger.</param>
	public BreachNotificationService(
		IOptions<BreachNotificationOptions> options,
		ILogger<BreachNotificationService> logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task<BreachNotificationResult> ReportBreachAsync(
		BreachReport report,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(report);

		var now = DateTimeOffset.UtcNow;
		var deadline = report.DetectedAt.AddHours(_options.Value.NotificationDeadlineHours);

		var result = new BreachNotificationResult
		{
			BreachId = report.BreachId,
			Status = BreachNotificationStatus.Reported,
			ReportedAt = now,
			NotificationDeadline = deadline
		};

		_breaches[report.BreachId] = result;

		LogBreachNotificationReported(report.BreachId, report.AffectedSubjectCount, deadline);

		if (_options.Value.AutoNotify)
		{
			result = result with
			{
				Status = BreachNotificationStatus.SubjectsNotified,
				SubjectsNotifiedAt = now
			};
			_breaches[report.BreachId] = result;

			LogBreachNotificationSent(report.BreachId);
		}

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<BreachNotificationResult?> GetBreachStatusAsync(
		string breachId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(breachId);

		_breaches.TryGetValue(breachId, out var result);
		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<BreachNotificationResult> NotifyAffectedSubjectsAsync(
		string breachId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(breachId);

		if (!_breaches.TryGetValue(breachId, out var existing))
		{
			throw new InvalidOperationException($"Breach '{breachId}' not found.");
		}

		if (existing.Status is BreachNotificationStatus.SubjectsNotified or
			BreachNotificationStatus.Resolved)
		{
			throw new InvalidOperationException($"Breach '{breachId}' subjects have already been notified.");
		}

		var updated = existing with
		{
			Status = BreachNotificationStatus.SubjectsNotified,
			SubjectsNotifiedAt = DateTimeOffset.UtcNow
		};

		_breaches[breachId] = updated;

		LogBreachNotificationSent(breachId);

		return Task.FromResult(updated);
	}

	[LoggerMessage(
		ComplianceEventId.BreachNotificationReported,
		LogLevel.Warning,
		"Breach {BreachId} reported. Affected subjects: {AffectedSubjectCount}. Notification deadline: {Deadline}")]
	private partial void LogBreachNotificationReported(string breachId, int affectedSubjectCount, DateTimeOffset deadline);

	[LoggerMessage(
		ComplianceEventId.BreachNotificationSent,
		LogLevel.Information,
		"Breach {BreachId} notification sent to affected subjects")]
	private partial void LogBreachNotificationSent(string breachId);

	[LoggerMessage(
		ComplianceEventId.BreachNotificationFailed,
		LogLevel.Error,
		"Breach {BreachId} notification failed")]
	private partial void LogBreachNotificationFailed(string breachId, Exception exception);
}
