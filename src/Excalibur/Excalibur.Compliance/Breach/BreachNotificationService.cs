// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Breach;

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
			// Same false affirmative as NotifyAffectedSubjectsAsync, reached automatically instead of by an
			// explicit call — and therefore quieter. The breach is recorded above before this throws, so the
			// report and its Article 33 deadline survive; what does not survive is the claim that subjects
			// were notified by an implementation that cannot notify anyone.
			var failure = new NotSupportedException(
				$"AutoNotify is enabled, but the registered {nameof(IBreachNotificationService)} " +
				$"({nameof(BreachNotificationService)}) records breach state only and has no notification " +
				"transport, so no subject would receive anything. The breach has been recorded with its " +
				"GDPR Article 33 deadline; automatic notification has NOT occurred and must not be reported " +
				$"as having occurred. Register an {nameof(IBreachNotificationService)} implementation that " +
				"performs real delivery, or disable AutoNotify.");

			LogBreachNotificationFailed(report.BreachId, failure);

			throw failure;
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

		// This implementation tracks breach records. It has no transport, makes no external call, and
		// reaches no recipient, so it cannot notify anybody — and it must not say that it did.
		//
		// Recording SubjectsNotified here would satisfy every observable signal an operator, an auditor or
		// a dashboard consults: a status, a timestamp, and a log line reading "notification sent". GDPR
		// Articles 33 and 34 make that a statutory duty with administrative fines attached, so a false
		// affirmative is worse than no answer — it converts an unmet obligation into one nobody will look
		// at again. Refusing is the only truthful outcome available to a service that cannot send.
		//
		// A deployment that must discharge this obligation registers its own IBreachNotificationService
		// with a real transport; that registration wins over this fallback.
		throw new NotSupportedException(
			$"Cannot notify subjects affected by breach '{breachId}': the registered " +
			$"{nameof(IBreachNotificationService)} ({nameof(BreachNotificationService)}) records breach " +
			"state only and has no notification transport, so no subject would receive anything. GDPR " +
			"Articles 33 and 34 impose a statutory notification duty, and reporting it as discharged " +
			$"without sending would be false. Register an {nameof(IBreachNotificationService)} " +
			"implementation that performs real delivery before calling this method.");
	}

	[LoggerMessage(
		ComplianceEventId.BreachNotificationReported,
		LogLevel.Warning,
		"Breach {BreachId} reported. Affected subjects: {AffectedSubjectCount}. Notification deadline: {Deadline}")]
	private partial void LogBreachNotificationReported(string breachId, int affectedSubjectCount, DateTimeOffset deadline);


	[LoggerMessage(
		ComplianceEventId.BreachNotificationFailed,
		LogLevel.Error,
		"Breach {BreachId} notification failed")]
	private partial void LogBreachNotificationFailed(string breachId, Exception exception);
}
