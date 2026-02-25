// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Unit tests for <see cref="SagaInfo"/>.
/// Verifies saga metadata configuration, event registration, and event type queries.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Priority", "1")]
public sealed class SagaInfoShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void InitializeWithSagaType()
	{
		// Arrange & Act
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Assert
		sagaInfo.SagaType.ShouldBe(typeof(TestInfoSaga));
	}

	[Fact]
	public void InitializeWithStateType()
	{
		// Arrange & Act
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Assert
		sagaInfo.StateType.ShouldBe(typeof(TestInfoSagaState));
	}

	#endregion

	#region StartsWith Tests

	[Fact]
	public void RegisterStartEventType()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		sagaInfo.StartsWith<TestInfoStartEvent>();

		// Assert
		sagaInfo.IsStartEvent(typeof(TestInfoStartEvent)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterStartEventAsHandledEvent()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		sagaInfo.StartsWith<TestInfoStartEvent>();

		// Assert
		sagaInfo.HandlesEvent(typeof(TestInfoStartEvent)).ShouldBeTrue();
	}

	[Fact]
	public void ReturnSelfForFluentChaining()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		var result = sagaInfo.StartsWith<TestInfoStartEvent>();

		// Assert
		result.ShouldBeSameAs(sagaInfo);
	}

	[Fact]
	public void AllowMultipleStartEvents()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		sagaInfo
			.StartsWith<TestInfoStartEvent>()
			.StartsWith<TestInfoAlternateStartEvent>();

		// Assert
		sagaInfo.IsStartEvent(typeof(TestInfoStartEvent)).ShouldBeTrue();
		sagaInfo.IsStartEvent(typeof(TestInfoAlternateStartEvent)).ShouldBeTrue();
	}

	#endregion

	#region Handles Tests

	[Fact]
	public void RegisterHandledEventType()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		sagaInfo.Handles<TestInfoContinuationEvent>();

		// Assert
		sagaInfo.HandlesEvent(typeof(TestInfoContinuationEvent)).ShouldBeTrue();
	}

	[Fact]
	public void NotRegisterHandledEventAsStartEvent()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		sagaInfo.Handles<TestInfoContinuationEvent>();

		// Assert
		sagaInfo.IsStartEvent(typeof(TestInfoContinuationEvent)).ShouldBeFalse();
	}

	[Fact]
	public void ReturnSelfForFluentChainingOnHandles()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		var result = sagaInfo.Handles<TestInfoContinuationEvent>();

		// Assert
		result.ShouldBeSameAs(sagaInfo);
	}

	[Fact]
	public void AllowFluentChainingOfStartsWithAndHandles()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		sagaInfo
			.StartsWith<TestInfoStartEvent>()
			.Handles<TestInfoContinuationEvent>()
			.Handles<TestInfoCompletionEvent>();

		// Assert
		sagaInfo.IsStartEvent(typeof(TestInfoStartEvent)).ShouldBeTrue();
		sagaInfo.HandlesEvent(typeof(TestInfoContinuationEvent)).ShouldBeTrue();
		sagaInfo.HandlesEvent(typeof(TestInfoCompletionEvent)).ShouldBeTrue();
	}

	#endregion

	#region IsStartEvent Tests

	[Fact]
	public void ReturnFalseForUnregisteredEventType()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		var result = sagaInfo.IsStartEvent(typeof(TestInfoUnregisteredEvent));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForHandledButNotStartEvent()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));
		sagaInfo.Handles<TestInfoContinuationEvent>();

		// Act
		var result = sagaInfo.IsStartEvent(typeof(TestInfoContinuationEvent));

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region HandlesEvent Tests

	[Fact]
	public void ReturnFalseForUnregisteredEventTypeInHandlesEvent()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		var result = sagaInfo.HandlesEvent(typeof(TestInfoUnregisteredEvent));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueForBothStartAndContinuationEvents()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));
		sagaInfo.StartsWith<TestInfoStartEvent>();
		sagaInfo.Handles<TestInfoContinuationEvent>();

		// Act & Assert
		sagaInfo.HandlesEvent(typeof(TestInfoStartEvent)).ShouldBeTrue();
		sagaInfo.HandlesEvent(typeof(TestInfoContinuationEvent)).ShouldBeTrue();
	}

	#endregion

	#region GetHandledEvents Tests

	[Fact]
	public void ReturnEmptyCollectionWhenNoEventsRegistered()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act
		var handledEvents = sagaInfo.GetHandledEvents();

		// Assert
		handledEvents.ShouldBeEmpty();
	}

	[Fact]
	public void ReturnAllHandledEvents()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));
		sagaInfo
			.StartsWith<TestInfoStartEvent>()
			.Handles<TestInfoContinuationEvent>()
			.Handles<TestInfoCompletionEvent>();

		// Act
		var handledEvents = sagaInfo.GetHandledEvents().ToList();

		// Assert
		handledEvents.Count.ShouldBe(3);
		handledEvents.ShouldContain(typeof(TestInfoStartEvent));
		handledEvents.ShouldContain(typeof(TestInfoContinuationEvent));
		handledEvents.ShouldContain(typeof(TestInfoCompletionEvent));
	}

	[Fact]
	public void NotContainDuplicatesWhenSameEventRegisteredMultipleTimes()
	{
		// Arrange
		var sagaInfo = new SagaInfo(typeof(TestInfoSaga), typeof(TestInfoSagaState));

		// Act - register same event as both start and handle (edge case)
		sagaInfo.StartsWith<TestInfoStartEvent>();
		sagaInfo.Handles<TestInfoStartEvent>(); // Should not add duplicate

		var handledEvents = sagaInfo.GetHandledEvents().ToList();

		// Assert - HashSet ensures uniqueness
		handledEvents.Count.ShouldBe(1);
		handledEvents.ShouldContain(typeof(TestInfoStartEvent));
	}

	#endregion

	#region Test Doubles

	private sealed class TestInfoSagaState : SagaState { }

	private sealed class TestInfoSaga(
		TestInfoSagaState initialState,
		IDispatcher dispatcher,
		ILogger<TestInfoSaga> logger)
		: SagaBase<TestInfoSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;
		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestInfoStartEvent : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class TestInfoAlternateStartEvent : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class TestInfoContinuationEvent : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class TestInfoCompletionEvent : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class TestInfoUnregisteredEvent : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	#endregion
}
