// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Regression tests for S541.7 (bd-w2aik): SagaManager missing IDispatcher injection.
/// Validates that SagaManager uses ActivatorUtilities.CreateInstance for DI-aware saga creation,
/// resolving IDispatcher and ILogger from the DI container.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
public sealed class SagaManagerDiResolutionShould : UnitTestBase
{
	#region Constructor Verification

	[Fact]
	public void AcceptIServiceProviderParameter()
	{
		// SagaManager must accept IServiceProvider for DI-aware saga instantiation (AD-541.4)
		var constructor = typeof(SagaManager).GetConstructors().FirstOrDefault();
		constructor.ShouldNotBeNull();

		var parameters = constructor.GetParameters();
		parameters.ShouldContain(p => p.ParameterType == typeof(IServiceProvider),
			"SagaManager must accept IServiceProvider for ActivatorUtilities.CreateInstance");
	}

	[Fact]
	public void AcceptISagaStoreParameter()
	{
		var constructor = typeof(SagaManager).GetConstructors().FirstOrDefault();
		constructor.ShouldNotBeNull();

		var parameters = constructor.GetParameters();
		parameters.ShouldContain(p => p.ParameterType == typeof(ISagaStore),
			"SagaManager must accept ISagaStore for saga state persistence");
	}

	[Fact]
	public void AcceptILoggerFactoryParameter()
	{
		var constructor = typeof(SagaManager).GetConstructors().FirstOrDefault();
		constructor.ShouldNotBeNull();

		var parameters = constructor.GetParameters();
		parameters.ShouldContain(p => p.ParameterType == typeof(ILoggerFactory),
			"SagaManager must accept ILoggerFactory for creating saga-specific loggers");
	}

	[Fact]
	public void HaveExactlyThreeConstructorParameters()
	{
		// SagaManager(ISagaStore, IServiceProvider, ILoggerFactory) — 3 params
		var constructor = typeof(SagaManager).GetConstructors().FirstOrDefault();
		constructor.ShouldNotBeNull();
		constructor.GetParameters().Length.ShouldBe(3,
			"SagaManager should have exactly 3 constructor parameters: ISagaStore, IServiceProvider, ILoggerFactory");
	}

	#endregion

	#region DI Resolution Integration

	[Fact]
	[RequiresUnreferencedCode("Test uses ActivatorUtilities")]
	[RequiresDynamicCode("Test uses ActivatorUtilities")]
	public async Task ResolveIDispatcherFromDiContainer()
	{
		// Arrange — register IDispatcher in the DI container so ActivatorUtilities can resolve it
		var sagaStore = A.Fake<ISagaStore>();
		A.CallTo(() => sagaStore.LoadAsync<DiTestSagaState>(A<Guid>._, A<CancellationToken>._))
			.Returns((DiTestSagaState?)null);

		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		var sut = new SagaManager(sagaStore, sp, sp.GetRequiredService<ILoggerFactory>());

		// Act — ActivatorUtilities.CreateInstance<TSaga> resolves IDispatcher from DI
		// If IDispatcher is NOT available, this would throw InvalidOperationException
		var sagaId = Guid.NewGuid();
		await sut.HandleEventAsync<DiTestSaga, DiTestSagaState>(
			sagaId, new DiTestEvent("dispatch-me"), CancellationToken.None);

		// Assert — state was saved, proving saga was instantiated successfully (DI resolved IDispatcher)
		A.CallTo(() => sagaStore.SaveAsync(
			A<DiTestSagaState>.That.Matches(s => s.SagaId == sagaId && s.ProcessedData == "dispatch-me"),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses ActivatorUtilities")]
	[RequiresDynamicCode("Test uses ActivatorUtilities")]
	public async Task FailWhenIDispatcherNotRegistered()
	{
		// Arrange — intentionally do NOT register IDispatcher
		var sagaStore = A.Fake<ISagaStore>();
		A.CallTo(() => sagaStore.LoadAsync<DiTestSagaState>(A<Guid>._, A<CancellationToken>._))
			.Returns((DiTestSagaState?)null);

		var services = new ServiceCollection();
		// No IDispatcher registered!
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		var sut = new SagaManager(sagaStore, sp, sp.GetRequiredService<ILoggerFactory>());

		// Act & Assert — ActivatorUtilities should fail since IDispatcher is not registered
		var sagaId = Guid.NewGuid();
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await sut.HandleEventAsync<DiTestSaga, DiTestSagaState>(
				sagaId, new DiTestEvent("should-fail"), CancellationToken.None));
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses ActivatorUtilities")]
	[RequiresDynamicCode("Test uses ActivatorUtilities")]
	public async Task ResolveILoggerFromDiContainer()
	{
		// Arrange
		var sagaStore = A.Fake<ISagaStore>();
		A.CallTo(() => sagaStore.LoadAsync<DiLoggerTestSagaState>(A<Guid>._, A<CancellationToken>._))
			.Returns((DiLoggerTestSagaState?)null);

		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		var sut = new SagaManager(sagaStore, sp, sp.GetRequiredService<ILoggerFactory>());

		// Act — should not throw (ILogger<T> resolved from DI)
		var sagaId = Guid.NewGuid();
		await sut.HandleEventAsync<DiLoggerTestSaga, DiLoggerTestSagaState>(
			sagaId, new DiLoggerTestEvent(), CancellationToken.None);

		// Assert — if we got here without exception, ILogger was resolved
		A.CallTo(() => sagaStore.SaveAsync(A<DiLoggerTestSagaState>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region AOT Annotations

	[Fact]
	public void HandleEventAsyncHasRequiresUnreferencedCodeAttribute()
	{
		// HandleEventAsync must be annotated for AOT safety since it uses ActivatorUtilities
		var method = typeof(SagaManager)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.FirstOrDefault(m => m.Name == "HandleEventAsync");

		method.ShouldNotBeNull();
		var attr = method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>();
		attr.ShouldNotBeNull("HandleEventAsync must have [RequiresUnreferencedCode] since it uses ActivatorUtilities");
	}

	#endregion

	#region Test Doubles

	private sealed class DiTestSagaState : SagaState
	{
		public string? ProcessedData { get; set; }
	}

	private sealed class DiTestSaga(
		DiTestSagaState initialState,
		IDispatcher dispatcher,
		ILogger<DiTestSaga> logger)
		: SagaBase<DiTestSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => eventMessage is DiTestEvent;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			if (eventMessage is DiTestEvent evt)
			{
				State.ProcessedData = evt.Data;
			}

			return Task.CompletedTask;
		}
	}

	private sealed record DiTestEvent(string Data);

	private sealed class DiLoggerTestSagaState : SagaState;

	private sealed class DiLoggerTestSaga(
		DiLoggerTestSagaState initialState,
		IDispatcher dispatcher,
		ILogger<DiLoggerTestSaga> logger)
		: SagaBase<DiLoggerTestSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			// Logger should not be null — verify it was injected
			logger.LogInformation("DiLoggerTestSaga handled event");
			return Task.CompletedTask;
		}
	}

	private sealed class DiLoggerTestEvent;

	#endregion
}
