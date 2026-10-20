// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>Local native-call observations; these tests do not establish live broker acceptance.</summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class RabbitMqReceiverConformanceEffectsShould
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
			? "Acknowledgement has no independent native-call effect." : "Rejection has no independent native-call effect.");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task AcceptSettlementWithAnIndependentNativeCallObservation(int operation)
	{
		await using var fixture = new EffectsFixture(noOp: false);
		await fixture.RunAsync(operation);
		await fixture.ObserveAsync(operation);
	}

	[Fact]
	public async Task KeepSecondBatchReceiptIndependentWhenFirstNativeSettlementFails()
	{
		using var channel = A.Fake<IChannel>();
		await using var receiver = new RabbitMqTransportReceiver(channel, "batch-effects", "batch-effects", NullLogger<RabbitMqTransportReceiver>.Instance);
		var first = new BasicGetResult(42, false, "", "batch-effects", 1,
			new global::RabbitMQ.Client.BasicProperties { MessageId = "first" }, ReadOnlyMemory<byte>.Empty);
		var second = new BasicGetResult(43, false, "", "batch-effects", 0,
			new global::RabbitMQ.Client.BasicProperties { MessageId = "second" }, ReadOnlyMemory<byte>.Empty);
		var receives = 0;
		A.CallTo(() => channel.BasicGetAsync("batch-effects", false, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult<BasicGetResult?>(receives++ == 0 ? first : second));
		var messages = await receiver.ReceiveAsync(2, CancellationToken.None);
		messages.Count.ShouldBe(2);
		var cause = new InvalidOperationException("First native acknowledgement failed");
		A.CallTo(() => channel.BasicAckAsync(42, false, A<CancellationToken>._)).Returns(new ValueTask(Task.FromException(cause)));
		var error = await Should.ThrowAsync<TransportSettlementException>(() => receiver.AcknowledgeAsync(messages[0], CancellationToken.None));
		error.InnerException.ShouldBeSameAs(cause);
		error.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		error.Retryability.ShouldBe(SettlementRetryability.Permanent);
		await receiver.AcknowledgeAsync(messages[1], CancellationToken.None);
		var reused = await Should.ThrowAsync<TransportSettlementException>(() => receiver.AcknowledgeAsync(messages[0], CancellationToken.None));
		reused.Retryability.ShouldBe(SettlementRetryability.Permanent);
		A.CallTo(() => channel.BasicAckAsync(42, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => channel.BasicAckAsync(43, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => channel.BasicNackAsync(A<ulong>._, A<bool>._, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	private sealed class EffectsFixture(bool noOp) : TransportReceiverConformanceTests, IAsyncDisposable
	{
		private const string Queue = "conformance-effects";
		private readonly IChannel _channel = A.Fake<IChannel>();

		public ValueTask DisposeAsync() => _channel.DisposeAsync();

		protected override Task<ITransportReceiver> CreateReceiverAsync()
		{
			ITransportReceiver receiver = new RabbitMqTransportReceiver(_channel, Queue, Queue, NullLogger<RabbitMqTransportReceiver>.Instance);
			return Task.FromResult(noOp ? new NoOpSettlementReceiver(receiver) : receiver);
		}

		protected override Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
		{
			var seeded = messages.Single();
			var native = new BasicGetResult(42, false, "", Queue, 0,
				new global::RabbitMQ.Client.BasicProperties { MessageId = seeded.Id }, seeded.Body);
			A.CallTo(() => _channel.BasicGetAsync(Queue, false, A<CancellationToken>._)).Returns(native);
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
			var calls = Fake.GetCalls(_channel).ToArray();
			if (operation == 0)
			{
				if (calls.Count(call => call.Method.Name == nameof(IChannel.BasicAckAsync)) != 1)
				{
					throw new InvalidOperationException("Acknowledgement has no independent native-call effect.");
				}
				A.CallTo(() => _channel.BasicAckAsync(42, false, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
				A.CallTo(() => _channel.BasicNackAsync(A<ulong>._, A<bool>._, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
			}
			else
			{
				if (calls.Count(call => call.Method.Name == nameof(IChannel.BasicNackAsync)) != 1)
				{
					throw new InvalidOperationException("Rejection has no independent native-call effect.");
				}
				A.CallTo(() => _channel.BasicNackAsync(42, false, operation == 2, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
				A.CallTo(() => _channel.BasicAckAsync(A<ulong>._, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
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
