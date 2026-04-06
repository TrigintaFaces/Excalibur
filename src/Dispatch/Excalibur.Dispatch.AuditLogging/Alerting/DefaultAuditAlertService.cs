// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Alerting;

/// <summary>
/// Default implementation of <see cref="IAuditAlertService"/> with rate limiting.
/// </summary>
public sealed partial class DefaultAuditAlertService : IAuditAlertService
{
	private readonly ConcurrentDictionary<string, AuditAlertRule> _rules = new();
	private readonly AuditAlertOptions _options;
	private readonly ILogger<DefaultAuditAlertService> _logger;
	private int _alertsThisMinute;
	private DateTimeOffset _currentMinuteStart = DateTimeOffset.UtcNow;

#if NET9_0_OR_GREATER
	private readonly Lock _rateLimitLock = new();
#else
	private readonly object _rateLimitLock = new();
#endif

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultAuditAlertService"/> class.
	/// </summary>
	/// <param name="options">The alerting options.</param>
	/// <param name="logger">The logger.</param>
	public DefaultAuditAlertService(
		IOptions<AuditAlertOptions> options,
		ILogger<DefaultAuditAlertService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task EvaluateAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		foreach (var rule in _rules.Values)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				if (rule.Condition(auditEvent) && TryConsumeAlertQuota())
				{
					LogAlertTriggered(rule.Name, rule.Severity.ToString(), auditEvent.EventId);
				}
			}
			catch (Exception ex)
			{
				LogAlertEvaluationFailed(ex, rule.Name, auditEvent.EventId);
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task RegisterRuleAsync(AuditAlertRule rule, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(rule);

		_ = _rules.AddOrUpdate(rule.Name, rule, (_, _) => rule);

		LogRuleRegistered(rule.Name, rule.Severity.ToString());

		return Task.CompletedTask;
	}

	private bool TryConsumeAlertQuota()
	{
		lock (_rateLimitLock)
		{
			var now = DateTimeOffset.UtcNow;

			if (now - _currentMinuteStart >= TimeSpan.FromMinutes(1))
			{
				_currentMinuteStart = now;
				_alertsThisMinute = 0;
			}

			if (_alertsThisMinute >= _options.MaxAlertsPerMinute)
			{
				LogAlertRateLimited(_options.MaxAlertsPerMinute);
				return false;
			}

			_alertsThisMinute++;
			return true;
		}
	}

	[LoggerMessage(LogLevel.Warning,
		"Audit alert triggered: Rule={RuleName}, Severity={Severity}, EventId={EventId}")]
	private partial void LogAlertTriggered(string ruleName, string severity, string eventId);

	[LoggerMessage(LogLevel.Error,
		"Failed to evaluate alert rule {RuleName} for event {EventId}")]
	private partial void LogAlertEvaluationFailed(Exception exception, string ruleName, string eventId);

	[LoggerMessage(LogLevel.Information,
		"Registered audit alert rule: {RuleName} with severity {Severity}")]
	private partial void LogRuleRegistered(string ruleName, string severity);

	[LoggerMessage(LogLevel.Warning,
		"Audit alert rate limit reached ({MaxAlertsPerMinute}/min). Suppressing further alerts.")]
	private partial void LogAlertRateLimited(int maxAlertsPerMinute);
}
