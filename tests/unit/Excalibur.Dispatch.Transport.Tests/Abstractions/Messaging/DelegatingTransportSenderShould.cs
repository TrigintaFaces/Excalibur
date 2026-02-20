// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores the ValueTask

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Tests for <see cref="DelegatingTransportSender"/> base class.
/// Verifies all virtual methods forward to the inner sender correctly.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public class DelegatingTransportSenderShould
{
	private readonly ITransportSender _innerSender = A.Fake<ITransportSender>();

	[Fact]
	public void Forward_Destination_To_InnerSender()
	{
		A.CallTo(() => _innerSender.Destination).Returns("test-queue");
		var sut = new TestDelegatingSender(_innerSender);

		sut.Destination.ShouldBe("test-queue");
	}

	[Fact]
	public async Task Forward_SendAsync_To_InnerSender()
	{
		var message = TransportMessage.FromString("hello");
		var expected = SendResult.Success("msg-1");
		A.CallTo(() => _innerSender.SendAsync(message, A<CancellationToken>._)).Returns(expected);

		var sut = new TestDelegatingSender(_innerSender);
		var result = await sut.SendAsync(message, CancellationToken.None);

		result.ShouldBeSameAs(expected);
		A.CallTo(() => _innerSender.SendAsync(message, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Forward_SendBatchAsync_To_InnerSender()
	{
		var messages = new[] { TransportMessage.FromString("a"), TransportMessage.FromString("b") };
		var expected = new BatchSendResult { TotalMessages = 2, SuccessCount = 2, FailureCount = 0 };
		A.CallTo(() => _innerSender.SendBatchAsync(messages, A<CancellationToken>._)).Returns(expected);

		var sut = new TestDelegatingSender(_innerSender);
		var result = await sut.SendBatchAsync(messages, CancellationToken.None);

		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Forward_FlushAsync_To_InnerSender()
	{
		var sut = new TestDelegatingSender(_innerSender);
		await sut.FlushAsync(CancellationToken.None);

		A.CallTo(() => _innerSender.FlushAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Forward_GetService_To_InnerSender()
	{
		var expected = new object();
		A.CallTo(() => _innerSender.GetService(typeof(string))).Returns(expected);

		var sut = new TestDelegatingSender(_innerSender);
		sut.GetService(typeof(string)).ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Forward_DisposeAsync_To_InnerSender()
	{
		A.CallTo(() => _innerSender.DisposeAsync()).Returns(ValueTask.CompletedTask);
		var sut = new TestDelegatingSender(_innerSender);
		await sut.DisposeAsync();

		A.CallTo(() => _innerSender.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Throw_When_InnerSender_Is_Null()
	{
		Should.Throw<ArgumentNullException>(() => new TestDelegatingSender(null!));
	}

	/// <summary>Minimal concrete subclass for testing the abstract base.</summary>
	private sealed class TestDelegatingSender(ITransportSender innerSender) : DelegatingTransportSender(innerSender);
}
