// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Unit tests for <see cref="SagaRegistry"/>.
/// Verifies saga registration, event-to-saga mapping, and saga info retrieval.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Priority", "1")]
public sealed class SagaRegistryShould : UnitTestBase
{
	#region Register Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenConfigureIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagaRegistry.Register<TestRegistrySaga, TestRegistrySagaState>(null!));
	}

	[Fact]
	public void RegisterSagaWithConfiguredEvents()
	{
		// Arrange & Act
		SagaRegistry.Register<TestRegistrySaga, TestRegistrySagaState>(info =>
		{
			info.StartsWith<TestRegistryStartEvent>();
			info.Handles<TestRegistryContinuationEvent>();
		});

		// Assert
		var sagaType = SagaRegistry.GetSagaTypeForEvent(typeof(TestRegistryStartEvent));
		sagaType.ShouldBe(typeof(TestRegistrySaga));

		var continuationSagaType = SagaRegistry.GetSagaTypeForEvent(typeof(TestRegistryContinuationEvent));
		continuationSagaType.ShouldBe(typeof(TestRegistrySaga));
	}

	/// <summary>
	/// bzweu7 (correctness): the event→saga ROUTING table must register every declared mapping — it is
	/// NOT a recomputable cache. The pre-fix code gated registration behind <c>Count &lt; 1024</c>
	/// (a skip-when-full cache idiom), silently dropping every route past the 1024th so
	/// <see cref="SagaRegistry.GetSagaTypeForEvent"/> returned null and those events never routed.
	/// Non-vacuous: registers 2000 distinct event types and asserts ALL resolve — RED on the cap
	/// (the entries past 1024 were dropped), GREEN once the cap is removed. Baseline-independent
	/// (the process-global map may already hold real registrations; 2000 &gt; 1024 crosses it regardless).
	/// </summary>
	[Fact]
	public void RegisterMoreThan1024EventMappingsWithoutDroppingRoutes()
	{
		// Arrange — build 2000 DISTINCT closed generic event types (Marker<object>, Marker<Marker<object>>, …).
		const int count = 2000;
		var markerTypes = new Type[count];
		var current = typeof(object);
		for (var i = 0; i < count; i++)
		{
			current = typeof(Marker<>).MakeGenericType(current);
			markerTypes[i] = current;
		}

		var handles = typeof(SagaInfo).GetMethod(nameof(SagaInfo.Handles))!;

		// Act — register all 2000 as handled events on one saga (via the real Handles<TEvent> API).
		SagaRegistry.Register<CapTestSaga, CapTestSagaState>(info =>
		{
			foreach (var t in markerTypes)
			{
				_ = handles.MakeGenericMethod(t).Invoke(info, null);
			}
		});

		// Assert — every one of the 2000 routes (RED on the pre-fix Count<1024 silent-drop cap).
		var resolved = markerTypes.Count(t => SagaRegistry.GetSagaTypeForEvent(t) == typeof(CapTestSaga));
		resolved.ShouldBe(count, "every declared saga-event route must register (no 1024 cap)");
	}

	#endregion

	#region GetSagaTypeForEvent Tests

	[Fact]
	public void ReturnNullWhenNoSagaRegisteredForEventType()
	{
		// Act
		var sagaType = SagaRegistry.GetSagaTypeForEvent(typeof(UnregisteredTestEvent));

		// Assert
		sagaType.ShouldBeNull();
	}

	[Fact]
	public void ReturnCorrectSagaTypeForRegisteredEvent()
	{
		// Arrange
		SagaRegistry.Register<TestRegistrySaga2, TestRegistrySagaState2>(info =>
		{
			info.StartsWith<TestRegistryStartEvent2>();
		});

		// Act
		var sagaType = SagaRegistry.GetSagaTypeForEvent(typeof(TestRegistryStartEvent2));

		// Assert
		sagaType.ShouldBe(typeof(TestRegistrySaga2));
	}

	#endregion

	#region GetSagaInfo Tests

	[Fact]
	public void ReturnNullWhenSagaTypeNotRegistered()
	{
		// Act
		var sagaInfo = SagaRegistry.GetSagaInfo(typeof(UnregisteredSaga));

		// Assert
		sagaInfo.ShouldBeNull();
	}

	[Fact]
	public void ReturnSagaInfoForRegisteredSagaType()
	{
		// Arrange
		SagaRegistry.Register<TestRegistrySaga3, TestRegistrySagaState3>(info =>
		{
			info.StartsWith<TestRegistryStartEvent3>();
			info.Handles<TestRegistryContinuationEvent3>();
		});

		// Act
		var sagaInfo = SagaRegistry.GetSagaInfo(typeof(TestRegistrySaga3));

		// Assert
		sagaInfo.ShouldNotBeNull();
		sagaInfo.SagaType.ShouldBe(typeof(TestRegistrySaga3));
		sagaInfo.StateType.ShouldBe(typeof(TestRegistrySagaState3));
	}

	#endregion

	#region Test Doubles

	private sealed class TestRegistrySagaState : SagaState { }
	private sealed class TestRegistrySagaState2 : SagaState { }
	private sealed class TestRegistrySagaState3 : SagaState { }
	private sealed class CapTestSagaState : SagaState { }

	// Generic marker event used to synthesize many DISTINCT closed types for the >1024 route test.
	private sealed class Marker<T> : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class CapTestSaga(
		CapTestSagaState initialState,
		IDispatcher dispatcher,
		ILogger<CapTestSaga> logger)
		: SagaBase<CapTestSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;
		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestRegistrySaga(
		TestRegistrySagaState initialState,
		IDispatcher dispatcher,
		ILogger<TestRegistrySaga> logger)
		: SagaBase<TestRegistrySagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;
		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestRegistrySaga2(
		TestRegistrySagaState2 initialState,
		IDispatcher dispatcher,
		ILogger<TestRegistrySaga2> logger)
		: SagaBase<TestRegistrySagaState2>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;
		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestRegistrySaga3(
		TestRegistrySagaState3 initialState,
		IDispatcher dispatcher,
		ILogger<TestRegistrySaga3> logger)
		: SagaBase<TestRegistrySagaState3>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;
		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class UnregisteredSaga(
		TestRegistrySagaState initialState,
		IDispatcher dispatcher,
		ILogger<UnregisteredSaga> logger)
		: SagaBase<TestRegistrySagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;
		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestRegistryStartEvent : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class TestRegistryContinuationEvent : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class TestRegistryStartEvent2 : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class TestRegistryStartEvent3 : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class TestRegistryContinuationEvent3 : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class UnregisteredTestEvent : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	#endregion
}
