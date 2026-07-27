// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch;

/// <summary>
/// Thrown at a message-ingress trust boundary when an inbound payload exceeds the configured
/// maximum length, before the body is materialized/deserialized.
/// </summary>
/// <remarks>
/// An <strong>internal control signal</strong> at the message-receive ingress: the framework catches
/// it and rejects/dead-letters the oversized message before it enters the pipeline. Rejection surfaces
/// to consumers via logging/metrics/DLQ — <em>not</em> exception propagation — so this is deliberately
/// not a consumer-catchable contract (distinct altitude from Kestrel's consumer-facing
/// <c>BadHttpRequestException</c>). The size limit is a security/DoS policy, so ingress fails
/// <em>closed</em>: an oversized payload is rejected, never silently accepted.
/// </remarks>
[SuppressMessage("Design", "CA1064:ExceptionsShouldBePublic",
    Justification = "Internal control signal at message-receive ingress; rejection surfaces via logging/metrics/DLQ, not consumer exception-catching. Internal-first per SA seam ruling.")]
internal sealed class PayloadTooLargeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadTooLargeException"/> class.
    /// </summary>
    /// <param name="actualBytes"> The rejected payload length, in bytes. </param>
    /// <param name="maxBytes"> The configured maximum payload length, in bytes. </param>
    public PayloadTooLargeException(int actualBytes, int maxBytes)
        : base($"Inbound payload of {actualBytes} bytes exceeds the configured maximum of {maxBytes} bytes and was rejected before deserialization.")
    {
        ActualBytes = actualBytes;
        MaxBytes = maxBytes;
    }

    /// <summary>
    /// Gets the rejected payload length, in bytes.
    /// </summary>
    public int ActualBytes { get; }

    /// <summary>
    /// Gets the configured maximum payload length, in bytes.
    /// </summary>
    public int MaxBytes { get; }
}
