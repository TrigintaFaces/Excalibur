// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace ProjectionsSample.Infrastructure;

// ============================================================================
// Projection Checkpoint Tracking
// ============================================================================
// Checkpoints track the last processed position for async projections.
// This enables:
// - Resuming projection processing after restarts
// - Rebuilding projections from scratch
// - Gap detection and recovery

/// <summary>
/// Represents a checkpoint for tracking projection progress.
/// </summary>
/// <remarks>
/// Checkpoints store the last successfully processed event position,
/// enabling projections to resume from where they left off.
/// </remarks>
public sealed record ProjectionCheckpoint
{
	/// <summary>Gets or sets the projection name (unique identifier).</summary>
	public string ProjectionName { get; set; } = string.Empty;

	/// <summary>Gets or sets the last processed global position.</summary>
	public long LastPosition { get; set; }

	/// <summary>Gets or sets the last processed event timestamp.</summary>
	public DateTimeOffset LastProcessedAt { get; set; }

	/// <summary>Gets or sets the total events processed.</summary>
	public long TotalEventsProcessed { get; set; }

	/// <summary>Gets or sets the last error (if any).</summary>
	public string? LastError { get; set; }

	/// <summary>Gets or sets when the last error occurred.</summary>
	public DateTimeOffset? LastErrorAt { get; set; }
}

/// <summary>
/// In-memory checkpoint store for demonstration.
/// </summary>
/// <remarks>
/// In production, checkpoints are stored in the database alongside projections.
/// SQL Server stores use the <c>eventsourcing.ProjectionCheckpoints</c> table.
/// </remarks>
public sealed class InMemoryCheckpointStore
{
	private readonly ConcurrentDictionary<string, ProjectionCheckpoint> _checkpoints = new();

	/// <summary>
	/// Gets the checkpoint for a projection.
	/// </summary>
	public ProjectionCheckpoint? GetCheckpoint(string projectionName)
	{
		_ = _checkpoints.TryGetValue(projectionName, out var checkpoint);
		return checkpoint;
	}

	/// <summary>
	/// Saves a checkpoint.
	/// </summary>
	public void SaveCheckpoint(ProjectionCheckpoint checkpoint)
	{
		ArgumentNullException.ThrowIfNull(checkpoint);
		_ = _checkpoints.AddOrUpdate(checkpoint.ProjectionName, checkpoint, (_, _) => checkpoint);
	}

	/// <summary>
	/// Resets a checkpoint to position 0 (for projection rebuilds).
	/// </summary>
	public void ResetCheckpoint(string projectionName)
	{
		_ = _checkpoints.TryRemove(projectionName, out _);
	}

	/// <summary>
	/// Gets all checkpoints.
	/// </summary>
	public IEnumerable<ProjectionCheckpoint> GetAllCheckpoints() => _checkpoints.Values;
}
