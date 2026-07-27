// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// Thrown when an activity invoked from a workflow fails. The failure is journaled, so on replay the
/// recorded failure is re-surfaced deterministically rather than re-invoking the activity.
/// </summary>
public sealed class WorkflowActivityException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowActivityException"/> class.
    /// </summary>
    /// <param name="activityName">The name of the failed activity.</param>
    /// <param name="stepOrdinal">The step ordinal of the failed activity.</param>
    /// <param name="error">The recorded failure detail.</param>
    /// <param name="innerException">The underlying exception, when available.</param>
    public WorkflowActivityException(
        string activityName,
        int stepOrdinal,
        string? error,
        Exception? innerException = null)
        : base($"Activity '{activityName}' at step {stepOrdinal} failed: {error}", innerException)
    {
        ActivityName = activityName;
        StepOrdinal = stepOrdinal;
    }

    /// <summary>
    /// Gets the name of the failed activity.
    /// </summary>
    public string ActivityName { get; }

    /// <summary>
    /// Gets the step ordinal of the failed activity within the workflow instance.
    /// </summary>
    public int StepOrdinal { get; }
}

/// <summary>
/// Thrown when a workflow instance's journal cannot be appended because another writer advanced the
/// instance concurrently. A workflow instance has a single logical writer; a conflict indicates a
/// duplicate concurrent execution of the same instance.
/// </summary>
public sealed class WorkflowConcurrencyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowConcurrencyException"/> class.
    /// </summary>
    /// <param name="instanceId">The workflow instance identifier.</param>
    /// <param name="error">The underlying store error detail.</param>
    public WorkflowConcurrencyException(string instanceId, string? error)
        : base($"Concurrent write conflict on workflow instance '{instanceId}': {error}")
    {
        InstanceId = instanceId;
    }

    /// <summary>
    /// Gets the workflow instance identifier that conflicted.
    /// </summary>
    public string InstanceId { get; }
}

/// <summary>
/// Thrown when an in-flight workflow instance is resumed but the definition version it was pinned to at
/// start is no longer registered. The engine fails loud rather than silently replaying the instance against
/// a different definition version, which would break deterministic replay.
/// </summary>
public sealed class WorkflowVersionNotRegisteredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowVersionNotRegisteredException"/> class.
    /// </summary>
    /// <param name="workflowName">The workflow name.</param>
    /// <param name="version">The pinned definition version that is no longer registered.</param>
    public WorkflowVersionNotRegisteredException(string workflowName, int version)
        : base(
            $"Workflow '{workflowName}' definition version {version} is no longer registered, but an "
            + "in-flight instance is pinned to it. Keep the pinned version registered until all instances on "
            + "it have completed.")
    {
        WorkflowName = workflowName;
        Version = version;
    }

    /// <summary>
    /// Gets the workflow name whose pinned version is missing.
    /// </summary>
    public string WorkflowName { get; }

    /// <summary>
    /// Gets the pinned definition version that is no longer registered.
    /// </summary>
    public int Version { get; }
}

/// <summary>
/// Thrown when a workflow body diverges from its durable journal on replay — for example, the workflow
/// definition was edited between deployments so the operation recorded at a step no longer matches the
/// operation the body now performs there.
/// </summary>
/// <remarks>
/// A durable workflow must replay deterministically: the sequence of operations a body performs must match
/// the recorded journal step-for-step. When a step's recorded operation disagrees with the current body
/// (a different activity name at the same step), continuing would silently return a result belonging to a
/// different operation, so the engine fails fast instead.
/// </remarks>
public sealed class WorkflowNonDeterminismException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowNonDeterminismException"/> class.
    /// </summary>
    /// <param name="stepOrdinal">The step ordinal at which the divergence was detected.</param>
    /// <param name="expected">The operation recorded in the journal at this step.</param>
    /// <param name="actual">The operation the current workflow body performed at this step.</param>
    public WorkflowNonDeterminismException(int stepOrdinal, string expected, string actual)
        : base(
            $"Workflow replay diverged at step {stepOrdinal}: the journal recorded '{expected}' but the "
            + $"workflow body performed '{actual}'. The workflow definition changed incompatibly for an "
            + "in-flight instance.")
    {
        StepOrdinal = stepOrdinal;
        Expected = expected;
        Actual = actual;
    }

    /// <summary>
    /// Gets the step ordinal at which the replay divergence was detected.
    /// </summary>
    public int StepOrdinal { get; }

    /// <summary>
    /// Gets the operation recorded in the journal at the diverging step.
    /// </summary>
    public string Expected { get; }

    /// <summary>
    /// Gets the operation the current workflow body performed at the diverging step.
    /// </summary>
    public string Actual { get; }
}
