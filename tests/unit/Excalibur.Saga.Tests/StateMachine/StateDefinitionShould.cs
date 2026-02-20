// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.StateMachine;

namespace Excalibur.Saga.Tests.StateMachine;

/// <summary>
/// Unit tests for <see cref="StateDefinition{TData}"/>.
/// Verifies state definition, message handlers, and lifecycle callbacks.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class StateDefinitionShould
{
	#region Constructor Tests

	[Fact]
	public void SetName_FromConstructor()
	{
		// Act
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Assert
		sut.Name.ShouldBe("TestState");
	}

	[Fact]
	public void ThrowArgumentException_WhenNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new StateDefinition<TestSagaData>(null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenNameIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new StateDefinition<TestSagaData>(""));
	}

	[Fact]
	public void ThrowArgumentException_WhenNameIsWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new StateDefinition<TestSagaData>("   "));
	}

	#endregion

	#region When Tests

	[Fact]
	public void ReturnSelf_FromWhen()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act
		var result = sut.When<TestMessage>(h => { });

		// Assert
		result.ShouldBeSameAs(sut);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			sut.When<TestMessage>(null!));
	}

	[Fact]
	public void RegisterHandler_WhenWhenIsCalled()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act
		sut.When<TestMessage>(h => { });

		// Assert
		sut.HasHandler(typeof(TestMessage)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterMultipleHandlers_ForDifferentMessageTypes()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act
		sut.When<TestMessage>(h => { })
			.When<AnotherMessage>(h => { });

		// Assert
		sut.HasHandler(typeof(TestMessage)).ShouldBeTrue();
		sut.HasHandler(typeof(AnotherMessage)).ShouldBeTrue();
	}

	[Fact]
	public void OverwriteHandler_WhenSameMessageTypeRegisteredTwice()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act
		sut.When<TestMessage>(h => h.TransitionTo("First"));
		sut.When<TestMessage>(h => h.TransitionTo("Second"));

		// Assert - Only second handler should be registered
		var handler = sut.GetHandler<TestMessage>();
		handler.ShouldNotBeNull();
		handler.TargetState.ShouldBe("Second");
	}

	#endregion

	#region HasHandler Tests

	[Fact]
	public void ReturnFalse_WhenNoHandlerRegistered()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act
		var result = sut.HasHandler(typeof(TestMessage));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrue_WhenHandlerRegistered()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");
		sut.When<TestMessage>(h => { });

		// Act
		var result = sut.HasHandler(typeof(TestMessage));

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region GetHandler Tests

	[Fact]
	public void ReturnNull_WhenNoHandlerRegistered()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act
		var result = sut.GetHandler<TestMessage>();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnHandler_WhenHandlerRegistered()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");
		sut.When<TestMessage>(h => h.TransitionTo("NextState"));

		// Act
		var result = sut.GetHandler<TestMessage>();

		// Assert
		result.ShouldNotBeNull();
		result.TargetState.ShouldBe("NextState");
	}

	#endregion

	#region GetHandlerForType Tests

	[Fact]
	public void ReturnNull_WhenNoHandlerForType()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act
		var result = sut.GetHandlerForType(typeof(TestMessage));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnHandler_WhenHandlerForTypeExists()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");
		sut.When<TestMessage>(h => h.TransitionTo("NextState"));

		// Act
		var result = sut.GetHandlerForType(typeof(TestMessage));

		// Assert
		result.ShouldNotBeNull();
	}

	#endregion

	#region OnEnter Tests

	[Fact]
	public void ReturnSelf_FromOnEnter()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act
		var result = sut.OnEnter(_ => { });

		// Assert
		result.ShouldBeSameAs(sut);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOnEnterActionIsNull()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.OnEnter(null!));
	}

	[Fact]
	public void ExecuteOnEnterAction_WhenInvokeOnEnterIsCalled()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");
		var executed = false;
		sut.OnEnter(_ => executed = true);

		var data = new TestSagaData { SagaId = Guid.NewGuid() };

		// Act
		sut.InvokeOnEnter(data);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public void PassDataToOnEnterAction()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");
		TestSagaData? receivedData = null;
		sut.OnEnter(d => receivedData = d);

		var data = new TestSagaData { SagaId = Guid.NewGuid(), OrderId = "order-123" };

		// Act
		sut.InvokeOnEnter(data);

		// Assert
		receivedData.ShouldBeSameAs(data);
	}

	[Fact]
	public void DoNothing_WhenInvokeOnEnterWithoutCallback()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");
		var data = new TestSagaData { SagaId = Guid.NewGuid() };

		// Act & Assert - should not throw
		sut.InvokeOnEnter(data);
	}

	#endregion

	#region OnExit Tests

	[Fact]
	public void ReturnSelf_FromOnExit()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act
		var result = sut.OnExit(_ => { });

		// Assert
		result.ShouldBeSameAs(sut);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOnExitActionIsNull()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.OnExit(null!));
	}

	[Fact]
	public void ExecuteOnExitAction_WhenInvokeOnExitIsCalled()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");
		var executed = false;
		sut.OnExit(_ => executed = true);

		var data = new TestSagaData { SagaId = Guid.NewGuid() };

		// Act
		sut.InvokeOnExit(data);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public void PassDataToOnExitAction()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");
		TestSagaData? receivedData = null;
		sut.OnExit(d => receivedData = d);

		var data = new TestSagaData { SagaId = Guid.NewGuid(), OrderId = "order-456" };

		// Act
		sut.InvokeOnExit(data);

		// Assert
		receivedData.ShouldBeSameAs(data);
	}

	[Fact]
	public void DoNothing_WhenInvokeOnExitWithoutCallback()
	{
		// Arrange
		var sut = new StateDefinition<TestSagaData>("TestState");
		var data = new TestSagaData { SagaId = Guid.NewGuid() };

		// Act & Assert - should not throw
		sut.InvokeOnExit(data);
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void SupportFullFluentChaining()
	{
		// Arrange
		var onEnterExecuted = false;
		var onExitExecuted = false;

		// Act
		var sut = new StateDefinition<TestSagaData>("TestState");
		sut.When<TestMessage>(h => h.TransitionTo("NextState"))
			.When<AnotherMessage>(h => h.Complete())
			.OnEnter(_ => onEnterExecuted = true)
			.OnExit(_ => onExitExecuted = true);

		var data = new TestSagaData { SagaId = Guid.NewGuid() };
		sut.InvokeOnEnter(data);
		sut.InvokeOnExit(data);

		// Assert
		sut.Name.ShouldBe("TestState");
		sut.HasHandler(typeof(TestMessage)).ShouldBeTrue();
		sut.HasHandler(typeof(AnotherMessage)).ShouldBeTrue();
		onEnterExecuted.ShouldBeTrue();
		onExitExecuted.ShouldBeTrue();
	}

	#endregion

	#region Test Types

	internal sealed class TestSagaData : SagaState
	{
		public string? OrderId { get; set; }
	}

	internal sealed record TestMessage(string Value);
	internal sealed record AnotherMessage(int Count);

	#endregion
}
