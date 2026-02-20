// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores the ValueTask

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Tests for <see cref="DelegatingTransportReceiver"/> base class.
/// Verifies all virtual methods forward to the inner receiver correctly.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public class DelegatingTransportReceiverShould
{
	private readonly ITransportReceiver _innerReceiver = A.Fake<ITransportReceiver>();

	[Fact]
	public void Forward_Source_To_InnerReceiver()
	{
		A.CallTo(() => _innerReceiver.Source).Returns("test-subscription");
		var sut = new TestDelegatingReceiver(_innerReceiver);

		sut.Source.ShouldBe("test-subscription");
	}

	[Fact]
	public async Task Forward_ReceiveAsync_To_InnerReceiver()
	{
		var expected = new List<TransportReceivedMessage>
		{
			new() { Id = "msg-1", Body = new byte[] { 1 } },
		};
		A.CallTo(() => _innerReceiver.ReceiveAsync(10, A<CancellationToken>._))
			.Returns((IReadOnlyList<TransportReceivedMessage>)expected);

		var sut = new TestDelegatingReceiver(_innerReceiver);
		var result = await sut.ReceiveAsync(10, CancellationToken.None);

		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Forward_AcknowledgeAsync_To_InnerReceiver()
	{
		var message = new TransportReceivedMessage { Id = "msg-1" };
		var sut = new TestDelegatingReceiver(_innerReceiver);
		await sut.AcknowledgeAsync(message, CancellationToken.None);

		A.CallTo(() => _innerReceiver.AcknowledgeAsync(message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Forward_RejectAsync_To_InnerReceiver()
	{
		var message = new TransportReceivedMessage { Id = "msg-1" };
		var sut = new TestDelegatingReceiver(_innerReceiver);
		await sut.RejectAsync(message, "bad data", true, CancellationToken.None);

		A.CallTo(() => _innerReceiver.RejectAsync(message, "bad data", true, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Forward_GetService_To_InnerReceiver()
	{
		var expected = new object();
		A.CallTo(() => _innerReceiver.GetService(typeof(string))).Returns(expected);

		var sut = new TestDelegatingReceiver(_innerReceiver);
		sut.GetService(typeof(string)).ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Forward_DisposeAsync_To_InnerReceiver()
	{
		A.CallTo(() => _innerReceiver.DisposeAsync()).Returns(ValueTask.CompletedTask);
		var sut = new TestDelegatingReceiver(_innerReceiver);
		await sut.DisposeAsync();

		A.CallTo(() => _innerReceiver.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Throw_When_InnerReceiver_Is_Null()
	{
		Should.Throw<ArgumentNullException>(() => new TestDelegatingReceiver(null!));
	}

	/// <summary>Minimal concrete subclass for testing the abstract base.</summary>
	private sealed class TestDelegatingReceiver(ITransportReceiver innerReceiver) : DelegatingTransportReceiver(innerReceiver);
}
