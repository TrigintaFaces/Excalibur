// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Azure.Messaging.ServiceBus;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.ServiceBus;

/// <summary>Observes actual receiver calls to independently issued local SDK receipts.</summary>
/// <remarks>Does not certify broker acceptance or broker lock expiry.</remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class ServiceBusReceiverSettlementConformanceShould
{
	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task CompletePausedNativeSettlementDespiteCallerCancellation(MessageAction action)
	{
		await using var fixture = new Fixture();
		await fixture.CancellationAsync(action);
	}

	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task KeepHealthyBatchPeerIndependentAndRefuseConsumedReceiptRetry(MessageAction action)
	{
		await using var fixture = new Fixture();
		await fixture.BatchAsync(action);
	}

	private sealed class Fixture : TransportReceiverConformanceTests, IAsyncDisposable
	{
		private readonly IServiceBusReceiverSeam _native = A.Fake<IServiceBusReceiverSeam>();
		private readonly List<ServiceBusReceivedMessage> _issued = [];
		private readonly Queue<ServiceBusReceivedMessage> _available = new();
		private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private CancellationToken _nativeToken;

		public Fixture() => A.CallTo(() => _native.ReceiveMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily((int count, CancellationToken _) =>
			{
				var result = new List<ServiceBusReceivedMessage>();
				while (result.Count < count && _available.TryDequeue(out var message)) result.Add(message);
				return Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>(result);
			});

		public ValueTask DisposeAsync() => _native.DisposeAsync();
		protected override Task<ITransportReceiver> CreateReceiverAsync() => Task.FromResult<ITransportReceiver>(
			new ServiceBusTransportReceiver(_native, "local-settlement", NullLogger<ServiceBusTransportReceiver>.Instance));
		protected override Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
		{
			foreach (var message in messages)
			{
				var native = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromBytes(message.Body),
					messageId: "same-public-id", lockTokenGuid: Guid.NewGuid());
				_issued.Add(native);
				_available.Enqueue(native);
			}
			return Task.CompletedTask;
		}

		public Task CancellationAsync(MessageAction action) => VerifySettlementCancellation(action, SettlementExpectation.Completed,
			(_, message) =>
			{
				AssertReceipt(message, 0);
				Configure(action, token => { _nativeToken = token; _entered.TrySetResult(); return _release.Task; });
				return Task.CompletedTask;
			}, () => _entered.Task, ReleaseAsync,
			(_, message) =>
			{
				_nativeToken.CanBeCanceled.ShouldBeTrue();
				_nativeToken.IsCancellationRequested.ShouldBeFalse();
				AssertReceipt(message, 0);
				AssertEffect(0, action);
				SettlementCalls().ShouldBe(1);
				return Task.CompletedTask;
			}, TimeSpan.FromSeconds(5));

		public Task BatchAsync(MessageAction action) => VerifySettlementBatchRetry(action, MessageAction.Acknowledge,
			(_, message) =>
			{
				AssertReceipt(message, 0);
				Exception cause = new IOException("Local native response lost");
				Configure(action, _ => Task.FromException(cause));
				return Task.FromResult(cause);
			}, Refusal(), Refusal(),
			(_, message) =>
			{
				AssertReceipt(message, 0);
				AssertEffect(0, action);
				SettlementCalls().ShouldBe(1);
				return Task.CompletedTask;
			}, (_, messages) =>
			{
				AssertReceipt(messages[0], 0);
				AssertReceipt(messages[1], 1);
				messages[0].Id.ShouldBe(messages[1].Id);
				AssertEffect(0, action);
				AssertEffect(1, MessageAction.Acknowledge);
				SettlementCalls().ShouldBe(2);
				return Task.CompletedTask;
			}, TimeSpan.FromSeconds(5));

		private static SettlementExpectation Refusal() => SettlementExpectation.Refused(TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Permanent);
		private Task ReleaseAsync() { _release.TrySetResult(); return Task.CompletedTask; }
		private void AssertReceipt(TransportReceivedMessage message, int index) => message.ProviderData["azure.lock_token"].ShouldBe(_issued[index].LockToken);
		private void Configure(MessageAction action, Func<CancellationToken, Task> operation)
		{
			var issued = _issued[0];
			if (action == MessageAction.Acknowledge)
				A.CallTo(() => _native.CompleteMessageAsync(issued, A<CancellationToken>._)).ReturnsLazily((ServiceBusReceivedMessage _, CancellationToken token) => operation(token));
			else if (action == MessageAction.Requeue)
				A.CallTo(() => _native.AbandonMessageAsync(issued, A<CancellationToken>._)).ReturnsLazily((ServiceBusReceivedMessage _, CancellationToken token) => operation(token));
			else
				A.CallTo(() => _native.DeadLetterMessageAsync(issued, A<string>._, A<string>._, A<CancellationToken>._))
					.ReturnsLazily((ServiceBusReceivedMessage _, string? _, string? _, CancellationToken token) => operation(token));
		}
		private void AssertEffect(int index, MessageAction action)
		{
			var issued = _issued[index];
			A.CallTo(() => _native.CompleteMessageAsync(issued, A<CancellationToken>._)).MustHaveHappened(action == MessageAction.Acknowledge ? 1 : 0, Times.Exactly);
			A.CallTo(() => _native.AbandonMessageAsync(issued, A<CancellationToken>._)).MustHaveHappened(action == MessageAction.Requeue ? 1 : 0, Times.Exactly);
			A.CallTo(() => _native.DeadLetterMessageAsync(issued, "Rejected", A<string>._, A<CancellationToken>._)).MustHaveHappened(action == MessageAction.Reject ? 1 : 0, Times.Exactly);
		}
		private int SettlementCalls() => Fake.GetCalls(_native).Count(call => call.Method.Name is "CompleteMessageAsync" or "AbandonMessageAsync" or "DeadLetterMessageAsync");
	}
}
