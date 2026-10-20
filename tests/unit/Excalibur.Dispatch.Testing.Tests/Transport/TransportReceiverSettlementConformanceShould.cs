// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Tests.Transport;

[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Testing)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class TransportReceiverSettlementConformanceShould : TransportReceiverConformanceTests, IAsyncDisposable
{
	private static readonly TimeSpan BoundaryTimeout = TimeSpan.FromSeconds(2);
	private readonly ProbeReceiver _probe = new();

	public ValueTask DisposeAsync() => _probe.DisposeAsync();

	protected override Task<ITransportReceiver> CreateReceiverAsync() => Task.FromResult<ITransportReceiver>(_probe);

	protected override Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
	{
		((ProbeReceiver)receiver).Seed(messages);
		return Task.CompletedTask;
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public Task AcceptInvalidReceiptRefusal_AndObserveUnrelatedValidAcknowledgment(MessageAction action) => Refusal(action, expired: false);

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public Task AcceptExpiredReceiptRefusal_AfterARealDeliveryTransition(MessageAction action) => Refusal(action, expired: true);

	[Theory]
	[InlineData("always-refuse")]
	[InlineData("noop")]
	[InlineData("wrong-axes")]
	public async Task RejectBrokenReceiptRefusalOrUnrelatedReceiptLiveness(string defect)
	{
		_probe.Defect = defect;
		var failure = await Should.ThrowAsync<InvalidOperationException>(() => Refusal(MessageAction.Acknowledge, expired: true));
		failure.Message.ShouldContain(defect switch
		{
			"always-refuse" => "expected to complete normally",
			"noop" => "unrelated valid receipt effects",
			_ => "portable failure",
		});
		if (defect == "noop")
		{
			failure.InnerException.ShouldBeOfType<Shouldly.ShouldAssertException>();
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task ObserveFirstEffect_AndDeclaredRepeatedSettlementPolicy(bool completedDuplicate)
	{
		_probe.CompletedDuplicate = completedDuplicate;
		return Repeated();
	}

	[Fact]
	public async Task RejectNoOpFirstSettlement_BeforeCheckingDuplicatePolicy()
	{
		_probe.Defect = "noop";
		var failure = await Should.ThrowAsync<InvalidOperationException>(Repeated);
		failure.Message.ShouldContain("first settlement effects");
		failure.InnerException.ShouldBeOfType<Shouldly.ShouldAssertException>();
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public Task PreserveExactNativeCause_AndDeclaredFailureAxes(MessageAction action) => NativeFailure(action);

	[Theory]
	[InlineData("wrong-cause")]
	[InlineData("wrong-axes")]
	[InlineData("blank-transport")]
	[InlineData("raw-native")]
	public async Task RejectBrokenNativeFailureBoundary(string defect)
	{
		_probe.Defect = defect;
		var failure = await Should.ThrowAsync<InvalidOperationException>(() => NativeFailure(MessageAction.Requeue));
		failure.Message.ShouldContain(defect == "wrong-cause" ? "exact arranged native exception" : "portable failure");
		_probe.Attempts.Count.ShouldBe(1);
		if (defect == "wrong-cause")
		{
			failure.InnerException.ShouldBeOfType<TransportSettlementException>().InnerException.ShouldNotBeSameAs(_probe.NativeCause);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task HonourDeclaredCallerCancellation_AtPausedNativeBoundary(bool ignoresCancellation)
	{
		_probe.IgnoreCancellation = ignoresCancellation;
		return Cancellation(ignoresCancellation ? SettlementExpectation.Completed : SettlementExpectation.CallerCanceled);
	}

	[Theory]
	[InlineData("wrong-token")]
	[InlineData("tokenless")]
	[InlineData("premature")]
	[InlineData("missing-boundary")]
	public async Task RejectUnrelatedCancellationOrPrematureCompletion(string defect)
	{
		_probe.Defect = defect;
		var failure = await Should.ThrowAsync<InvalidOperationException>(() => Cancellation(SettlementExpectation.CallerCanceled));
		if (defect == "missing-boundary")
		{
			failure.Message.ShouldContain("reach pending native boundary");
		}
		else if (defect == "premature")
		{
			(failure.Message.Contains("paused native boundary", StringComparison.Ordinal)
				|| failure.Message.Contains("exact requested caller token", StringComparison.Ordinal)).ShouldBeTrue();
		}
		else
		{
			failure.Message.ShouldContain("exact requested caller token");
			failure.InnerException.ShouldBeAssignableTo<OperationCanceledException>().CancellationToken
				.ShouldNotBe(_probe.Attempts.Single().Token);
		}
		_probe.ReleaseCalls.ShouldBeGreaterThan(0, "the helper must release and drain even when its boundary fails");
		_probe.ActiveCalls.ShouldBe(0);
	}

	[Fact]
	public Task ObserveBothCompetingSettlementAttempts_BeforeOwnerRelease() => Competition();

	[Theory]
	[InlineData("overlap")]
	[InlineData("premature")]
	[InlineData("missing-boundary")]
	public async Task RejectBrokenCompetingSettlementOwnership(string defect)
	{
		_probe.Defect = defect;
		var failure = await Should.ThrowAsync<InvalidOperationException>(Competition);
		if (defect == "overlap")
		{
			failure.Message.ShouldContain("portable failure");
			_probe.Overlapped.ShouldBeTrue();
		}
		else if (defect == "missing-boundary")
		{
			failure.Message.ShouldContain("reach pending native boundary");
		}
		else
		{
			(failure.Message.Contains("completed before", StringComparison.Ordinal)
				|| failure.Message.Contains("competing settlement effects", StringComparison.Ordinal)).ShouldBeTrue();
		}
		_probe.ReleaseCalls.ShouldBeGreaterThan(0);
		_probe.ActiveCalls.ShouldBe(0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task SettleHealthyBatchPeer_AndExerciseDeclaredFailedReceiptRetry(bool retry) => Batch(retry);

	[Theory]
	[InlineData("wrong-delivery")]
	[InlineData("noop")]
	[InlineData("wrong-retry")]
	[InlineData("wrong-cause")]
	public async Task RejectBatchWideNoOpWrongDeliveryOrWrongRetry(string defect)
	{
		_probe.Defect = defect;
		var failure = await Should.ThrowAsync<InvalidOperationException>(() => Batch(retry: true));
		failure.Message.ShouldContain(defect switch
		{
			"wrong-cause" => "exact arranged native exception",
			"wrong-retry" => "expected to complete normally",
			_ => "batch settlement effects",
		});
		if (defect is "noop" or "wrong-delivery")
		{
			failure.InnerException.ShouldBeOfType<Shouldly.ShouldAssertException>();
		}
	}

	[Fact]
	public Task AcceptDeclaredAlternativeReceiptFailureAxes_WithoutLosingValidReceiptEffect()
	{
		_probe.ReceiptRefusalRetryability = SettlementRetryability.Unspecified;
		return Refusal(MessageAction.Requeue, expired: true);
	}

	[Fact]
	public Task ReleaseAndDrainASynchronouslyPausedNativeSettlement()
	{
		_probe.SynchronousPause = true;
		return Cancellation(SettlementExpectation.CallerCanceled);
	}

	[Fact]
	public async Task AcceptEarlyPortableLoser_WithoutAnOptionalNativeEntrySignal()
	{
		_probe.SkipSecondSignal = true;
		await Competition();
		_probe.SecondInvoked.Task.IsCompleted.ShouldBeFalse("the helper must not require native entry after a completed portable refusal");
		_probe.ActiveCalls.ShouldBe(0);
	}

	[Fact]
	public async Task PreservePrimaryObservationFailure_WhenCleanupReleaseAlsoFails()
	{
		_probe.Defect = "noop";
		_probe.IgnoreCancellation = true;
		_probe.FailCleanupRelease = true;
		var failure = await Should.ThrowAsync<InvalidOperationException>(() => Cancellation(SettlementExpectation.Completed));
		failure.Message.ShouldContain("cancellation effects");
		failure.InnerException.ShouldBeOfType<Shouldly.ShouldAssertException>();
		var cleanup = failure.Data["SettlementConformanceCleanupFailure"].ShouldBeOfType<AggregateException>();
		cleanup.InnerExceptions.Any(cause => ReferenceEquals(cause, _probe.CleanupFailure)).ShouldBeTrue();
		_probe.ReleaseCalls.ShouldBe(2);
		_probe.ActiveCalls.ShouldBe(0);
	}

	[Fact]
	public async Task PreserveOptionalSignalFault_DuringCleanupOfAnImmediatePortableLoser()
	{
		_probe.SkipSecondSignal = true;
		_probe.FaultSignalOnCleanup = true;
		var failure = await Should.ThrowAsync<InvalidOperationException>(Competition);
		failure.Message.ShouldContain("Settlement fixture cleanup failed");
		var cleanup = failure.InnerException.ShouldBeOfType<AggregateException>();
		cleanup.Flatten().InnerExceptions.Any(cause => ReferenceEquals(cause, _probe.SignalFailure)).ShouldBeTrue();
		_probe.SecondBeforeRelease.ShouldBeTrue();
		_probe.ActiveCalls.ShouldBe(0);
	}

	[Fact]
	public async Task ReportUnfinishedNativeUncertainty_BeforeDisposingASimpleFailureFixture()
	{
		_probe.HoldNativeFailure = true;
		var verification = NativeFailure(MessageAction.Acknowledge);
		try
		{
			await _probe.FirstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
			var failure = await Should.ThrowAsync<InvalidOperationException>(() => verification.WaitAsync(TimeSpan.FromSeconds(70)));
			failure.Message.ShouldContain("Settlement did not finish");
			var cleanup = failure.Data["SettlementConformanceReceiverCleanupFailure"].ShouldBeOfType<AggregateException>();
			cleanup.InnerExceptions.Any(cause => cause.Message.Contains("remained unfinished", StringComparison.Ordinal)).ShouldBeTrue();
			_probe.ActiveCalls.ShouldBe(1, "a harness timeout does not certify native cancellation");
			_probe.DisposeCalls.ShouldBe(0, "an unfinished native attempt still owns the receiver resources");
		}
		finally
		{
			await _probe.ReleaseAsync();
			await _probe.NativeExited.Task.WaitAsync(TimeSpan.FromSeconds(5));
		}
	}

	[Fact]
	public async Task RejectCallerCanceledProfiles_WhenTheHelperOwnsNoCanceledCallerToken()
	{
		await Should.ThrowAsync<ArgumentException>(() => VerifyRepeatedSettlement(MessageAction.Acknowledge,
			SettlementExpectation.CallerCanceled, ObserveFirstEffect, ObserveFirstEffect));
		await Should.ThrowAsync<ArgumentException>(() => VerifyCompetingSettlements(MessageAction.Acknowledge, MessageAction.Requeue,
			SettlementExpectation.Completed, SettlementExpectation.CallerCanceled, ArrangePause,
			() => _probe.FirstEntered.Task, () => _probe.SecondInvoked.Task, _probe.ReleaseAsync, ObserveFirstEffect, BoundaryTimeout));
		await Should.ThrowAsync<ArgumentException>(() => VerifySettlementBatchRetry(MessageAction.Acknowledge, MessageAction.Reject,
			ArrangeFailure, RefusalExpectation(), SettlementExpectation.CallerCanceled, ObserveFailure,
			(_, _) => Task.CompletedTask, BoundaryTimeout));
	}

	[Fact]
	public async Task RejectNullRequiredArrangementAndObservationHooks()
	{
		await Should.ThrowAsync<ArgumentNullException>(() => VerifyNativeSettlementFailure(MessageAction.Acknowledge, null!,
			TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Unspecified, ObserveFailure));
		await Should.ThrowAsync<ArgumentNullException>(() => VerifySettlementRefusal(MessageAction.Acknowledge,
			_ => Task.FromResult(new TransportReceivedMessage()), TransportRedeliveryExpectation.Unspecified,
			SettlementRetryability.Permanent, null!, ObserveFirstEffect));
	}

	[Fact]
	public void RejectUndefinedExpectationAxes()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => SettlementExpectation.Refused((TransportRedeliveryExpectation)999, SettlementRetryability.Permanent));
		Should.Throw<ArgumentOutOfRangeException>(() => SettlementExpectation.Refused(TransportRedeliveryExpectation.Unspecified, (SettlementRetryability)999));
	}

	[Fact]
	public async Task RejectUndefinedActionAndNonpositiveTimeout()
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() => VerifyRepeatedSettlement((MessageAction)999,
			SettlementExpectation.Completed, ObserveFirstEffect, ObserveFirstEffect));
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() => VerifySettlementCancellation(MessageAction.Acknowledge,
			SettlementExpectation.CallerCanceled, ArrangePause, () => _probe.FirstEntered.Task, _probe.ReleaseAsync,
			ObserveFirstEffect, TimeSpan.Zero));
	}

	private Task Refusal(MessageAction action, bool expired) => VerifySettlementRefusal(action, async receiver =>
	{
		if (!expired)
		{
			return new TransportReceivedMessage { Id = "invalid" };
		}
		var seeded = CreateTestMessage("expired");
		await SeedMessagesAsync(receiver, new[] { seeded });
		var original = (await receiver.ReceiveAsync(1, CancellationToken.None))[0];
		_probe.Expire(original);
		return original;
	}, TransportRedeliveryExpectation.Unspecified, _probe.ReceiptRefusalRetryability,
	(_, message) =>
	{
		_probe.Effects.Any(effect => ReferenceEquals(effect.Message, message)).ShouldBeFalse();
		return Task.CompletedTask;
	}, ObserveFirstEffect);

	private Task Repeated() => VerifyRepeatedSettlement(MessageAction.Acknowledge,
		_probe.CompletedDuplicate ? SettlementExpectation.Completed : RefusalExpectation(), ObserveFirstEffect,
		(_, message) =>
		{
			_probe.Effects.Count(effect => ReferenceEquals(effect.Message, message)).ShouldBe(_probe.CompletedDuplicate ? 2 : 1);
			_probe.Attempts.Count.ShouldBe(2, "duplicate policy must be exercised after observing first success");
			return Task.CompletedTask;
		});

	private Task NativeFailure(MessageAction action) => VerifyNativeSettlementFailure(action, ArrangeFailure,
		TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Unspecified, ObserveFailure);

	private Task Cancellation(SettlementExpectation expected) => VerifySettlementCancellation(MessageAction.Acknowledge,
		expected, ArrangePause, () => _probe.FirstEntered.Task, _probe.ReleaseAsync, (_, message) =>
		{
			_probe.Attempts.Single().Message.ShouldBeSameAs(message);
			_probe.Attempts.Single().Token.IsCancellationRequested.ShouldBeTrue();
			_probe.Effects.Count.ShouldBe(_probe.IgnoreCancellation ? 1 : 0);
			_probe.ActiveCalls.ShouldBe(0);
			return Task.CompletedTask;
		}, BoundaryTimeout);

	private Task Competition() => VerifyCompetingSettlements(MessageAction.Acknowledge, MessageAction.Requeue,
		SettlementExpectation.Completed, RefusalExpectation(), (_, _) =>
		{
			_probe.Pause = true;
			_probe.IgnoreCancellation = true;
			return Task.CompletedTask;
		}, () => _probe.FirstEntered.Task, () => _probe.SecondInvoked.Task, _probe.ReleaseAsync, (_, message) =>
		{
			_probe.Attempts.Count.ShouldBe(2);
			_probe.Attempts.All(attempt => ReferenceEquals(attempt.Message, message)).ShouldBeTrue();
			_probe.SecondBeforeRelease.ShouldBeTrue();
			_probe.Overlapped.ShouldBeFalse("opposing native dispositions cannot both act on one receipt");
			_probe.Effects.Single().Message.ShouldBeSameAs(message);
			_probe.ActiveCalls.ShouldBe(0);
			return Task.CompletedTask;
		}, BoundaryTimeout);

	private Task Batch(bool retry)
	{
		_probe.CollapseIds = true;
		return VerifySettlementBatchRetry(MessageAction.Acknowledge, MessageAction.Reject,
		ArrangeFailure, SettlementExpectation.Refused(TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Unspecified),
		retry ? SettlementExpectation.Completed : null, ObserveFailure, (_, messages) =>
		{
			messages.Count.ShouldBe(2);
			messages[0].ShouldNotBeSameAs(messages[1]);
			messages[0].Id.ShouldBe(messages[1].Id, "delivery identity must not be inferred from public IDs");
			_probe.FailureObservedBeforeHealthy.ShouldBeTrue();
			_probe.Effects.Count(effect => ReferenceEquals(effect.Message, messages[1])).ShouldBe(1);
			_probe.Effects.Count(effect => ReferenceEquals(effect.Message, messages[0])).ShouldBe(retry ? 1 : 0);
			_probe.Attempts.Count(attempt => ReferenceEquals(attempt.Message, messages[0])).ShouldBe(retry ? 2 : 1);
			_probe.Effects.First().Message.ShouldBeSameAs(messages[1], "the healthy peer must act before retrying the failed receipt");
			return Task.CompletedTask;
		}, BoundaryTimeout);
	}

	private Task<Exception> ArrangeFailure(ITransportReceiver receiver, TransportReceivedMessage message)
	{
		_probe.FaultMessage = message;
		_probe.NativeCause = new InvalidDataException("arranged native failure");
		return Task.FromResult<Exception>(_probe.NativeCause);
	}

	private Task ArrangePause(ITransportReceiver receiver, TransportReceivedMessage message)
	{
		_probe.Pause = true;
		return Task.CompletedTask;
	}

	private Task ObserveFirstEffect(ITransportReceiver receiver, TransportReceivedMessage message)
	{
		_probe.Effects.Count.ShouldBe(1, "the exact delivery must have one recorded native effect before inspecting its identity");
		_probe.Effects.Single().Message.ShouldBeSameAs(message);
		return Task.CompletedTask;
	}

	private Task ObserveFailure(ITransportReceiver receiver, TransportReceivedMessage message)
	{
		_probe.Attempts.Single().Message.ShouldBeSameAs(message);
		_probe.Effects.ShouldBeEmpty("classification is observed before any unrelated receipt acts");
		_probe.FailureObserved = true;
		return Task.CompletedTask;
	}

	private static SettlementExpectation RefusalExpectation() =>
		SettlementExpectation.Refused(TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Permanent);

	private sealed class ProbeReceiver : ITransportReceiver
	{
		private readonly Queue<TransportReceivedMessage> _incoming = new();
		private readonly HashSet<TransportReceivedMessage> _valid = [];
		private readonly HashSet<TransportReceivedMessage> _settled = [];
		private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _active;
		private int _releaseCalls;
		private bool _faulted;
		public string Source => "independent-probe";
		public string Defect { get; set; } = string.Empty;
		public bool CompletedDuplicate { get; set; }
		public SettlementRetryability ReceiptRefusalRetryability { get; set; } = SettlementRetryability.Permanent;
		public bool CollapseIds { get; set; }
		public bool SynchronousPause { get; set; }
		public bool SkipSecondSignal { get; set; }
		public bool FailCleanupRelease { get; set; }
		public bool FaultSignalOnCleanup { get; set; }
		public bool HoldNativeFailure { get; set; }
		public int DisposeCalls { get; private set; }
		public Exception SignalFailure { get; } = new InvalidDataException("borrowed signal failed during cleanup");
		public TaskCompletionSource NativeExited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public Exception CleanupFailure { get; } = new InvalidDataException("fixture cleanup release failure");
		private readonly ManualResetEventSlim _syncRelease = new();
		private int _disposed;
		public bool IgnoreCancellation { get; set; }
		public bool Pause { get; set; }
		public bool FailureObserved { get; set; }
		public bool FailureObservedBeforeHealthy { get; private set; }
		public bool SecondBeforeRelease { get; private set; }
		public bool Overlapped { get; private set; }
		public TransportReceivedMessage? FaultMessage { get; set; }
		public Exception? NativeCause { get; set; }
		public int ActiveCalls => Volatile.Read(ref _active);
		public int ReleaseCalls => Volatile.Read(ref _releaseCalls);
		public ConcurrentQueue<(TransportReceivedMessage Message, MessageAction Action, CancellationToken Token)> Attempts { get; } = new();
		public ConcurrentQueue<(TransportReceivedMessage Message, MessageAction Action)> Effects { get; } = new();
		public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource SecondInvoked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public void Seed(IReadOnlyList<TransportReceivedMessage> messages)
		{
			foreach (var message in messages)
			{
				var delivery = CollapseIds ? new TransportReceivedMessage { Id = "same-public-id", Body = message.Body } : message;
				_incoming.Enqueue(delivery);
				_valid.Add(delivery);
			}
		}

		public void Expire(TransportReceivedMessage message) => _valid.Remove(message);

		public Task ReleaseAsync()
		{
			Interlocked.Increment(ref _releaseCalls);
			_syncRelease.Set();
			_release.TrySetResult();
			if (FaultSignalOnCleanup && ReleaseCalls > 1)
			{
				SecondInvoked.TrySetException(SignalFailure);
			}
			if (FailCleanupRelease && ReleaseCalls > 1)
			{
				throw CleanupFailure;
			}
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
		{
			var result = new List<TransportReceivedMessage>();
			while (result.Count < maxMessages && _incoming.TryDequeue(out var message))
			{
				result.Add(message);
			}
			return Task.FromResult<IReadOnlyList<TransportReceivedMessage>>(result);
		}

		public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken) => InvokeAsync(message, MessageAction.Acknowledge, cancellationToken);
		public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken) =>
			InvokeAsync(message, requeue ? MessageAction.Requeue : MessageAction.Reject, cancellationToken);
		public object? GetService(Type serviceType) => null;
		public ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _disposed, 1) == 0)
			{
				DisposeCalls++;
				_syncRelease.Dispose();
			}
			return ValueTask.CompletedTask;
		}

		private async Task InvokeAsync(TransportReceivedMessage message, MessageAction action, CancellationToken token)
		{
			Attempts.Enqueue((message, action, token));
			if (!_valid.Contains(message) || Defect == "always-refuse")
			{
				throw Refuse(ReceiptRefusalRetryability);
			}
			if (ReferenceEquals(message, FaultMessage) && (!_faulted || Defect == "wrong-retry"))
			{
				_faulted = true;
				if (HoldNativeFailure)
				{
					Interlocked.Increment(ref _active);
					FirstEntered.TrySetResult();
					try
					{
						await _release.Task;
					}
					finally
					{
						Interlocked.Decrement(ref _active);
						NativeExited.TrySetResult();
					}
				}
				if (Defect == "raw-native")
				{
					throw NativeCause!;
				}
				throw Refuse(SettlementRetryability.Unspecified, Defect == "wrong-cause" ? new InvalidDataException("substituted cause") : NativeCause);
			}
			if (_settled.Contains(message))
			{
				if (!CompletedDuplicate)
				{
					throw Refuse(SettlementRetryability.Permanent);
				}
				Effects.Enqueue((message, action));
				return;
			}
			if (Pause && Interlocked.Increment(ref _active) > 1)
			{
				SecondBeforeRelease = !_release.Task.IsCompleted;
				if (!SkipSecondSignal)
				{
					SecondInvoked.TrySetResult();
				}
				Interlocked.Decrement(ref _active);
				if (Defect != "overlap")
				{
					throw Refuse(SettlementRetryability.Permanent);
				}
				Overlapped = true;
				Effects.Enqueue((message, action));
				return;
			}
			try
			{
				if (Pause)
				{
					if (Defect != "missing-boundary")
					{
						FirstEntered.TrySetResult();
					}
					if (Defect != "premature")
					{
						if (SynchronousPause)
						{
							if (!_syncRelease.Wait(TimeSpan.FromSeconds(5)))
							{
								throw new TimeoutException("Synchronous native pause was not released.");
							}
						}
						else
						{
							await _release.Task;
						}
					}
					if (!IgnoreCancellation && token.IsCancellationRequested)
					{
						throw Defect switch
						{
							"wrong-token" => new OperationCanceledException(new CancellationToken(canceled: true)),
							"tokenless" => new OperationCanceledException(),
							_ => new OperationCanceledException(token),
						};
					}
				}
				if (!ReferenceEquals(message, FaultMessage) && FaultMessage is not null)
				{
					FailureObservedBeforeHealthy = FailureObserved;
				}
				if (Defect != "noop")
				{
					Effects.Enqueue((Defect == "wrong-delivery" ? FaultMessage! : message, action));
				}
				_settled.Add(message);
			}
			finally
			{
				if (Pause)
				{
					Interlocked.Decrement(ref _active);
				}
			}
		}

		private TransportSettlementException Refuse(SettlementRetryability retryability, Exception? cause = null) =>
			new("fixture settlement refusal", cause!)
			{
				TransportName = Defect == "blank-transport" ? " " : "Probe",
				RedeliveryExpectation = TransportRedeliveryExpectation.Unspecified,
				Retryability = Defect == "wrong-axes" ? SettlementRetryability.Retryable : retryability,
			};
	}
}
