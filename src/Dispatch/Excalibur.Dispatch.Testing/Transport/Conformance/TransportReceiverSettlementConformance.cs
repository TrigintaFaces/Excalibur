// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Transport;

public abstract partial class TransportReceiverConformanceTests
{
	private static readonly TimeSpan SettlementStageTimeout = TimeSpan.FromSeconds(30);

	/// <summary>Declares the settlement outcome supported by a provider fixture.</summary>
	protected sealed class SettlementExpectation
	{
		private readonly Outcome _kind;
		private readonly TransportRedeliveryExpectation _redelivery;
		private readonly SettlementRetryability _retryability;

		private SettlementExpectation(Outcome kind, TransportRedeliveryExpectation redelivery = default, SettlementRetryability retryability = default)
		{
			_kind = kind;
			_redelivery = redelivery;
			_retryability = retryability;
		}

		/// <summary>Gets the expectation that settlement completes normally.</summary>
		public static SettlementExpectation Completed { get; } = new(Outcome.Completed);

		/// <summary>Gets the expectation of cancellation on the requested caller token.</summary>
		public static SettlementExpectation CallerCanceled { get; } = new(Outcome.CallerCanceled);

		internal bool IsCompleted => _kind == Outcome.Completed;
		internal bool IsCallerCanceled => _kind == Outcome.CallerCanceled;
		internal TransportRedeliveryExpectation Redelivery => _redelivery;
		internal SettlementRetryability Retryability => _retryability;

		/// <summary>Declares a portable refusal with explicitly supported outcome axes.</summary>
		public static SettlementExpectation Refused(TransportRedeliveryExpectation redelivery, SettlementRetryability retryability)
		{
			ValidateAxes(redelivery, retryability);
			return new SettlementExpectation(Outcome.Refused, redelivery, retryability);
		}

		private enum Outcome { Completed, CallerCanceled, Refused }
	}

	/// <summary>Verifies portable refusal and independent effects, then acknowledges an unrelated valid receipt.</summary>
	/// <remarks>Each asynchronous setup, operation, observation, and disposal stage is bounded to 30 seconds.</remarks>
	protected Task VerifySettlementRefusal(MessageAction action,
		Func<ITransportReceiver, Task<TransportReceivedMessage>> arrangeRefusedMessage,
		TransportRedeliveryExpectation expectedRedelivery, SettlementRetryability expectedRetryability,
		Func<ITransportReceiver, TransportReceivedMessage, Task> verifyNoEffect,
		Func<ITransportReceiver, TransportReceivedMessage, Task> verifyValidEffect)
	{
		ValidateAction(action);
		ValidateAxes(expectedRedelivery, expectedRetryability);
		ArgumentNullException.ThrowIfNull(arrangeRefusedMessage);
		ArgumentNullException.ThrowIfNull(verifyNoEffect);
		ArgumentNullException.ThrowIfNull(verifyValidEffect);
		return WithSettlementReceiverAsync(SettlementStageTimeout, async (receiver, operations) =>
		{
			var refused = await BoundStageAsync(() => arrangeRefusedMessage(receiver), SettlementStageTimeout, "arrange refused receipt").ConfigureAwait(false);
			ArgumentNullException.ThrowIfNull(refused);
			await CheckSettlementAsync(StartSettlement(operations, receiver, refused, action, CancellationToken.None),
				SettlementExpectation.Refused(expectedRedelivery, expectedRetryability), null, SettlementStageTimeout, CancellationToken.None).ConfigureAwait(false);
			await ObserveSettlementAsync(() => verifyNoEffect(receiver, refused), SettlementStageTimeout, "refusal effects").ConfigureAwait(false);
			var valid = await BoundStageAsync(() => ReceiveExactlyOneAsync(receiver, "unrelated-valid"), SettlementStageTimeout, "receive unrelated valid receipt").ConfigureAwait(false);
			if (ReferenceEquals(valid, refused))
			{
				throw new InvalidOperationException("The refusal fixture must provide an unrelated valid receipt.");
			}
			await CheckSettlementAsync(StartSettlement(operations, receiver, valid, MessageAction.Acknowledge, CancellationToken.None),
				SettlementExpectation.Completed, null, SettlementStageTimeout, CancellationToken.None).ConfigureAwait(false);
			await ObserveSettlementAsync(() => verifyValidEffect(receiver, valid), SettlementStageTimeout, "unrelated valid receipt effects").ConfigureAwait(false);
		});
	}

