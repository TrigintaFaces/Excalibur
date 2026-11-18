// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
/// Recognized transient faults receive bounded retries. Unrecognized faults return
/// <see cref="MessageFailureKind.Unknown"/>: insufficient evidence for automatic retry is not proof
/// of permanent rejection, poison content or successful delivery. Durable consumers retain unknown
/// failures for explicit review rather than discard them.
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
            DispatchDeliveryUncertainException => MessageFailureKind.Uncertain,
            // cancellation is never poison and never dead-lettered — consumers honour the
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

            // Lack of classification is not evidence that replay will recover.
            _ => ClassifyUnrecognized(ex),
        };
    }

    /// <summary>
    /// Classifies exceptions whose types live in packages this component does not (and, by design,
    /// must not) reference, matched by their fully-qualified type name. Signing and signature-
    /// verification failures are provably non-retryable — the message will fail identically on every
    /// attempt — so they are classified <see cref="MessageFailureKind.Permanent"/> rather than being
    /// given a futile bounded retry. A trusted provider's positive transient observation is retained on
    /// the original exception; everything else remains <see cref="MessageFailureKind.Unknown"/>.
    /// </summary>
    private static MessageFailureKind ClassifyUnrecognized(Exception exception) =>
        exception.GetType().FullName switch
        {
            "Excalibur.Security.VerificationException" => MessageFailureKind.Permanent,
            "Excalibur.Security.SigningException" => MessageFailureKind.Permanent,
            _ when KnownTransientFailureEvidence.Contains(exception) => MessageFailureKind.Transient,
            _ => MessageFailureKind.Unknown,
        };

    /// <summary>
    /// Unwraps a single-inner <see cref="AggregateException"/> chain to its root cause so the
    /// classification reflects the real failure rather than the wrapper. A genuinely multi-fault
    /// aggregate is left flattened (and classified unknown via the default arm).
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
