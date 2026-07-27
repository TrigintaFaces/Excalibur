// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// A read-only summary of a durable workflow instance for administrative listing (for example an
/// operational dashboard), without the full journal projection.
/// </summary>
public sealed record WorkflowInstanceSummary
{
	/// <summary>
	/// Gets the workflow instance identifier.
	/// </summary>
	/// <value>The workflow instance identifier.</value>
	public string InstanceId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the registered workflow name the instance is running.
	/// </summary>
	/// <value>The registered workflow name.</value>
	public string WorkflowName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the lifecycle status of the instance.
	/// </summary>
	/// <value>The lifecycle status.</value>
	public WorkflowStatus Status { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the instance was started.
	/// </summary>
	/// <value>The UTC start timestamp.</value>
	public DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the instance completed, when it has completed.
	/// </summary>
	/// <value>The UTC completion timestamp, or <see langword="null"/> when the instance is still running.</value>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>
	/// Gets the tenant identifier the instance belongs to in a multi-tenant host, when tenant-scoped.
	/// </summary>
	/// <value>The tenant identifier, or <see langword="null"/> when the instance is not tenant-scoped.</value>
	public string? TenantId { get; init; }
}
