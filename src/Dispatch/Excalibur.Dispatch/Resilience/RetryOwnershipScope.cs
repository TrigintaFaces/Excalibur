// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace Excalibur.Dispatch.Resilience;

/// <summary>Tracks the retry owner within an asynchronous execution flow, without modifying message context data.</summary>
internal sealed class RetryOwnershipScope : IDisposable
{
	private static readonly AsyncLocal<RetryOwnershipScope?> Current = new();
	private static readonly AsyncLocal<PublishingFrame?> Publishing = new();
	private static readonly IMessageFailureClassifier DefaultClassifier = new DefaultMessageFailureClassifier();
	private readonly RetryOwnershipScope? _previous;
	private readonly IDispatchMessage _message;
	private readonly IMessageContext _context;
	private int _active = 1;
	private readonly Func<Exception, bool>? _canRetry;
	private readonly IMessageFailureClassifier? _failureClassifier;
	private readonly ConcurrentDictionary<string, DestinationOutcome> _terminal = new(StringComparer.OrdinalIgnoreCase);
	private AttemptState _attempt = new();

	private RetryOwnershipScope(IDispatchMessage message, IMessageContext context, Func<Exception, bool>? canRetry,
		IMessageFailureClassifier? failureClassifier)
	{
		_message = message;
		_context = context;
		_canRetry = canRetry;
		_failureClassifier = failureClassifier;
		_previous = Current.Value;
		Current.Value = this;
	}

	internal static RetryOwnershipScope Enter(IDispatchMessage message, IMessageContext context,
		Func<Exception, bool>? canRetry = null, IMessageFailureClassifier? failureClassifier = null) => new(message, context, canRetry, failureClassifier);

	internal static RetryOwnershipScope EnterClassified(IDispatchMessage message, IMessageContext context,
		IMessageFailureClassifier failureClassifier)
	{
		ArgumentNullException.ThrowIfNull(failureClassifier);
		return new(message, context, canRetry: null, failureClassifier);
	}

	internal static bool Owns(IDispatchMessage message, IMessageContext context) => GetOwner(message, context) is not null;

	internal static RetryOwnershipScope? GetOwner(IDispatchMessage message, IMessageContext context)
	{
		for (var scope = Current.Value; scope is not null; scope = scope._previous)
		{
			if (Volatile.Read(ref scope._active) != 0 && ReferenceEquals(scope._message, message) && ReferenceEquals(scope._context, context))
			{
				return scope;
			}
		}
		return null;
	}

	internal static bool AllowsRetryAfterResult(AttemptState attempt, IMessageResult result) =>
		!attempt.Results.TryGetValue(result, out var evidence) || evidence.Retryable;

	internal AttemptState CaptureAttempt() => Volatile.Read(ref _attempt);

	internal AttemptState BeginAttempt()
	{
		var attempt = new AttemptState();
		Volatile.Write(ref _attempt, attempt);
		return attempt;
	}

	internal static void ReportFanout(AttemptState attempt, IMessageResult result, bool hasRetryableFailure,
		MessageFailureKind? knownFailureKind = null, Exception? originalFault = null)
	{
		// False is a retry-policy decision, not proof of a permanent fault. Store only a
		// separately established typed classification, bound to this exact attempt/result.
		// One immutable value replaces all evidence atomically. A later unknown/retryable
		// report must replace earlier terminal evidence rather than leaving it in a second map.
		var kind = !result.Succeeded
			&& (knownFailureKind == MessageFailureKind.Uncertain || (!hasRetryableFailure
				&& knownFailureKind is MessageFailureKind.Permanent or MessageFailureKind.Poison))
			? knownFailureKind : null;
		attempt.Results[result] = new ResultEvidence(hasRetryableFailure, kind, kind is null ? null : originalFault);
	}

	internal static bool TryGetKnownFailureKind(AttemptState attempt, IMessageResult result,
		out MessageFailureKind failureKind) => TryGetKnownFailureKind(attempt, result, out failureKind, out _);

	internal static bool TryGetKnownFailureKind(AttemptState attempt, IMessageResult result,
		out MessageFailureKind failureKind, out Exception? originalFault)
	{
		if (attempt.Results.TryGetValue(result, out var known) && known.Kind is { } kind)
		{
			failureKind = kind;
			originalFault = known.Fault;
			return true;
		}
		failureKind = default;
		originalFault = null;
		return false;
	}

	internal Task<DestinationOutcome> PublishDestinationAsync(AttemptState attempt, string name, Func<Task> publish, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (_terminal.TryGetValue(name, out var completed))
		{
			return Task.FromResult(completed);
		}
		for (var frame = Publishing.Value; frame is not null; frame = frame.Previous)
		{
			if (Volatile.Read(ref frame.Active) != 0 && ReferenceEquals(frame.Owner, this) && string.Equals(frame.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("A destination cannot await its own in-flight publication.");
			}
		}
		return attempt.Publications.GetOrAdd(name, _ => new Lazy<Task<DestinationOutcome>>(
			() => PublishCoreAsync(name, publish, cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
	}

	private async Task<DestinationOutcome> PublishCoreAsync(string name, Func<Task> publish, CancellationToken cancellationToken)
	{
		var previous = Publishing.Value;
		var frame = new PublishingFrame(this, name, previous);
		Publishing.Value = frame;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			await publish().ConfigureAwait(false);
			var success = new DestinationOutcome(null, false);
			_terminal[name] = success;
			return success;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			var classified = (_failureClassifier ?? DefaultClassifier).Classify(exception);
			MessageFailureKind? kind = classified is MessageFailureKind.Transient or MessageFailureKind.Permanent or MessageFailureKind.Poison or MessageFailureKind.Uncertain
				? classified : null;
			var retryable = _canRetry?.Invoke(exception) ?? (kind is MessageFailureKind.Transient or MessageFailureKind.Uncertain);
			var outcome = new DestinationOutcome(ExceptionDispatchInfo.Capture(exception), retryable, kind);
			if (!retryable)
			{
				_terminal[name] = outcome;
			}
			return outcome;
		}
		finally
		{
			Interlocked.Exchange(ref frame.Active, 0);
			Publishing.Value = previous;
		}
	}

	internal sealed record DestinationOutcome(ExceptionDispatchInfo? Fault, bool Retryable, MessageFailureKind? FailureKind = null);
	internal sealed record ResultEvidence(bool Retryable, MessageFailureKind? Kind, Exception? Fault);
	private sealed class PublishingFrame(RetryOwnershipScope owner, string name, PublishingFrame? previous)
	{
		internal RetryOwnershipScope Owner { get; } = owner;
		internal string Name { get; } = name;
		internal PublishingFrame? Previous { get; } = previous;
		internal int Active = 1;
	}
	internal sealed class AttemptState
	{
		internal readonly ConcurrentDictionary<string, Lazy<Task<DestinationOutcome>>> Publications = new(StringComparer.OrdinalIgnoreCase);
		internal readonly ConcurrentDictionary<IMessageResult, ResultEvidence> Results = new(ReferenceEqualityComparer.Instance);
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _active, 0) != 0)
		{
			// Captured child flows see the inactive lifetime as well; they cannot retain a stale owner.
			Current.Value = _previous;
		}
	}
}