	/// <summary>Verifies an arranged native failure retains its exact cause at the portable boundary.</summary>
	/// <remarks>Each asynchronous stage is bounded to 30 seconds.</remarks>
	protected Task VerifyNativeSettlementFailure(MessageAction action,
		Func<ITransportReceiver, TransportReceivedMessage, Task<Exception>> arrangeFailure,
		TransportRedeliveryExpectation expectedRedelivery, SettlementRetryability expectedRetryability,
		Func<ITransportReceiver, TransportReceivedMessage, Task> verifyEffect)
	{
		ValidateAction(action);
		ValidateAxes(expectedRedelivery, expectedRetryability);
		ArgumentNullException.ThrowIfNull(arrangeFailure);
		ArgumentNullException.ThrowIfNull(verifyEffect);
		return WithSettlementReceiverAsync(SettlementStageTimeout, async (receiver, operations) =>
		{
			var message = await BoundStageAsync(() => ReceiveExactlyOneAsync(receiver, "native-failure"), SettlementStageTimeout, "receive failure receipt").ConfigureAwait(false);
			var cause = await BoundStageAsync(() => arrangeFailure(receiver, message), SettlementStageTimeout, "arrange native failure").ConfigureAwait(false);
			ArgumentNullException.ThrowIfNull(cause);
			await CheckSettlementAsync(StartSettlement(operations, receiver, message, action, CancellationToken.None),
				SettlementExpectation.Refused(expectedRedelivery, expectedRetryability), cause, SettlementStageTimeout, CancellationToken.None).ConfigureAwait(false);
			await ObserveSettlementAsync(() => verifyEffect(receiver, message), SettlementStageTimeout, "native failure effects").ConfigureAwait(false);
		});
	}

	/// <summary>Verifies first settlement and the provider's explicit repeated-settlement policy.</summary>
	/// <remarks>Each asynchronous stage is bounded to 30 seconds. Both observations must independently check effects.</remarks>
	protected Task VerifyRepeatedSettlement(MessageAction action, SettlementExpectation expectedRepeated,
		Func<ITransportReceiver, TransportReceivedMessage, Task> verifyFirstEffect,
		Func<ITransportReceiver, TransportReceivedMessage, Task> verifyRepeatedEffect)
	{
		ValidateAction(action);
		ArgumentNullException.ThrowIfNull(expectedRepeated);
		RequireOwnedCaller(expectedRepeated, nameof(expectedRepeated));
		ArgumentNullException.ThrowIfNull(verifyFirstEffect);
		ArgumentNullException.ThrowIfNull(verifyRepeatedEffect);
		return WithSettlementReceiverAsync(SettlementStageTimeout, async (receiver, operations) =>
		{
			var message = await BoundStageAsync(() => ReceiveExactlyOneAsync(receiver, "repeated-settlement"), SettlementStageTimeout, "receive repeated receipt").ConfigureAwait(false);
			await CheckSettlementAsync(StartSettlement(operations, receiver, message, action, CancellationToken.None), SettlementExpectation.Completed,
				null, SettlementStageTimeout, CancellationToken.None).ConfigureAwait(false);
			await ObserveSettlementAsync(() => verifyFirstEffect(receiver, message), SettlementStageTimeout, "first settlement effects").ConfigureAwait(false);
			await CheckSettlementAsync(StartSettlement(operations, receiver, message, action, CancellationToken.None), expectedRepeated,
				null, SettlementStageTimeout, CancellationToken.None).ConfigureAwait(false);
			await ObserveSettlementAsync(() => verifyRepeatedEffect(receiver, message), SettlementStageTimeout, "repeated settlement effects").ConfigureAwait(false);
		});
	}

