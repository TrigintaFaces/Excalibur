// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Regression tests for S541.8 (bd-a28au): SagaRegistry static dictionary race condition.
/// Validates that EventToSagaMap uses ConcurrentDictionary and concurrent registration is safe.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
public sealed class SagaRegistryConcurrencyShould : UnitTestBase
{
	#region ConcurrentDictionary Verification

	[Fact]
	public void UsesConcurrentDictionaryForEventToSagaMap()
	{
		// EventToSagaMap must be ConcurrentDictionary (AD-541.2)
		var field = typeof(SagaRegistry)
			.GetField("EventToSagaMap", BindingFlags.NonPublic | BindingFlags.Static);

		field.ShouldNotBeNull("EventToSagaMap static field must exist");
		field.FieldType.ShouldBe(
			typeof(ConcurrentDictionary<Type, SagaInfo>),
			"EventToSagaMap must be ConcurrentDictionary<Type, SagaInfo> for thread-safe concurrent registration");
	}

	[Fact]
	public void EventToSagaMapIsStatic()
	{
		// EventToSagaMap must be static and readonly
		var field = typeof(SagaRegistry)
			.GetField("EventToSagaMap", BindingFlags.NonPublic | BindingFlags.Static);

		field.ShouldNotBeNull();
		field.IsStatic.ShouldBeTrue("EventToSagaMap must be static");
		field.IsInitOnly.ShouldBeTrue("EventToSagaMap must be readonly");
	}

	#endregion

	#region Concurrent Registration Safety

	[Fact]
	public async Task HandleConcurrentRegistrationsWithoutCorruption()
	{
		// Multiple threads registering different saga types simultaneously should not corrupt the dictionary
		const int concurrentRegistrations = 50;
		var registrationTasks = new Task[concurrentRegistrations];
		var exceptions = new ConcurrentBag<Exception>();

		for (var i = 0; i < concurrentRegistrations; i++)
		{
			var index = i;
			registrationTasks[i] = Task.Run(() =>
			{
				try
				{
					// Each registration creates a unique event type dynamically,
					// but we can use existing types for the test since SagaRegistry's indexer
					// setter is idempotent (ConcurrentDictionary[key] = value is safe)
					SagaRegistry.Register<ConcurrencyTestSaga, ConcurrencyTestSagaState>(info =>
					{
						info.StartsWith<ConcurrencyTestStartEvent>();
					});
				}
				catch (Exception ex)
				{
					exceptions.Add(ex);
				}
			});
		}

		await Task.WhenAll(registrationTasks);

		exceptions.ShouldBeEmpty("Concurrent registrations should not throw exceptions");

		// The registration should have succeeded — verify lookup works
		var sagaType = SagaRegistry.GetSagaTypeForEvent(typeof(ConcurrencyTestStartEvent));
		sagaType.ShouldBe(typeof(ConcurrencyTestSaga));
	}

	[Fact]
	public async Task HandleConcurrentGetSagaTypeForEventWithoutCorruption()
	{
		// First register
		SagaRegistry.Register<ConcurrencyReadTestSaga, ConcurrencyReadTestSagaState>(info =>
		{
			info.StartsWith<ConcurrencyReadTestStartEvent>();
		});

		// Concurrent reads should all return correct result
		const int concurrentReads = 100;
		var results = new ConcurrentBag<Type?>();
		var readTasks = new Task[concurrentReads];

		for (var i = 0; i < concurrentReads; i++)
		{
			readTasks[i] = Task.Run(() =>
			{
				var sagaType = SagaRegistry.GetSagaTypeForEvent(typeof(ConcurrencyReadTestStartEvent));
				results.Add(sagaType);
			});
		}

		await Task.WhenAll(readTasks);

		results.Count.ShouldBe(concurrentReads);
		results.ShouldAllBe(t => t == typeof(ConcurrencyReadTestSaga));
	}

	#endregion

	#region Test Doubles (unique to avoid cross-test interference with SagaRegistryShould)

	private sealed class ConcurrencyTestSagaState : SagaState;

	private sealed class ConcurrencyTestSaga(
		ConcurrencyTestSagaState initialState,
		IDispatcher dispatcher,
		ILogger<ConcurrencyTestSaga> logger)
		: SagaBase<ConcurrencyTestSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;
		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class ConcurrencyTestStartEvent : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	private sealed class ConcurrencyReadTestSagaState : SagaState;

	private sealed class ConcurrencyReadTestSaga(
		ConcurrencyReadTestSagaState initialState,
		IDispatcher dispatcher,
		ILogger<ConcurrencyReadTestSaga> logger)
		: SagaBase<ConcurrencyReadTestSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;
		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class ConcurrencyReadTestStartEvent : ISagaEvent
	{
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	#endregion
}
