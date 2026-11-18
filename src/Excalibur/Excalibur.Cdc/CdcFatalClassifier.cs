// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Cdc;

/// <summary>
/// Decides whether a CDC processing fault requires the loop to stop for review or permits
/// recovery by the existing backoff-reconnect policy. Stopped faults never advance the checkpoint.
/// </summary>
/// <remarks>
/// <para>
/// When an <see cref="IMessageFailureClassifier"/> is available (the registered
/// <c>DefaultMessageFailureClassifier</c> when Dispatch is wired) it is the single source of truth, so
/// the CDC retry-vs-terminate decision stays consistent with the rest of the pipeline.
/// </para>
/// <para>
/// When no classifier is supplied, a conservative fallback reconnects recognized transient BCL faults. Unrecognized faults stop for review without
/// advancing the checkpoint. Stopping an unknown fault is not a classification as permanent or poison.
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
	/// <see langword="true"/> if the loop must stop (permanent, poison or unknown);
	/// <see langword="false"/> for recognized transient/uncertain failures or cooperative cancellation.
	/// A stopped unknown fault retains its checkpoint; it is not a permanent rejection.
	/// </returns>
	public static bool IsFatal(Exception exception, IMessageFailureClassifier? classifier)
	{
		ArgumentNullException.ThrowIfNull(exception);

		if (classifier is not null)
		{
			return classifier.Classify(exception) is not (MessageFailureKind.Transient or MessageFailureKind.Uncertain);
		}

		return IsDefinitivelyFatal(Unwrap(exception));
	}

	// Fail-safe fallback (no classifier): known transient BCL exceptions remain recoverable;
	// unrecognized faults stop for review without advancing the checkpoint.
	private static bool IsDefinitivelyFatal(Exception exception) => exception switch
	{
		// Cancellation is cooperative, not a fault — never fatal (callers handle the token separately).
		OperationCanceledException => false,
		// A superseded leadership fencing token is terminal: retrying the checkpoint write with the same
		// stale token is rejected identically, so the demoted instance must stop rather than spin.
		CdcLeadershipSupersededException => true,
		System.Security.Authentication.AuthenticationException => true,
		UnauthorizedAccessException => true,
		NotSupportedException => true,
		NotImplementedException => true,
		// A bad argument/contract (incl. ArgumentNull/ArgumentOutOfRange) cannot be fixed by retrying.
		ArgumentException => true,
		TimeoutException => false,
		System.IO.IOException => false,
		System.Net.Sockets.SocketException => false,
		System.Net.Http.HttpRequestException => false,
		_ => true,
	};

	// Unwrap a single-inner AggregateException chain to classify the real cause, matching
	// DefaultMessageFailureClassifier's behavior; a genuinely multi-fault aggregate remains unknown and stops.
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