	/// <summary>Verifies declared cancellation behavior at an independently observed native boundary.</summary>
	/// <remarks>
	/// Native settlement runs on the thread pool with ExecutionContext flow, without preserving caller thread affinity.
	/// Hooks must return promptly. Boundary hooks passively return existing fixture signals; their underlying tasks are
	/// borrowed observations, not provider operations owned by the helper. Timeout bounds a wait, not native cancellation.
	/// Release must be idempotent. The helper releases and drains native attempts, then cancels its own signal reads.
	/// </remarks>
	protected Task VerifySettlementCancellation(MessageAction action, SettlementExpectation expected,
		Func<ITransportReceiver, TransportReceivedMessage, Task> arrangePause,
		Func<Task> waitForNativeBoundary, Func<Task> release,
		Func<ITransportReceiver, TransportReceivedMessage, Task> verifyEffect, TimeSpan timeout)
	{
		ValidateAction(action);
		ValidateTimeout(timeout);
		ArgumentNullException.ThrowIfNull(expected);
		ArgumentNullException.ThrowIfNull(arrangePause);
		ArgumentNullException.ThrowIfNull(waitForNativeBoundary);
		ArgumentNullException.ThrowIfNull(release);
		ArgumentNullException.ThrowIfNull(verifyEffect);
		return WithSettlementReceiverAsync(timeout, async (receiver, operations) =>
		{
			using var caller = new CancellationTokenSource();
			using var observations = new CancellationTokenSource();
			Task? signal = null;
			Task? operation = null;
			Task? boundary = null;
			Exception? primary = null;
			try
			{
				var message = await BoundStageAsync(() => ReceiveExactlyOneAsync(receiver, "caller-cancellation"), timeout, "receive cancellation receipt").ConfigureAwait(false);
				await BoundStageAsync(() => arrangePause(receiver, message), timeout, "arrange cancellation pause").ConfigureAwait(false);
				operation = StartSettlement(operations, receiver, message, action, caller.Token);
				signal = StartStage(waitForNativeBoundary);
				boundary = signal.WaitAsync(observations.Token);
				await RequirePendingBoundaryAsync(boundary, operation, timeout).ConfigureAwait(false);
				await BoundStageAsync(() => caller.CancelAsync(), timeout, "cancel owned caller").ConfigureAwait(false);
				await BoundStageAsync(release, timeout, "release canceled native operation").ConfigureAwait(false);
				await CheckSettlementAsync(operation, expected, null, timeout, caller.Token).ConfigureAwait(false);
				await ObserveSettlementAsync(() => verifyEffect(receiver, message), timeout, "cancellation effects").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				primary = ex;
				throw;
			}
			finally
			{
				await CleanupSettlementAsync(release, new[] { operation }, new[] { boundary }, new[] { signal }, observations, timeout, primary).ConfigureAwait(false);
			}
		});
	}

