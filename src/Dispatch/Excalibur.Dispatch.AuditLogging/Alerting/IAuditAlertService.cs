// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.AuditLogging.Alerting;

/// <summary>
/// Provides real-time audit event evaluation and alerting.
/// </summary>
/// <remarks>
/// <para>
/// Evaluates audit events against registered rules and triggers alerts
/// when conditions are met. Includes rate limiting to prevent alert storms.
/// </para>
/// </remarks>
public interface IAuditAlertService
{
	/// <summary>
	/// Evaluates an audit event against all registered alert rules.
	/// </summary>
	/// <param name="auditEvent">The audit event to evaluate.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous evaluation.</returns>
	Task EvaluateAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Registers a new alert rule.
	/// </summary>
	/// <param name="rule">The alert rule to register.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous registration.</returns>
	Task RegisterRuleAsync(AuditAlertRule rule, CancellationToken cancellationToken);
}
