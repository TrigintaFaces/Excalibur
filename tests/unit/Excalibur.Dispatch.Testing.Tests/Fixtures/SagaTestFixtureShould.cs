// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Testing;

namespace Excalibur.Dispatch.Testing.Tests.Fixtures;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class SagaTestFixtureShould
{
	#region Given (state setup)

	[Fact]
	public async Task ApplyStateSetupAction()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.Given(state => state.OrderId = "order-1")
			.When(new TestEvent("step"))
			.ThenAsync();

		result.ThenState(s => s.OrderId == "order-1");
	}

	[Fact]
	public void ThrowOnNullStateSetupAction()
	{
		var fixture = new SagaTestFixture<TestSaga, TestSagaState>();
		Should.Throw<ArgumentNullException>(() => fixture.Given((Action<TestSagaState>)null!));
	}

	[Fact]
	public async Task AccumulateMultipleStateSetups()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.Given(state => state.OrderId = "order-1")
			.Given(state => state.Step = "prepared")
			.When(new TestEvent("go"))
			.ThenAsync();

		result.ThenState(s => s.OrderId == "order-1" && s.Step == "prepared");
	}

	#endregion

	#region Given (events)

	[Fact]
	public async Task ReplayGivenEventsThroughHandleAsync()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.Given(new TestEvent("init"), new TestEvent("prepare"))
			.When(new TestEvent("execute"))
			.ThenAsync();

		result.ThenState(s => s.EventCount == 3);
	}

	#endregion

	#region When

	[Fact]
	public void ThrowOnNullWhenEvent()
	{
		var fixture = new SagaTestFixture<TestSaga, TestSagaState>();
		Should.Throw<ArgumentNullException>(() => fixture.When(null!));
	}

	[Fact]
	public async Task ExecuteWhenEventThroughHandleAsync()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("action"))
			.ThenAsync();

		result.ThenState(s => s.LastEvent == "action");
	}

	#endregion

	#region ThenAsync / ThenState

	[Fact]
	public async Task ThenStatePassesWhenPredicateTrue()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("ok"))
			.ThenAsync();

		result.ThenState(s => s.LastEvent == "ok");
	}

	[Fact]
	public async Task ThenStateThrowsWhenPredicateFalse()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("actual"))
			.ThenAsync();

		Should.Throw<TestFixtureAssertionException>(() =>
			result.ThenState(s => s.LastEvent == "expected"));
	}

	[Fact]
	public async Task ThenStateThrowsWithCustomMessage()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("x"))
			.ThenAsync();

		var ex = Should.Throw<TestFixtureAssertionException>(() =>
			result.ThenState(_ => false, "Custom saga message"));
		ex.Message.ShouldBe("Custom saga message");
	}

	[Fact]
	public async Task ThenStateThrowsOnNullPredicate()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("x"))
			.ThenAsync();

		Should.Throw<ArgumentNullException>(() => result.ThenState(null!));
	}

	#endregion

	#region AssertState

	[Fact]
	public async Task AssertStateExecutesAction()
	{
		var called = false;
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("check"))
			.ThenAsync();

		result.AssertState(s =>
		{
			s.LastEvent.ShouldBe("check");
			called = true;
		});

		called.ShouldBeTrue();
	}

	[Fact]
	public async Task AssertStateThrowsOnNullAssertion()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("x"))
			.ThenAsync();

		Should.Throw<ArgumentNullException>(() => result.AssertState(null!));
	}

	#endregion

	#region ShouldBeCompleted / ShouldNotBeCompleted

	[Fact]
	public async Task ShouldBeCompletedPassesWhenCompleted()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("complete"))
			.ThenAsync();

		result.ShouldBeCompleted();
	}

	[Fact]
	public async Task ShouldBeCompletedThrowsWhenNotCompleted()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("normal"))
			.ThenAsync();

		Should.Throw<TestFixtureAssertionException>(() => result.ShouldBeCompleted());
	}

	[Fact]
	public async Task ShouldNotBeCompletedPassesWhenActive()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("normal"))
			.ThenAsync();

		result.ShouldNotBeCompleted();
	}

	[Fact]
	public async Task ShouldNotBeCompletedThrowsWhenCompleted()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("complete"))
			.ThenAsync();

		Should.Throw<TestFixtureAssertionException>(() => result.ShouldNotBeCompleted());
	}

	#endregion

	#region ShouldHandleEvent

	[Fact]
	public async Task ShouldHandleEventPassesForHandledEvent()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("x"))
			.ThenAsync();

		result.ShouldHandleEvent(new TestEvent("anything"));
	}

	[Fact]
	public async Task ShouldHandleEventThrowsForUnhandledEvent()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("x"))
			.ThenAsync();

		Should.Throw<TestFixtureAssertionException>(() =>
			result.ShouldHandleEvent("not a handled event type"));
	}

	[Fact]
	public async Task ShouldHandleEventThrowsOnNull()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("x"))
			.ThenAsync();

		Should.Throw<ArgumentNullException>(() => result.ShouldHandleEvent(null!));
	}

	#endregion

	#region ShouldThrow

	[Fact]
	public async Task ShouldThrowPassesOnCorrectException()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("throw"))
			.ThenAsync();

		result.ShouldThrow<InvalidOperationException>();
	}

	[Fact]
	public async Task ShouldThrowWithMessagePassesOnMatch()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("throw"))
			.ThenAsync();

		result.ShouldThrow<InvalidOperationException>("forced error");
	}

	[Fact]
	public async Task ShouldThrowFailsWhenNoException()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("normal"))
			.ThenAsync();

		Should.Throw<TestFixtureAssertionException>(() =>
			result.ShouldThrow<InvalidOperationException>());
	}

	[Fact]
	public async Task ShouldThrowFailsOnWrongType()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("throw"))
			.ThenAsync();

		Should.Throw<TestFixtureAssertionException>(() =>
			result.ShouldThrow<ArgumentException>());
	}

	[Fact]
	public async Task ShouldThrowWithMessageFailsOnMismatch()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("throw"))
			.ThenAsync();

		Should.Throw<TestFixtureAssertionException>(() =>
			result.ShouldThrow<InvalidOperationException>("wrong message"));
	}

	#endregion

	#region ShouldThrowAsync shortcut

	[Fact]
	public async Task ShouldThrowAsyncPassesOnCorrectException()
	{
		await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("throw"))
			.ShouldThrowAsync<InvalidOperationException>();
	}

	#endregion

	#region ShouldNotThrow

	[Fact]
	public async Task ShouldNotThrowPassesWhenNoException()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("normal"))
			.ThenAsync();

		result.ShouldNotThrow();
	}

	[Fact]
	public async Task ShouldNotThrowFailsWhenExceptionExists()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.When(new TestEvent("throw"))
			.ThenAsync();

		Should.Throw<TestFixtureAssertionException>(() => result.ShouldNotThrow());
	}

	#endregion

	#region ThenAsync without When event

	[Fact]
	public async Task ThenAsyncWithoutWhenEventSkipsHandling()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.Given(state => state.OrderId = "preset")
			.ThenAsync();

		result.ThenState(s => s.OrderId == "preset");
		result.ThenState(s => s.EventCount == 0);
	}

	#endregion

	#region Chaining

	[Fact]
	public async Task SupportFluentChaining()
	{
		var result = await new SagaTestFixture<TestSaga, TestSagaState>()
			.Given(state => state.OrderId = "chain-test")
			.When(new TestEvent("go"))
			.ThenAsync();

		result
			.ThenState(s => s.OrderId == "chain-test")
			.ThenState(s => s.LastEvent == "go")
			.ShouldNotBeCompleted()
			.ShouldNotThrow();
	}

	#endregion

	#region Test doubles

	private sealed record TestEvent(string Name);

	private sealed class TestSagaState : SagaState
	{
		public string? OrderId { get; set; }
		public string? Step { get; set; }
		public string? LastEvent { get; set; }
		public int EventCount { get; set; }
	}

	private sealed class TestSaga : ISaga<TestSagaState>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public bool IsCompleted { get; private set; }
		public TestSagaState State { get; } = new();

		public bool HandlesEvent(object eventMessage) => eventMessage is TestEvent;

		public Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			if (eventMessage is TestEvent evt)
			{
				State.LastEvent = evt.Name;
				State.EventCount++;

				if (evt.Name == "complete")
				{
					IsCompleted = true;
				}

				if (evt.Name == "throw")
				{
					throw new InvalidOperationException("forced error");
				}
			}

			return Task.CompletedTask;
		}
	}

	#endregion
}