	/// <summary>Verifies competing outcomes with fixture-controlled entry and release boundaries.</summary>
	/// <remarks>
	/// Native attempts run on the thread pool; caller SynchronizationContext affinity is not certified.
	/// Hooks return promptly and only observe existing fixture signals. A completed second attempt can establish invocation
	/// without a native-entry signal. The helper owns cancellable signal reads, not the underlying borrowed signal tasks.
	/// Release is idempotent; every started native attempt is drained or reported as unfinished cleanup uncertainty.
	/// </remarks>
	protected Task VerifyCompetingSettlements(MessageAction firstAction, MessageAction secondAction,
		SettlementExpectation firstExpected, SettlementExpectation secondExpected,
		Func<ITransportReceiver, TransportReceivedMessage, Task> arrangePause,
		Func<Task> waitFirstBoundary, Func<Task> waitSecondInvocation, Func<Task> release,
		Func<ITransportReceiver, TransportReceivedMessage, Task> verifyEffects, TimeSpan timeout)
	{
		ValidateAction(firstAction);
		ValidateAction(secondAction);
		ValidateTimeout(timeout);
		ArgumentNullException.ThrowIfNull(firstExpected);
		RequireOwnedCaller(firstExpected, nameof(firstExpected));
		ArgumentNullException.ThrowIfNull(secondExpected);
		RequireOwnedCaller(secondExpected, nameof(secondExpected));
		ArgumentNullException.ThrowIfNull(arrangePause);
		ArgumentNullException.ThrowIfNull(waitFirstBoundary);
		ArgumentNullException.ThrowIfNull(waitSecondInvocation);
		ArgumentNullException.ThrowIfNull(release);
		ArgumentNullException.ThrowIfNull(verifyEffects);
		return WithSettlementReceiverAsync(timeout, async (receiver, operations) =>
		{
			using var observations = new CancellationTokenSource();
			Task? firstSignal = null;
			Task? secondSignal = null;
			Task? first = null;
			Task? second = null;
			Task? firstBoundary = null;
			Task? secondInvocation = null;
			Exception? primary = null;
			try
			{
				var message = await BoundStageAsync(() => ReceiveExactlyOneAsync(receiver, "competing-settlement"), timeout, "receive competing receipt").ConfigureAwait(false);
				await BoundStageAsync(() => arrangePause(receiver, message), timeout, "arrange competing pause").ConfigureAwait(false);
				first = StartSettlement(operations, receiver, message, firstAction, CancellationToken.None);
				firstSignal = StartStage(waitFirstBoundary);
				firstBoundary = firstSignal.WaitAsync(observations.Token);
				await RequirePendingBoundaryAsync(firstBoundary, first, timeout).ConfigureAwait(false);
				second = StartSettlement(operations, receiver, message, secondAction, CancellationToken.None);
				secondSignal = StartStage(waitSecondInvocation);
				secondInvocation = secondSignal.WaitAsync(observations.Token);
				await BoundStageAsync(() => Task.WhenAny(second, secondInvocation), timeout, "observe competing invocation").ConfigureAwait(false);
				if (!second.IsCompleted)
				{
					await BoundStageAsync(() => secondInvocation, timeout, "confirm competing invocation").ConfigureAwait(false);
				}
				if (first.IsCompleted)
				{
					throw new InvalidOperationException("The first settlement completed before the competing invocation was observed.");
				}
				await BoundStageAsync(release, timeout, "release competing owner").ConfigureAwait(false);
				await CheckSettlementAsync(first, firstExpected, null, timeout, CancellationToken.None).ConfigureAwait(false);
				await CheckSettlementAsync(second, secondExpected, null, timeout, CancellationToken.None).ConfigureAwait(false);
				await ObserveSettlementAsync(() => verifyEffects(receiver, message), timeout, "competing settlement effects").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				primary = ex;
				throw;
			}
			finally
			{
				await CleanupSettlementAsync(release, new[] { first, second }, new[] { firstBoundary, secondInvocation },
					new[] { firstSignal, secondSignal }, observations, timeout, primary).ConfigureAwait(false);
			}
		});
	}

