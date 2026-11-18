// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Saga.Abstractions;

/// <summary>Detached source snapshot and opaque expected-state token; no dispatch lease is granted.</summary>
public sealed class SagaTimeoutHoldReceipt
{
    private readonly SagaTimeout _timeout;
    /// <summary>Initializes a detached provider receipt.</summary>
    /// <param name="token">The opaque expected-state binding.</param>
    /// <param name="state">The observed disposition.</param>
    /// <param name="timeout">The original source snapshot.</param>
    /// <param name="kind">The nonretryable classification.</param>
    /// <param name="reason">Diagnostic text of at most 1024 UTF-16 code units.</param>
    public SagaTimeoutHoldReceipt(SagaTimeoutHoldToken token, SagaTimeoutHoldState state,
        SagaTimeout timeout, MessageFailureKind kind, string reason)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(timeout);
        ArgumentNullException.ThrowIfNull(reason);
        if (!Enum.IsDefined(state)) { throw new ArgumentOutOfRangeException(nameof(state)); }
        if (kind is not (MessageFailureKind.Permanent or MessageFailureKind.Poison or MessageFailureKind.Unknown))
        { throw new ArgumentOutOfRangeException(nameof(kind)); }
        if (reason.Length > 1024) { throw new ArgumentOutOfRangeException(nameof(reason)); }
        Token = token;
        State = state;
        _timeout = timeout with { TimeoutData = timeout.TimeoutData?.ToArray() };
        Kind = kind;
        Reason = reason;
    }
    /// <summary>Gets the opaque expected-state handle.</summary>
    public SagaTimeoutHoldToken Token { get; }
    /// <summary>Gets the observed disposition; only Held certifies current exclusion.</summary>
    public SagaTimeoutHoldState State { get; }
    /// <summary>Gets a detached original source snapshot.</summary>
    public SagaTimeout Timeout => _timeout with { TimeoutData = _timeout.TimeoutData?.ToArray() };
    /// <summary>Gets the observed nonretryable classification.</summary>
    public MessageFailureKind Kind { get; }
    /// <summary>Gets bounded diagnostic text, independent of the original payload.</summary>
    public string Reason { get; }
}

