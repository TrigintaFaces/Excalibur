// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Manages the generation pointer paired with a registered projection reader.</summary>
/// <typeparam name="TProjection">The reader's projection schema.</typeparam>
public sealed class ProjectionGenerationControl<TProjection> where TProjection : class
{
    internal ProjectionCapacity? Capacity { get; init; }

    private readonly ProjectionGenerationBinding<TProjection> _binding;
    private readonly HashSet<string> _compatibleKeys;

    internal ProjectionGenerationControl(ProjectionGenerationBinding<TProjection> binding, IReadOnlyCollection<string>? compatibleKeys)
    {
        _binding = binding;
        _compatibleKeys = new HashSet<string>(compatibleKeys ?? [SubscriptionKeys.ProjectionKey(typeof(TProjection))], StringComparer.Ordinal);
    }

    /// <summary>Reads the authoritative pointer revision before a management decision.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active generation, or null when no pointer exists.</returns>
    public Task<ProjectionGeneration?> ReadGenerationAsync(CancellationToken cancellationToken) =>
        RunAsync(ProjectionCapacityKind.Read, token => ((IProjectionGenerationStore)_binding.Checkpoints).ReadGenerationAsync(_binding.SubscriptionName, _binding.ReaderName, token), cancellationToken);

    /// <summary>Removes this reader pointer only if its captured revision still matches.</summary>
    /// <param name="expectedRevision">The revision observed before stopping and draining readers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when removed; false when absent or changed. The projection binding and data remain.</returns>
    public Task<bool> DecommissionAsync(Guid expectedRevision, CancellationToken cancellationToken) =>
        RunAsync(ProjectionCapacityKind.Write, token => ((IProjectionGenerationStore)_binding.Checkpoints).TryRemoveGenerationAsync(
            _binding.SubscriptionName, _binding.ReaderName, expectedRevision, token), cancellationToken);

    /// <summary>Switches this reader using its paired source and the previously observed revision.</summary>
    /// <param name="newProjectionKey">The target generation's durable member key.</param>
    /// <param name="expectedRevision">The observed revision; null requires an absent pointer.</param>
    /// <param name="maxAcceptableLag">The accepted lag in stream positions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The observed cutover outcome and lag.</returns>
    public Task<ProjectionGenerationCutoverResult> CutOverAsync(string newProjectionKey, Guid? expectedRevision,
        long maxAcceptableLag, CancellationToken cancellationToken)
    {
        if (!_compatibleKeys.Contains(newProjectionKey))
        {
            throw new InvalidOperationException($"The registered reader does not support projection generation '{newProjectionKey}'. Deploy a compatible reader before cutover.");
        }
        return new ProjectionGenerationCutover(_binding.Checkpoints, _binding.Query) { Capacity = Capacity }.CutOverAsync(new ProjectionGenerationCutoverRequest
        {
            SubscriptionName = _binding.SubscriptionName,
            ReaderName = _binding.ReaderName,
            NewProjectionKey = newProjectionKey,
            ExpectedRevision = expectedRevision,
            MaxAcceptableLag = maxAcceptableLag,
        }, cancellationToken);
    }
    private Task<TResult> RunAsync<TResult>(ProjectionCapacityKind kind, Func<CancellationToken, Task<TResult>> operation, CancellationToken token) =>
        Capacity is { } capacity
            ? capacity.ExecuteAsync((_binding.Checkpoints as IProjectionDestinationScope)?.Scope, kind, ct => new ValueTask<TResult>(operation(ct)), token).AsTask()
            : operation(token);

}

internal sealed record ProjectionGenerationBinding<TProjection>(ISubscriptionCheckpointStore Checkpoints,
    IGlobalStreamQuery Query, string SubscriptionName, string ReaderName) where TProjection : class;