	/// <summary>Verifies first failure effects, independent healthy-batch settlement, and a declared failed-receipt retry.</summary>
	/// <remarks>The timeout bounds each asynchronous stage. Received deliveries must be distinct objects; public IDs may match.</remarks>
	protected Task VerifySettlementBatchRetry(MessageAction failedAction, MessageAction healthyAction,
		Func<ITransportReceiver, TransportReceivedMessage, Task<Exception>> arrangeFailure,
		SettlementExpectation failureExpected, SettlementExpectation? retryExpected,
		Func<ITransportReceiver, TransportReceivedMessage, Task> verifyFailureEffect,
		Func<ITransportReceiver, IReadOnlyList<TransportReceivedMessage>, Task> verifyEffects, TimeSpan timeout)
	{
		ValidateAction(failedAction);
		ValidateAction(healthyAction);
		ValidateTimeout(timeout);
		ArgumentNullException.ThrowIfNull(arrangeFailure);
		ArgumentNullException.ThrowIfNull(failureExpected);
		if (retryExpected is not null)
		{
			RequireOwnedCaller(retryExpected, nameof(retryExpected));
		}
		ArgumentNullException.ThrowIfNull(verifyFailureEffect);
		ArgumentNullException.ThrowIfNull(verifyEffects);
		if (failureExpected.IsCompleted || failureExpected.IsCallerCanceled)
		{
			throw new ArgumentException("A native-failure batch requires a portable refusal expectation.", nameof(failureExpected));
		}
		return WithSettlementReceiverAsync(timeout, async (receiver, operations) =>
		{
			await BoundStageAsync(() => SeedMessagesAsync(receiver, new[] { CreateTestMessage("batch-failed"), CreateTestMessage("batch-healthy") }), timeout, "seed batch").ConfigureAwait(false);
			var messages = await BoundStageAsync(() => receiver.ReceiveAsync(2, CancellationToken.None), timeout, "receive batch").ConfigureAwait(false);
			if (messages.Count != 2 || messages[0] is null || messages[1] is null || ReferenceEquals(messages[0], messages[1]))
			{
				throw new InvalidOperationException("Batch verification requires exactly two distinct received delivery objects.");
			}
			var cause = await BoundStageAsync(() => arrangeFailure(receiver, messages[0]), timeout, "arrange first batch failure").ConfigureAwait(false);
			ArgumentNullException.ThrowIfNull(cause);
			await CheckSettlementAsync(StartSettlement(operations, receiver, messages[0], failedAction, CancellationToken.None), failureExpected,
				cause, timeout, CancellationToken.None).ConfigureAwait(false);
			await ObserveSettlementAsync(() => verifyFailureEffect(receiver, messages[0]), timeout, "first batch failure effects").ConfigureAwait(false);
			await CheckSettlementAsync(StartSettlement(operations, receiver, messages[1], healthyAction, CancellationToken.None), SettlementExpectation.Completed,
				null, timeout, CancellationToken.None).ConfigureAwait(false);
			if (retryExpected is not null)
			{
				await CheckSettlementAsync(StartSettlement(operations, receiver, messages[0], failedAction, CancellationToken.None), retryExpected,
					null, timeout, CancellationToken.None).ConfigureAwait(false);
			}
			await ObserveSettlementAsync(() => verifyEffects(receiver, messages), timeout, "batch settlement effects").ConfigureAwait(false);
		});
	}

#pragma warning disable RS0030 // Owned testing seam: a synchronous native pause must not block the harness's timeout/release path; this task is tracked and drained, not fire-and-forget.
	private static Task StartSettlement(List<Task> operations, ITransportReceiver receiver, TransportReceivedMessage message, MessageAction action, CancellationToken token)
	{
		var operation = Task.Run(() => action switch
		{
			MessageAction.Acknowledge => receiver.AcknowledgeAsync(message, token),
			MessageAction.Reject => receiver.RejectAsync(message, "conformance", false, token),
			MessageAction.Requeue => receiver.RejectAsync(message, "conformance", true, token),
			_ => throw new ArgumentOutOfRangeException(nameof(action)),
		});
		operations.Add(operation);
		return operation;
	}
#pragma warning restore RS0030

	private static Task StartStage(Func<Task> stage)
	{
		try
		{
			return stage() ?? Task.FromException(new InvalidOperationException("A fixture returned a null task."));
		}
		catch (Exception ex)
		{
			return Task.FromException(ex);
		}
	}

