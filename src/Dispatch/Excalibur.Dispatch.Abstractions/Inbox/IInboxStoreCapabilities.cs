// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Reports the <em>effective</em> durable capabilities of an <see cref="IInboxStore"/>, composing through
/// decorator chains so a startup capability guard can probe the true innermost behavior rather than a
/// statically-declared interface that may throw at runtime.
/// </summary>
/// <remarks>
/// <para>
/// A decorating inbox store (for example a transparent-encryption decorator) typically declares the
/// segregated capability interfaces (<see cref="IClaimableInboxStore"/>,
/// <see cref="IProcessingTrackingInboxStore"/>) so it can forward them to its inner store. A simple
/// <c>is IClaimableInboxStore</c> check therefore <em>passes</em> for the decorator even when the wrapped
/// inner store lacks the capability — the decorator then throws <see cref="NotSupportedException"/> at
/// first call (pass-then-throw-at-runtime). A decorator that implements this interface instead reports the
/// capability it can actually forward, letting the <c>ValidateOnStart</c> presence-guards fail fast at
/// startup and making the runtime <see cref="NotSupportedException"/> structurally unreachable.
/// </para>
/// <para>
/// Implementations MUST report the <b>effective</b> capability and compose through chains: a decorator
/// reports <see langword="true"/> when its inner store either directly implements the matching capability
/// interface or itself reports <see langword="true"/> via this interface. Plain (non-decorating) stores do
/// not need to implement this interface; the guards fall back to the direct interface check for them.
/// </para>
/// </remarks>
public interface IInboxStoreCapabilities
{
    /// <summary>
    /// Gets a value indicating whether the store can atomically claim a message for idempotent processing
    /// (the effective <see cref="IClaimableInboxStore"/> capability, forwarded through any decoration).
    /// </summary>
    /// <value>
    /// <see langword="true"/> if an atomic claim/release can be forwarded to a capable store; otherwise
    /// <see langword="false"/>.
    /// </value>
    bool SupportsClaim { get; }

    /// <summary>
    /// Gets a value indicating whether the store can durably persist the in-flight
    /// <see cref="InboxStatus.Processing"/> status (the effective
    /// <see cref="IProcessingTrackingInboxStore"/> capability, forwarded through any decoration).
    /// </summary>
    /// <value>
    /// <see langword="true"/> if durable Processing tracking can be forwarded to a capable store; otherwise
    /// <see langword="false"/>.
    /// </value>
    bool SupportsProcessingTracking { get; }
}
