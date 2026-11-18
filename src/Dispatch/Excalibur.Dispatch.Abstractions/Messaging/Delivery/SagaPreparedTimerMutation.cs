// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Owns the encoded immutable inputs for one SQL timer mutation.</summary>
public sealed class SagaPreparedTimerMutation
{
    private readonly byte[]? _data;

    /// <summary>Initializes a validated timer mutation and copies encoded data.</summary>
    /// <param name="action">The timer operation.</param>
    /// <param name="timeoutId">The stable timeout identifier; empty only for cancel-all.</param>
    /// <param name="generation">The scheduling generation.</param>
    /// <param name="timeoutType">The registered timeout type for schedules.</param>
    /// <param name="data">The already-encoded timeout data.</param>
    /// <param name="dueAt">The exact scheduled due instant.</param>
    /// <param name="scheduledAt">The exact original scheduling instant.</param>
    /// <param name="sagaType">The original registered saga routing type for schedules, distinct from the persisted state type.</param>
    public SagaPreparedTimerMutation(SagaPreparedTimerAction action, string timeoutId, long generation,
        string? timeoutType, byte[]? data, DateTimeOffset? dueAt, DateTimeOffset? scheduledAt, string? sagaType = null)
    {
        if (action is not (SagaPreparedTimerAction.Schedule or SagaPreparedTimerAction.Cancel or SagaPreparedTimerAction.CancelAll))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        ArgumentNullException.ThrowIfNull(timeoutId);
        if (action == SagaPreparedTimerAction.CancelAll)
        {
            if (timeoutId.Length != 0) { throw new ArgumentException("Cancel-all has no timeout identifier.", nameof(timeoutId)); }
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(timeoutId.Length, 450, nameof(timeoutId));
        }
        if (action == SagaPreparedTimerAction.Schedule)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(timeoutType);
            ArgumentException.ThrowIfNullOrWhiteSpace(sagaType);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(sagaType.Length, 512, nameof(sagaType));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(timeoutType.Length, 512, nameof(timeoutType));
            if (dueAt is null || scheduledAt is null) { throw new ArgumentException("Schedules require their original due and scheduling instants."); }
        }
        else if (timeoutType is not null || data is not null || dueAt is not null || scheduledAt is not null || sagaType is not null)
        {
            throw new ArgumentException("Cancellation carries no schedule payload.");
        }

        Action = action;
        TimeoutId = timeoutId;
        Generation = generation;
        TimeoutType = timeoutType;
        SagaType = sagaType;
        _data = data is null ? null : (byte[])data.Clone();
        DueAt = dueAt;
        ScheduledAt = scheduledAt;
    }

    /// <summary>Gets the operation.</summary>
    public SagaPreparedTimerAction Action { get; }
    /// <summary>Gets the stable timeout identifier.</summary>
    public string TimeoutId { get; }
    /// <summary>Gets the scheduling generation.</summary>
    public long Generation { get; }
    /// <summary>Gets the registered timeout type.</summary>
    public string? TimeoutType { get; }
    /// <summary>Gets the original saga routing type for schedules.</summary>
    public string? SagaType { get; }
    /// <summary>Gets the exact due instant.</summary>
    public DateTimeOffset? DueAt { get; }
    /// <summary>Gets the exact original scheduling instant.</summary>
    public DateTimeOffset? ScheduledAt { get; }
    /// <summary>Returns a caller-owned copy of encoded timer data.</summary>
    /// <returns>The encoded data, or null.</returns>
    public byte[]? GetData() => _data is null ? null : (byte[])_data.Clone();
}