	private static async Task CheckSettlementAsync(Task operation, SettlementExpectation expected, Exception? nativeCause, TimeSpan timeout, CancellationToken caller)
	{
		Exception? failure = null;
		try
		{
			await operation.WaitAsync(timeout).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			if (ex is TimeoutException && !operation.IsCompleted)
			{
				throw new InvalidOperationException("Settlement did not finish within the fixture timeout.", ex);
			}
			failure = ex;
		}
		if (expected.IsCompleted)
		{
			if (failure is not null)
			{
				throw new InvalidOperationException("Settlement was expected to complete normally.", failure);
			}
			return;
		}
		if (expected.IsCallerCanceled)
		{
			if (failure is not OperationCanceledException canceled || !caller.IsCancellationRequested || canceled.CancellationToken != caller)
			{
				throw new InvalidOperationException("Settlement must cancel on the exact requested caller token.", failure);
			}
			return;
		}
		if (failure is not TransportSettlementException portable || string.IsNullOrWhiteSpace(portable.TransportName)
			|| portable.RedeliveryExpectation != expected.Redelivery || portable.Retryability != expected.Retryability)
		{
			throw new InvalidOperationException("Settlement must report a portable failure with the declared transport and outcome axes.", failure);
		}
		if (nativeCause is not null && !ReferenceEquals(portable.InnerException, nativeCause))
		{
			throw new InvalidOperationException("Settlement must retain the exact arranged native exception as InnerException.", portable);
		}
	}

	private static async Task RequirePendingBoundaryAsync(Task boundary, Task operation, TimeSpan timeout)
	{
		await BoundStageAsync(() => Task.WhenAny(boundary, operation), timeout, "reach pending native boundary").ConfigureAwait(false);
		if (operation.IsCompleted)
		{
			throw new InvalidOperationException("Settlement completed before the required paused native boundary.");
		}
		await BoundStageAsync(() => boundary, timeout, "confirm native boundary").ConfigureAwait(false);
	}

	private static async Task ObserveSettlementAsync(Func<Task> observation, TimeSpan timeout, string stage)
	{
		try
		{
			await BoundStageAsync(observation, timeout, stage).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Independent fixture observation failed: {stage}.", ex);
		}
	}

	private static async Task BoundStageAsync(Func<Task> stage, TimeSpan timeout, string description)
	{
		var task = StartStage(stage);
		try
		{
			await task.WaitAsync(timeout).ConfigureAwait(false);
		}
		catch (TimeoutException ex) when (!task.IsCompleted)
		{
			throw new InvalidOperationException($"Fixture stage timed out: {description}.", ex);
		}
	}

	private static async Task<T> BoundStageAsync<T>(Func<Task<T>> stage, TimeSpan timeout, string description)
	{
		var task = stage() ?? throw new InvalidOperationException($"Fixture returned a null task: {description}.");
		try
		{
			return await task.WaitAsync(timeout).ConfigureAwait(false);
		}
		catch (TimeoutException ex) when (!task.IsCompleted)
		{
			throw new InvalidOperationException($"Fixture stage timed out: {description}.", ex);
		}
	}

