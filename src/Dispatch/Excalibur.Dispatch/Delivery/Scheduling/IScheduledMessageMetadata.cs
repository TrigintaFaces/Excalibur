// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Defines the execution tracking and operational metadata for a scheduled message including
/// last execution time, missed execution policy, tenant isolation, tracing, and audit identity.
/// </summary>
public interface IScheduledMessageMetadata
{
	/// <summary>
	/// Gets or sets the timestamp of the most recent execution attempt in UTC.
	/// </summary>
	/// <value> The UTC timestamp when this schedule was last executed, or null if it has never been executed. </value>
	DateTimeOffset? LastExecutionUtc { get; set; }

	/// <summary>
	/// Gets or sets the policy for handling executions that were missed due to system downtime or delays.
	/// </summary>
	/// <value> The missed execution behavior policy, or null to use system defaults. </value>
	MissedExecutionBehavior? MissedExecutionBehavior { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier for multi-tenant scheduling isolation.
	/// </summary>
	/// <value> The tenant ID that owns this scheduled message, or null for system-wide schedules. </value>
	string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the distributed tracing parent identifier for observability integration.
	/// </summary>
	/// <value>
	/// The trace parent ID following W3C Trace Context specification.
	/// </value>
	string? TraceParent { get; set; }

	/// <summary>
	/// Gets or sets the identifier of the user or service that created this schedule.
	/// </summary>
	/// <value>
	/// The user ID, service account, or system identifier responsible for creating this scheduled message.
	/// </value>
	string? UserId { get; set; }
}
