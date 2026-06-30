// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Cdc;

/// <summary>
/// Decides whether an exception raised during CDC processing is <em>fatal</em> (non-retryable — the
/// processor must stop and surface it loudly, never an infinite silent reconnect loop) or
/// <em>transient</em> (recover on a later attempt — keep the existing backoff-reconnect).
/// </summary>
/// <remarks>
/// <para>
/// When an <see cref="IMessageFailureClassifier"/> is available (the registered
/// <c>DefaultMessageFailureClassifier</c> when Dispatch is wired) it is the single source of truth, so
/// the CDC retry-vs-terminate decision stays consistent with the rest of the pipeline.
/// </para>
/// <para>
/// When no classifier is supplied, a conservative, fail-safe fallback is used: only a small set of
/// <em>definitively</em> non-retryable BCL/framework exceptions are treated as fatal; everything else
/// (including unrecognised faults) is treated as transient so it receives a bounded backoff-reconnect
/// rather than being terminated — a genuinely transient fault must never be mistaken for fatal.
/// </para>
/// <para>
/// This is part of the supported CDC extensibility surface: a consumer implementing a custom
/// <c>ICdcProcessor</c> composes it to apply the framework's recommended fatal-vs-transient decision
/// (the same one the built-in providers use), rather than re-deriving it.
/// </para>
/// </remarks>
public static class CdcFatalClassifier
{
	/// <summary>
	/// Determines whether <paramref name="exception"/> is fatal (non-retryable).
	/// </summary>
	/// <param name="exception">The exception raised during CDC processing.</param>
	/// <param name="classifier">
	/// The shared failure classifier, or <see langword="null"/> to use the conservative built-in fallback.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the failure is permanent/poison (fatal); <see langword="false"/> if it
	/// is transient and the loop should keep reconnecting with backoff.
	/// </returns>
	public static bool IsFatal(Exception exception, IMessageFailureClassifier? classifier)
	{
		ArgumentNullException.ThrowIfNull(exception);

		if (classifier is not null)
		{
			return classifier.Classify(exception) is MessageFailureKind.Permanent or MessageFailureKind.Poison;
		}

		return IsDefinitivelyFatal(Unwrap(exception));
	}

	// Fail-safe fallback (no classifier): only definitively non-retryable BCL/framework exceptions are
	// fatal. The Dispatch-specific permanent/poison types (serialization, validation, configuration, …)
	// are covered when a classifier is present. Anything unrecognised stays transient.
	private static bool IsDefinitivelyFatal(Exception exception) => exception switch
	{
		// Cancellation is cooperative, not a fault — never fatal (callers handle the token separately).
		OperationCanceledException => false,
		System.Security.Authentication.AuthenticationException => true,
		UnauthorizedAccessException => true,
		NotSupportedException => true,
		NotImplementedException => true,
		// A bad argument/contract (incl. ArgumentNull/ArgumentOutOfRange) cannot be fixed by retrying.
		ArgumentException => true,
		_ => false,
	};

	// Unwrap a single-inner AggregateException chain to classify the real cause, matching
	// DefaultMessageFailureClassifier's behavior; a genuinely multi-fault aggregate stays transient.
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