	private async Task WithSettlementReceiverAsync(TimeSpan timeout, Func<ITransportReceiver, List<Task>, Task> scenario)
	{
		var receiver = await BoundStageAsync(CreateReceiverAsync, timeout, "create receiver").ConfigureAwait(false);
		ArgumentNullException.ThrowIfNull(receiver);
		var operations = new List<Task>();
		Exception? primary = null;
		try
		{
			await scenario(receiver, operations).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			primary = ex;
		}
		var failures = new List<Exception>();
		foreach (var operation in operations)
		{
			try
			{
				await operation.WaitAsync(timeout).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				if (!operation.IsCompleted)
				{
					failures.Add(new InvalidOperationException("A native settlement remained unfinished after bounded drain; its completion is uncertain.", ex));
					// The failed harness cannot cancel arbitrary native work. Observe any eventual fault while
					// retaining the explicit unfinished-task uncertainty rather than disposing resources beneath it.
					_ = operation.ContinueWith(static completed => { _ = completed.Exception; }, CancellationToken.None,
						TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
				}
				else if (primary is not null)
				{
					failures.Add(ex);
				}
			}
		}
		// Never dispose a receiver while a known native attempt may still be using its resources.
		if (operations.All(operation => operation.IsCompleted))
		{
			try
			{
				await BoundStageAsync(() => receiver.DisposeAsync().AsTask(), timeout, "dispose receiver").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				failures.Add(ex);
			}
		}
		if (failures.Count != 0)
		{
			var cleanup = new AggregateException("Receiver native-operation drain or disposal failed.", failures);
			if (primary is not null)
			{
				primary.Data["SettlementConformanceReceiverCleanupFailure"] = cleanup;
			}
			else
			{
				throw new InvalidOperationException("Receiver native-operation drain or disposal failed.", cleanup);
			}
		}
		if (primary is not null)
		{
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
		}
	}

	private static async Task CleanupSettlementAsync(Func<Task> release, IReadOnlyList<Task?> nativeOperations,
		IReadOnlyList<Task?> observationReads, IReadOnlyList<Task?> borrowedSignals, CancellationTokenSource observations,
		TimeSpan timeout, Exception? primary)
	{
		var failures = new List<Exception>();
		try
		{
			await BoundStageAsync(release, timeout, "cleanup release").ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			failures.Add(ex);
		}
		// A completed borrowed observation fault must not disappear behind cancellation of its read.
		foreach (var signal in borrowedSignals)
		{
			if (signal?.IsFaulted == true)
			{
				failures.Add(signal.Exception!);
			}
			else if (signal?.IsCanceled == true)
			{
				failures.Add(new InvalidOperationException("A borrowed fixture signal was canceled independently of the helper."));
			}
		}
		try
		{
			await BoundStageAsync(() => observations.CancelAsync(), timeout, "cancel owned observation reads").ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			failures.Add(ex);
		}
		foreach (var task in nativeOperations.Concat(observationReads))
		{
			if (task is null)
			{
				continue;
			}
			try
			{
				await task.WaitAsync(timeout).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				if (!task.IsCompleted)
				{
					failures.Add(new InvalidOperationException("A native or owned observation task remained unfinished after cleanup release.", ex));
				}
				else if (observationReads.Contains(task)
					&& !(ex is OperationCanceledException canceled && observations.IsCancellationRequested
						&& canceled.CancellationToken == observations.Token))
				{
					failures.Add(ex);
				}
				else if (primary is not null && nativeOperations.Contains(task))
				{
					// A primary boundary/observation failure may occur before this outcome was checked.
					// Retain the completed native failure as cleanup evidence without replacing the primary.
					failures.Add(ex);
				}
				// Completed native outcomes were asserted by the scenario. Borrowed-signal faults are retained above;
				// cancellation of an owned read ends observation only and never certifies provider cancellation.
			}
		}
		// Faults can race the initial signal snapshot and cancellation of an owned read.
		foreach (var signal in borrowedSignals)
		{
			if (signal?.IsFaulted == true && !failures.Contains(signal.Exception!))
			{
				failures.Add(signal.Exception!);
			}
		}
		if (failures.Count != 0)
		{
			var cleanup = new AggregateException("Settlement fixture cleanup failed.", failures);
			if (primary is not null)
			{
				primary.Data["SettlementConformanceCleanupFailure"] = cleanup;
			}
			else
			{
				throw new InvalidOperationException("Settlement fixture cleanup failed.", cleanup);
			}
		}
	}

	private static void RequireOwnedCaller(SettlementExpectation expectation, string parameterName)
	{
		if (expectation.IsCallerCanceled)
		{
			throw new ArgumentException("CallerCanceled requires the cancellation helper's owned caller token.", parameterName);
		}
	}

	private static void ValidateAction(MessageAction action)
	{
		if (!Enum.IsDefined(action))
		{
			throw new ArgumentOutOfRangeException(nameof(action));
		}
	}

	private static void ValidateAxes(TransportRedeliveryExpectation redelivery, SettlementRetryability retryability)
	{
		if (!Enum.IsDefined(redelivery))
		{
			throw new ArgumentOutOfRangeException(nameof(redelivery));
		}
		if (!Enum.IsDefined(retryability))
		{
			throw new ArgumentOutOfRangeException(nameof(retryability));
		}
	}

	private static void ValidateTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), "Fixture timeout must be positive and supported by a bounded task wait.");
		}
	}
}
