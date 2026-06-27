// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Thrown when an event type name read from persisted or otherwise untrusted storage cannot be
/// resolved to a registered type.
/// </summary>
/// <remarks>
/// <para>
/// Event type resolution is allow-list-bounded: a name is resolved only if it is registered (via an
/// <c>IEventTypeRegistry</c> / source-generated type map, or an explicit registration). An unregistered
/// name is rejected with this exception rather than being resolved by an unbounded assembly scan, which
/// makes deserialization of an attacker-chosen (gadget-chain) type structurally inexpressible by default.
/// </para>
/// <para>
/// Derives from <see cref="InvalidOperationException"/> so existing callers that catch
/// <see cref="InvalidOperationException"/> around deserialization continue to work.
/// </para>
/// </remarks>
public sealed class UnknownEventTypeException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownEventTypeException"/> class.
    /// </summary>
    public UnknownEventTypeException()
        : base("The event type could not be resolved because it is not registered.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownEventTypeException"/> class with a specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public UnknownEventTypeException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownEventTypeException"/> class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public UnknownEventTypeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
