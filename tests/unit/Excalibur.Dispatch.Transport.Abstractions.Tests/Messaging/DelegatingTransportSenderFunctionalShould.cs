// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

/// <summary>
/// Functional tests for <see cref="DelegatingTransportSender"/> verifying
/// the decorator pattern delegation chain.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DelegatingTransportSenderFunctionalShould
{
	private sealed class TestDelegatingSender : DelegatingTransportSender
	{
		public int SendCallCount { get; private set; }
		public int BatchCallCount { get; private set; }

		public TestDelegatingSender(ITransportSender inner) : base(inner) { }

		public override Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
		{
			SendCallCount++;
			return base.SendAsync(message, cancellationToken);
		}

		public override Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
		{
			BatchCallCount++;
			return base.SendBatchAsync(messages, cancellationToken);
		}
	}

	[Fact]
	public async Task Delegate_send_to_inner_sender()
	{
		var inner = A.Fake<ITransportSender>();
		var expectedResult = new SendResult { IsSuccess = true, MessageId = "result-1" };
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(expectedResult);

		var sut = new TestDelegatingSender(inner);

		var result = await sut.SendAsync(
			new TransportMessage { Id = "msg-1", Subject = "q", Body = new byte[] { 1 } },
			CancellationToken.None);

		result.ShouldBeSameAs(expectedResult);
		sut.SendCallCount.ShouldBe(1);
	}

	[Fact]
	public async Task Delegate_batch_send_to_inner_sender()
	{
		var inner = A.Fake<ITransportSender>();
		var expectedResult = new BatchSendResult { SuccessCount = 2, TotalMessages = 2 };
		A.CallTo(() => inner.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Returns(expectedResult);

		var sut = new TestDelegatingSender(inner);

		var messages = new List<TransportMessage>
		{
			new() { Id = "a", Subject = "q", Body = new byte[] { 1 } },
			new() { Id = "b", Subject = "q", Body = new byte[] { 2 } },
		};

		var result = await sut.SendBatchAsync(messages, CancellationToken.None);

		result.ShouldBeSameAs(expectedResult);
		sut.BatchCallCount.ShouldBe(1);
	}

	[Fact]
	public async Task Chain_multiple_decorators()
	{
		var inner = A.Fake<ITransportSender>();
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(new SendResult { IsSuccess = true });

		// Chain: outerDecorator -> middleDecorator -> inner
		var middleDecorator = new TestDelegatingSender(inner);
		var outerDecorator = new TestDelegatingSender(middleDecorator);

		await outerDecorator.SendAsync(
			new TransportMessage { Id = "x", Subject = "q", Body = new byte[] { 0 } },
			CancellationToken.None);

		outerDecorator.SendCallCount.ShouldBe(1);
		middleDecorator.SendCallCount.ShouldBe(1);
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Dispose_inner_sender_on_dispose()
	{
		var inner = A.Fake<ITransportSender>(x => x.Implements<IAsyncDisposable>());
		var sut = new TestDelegatingSender(inner);

		await sut.DisposeAsync();

		A.CallTo(() => ((IAsyncDisposable)inner).DisposeAsync())
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Expose_inner_sender_via_get_service()
	{
		var inner = A.Fake<ITransportSender>();
		// DelegatingTransportSender delegates GetService to InnerSender,
		// so configure the inner to return itself for the ITransportSender type
		A.CallTo(() => inner.GetService(typeof(ITransportSender))).Returns(inner);
		var sut = new TestDelegatingSender(inner);

		var service = sut.GetService(typeof(ITransportSender));

		service.ShouldBeSameAs(inner);
	}
}
