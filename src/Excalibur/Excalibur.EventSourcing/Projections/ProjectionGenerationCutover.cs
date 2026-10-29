// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Atomically switches framework-routed readers to a caught-up generation.</summary>
/// <remarks>Cutover never retires a member or deletes a destination. Stop old processors and retire their binding explicitly after the rollback window.</remarks>
public sealed class ProjectionGenerationCutover
{
    internal ProjectionCapacity? Capacity { get; init; }
	private readonly ISubscriptionCheckpointStore _checkpoints;
	private readonly IGlobalStreamQuery _globalStream;
	private readonly IProjectionGenerationStore _generations;

	/// <summary>Creates the shared cutover gate.</summary>
	/// <param name="checkpoints">The authoritative roster and progress store.</param>
	/// <param name="globalStream">The stream whose head bounds the lag.</param>
	public ProjectionGenerationCutover(ISubscriptionCheckpointStore checkpoints, IGlobalStreamQuery globalStream)
	{
		ArgumentNullException.ThrowIfNull(checkpoints);
		ArgumentNullException.ThrowIfNull(globalStream);
		_checkpoints = checkpoints;
		_globalStream = globalStream;
		_generations = checkpoints as IProjectionGenerationStore
			?? throw new ArgumentException("The checkpoint store must provide atomic generation routing.", nameof(checkpoints));
	}

	/// <summary>Switches readers when the selected member's measured lag satisfies the threshold.</summary>
	/// <param name="request">The subscription, generation keys and accepted lag in positions.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The observed outcome and the lag measurement.</returns>
	/// <remarks>The database validates the captured target binding and minimum cursor while comparing the reader revision. A successful transition protects its target from retirement. Native search aliases and direct SDK clients do not follow this pointer.</remarks>
	public async Task<ProjectionGenerationCutoverResult> CutOverAsync(ProjectionGenerationCutoverRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.SubscriptionName);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.NewProjectionKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ReaderName);
		ArgumentOutOfRangeException.ThrowIfNegative(request.MaxAcceptableLag);
		var head = await RunAsync(ProjectionCapacityKind.Read, _globalStream.GetHeadPositionAsync, cancellationToken, sourceRead: true).ConfigureAwait(false);
		var roster = await RunAsync(ProjectionCapacityKind.Read, token => new ValueTask<SubscriptionRoster>(_checkpoints.ReadSubscriptionAsync(request.SubscriptionName, token)), cancellationToken).ConfigureAwait(false);
		var next = FindMember(roster, request.NewProjectionKey);
        if (string.IsNullOrWhiteSpace(next.Destination) || string.IsNullOrWhiteSpace(next.DestinationScope))
        {
            throw new InvalidOperationException("Cutover requires an authoritative destination identity and storage scope.");
        }
		if (next.Position is not { } cursor)
		{
			return new(ProjectionCutoverOutcome.NewGenerationCursorAbsent, null, head, null);
		}
		var lag = Math.Max(0, head - cursor);
		if (lag > request.MaxAcceptableLag)
		{
			return new(ProjectionCutoverOutcome.NewGenerationLagAboveThreshold, cursor, head, lag);
		}
        var applied = await RunAsync(ProjectionCapacityKind.Write, token => new ValueTask<bool>(_generations.TrySetGenerationAsync(request.SubscriptionName, request.ReaderName,
            request.ExpectedRevision, next.ProjectionKey, next.BindingId, Math.Max(0, head - request.MaxAcceptableLag), token)), cancellationToken).ConfigureAwait(false);
        return new(applied ? ProjectionCutoverOutcome.CutOver : ProjectionCutoverOutcome.Contested, cursor, head, lag);
	}

    private ValueTask<TResult> RunAsync<TResult>(ProjectionCapacityKind kind, Func<CancellationToken, ValueTask<TResult>> operation, CancellationToken token, bool sourceRead = false) =>
        Capacity is { } capacity
            ? capacity.ExecuteAsync(((sourceRead ? (object)_globalStream : _checkpoints) as IProjectionDestinationScope)?.Scope, kind, operation, token)
            : operation(token);

	private static SubscriptionMember FindMember(SubscriptionRoster roster, string key) =>
		roster.Members.FirstOrDefault(member => string.Equals(member.ProjectionKey, key, StringComparison.Ordinal))
		?? throw new InvalidOperationException($"Projection generation '{key}' is not bound to subscription '{roster.SubscriptionName}'.");
}

/// <summary>Identifies a reader, its observed revision, a target generation and a consumer-selected lag threshold.</summary>
public sealed record ProjectionGenerationCutoverRequest
{
	/// <summary>Gets the subscription containing both generations.</summary>
	/// <value>The subscription name.</value>
	public required string SubscriptionName { get; init; }
	/// <summary>Gets the new generation's durable member key.</summary>
	/// <value>The key composed by <see cref="SubscriptionKeys.ProjectionKey(Type)"/>.</value>
	public required string NewProjectionKey { get; init; }
	/// <summary>Gets the stable logical reader name.</summary>
	/// <value>A stable reader name; it does not change when a projection version changes.</value>
	public required string ReaderName { get; init; }
    /// <summary>Gets the pointer revision observed before requesting the transition.</summary>
    /// <value>Null initializes an absent reader; a value requires exactly that revision, including during rollback.</value>
    public Guid? ExpectedRevision { get; init; }
	/// <summary>Gets the maximum acceptable lag, in stream positions.</summary>
	/// <value>A nonnegative position count. There is no universally safe default.</value>
	public required long MaxAcceptableLag { get; init; }
}

/// <summary>The observed cutover outcome and the measurement used to decide it.</summary>
/// <param name="Outcome">The observed outcome.</param>
/// <param name="NewGenerationCursor">The cursor, or null when absent.</param>
/// <param name="HeadPosition">The captured stream head.</param>
/// <param name="Lag">The measured lag, or null when no cursor exists.</param>
public readonly record struct ProjectionGenerationCutoverResult(ProjectionCutoverOutcome Outcome, long? NewGenerationCursor, long HeadPosition, long? Lag);

/// <summary>The outcome of a generation cutover attempt.</summary>
public enum ProjectionCutoverOutcome
{
	/// <summary>No progress exists for the new generation; no reader update was attempted.</summary>
	NewGenerationCursorAbsent,
	/// <summary>The measured lag exceeds the threshold; no reader update was attempted.</summary>
	NewGenerationLagAboveThreshold,
	/// <summary>The reader revision or target binding changed; no unconditional retry was attempted.</summary>
	Contested,
	/// <summary>The reader pointer was atomically switched to the new generation. Neither generation was retired.</summary>
	CutOver,
}
