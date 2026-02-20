// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Testing;
using Excalibur.Dispatch.Testing.Tracking;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Shouldly.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class DispatchTestingShouldlyExtensionsDepthShould
{
	#region Null guard tests

	[Fact]
	public void ShouldHaveDispatched_ThrowsOnNullLog()
	{
		IDispatchedMessageLog log = null!;
		Should.Throw<ArgumentNullException>(() => log.ShouldHaveDispatched<TestMessage>());
	}

	[Fact]
	public void ShouldHaveDispatched_WithCount_ThrowsOnNullLog()
	{
		IDispatchedMessageLog log = null!;
		Should.Throw<ArgumentNullException>(() => log.ShouldHaveDispatched<TestMessage>(1));
	}

	[Fact]
	public void ShouldNotHaveDispatched_ThrowsOnNullLog()
	{
		IDispatchedMessageLog log = null!;
		Should.Throw<ArgumentNullException>(() => log.ShouldNotHaveDispatched<TestMessage>());
	}

	[Fact]
	public void ShouldHaveDispatchedCount_ThrowsOnNullLog()
	{
		IDispatchedMessageLog log = null!;
		Should.Throw<ArgumentNullException>(() => log.ShouldHaveDispatchedCount(0));
	}

	[Fact]
	public void ShouldHaveSent_ThrowsOnNullSender()
	{
		InMemoryTransportSender sender = null!;
		Should.Throw<ArgumentNullException>(() => sender.ShouldHaveSent(0));
	}

	[Fact]
	public void ShouldHaveSentTo_ThrowsOnNullSender()
	{
		InMemoryTransportSender sender = null!;
		Should.Throw<ArgumentNullException>(() => sender.ShouldHaveSentTo("test"));
	}

	[Fact]
	public void ShouldHaveSentMessageMatching_ThrowsOnNullSender()
	{
		InMemoryTransportSender sender = null!;
		Should.Throw<ArgumentNullException>(() => sender.ShouldHaveSentMessageMatching(_ => true));
	}

	[Fact]
	public void ShouldHaveSentMessageMatching_ThrowsOnNullPredicate()
	{
		var sender = new InMemoryTransportSender("test");
		Should.Throw<ArgumentNullException>(() => sender.ShouldHaveSentMessageMatching(null!));
	}

	[Fact]
	public void ShouldHaveAcknowledged_ThrowsOnNullReceiver()
	{
		InMemoryTransportReceiver receiver = null!;
		Should.Throw<ArgumentNullException>(() => receiver.ShouldHaveAcknowledged(0));
	}

	[Fact]
	public void ShouldHaveRejected_ThrowsOnNullReceiver()
	{
		InMemoryTransportReceiver receiver = null!;
		Should.Throw<ArgumentNullException>(() => receiver.ShouldHaveRejected(0));
	}

	#endregion

	#region IMessageResult assertions

	[Fact]
	public void ShouldHaveCompleted_PassesForSuccess()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		result.ShouldHaveCompleted();
	}

	[Fact]
	public void ShouldHaveCompleted_ThrowsForFailure()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(false);

		Should.Throw<ShouldAssertException>(() => result.ShouldHaveCompleted());
	}

	[Fact]
	public void ShouldHaveCompleted_ThrowsOnNull()
	{
		IMessageResult result = null!;
		Should.Throw<ArgumentNullException>(() => result.ShouldHaveCompleted());
	}

	[Fact]
	public void ShouldHaveFailed_PassesForFailure()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(false);

		result.ShouldHaveFailed();
	}

	[Fact]
	public void ShouldHaveFailed_ThrowsForSuccess()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		Should.Throw<ShouldAssertException>(() => result.ShouldHaveFailed());
	}

	[Fact]
	public void ShouldHaveFailed_ThrowsOnNull()
	{
		IMessageResult result = null!;
		Should.Throw<ArgumentNullException>(() => result.ShouldHaveFailed());
	}

	[Fact]
	public void ShouldHaveFailedWithError_PassesForFailureWithMessage()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(false);
		A.CallTo(() => result.ErrorMessage).Returns("Something went wrong");

		result.ShouldHaveFailedWithError();
	}

	[Fact]
	public void ShouldHaveFailedWithError_PassesWithMatchingSubstring()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(false);
		A.CallTo(() => result.ErrorMessage).Returns("Connection timeout: server unreachable");

		result.ShouldHaveFailedWithError("timeout");
	}

	[Fact]
	public void ShouldHaveFailedWithError_ThrowsOnSuccess()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		Should.Throw<ShouldAssertException>(() => result.ShouldHaveFailedWithError());
	}

	[Fact]
	public void ShouldHaveFailedWithError_ThrowsOnNull()
	{
		IMessageResult result = null!;
		Should.Throw<ArgumentNullException>(() => result.ShouldHaveFailedWithError());
	}

	[Fact]
	public void ShouldHaveFailedWithError_ThrowsWhenErrorMessageNull()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(false);
		A.CallTo(() => result.ErrorMessage).Returns(null);

		Should.Throw<ShouldAssertException>(() => result.ShouldHaveFailedWithError());
	}

	[Fact]
	public void ShouldHaveFailedWithError_ThrowsWhenSubstringNotFound()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(false);
		A.CallTo(() => result.ErrorMessage).Returns("Some error occurred");

		Should.Throw<ShouldAssertException>(() => result.ShouldHaveFailedWithError("timeout"));
	}

	#endregion

	#region DispatchTestHarness assertions

	[Fact]
	public void ShouldHavePublished_ThrowsOnNullHarness()
	{
		DispatchTestHarness harness = null!;
		Should.Throw<ArgumentNullException>(() => harness.ShouldHavePublished<TestMessage>());
	}

	[Fact]
	public void ShouldHavePublished_WithCount_ThrowsOnNullHarness()
	{
		DispatchTestHarness harness = null!;
		Should.Throw<ArgumentNullException>(() => harness.ShouldHavePublished<TestMessage>(1));
	}

	[Fact]
	public void ShouldHavePublished_PassesWhenHarnessContainsMessage()
	{
		var harness = new DispatchTestHarness();
		var log = (DispatchedMessageLog)harness.Dispatched;

		log.Record(new DispatchedMessage(
			new TestMessage(),
			A.Fake<IMessageContext>(),
			DateTimeOffset.UtcNow,
			null));

		harness.ShouldHavePublished<TestMessage>();
	}

	[Fact]
	public void ShouldHavePublished_WithCount_PassesWhenHarnessContainsExpectedCount()
	{
		var harness = new DispatchTestHarness();
		var log = (DispatchedMessageLog)harness.Dispatched;

		log.Record(new DispatchedMessage(
			new TestMessage(),
			A.Fake<IMessageContext>(),
			DateTimeOffset.UtcNow,
			null));
		log.Record(new DispatchedMessage(
			new TestMessage(),
			A.Fake<IMessageContext>(),
			DateTimeOffset.UtcNow,
			null));

		harness.ShouldHavePublished<TestMessage>(2);
	}

	#endregion

	#region Saga assertions

	[Fact]
	public void SagaShouldBeInState_PassesWhenCompleted()
	{
		var saga = A.Fake<ISaga>();
		A.CallTo(() => saga.IsCompleted).Returns(true);

		saga.SagaShouldBeInState(true);
	}

	[Fact]
	public void SagaShouldBeInState_PassesWhenActive()
	{
		var saga = A.Fake<ISaga>();
		A.CallTo(() => saga.IsCompleted).Returns(false);

		saga.SagaShouldBeInState(false);
	}

	[Fact]
	public void SagaShouldBeInState_ThrowsOnMismatch()
	{
		var saga = A.Fake<ISaga>();
		A.CallTo(() => saga.IsCompleted).Returns(false);

		Should.Throw<ShouldAssertException>(() => saga.SagaShouldBeInState(true));
	}

	[Fact]
	public void SagaShouldBeInState_ThrowsOnNull()
	{
		ISaga saga = null!;
		Should.Throw<ArgumentNullException>(() => saga.SagaShouldBeInState(true));
	}

	[Fact]
	public void SagaShouldBeCompleted_PassesWhenCompleted()
	{
		var saga = A.Fake<ISaga>();
		A.CallTo(() => saga.IsCompleted).Returns(true);

		saga.SagaShouldBeCompleted();
	}

	[Fact]
	public void SagaShouldBeCompleted_ThrowsWhenActive()
	{
		var saga = A.Fake<ISaga>();
		A.CallTo(() => saga.IsCompleted).Returns(false);

		Should.Throw<ShouldAssertException>(() => saga.SagaShouldBeCompleted());
	}

	[Fact]
	public void SagaShouldBeCompleted_ThrowsOnNull()
	{
		ISaga saga = null!;
		Should.Throw<ArgumentNullException>(() => saga.SagaShouldBeCompleted());
	}

	[Fact]
	public void SagaShouldBeActive_PassesWhenActive()
	{
		var saga = A.Fake<ISaga>();
		A.CallTo(() => saga.IsCompleted).Returns(false);

		saga.SagaShouldBeActive();
	}

	[Fact]
	public void SagaShouldBeActive_ThrowsWhenCompleted()
	{
		var saga = A.Fake<ISaga>();
		A.CallTo(() => saga.IsCompleted).Returns(true);

		Should.Throw<ShouldAssertException>(() => saga.SagaShouldBeActive());
	}

	[Fact]
	public void SagaShouldBeActive_ThrowsOnNull()
	{
		ISaga saga = null!;
		Should.Throw<ArgumentNullException>(() => saga.SagaShouldBeActive());
	}

	[Fact]
	public void SagaShouldHaveState_ThrowsOnNullSaga()
	{
		ISaga<TestSagaState> saga = null!;
		Should.Throw<ArgumentNullException>(() => saga.SagaShouldHaveState(_ => true));
	}

	[Fact]
	public void SagaShouldHaveState_ThrowsOnNullPredicate()
	{
		var saga = new FakeTypedSaga(new TestSagaState { Step = "Init" });
		Should.Throw<ArgumentNullException>(() => saga.SagaShouldHaveState(null!));
	}

	[Fact]
	public void SagaShouldHaveState_PassesWhenPredicateMatches()
	{
		var saga = new FakeTypedSaga(new TestSagaState { Step = "Completed" });
		saga.SagaShouldHaveState(s => s.Step == "Completed");
	}

	[Fact]
	public void SagaShouldHaveState_ThrowsWhenPredicateFails()
	{
		var saga = new FakeTypedSaga(new TestSagaState { Step = "Pending" });

		Should.Throw<ShouldAssertException>(() =>
			saga.SagaShouldHaveState(s => s.Step == "Completed"));
	}

	#endregion

	#region Failure path tests for missing coverage

	[Fact]
	public void ShouldHaveDispatchedCount_ThrowsOnMismatch()
	{
		var log = new DispatchedMessageLog();
		Should.Throw<ShouldAssertException>(() => log.ShouldHaveDispatchedCount(5));
	}

	[Fact]
	public void ShouldHaveDispatched_WithCount_ThrowsOnMismatch()
	{
		var log = CreateLogWith(new TestMessage());
		Should.Throw<ShouldAssertException>(() => log.ShouldHaveDispatched<TestMessage>(3));
	}

	[Fact]
	public void ShouldHaveSentTo_ThrowsOnMismatch()
	{
		var sender = new InMemoryTransportSender("actual-dest");
		Should.Throw<ShouldAssertException>(() => sender.ShouldHaveSentTo("expected-dest"));
	}

	[Fact]
	public async Task ShouldHaveSentMessageMatching_ThrowsWhenNoMatch()
	{
		var sender = new InMemoryTransportSender("test");
		await sender.SendAsync(TransportMessage.FromString("hello"), CancellationToken.None);

		Should.Throw<ShouldAssertException>(() =>
			sender.ShouldHaveSentMessageMatching(m => m.Subject == "nonexistent"));

		await sender.DisposeAsync();
	}

	#endregion

	#region Helpers

	private static DispatchedMessageLog CreateLogWith(params IDispatchMessage[] messages)
	{
		var log = new DispatchedMessageLog();

		foreach (var msg in messages)
		{
			log.Record(new DispatchedMessage(
				msg,
				A.Fake<IMessageContext>(),
				DateTimeOffset.UtcNow,
				null));
		}

		return log;
	}

	private sealed class TestMessage : IDispatchMessage;

	// Concrete ISaga<TestSagaState> implementation — FakeItEasy cannot proxy ISaga<T>
	// when T is a private nested class (DynamicProxy accessibility limitation)
	private sealed class TestSagaState : SagaState
	{
		public string Step { get; set; } = "Initial";
	}

	private sealed class FakeTypedSaga(TestSagaState sagaState) : ISaga<TestSagaState>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public bool IsCompleted { get; set; }
		public TestSagaState State { get; } = sagaState;
		public bool HandlesEvent(object eventMessage) => false;
		public Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	#endregion
}
