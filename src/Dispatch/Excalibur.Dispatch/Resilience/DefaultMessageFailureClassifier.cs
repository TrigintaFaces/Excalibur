// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Middleware.Versioning;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// The default <see cref="IMessageFailureClassifier"/>: a deterministic, dependency-free mapping from
/// an exception to a <see cref="MessageFailureKind"/>, shared by every resilience-aware component so
/// the "retry vs. dead-letter" decision is identical regardless of which path raised the failure.
/// </summary>
/// <remarks>
/// <para>
/// The mapping is intentionally conservative: only failures that are <em>provably</em> non-retryable
/// are classified <see cref="MessageFailureKind.Permanent"/> or <see cref="MessageFailureKind.Poison"/>;
/// everything else falls through to <see cref="MessageFailureKind.Transient"/> so an unrecognised
/// failure receives a <em>bounded</em> retry (the attempt cap is the backstop) rather than being
/// silently dropped or dead-lettered.
/// </para>
/// <para>
/// This exception-level classifier is distinct from, and complementary to, the message-level
/// <c>IPoisonMessageDetector</c> (which inspects replay counts and message content). A custom
/// classifier can be registered to override this default.
/// </para>
/// </remarks>
internal sealed class DefaultMessageFailureClassifier : IMessageFailureClassifier
{
    /// <inheritdoc />
    public MessageFailureKind Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var ex = Unwrap(exception);

        return ex switch
        {
            // EC-1: cancellation is never poison and never dead-lettered — consumers honour the
            // cancellation token before this is reached. Classify transient so it is never routed
            // to the DLQ if it ever does surface here (TaskCanceledException derives from this).
            OperationCanceledException => MessageFailureKind.Transient,

            // --- Poison: the message itself is defective and can never be processed. ---
            DispatchSerializationException => MessageFailureKind.Poison,
            Excalibur.Dispatch.Serialization.SerializationException => MessageFailureKind.Poison,
            System.Text.Json.JsonException => MessageFailureKind.Poison,
            System.Runtime.Serialization.SerializationException => MessageFailureKind.Poison,
            System.Text.DecoderFallbackException => MessageFailureKind.Poison,

            // --- Permanent: retrying will not help, but the message is not "poison". ---
            ValidationException => MessageFailureKind.Permanent,
            System.ComponentModel.DataAnnotations.ValidationException => MessageFailureKind.Permanent,
            AuthenticationException => MessageFailureKind.Permanent,
            ForbiddenException => MessageFailureKind.Permanent,
            UnauthorizedAccessException => MessageFailureKind.Permanent,
            ConfigurationException => MessageFailureKind.Permanent,
            ContractVersionException => MessageFailureKind.Permanent,
            NotSupportedException => MessageFailureKind.Permanent,
            NotImplementedException => MessageFailureKind.Permanent,
            ArgumentException => MessageFailureKind.Permanent, // incl. ArgumentNull/ArgumentOutOfRange

            // --- Transient: expected to recover on a later attempt; retry with backoff. ---
            OperationTimeoutException => MessageFailureKind.Transient,
            TimeoutException => MessageFailureKind.Transient,
            CircuitBreakerOpenException => MessageFailureKind.Transient,
            RateLimitExceededException => MessageFailureKind.Transient,
            System.Net.Sockets.SocketException => MessageFailureKind.Transient,
            System.Net.Http.HttpRequestException => MessageFailureKind.Transient,
            System.IO.IOException => MessageFailureKind.Transient,

            // EC-3: anything unrecognised is treated as transient so it gets a bounded retry —
            // never an infinite loop, never a silent drop. The attempt cap is the safety net.
            _ => MessageFailureKind.Transient,
        };
    }

    /// <summary>
    /// EC-2: unwraps a single-inner <see cref="AggregateException"/> chain to its root cause so the
    /// classification reflects the real failure rather than the wrapper. A genuinely multi-fault
    /// aggregate is left flattened (and classified transient via the default arm).
    /// </summary>
    private static Exception Unwrap(Exception exception)
    {
        var current = exception;

        while (current is AggregateException aggregate)
        {
            var flattened = aggregate.Flatten();
            if (flattened.InnerExceptions.Count != 1)
            {
                return flattened;
            }

            current = flattened.InnerExceptions[0];
        }

        return current;
    }
}
