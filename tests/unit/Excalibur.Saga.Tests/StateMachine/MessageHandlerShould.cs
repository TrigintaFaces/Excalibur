// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.StateMachine;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.StateMachine;

/// <summary>
/// Unit tests for <see cref="MessageHandler{TData, TMessage}"/>.
/// Verifies message handling, transitions, conditions, and actions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class MessageHandlerShould
{
	#region TransitionTo Tests

	[Fact]
	public void SetTargetState_WhenTransitionToIsCalled()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act
		sut.TransitionTo("NextState");

		// Assert
		sut.TargetState.ShouldBe("NextState");
	}

	[Fact]
	public void ReturnSelf_FromTransitionTo()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act
		var result = sut.TransitionTo("NextState");

		// Assert
		result.ShouldBeSameAs(sut);
	}

	[Fact]
	public void ThrowArgumentException_WhenTransitionToReceivesNull()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => sut.TransitionTo(null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenTransitionToReceivesEmpty()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => sut.TransitionTo(""));
	}

	[Fact]
	public void ThrowArgumentException_WhenTransitionToReceivesWhitespace()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => sut.TransitionTo("   "));
	}

	#endregion

	#region Then Tests

	[Fact]
	public void ReturnSelf_FromThen()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act
		var result = sut.Then(_ => { });

		// Assert
		result.ShouldBeSameAs(sut);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenThenReceivesNull()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.Then(null!));
	}

	[Fact]
	public void ExecuteAction_WhenExecuteActionsIsCalled()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();
		var actionExecuted = false;
		sut.Then(_ => actionExecuted = true);

		var data = new TestSagaData { SagaId = Guid.NewGuid() };
		var message = new TestMessage("test");
		var processManager = CreateProcessManager(data);
		var context = new SagaContext<TestSagaData, TestMessage>(data, message, processManager);

		// Act
		sut.ExecuteActions(context);

		// Assert
		actionExecuted.ShouldBeTrue();
	}

	[Fact]
	public void ExecuteMultipleActions_InOrder()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();
		var executionOrder = new List<int>();

		sut.Then(_ => executionOrder.Add(1))
			.Then(_ => executionOrder.Add(2))
			.Then(_ => executionOrder.Add(3));

		var data = new TestSagaData { SagaId = Guid.NewGuid() };
		var message = new TestMessage("test");
		var processManager = CreateProcessManager(data);
		var context = new SagaContext<TestSagaData, TestMessage>(data, message, processManager);

		// Act
		sut.ExecuteActions(context);

		// Assert
		executionOrder.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public void PassContextToAction()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();
		SagaContext<TestSagaData, TestMessage>? receivedContext = null;

		sut.Then(ctx => receivedContext = ctx);

		var data = new TestSagaData { SagaId = Guid.NewGuid() };
		var message = new TestMessage("test-value");
		var processManager = CreateProcessManager(data);
		var context = new SagaContext<TestSagaData, TestMessage>(data, message, processManager);

		// Act
		sut.ExecuteActions(context);

		// Assert
		receivedContext.ShouldNotBeNull();
		receivedContext.Data.ShouldBeSameAs(data);
		receivedContext.Message.ShouldBeSameAs(message);
	}

	#endregion

	#region If Tests

	[Fact]
	public void ReturnSelf_FromIf()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act
		var result = sut.If((_, _) => true);

		// Assert
		result.ShouldBeSameAs(sut);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenIfReceivesNull()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.If(null!));
	}

	[Fact]
	public void ReturnTrue_FromShouldHandle_WhenNoCondition()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();
		var data = new TestSagaData { SagaId = Guid.NewGuid() };
		var message = new TestMessage("test");

		// Act
		var result = sut.ShouldHandle(data, message);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrue_FromShouldHandle_WhenConditionReturnsTrue()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();
		sut.If((_, m) => m.Value == "expected");

		var data = new TestSagaData { SagaId = Guid.NewGuid() };
		var message = new TestMessage("expected");

		// Act
		var result = sut.ShouldHandle(data, message);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_FromShouldHandle_WhenConditionReturnsFalse()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();
		sut.If((_, m) => m.Value == "expected");

		var data = new TestSagaData { SagaId = Guid.NewGuid() };
		var message = new TestMessage("other");

		// Act
		var result = sut.ShouldHandle(data, message);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void EvaluateCondition_WithDataAndMessage()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();
		sut.If((d, m) => d.OrderId != null && m.Value.StartsWith("order", StringComparison.Ordinal));

		var data = new TestSagaData { SagaId = Guid.NewGuid(), OrderId = "order-123" };
		var message = new TestMessage("order-placed");

		// Act
		var result = sut.ShouldHandle(data, message);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region Complete Tests

	[Fact]
	public void SetShouldComplete_WhenCompleteIsCalled()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act
		sut.Complete();

		// Assert
		sut.ShouldComplete.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSelf_FromComplete()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Act
		var result = sut.Complete();

		// Assert
		result.ShouldBeSameAs(sut);
	}

	[Fact]
	public void HaveFalseShouldComplete_ByDefault()
	{
		// Arrange & Act
		var sut = new MessageHandler<TestSagaData, TestMessage>();

		// Assert
		sut.ShouldComplete.ShouldBeFalse();
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var sut = new MessageHandler<TestSagaData, TestMessage>();
		var actionExecuted = false;

		// Act
		sut.If((_, m) => m.Value == "go")
			.Then(_ => actionExecuted = true)
			.TransitionTo("NextState")
			.Complete();

		// Assert
		sut.TargetState.ShouldBe("NextState");
		sut.ShouldComplete.ShouldBeTrue();

		var data = new TestSagaData { SagaId = Guid.NewGuid() };
		var message = new TestMessage("go");
		sut.ShouldHandle(data, message).ShouldBeTrue();

		var processManager = CreateProcessManager(data);
		var context = new SagaContext<TestSagaData, TestMessage>(data, message, processManager);
		sut.ExecuteActions(context);
		actionExecuted.ShouldBeTrue();
	}

	#endregion

	#region Test Types & Helpers

	internal sealed class TestSagaData : SagaState
	{
		public string? OrderId { get; set; }
	}

	internal sealed record TestMessage(string Value);

	private static TestProcessManager CreateProcessManager(TestSagaData data)
	{
		var dispatcher = A.Fake<IDispatcher>();
		return new TestProcessManager(data, dispatcher, NullLogger.Instance);
	}

	internal sealed class TestProcessManager(TestSagaData state, IDispatcher dispatcher, ILogger logger)
		: ProcessManager<TestSagaData>(state, dispatcher, logger)
	{
	}

	#endregion
}
