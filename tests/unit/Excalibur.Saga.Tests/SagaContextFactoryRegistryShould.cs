// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.StateMachine;

namespace Excalibur.Saga.Tests;

/// <summary>
/// Tests for <see cref="SagaContextFactoryRegistry"/> covering registration, creation,
/// freeze behavior, and edge cases.
/// Sprint 739 B.5: Wave 4 AOT-safe dispatch path tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Feature", "AOT")]
public sealed class SagaContextFactoryRegistryShould : IDisposable
{
	public SagaContextFactoryRegistryShould()
	{
		// Clear static state before each test
		SagaContextFactoryRegistry.Clear();
	}

	public void Dispose()
	{
		SagaContextFactoryRegistry.Clear();
	}

	#region CreateContext Tests

	[Fact]
	public void ReturnNullForUnregisteredTypePair()
	{
		var result = SagaContextFactoryRegistry.CreateContext(
			typeof(TestSagaState), typeof(TestMessage),
			new TestSagaState(), new TestMessage(), new object());

		result.ShouldBeNull();
	}

	[Fact]
	public void CreateContextForRegisteredTypePair()
	{
		SagaContextFactoryRegistry.Register<TestSagaState, TestMessage>();

		var state = new TestSagaState { SagaId = Guid.NewGuid() };
		var message = new TestMessage { OrderId = "test-123" };

		// ProcessManager param needs to be the right type but we can't easily instantiate
		// the abstract ProcessManager<TData> here, so test that registration works
		// and CreateContext returns non-null by catching the expected cast
		var result = SagaContextFactoryRegistry.CreateContext(
			typeof(TestSagaState), typeof(TestMessage),
			state, message, null!);

		// The factory casts processManager to ProcessManager<TData> which will be null here,
		// but the record constructor still creates the context object
		result.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCorrectContextType()
	{
		SagaContextFactoryRegistry.Register<TestSagaState, TestMessage>();

		var state = new TestSagaState();
		var message = new TestMessage { OrderId = "order-1" };

		var result = SagaContextFactoryRegistry.CreateContext(
			typeof(TestSagaState), typeof(TestMessage),
			state, message, null!);

		result.ShouldBeOfType<SagaContext<TestSagaState, TestMessage>>();
		var ctx = (SagaContext<TestSagaState, TestMessage>)result!;
		ctx.Data.ShouldBeSameAs(state);
		ctx.Message.ShouldBeSameAs(message);
	}

	[Fact]
	public void DistinguishDifferentTypePairs()
	{
		SagaContextFactoryRegistry.Register<TestSagaState, TestMessage>();
		SagaContextFactoryRegistry.Register<TestSagaState, AnotherMessage>();

		var state = new TestSagaState();
		var msg1 = new TestMessage { OrderId = "a" };
		var msg2 = new AnotherMessage { Reason = "b" };

		var result1 = SagaContextFactoryRegistry.CreateContext(
			typeof(TestSagaState), typeof(TestMessage), state, msg1, null!);
		var result2 = SagaContextFactoryRegistry.CreateContext(
			typeof(TestSagaState), typeof(AnotherMessage), state, msg2, null!);

		result1.ShouldBeOfType<SagaContext<TestSagaState, TestMessage>>();
		result2.ShouldBeOfType<SagaContext<TestSagaState, AnotherMessage>>();
	}

	[Fact]
	public void ReturnNullWhenOnlyOneTypeMatches()
	{
		SagaContextFactoryRegistry.Register<TestSagaState, TestMessage>();

		// Correct data type, wrong message type
		var result = SagaContextFactoryRegistry.CreateContext(
			typeof(TestSagaState), typeof(AnotherMessage),
			new TestSagaState(), new AnotherMessage(), null!);

		result.ShouldBeNull();
	}

	#endregion

	#region Freeze Tests

	[Fact]
	public void ThrowWhenRegisteringAfterFreeze()
	{
		SagaContextFactoryRegistry.Freeze();

		Should.Throw<InvalidOperationException>(
			() => SagaContextFactoryRegistry.Register<TestSagaState, TestMessage>());
	}

	[Fact]
	public void AllowCreationAfterFreeze()
	{
		SagaContextFactoryRegistry.Register<TestSagaState, TestMessage>();
		SagaContextFactoryRegistry.Freeze();

		var result = SagaContextFactoryRegistry.CreateContext(
			typeof(TestSagaState), typeof(TestMessage),
			new TestSagaState(), new TestMessage(), null!);

		result.ShouldNotBeNull();
	}

	[Fact]
	public void AllowDoubleFreezeWithoutThrow()
	{
		SagaContextFactoryRegistry.Freeze();
		SagaContextFactoryRegistry.Freeze(); // Should not throw
	}

	#endregion

	#region Clear Tests

	[Fact]
	public void ClearRemovesAllRegistrations()
	{
		SagaContextFactoryRegistry.Register<TestSagaState, TestMessage>();
		SagaContextFactoryRegistry.Clear();

		var result = SagaContextFactoryRegistry.CreateContext(
			typeof(TestSagaState), typeof(TestMessage),
			new TestSagaState(), new TestMessage(), null!);

		result.ShouldBeNull();
	}

	[Fact]
	public void ClearResetsFrozenState()
	{
		SagaContextFactoryRegistry.Freeze();
		SagaContextFactoryRegistry.Clear();

		// Should not throw after clear resets frozen state
		SagaContextFactoryRegistry.Register<TestSagaState, TestMessage>();
	}

	#endregion

	#region Overwrite Tests

	[Fact]
	public void AllowOverwriteOfExistingRegistration()
	{
		SagaContextFactoryRegistry.Register<TestSagaState, TestMessage>();
		SagaContextFactoryRegistry.Register<TestSagaState, TestMessage>(); // Should not throw

		var result = SagaContextFactoryRegistry.CreateContext(
			typeof(TestSagaState), typeof(TestMessage),
			new TestSagaState(), new TestMessage(), null!);

		result.ShouldNotBeNull();
	}

	#endregion

	#region Test Fixtures

	internal sealed class TestSagaState : SagaState
	{
		public string CustomerId { get; set; } = string.Empty;
	}

	internal sealed class TestMessage
	{
		public string OrderId { get; set; } = string.Empty;
	}

	internal sealed class AnotherMessage
	{
		public string Reason { get; set; } = string.Empty;
	}

	#endregion
}
