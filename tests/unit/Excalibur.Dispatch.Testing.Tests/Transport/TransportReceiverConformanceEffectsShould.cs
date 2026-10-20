// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Tests.Transport;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Testing)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class TransportReceiverConformanceEffectsShould
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task RejectNoOpSettlementInsteadOfCertifyingNormalReturn(int operation)
	{
		await using var fixture = new EffectsFixture(noOp: true);
		var error = await Should.ThrowAsync<InvalidOperationException>(() => fixture.RunAsync(operation));
		error.Message.ShouldBe(operation == 0
			? "Acknowledgement has no independent recorded effect." : "Rejection has no independent recorded effect.");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task AcceptSettlementWithAnIndependentInMemoryObservation(int operation)
	{
		await using var fixture = new EffectsFixture(noOp: false);
		await fixture.RunAsync(operation);
		await fixture.ObserveAsync(operation);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task ValidateRequiredObserverBeforeCreatingOrSeedingFixture(int operation)
	{
		var fixture = new GuardFixture(1);
		await Should.ThrowAsync<ArgumentNullException>(() => fixture.RunAsync(operation, null!));
		fixture.CreateCount.ShouldBe(0);
		fixture.SeedCount.ShouldBe(0);
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(1, 0)]
	[InlineData(2, 0)]
	[InlineData(0, 2)]
	[InlineData(1, 2)]
	[InlineData(2, 2)]
	public async Task RefuseNonSingleReceiveBeforeSettlementOrObservation(int operation, int receivedCount)
	{
		var fixture = new GuardFixture(receivedCount);
		var observed = false;
		var error = await Should.ThrowAsync<InvalidOperationException>(() => fixture.RunAsync(operation, (_, _) =>
		{
			observed = true;
			return Task.CompletedTask;
		}));
		error.Message.ShouldBe($"Observable settlement verification requires exactly 1 received message, got {receivedCount}.");
		observed.ShouldBeFalse();
		fixture.Receiver.SettlementCount.ShouldBe(0);
		fixture.Receiver.Disposed.ShouldBeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task AwaitRequiredObserverWhileReceiverRemainsAlive(int operation)
	{
		var fixture = new GuardFixture(1);
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var run = fixture.RunAsync(operation, async (receiver, message) =>
		{
			receiver.ShouldBeSameAs(fixture.Receiver);
			message.ShouldBeSameAs(fixture.Receiver.Message);
			fixture.Receiver.Disposed.ShouldBeFalse();
			entered.SetResult();
			await release.Task;
			fixture.Receiver.Disposed.ShouldBeFalse();
		});
		try
		{
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
			run.IsCompleted.ShouldBeFalse();
			fixture.Receiver.Disposed.ShouldBeFalse();
			release.SetResult();
			await run.WaitAsync(TimeSpan.FromSeconds(5));
			fixture.Receiver.Disposed.ShouldBeTrue();
			fixture.Receiver.SettlementCount.ShouldBe(1);
		}
		finally
		{
			release.TrySetResult();
			await run.ContinueWith(static completed => { _ = completed.Exception; }, CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).WaitAsync(TimeSpan.FromSeconds(5));
		}
	}

	[Fact]
	public async Task ObserveInMemoryCancellationAndIndependentBatchRecordingPolicy()
	{
		await using var receiver = new InMemoryTransportReceiver("batch-effects");
		receiver.Enqueue(new TransportReceivedMessage { Id = "first" }, new TransportReceivedMessage { Id = "second" });
		var messages = await receiver.ReceiveAsync(2, CancellationToken.None);
		using var caller = new CancellationTokenSource();
		caller.Cancel();
		await Should.ThrowAsync<OperationCanceledException>(() => receiver.AcknowledgeAsync(messages[0], caller.Token));
		receiver.AcknowledgedMessages.ShouldBeEmpty();
		await receiver.AcknowledgeAsync(messages[1], CancellationToken.None);
		receiver.AcknowledgedMessages.Count.ShouldBe(1);
		receiver.AcknowledgedMessages[0].ShouldBeSameAs(messages[1]);
		await receiver.RejectAsync(messages[0], "retry", true, CancellationToken.None);
		receiver.RejectedMessages.Count.ShouldBe(1);
		receiver.RejectedMessages[0].Message.ShouldBeSameAs(messages[0]);
		receiver.RejectedMessages[0].Requeue.ShouldBeTrue();
	}

	private sealed class GuardFixture(int receivedCount) : TransportReceiverConformanceTests
	{
		public TrackingReceiver Receiver { get; } = new(receivedCount);
		public int CreateCount { get; private set; }
		public int SeedCount { get; private set; }

		protected override Task<ITransportReceiver> CreateReceiverAsync()
		{
			CreateCount++;
			return Task.FromResult<ITransportReceiver>(Receiver);
		}

		protected override Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
		{
			SeedCount++;
			Receiver.Message = messages.Single();
			return Task.CompletedTask;
		}

		public Task RunAsync(int operation, Func<ITransportReceiver, TransportReceivedMessage, Task> observer) =>
			operation == 0 ? VerifyAcknowledgeHasObservableEffect(observer) : VerifyRejectHasObservableEffect(operation == 2, observer);
	}

	private sealed class TrackingReceiver(int receivedCount) : ITransportReceiver
	{
		public string Source => "helper-guard";
		public TransportReceivedMessage Message { get; set; } = new();
		public int SettlementCount { get; private set; }
		public bool Disposed { get; private set; }
		public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<TransportReceivedMessage>>(Enumerable.Repeat(Message, receivedCount).ToArray());
		public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
		{
			SettlementCount++;
			return Task.CompletedTask;
		}
		public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
		{
			SettlementCount++;
			return Task.CompletedTask;
		}
		public ValueTask DisposeAsync()
		{
			Disposed = true;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class EffectsFixture(bool noOp) : TransportReceiverConformanceTests, IAsyncDisposable
	{
		private readonly InMemoryTransportReceiver _native = new("conformance-effects");
		private TransportReceivedMessage? _seeded;

		public ValueTask DisposeAsync() => _native.DisposeAsync();

		protected override Task<ITransportReceiver> CreateReceiverAsync() =>
			Task.FromResult<ITransportReceiver>(noOp ? new NoOpSettlementReceiver(_native) : _native);

		protected override Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
		{
			_seeded = messages.Single();
			_native.Enqueue(_seeded);
			return Task.CompletedTask;
		}

		// The required observer runs inside the helper before fixture disposal.
		public Task RunAsync(int operation) => operation switch
		{
			0 => VerifyAcknowledgeHasObservableEffect((_, _) => ObserveAsync(0)),
			1 => VerifyRejectHasObservableEffect(false, (_, _) => ObserveAsync(1)),
			_ => VerifyRejectHasObservableEffect(true, (_, _) => ObserveAsync(2)),
		};

		public Task ObserveAsync(int operation)
		{
			if (operation == 0)
			{
				if (_native.AcknowledgedMessages.Count != 1 || !ReferenceEquals(_native.AcknowledgedMessages[0], _seeded))
				{
					throw new InvalidOperationException("Acknowledgement has no independent recorded effect.");
				}
				_native.RejectedMessages.ShouldBeEmpty();
			}
			else
			{
				if (_native.RejectedMessages.Count != 1)
				{
					throw new InvalidOperationException("Rejection has no independent recorded effect.");
				}
				var observed = _native.RejectedMessages[0];
				observed.Message.ShouldBeSameAs(_seeded);
				observed.Requeue.ShouldBe(operation == 2);
				observed.Reason.ShouldBe(operation == 2 ? "retry" : "test-reason");
				_native.AcknowledgedMessages.ShouldBeEmpty();
			}
			return Task.CompletedTask;
		}
	}

	private sealed class NoOpSettlementReceiver(ITransportReceiver inner) : ITransportReceiver
	{
		public string Source => inner.Source;
		public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken) =>
			inner.ReceiveAsync(maxMessages, cancellationToken);
		public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
		public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken) => Task.CompletedTask;
		public object? GetService(Type serviceType) => inner.GetService(serviceType);
		public ValueTask DisposeAsync() => inner.DisposeAsync();
	}
}
