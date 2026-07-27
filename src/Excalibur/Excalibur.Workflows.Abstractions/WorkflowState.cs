// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// A read-only, point-in-time projection of a durable workflow instance's current state, reconstructed
/// from its journal for querying (for example by an operational dashboard).
/// </summary>
/// <remarks>
/// This is a non-mutating projection: reading it never advances, resumes, or otherwise alters the instance.
/// It is derived from the ordered journal at the moment of the query, so a subsequent query may observe a
/// later state.
/// </remarks>
public sealed record WorkflowState
{
	/// <summary>
	/// Gets the workflow instance identifier this state describes.
	/// </summary>
	/// <value>The workflow instance identifier.</value>
	public string InstanceId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the registered workflow name the instance is running.
	/// </summary>
	/// <value>The registered workflow name.</value>
	public string WorkflowName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the workflow definition version this instance is pinned to.
	/// </summary>
	/// <remarks>
	/// An in-flight instance runs to completion on the definition version it started on; only new instances
	/// bind a newer registered version. A dashboard reads this to show which definition an instance replays
	/// against.
	/// </remarks>
	/// <value>The pinned definition version.</value>
	public int DefinitionVersion { get; init; }

	/// <summary>
	/// Gets the lifecycle status of the instance.
	/// </summary>
	/// <value>The lifecycle status.</value>
	public WorkflowStatus Status { get; init; }

	/// <summary>
	/// Gets the number of activity steps that have completed for the instance.
	/// </summary>
	/// <value>The count of completed activity steps.</value>
	public int CompletedActivitySteps { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the instance was started.
	/// </summary>
	/// <value>The UTC start timestamp.</value>
	public DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets the UTC timestamp of the most recent journal entry for the instance.
	/// </summary>
	/// <value>The UTC timestamp of the last recorded journal entry.</value>
	public DateTimeOffset LastUpdatedAt { get; init; }

	/// <summary>
	/// Gets the serialized workflow result when the instance has completed successfully.
	/// </summary>
	/// <value>The serialized result, or <see langword="null"/> when the instance has not completed.</value>
	public string? ResultJson { get; init; }

	/// <summary>
	/// Gets the failure detail when the instance has faulted.
	/// </summary>
	/// <value>The failure detail, or <see langword="null"/> when the instance has not faulted.</value>
	public string? FailureReason { get; init; }
}
