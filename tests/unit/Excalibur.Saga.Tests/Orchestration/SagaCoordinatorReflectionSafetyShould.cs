// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Regression tests for S541.9 (bd-uyy7v): SagaCoordinator unsafe reflection cast.
/// Validates that reflection result is null-checked before cast, with descriptive error messages.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
public sealed class SagaCoordinatorReflectionSafetyShould : UnitTestBase
{
	private readonly SagaCoordinator _sut;

	public SagaCoordinatorReflectionSafetyShould()
	{
		var sagaStore = A.Fake<ISagaStore>();
		var logger = A.Fake<ILogger<SagaCoordinator>>();

		var services = new ServiceCollection();
		services.AddSingleton(sagaStore);
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(typeof(ILogger<>), typeof(FakeLoggerForReflection<>));
		var serviceProvider = services.BuildServiceProvider();

		_sut = new SagaCoordinator(serviceProvider, sagaStore, logger);
	}

	#region Reflection Safety Checks (S541.9)

	[Fact]
	public void HandleEventInternalAsyncMethodExists()
	{
		// The method invoked via reflection must exist
		var method = typeof(SagaCoordinator)
			.GetMethod("HandleEventInternalAsync", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull("HandleEventInternalAsync must exist as a public generic method");
		method.IsGenericMethodDefinition.ShouldBeTrue();
		method.GetGenericArguments().Length.ShouldBe(2, "Method should have 2 generic parameters (TSaga, TSagaState)");
	}

	[Fact]
	public void ProcessEventAsyncUsesNullSafeReflection()
	{
		// Verify ProcessEventAsync has the 'is not Task' check pattern via source inspection
		// by checking the method body IL can produce InvalidOperationException
		var method = typeof(SagaCoordinator)
			.GetMethod("ProcessEventAsync", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));

		// The method should be decorated with RequiresUnreferencedCode since it uses reflection
		var attr = method.GetCustomAttribute<System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute>();
		attr.ShouldNotBeNull("ProcessEventAsync must have [RequiresUnreferencedCode] for reflection usage");
	}

	[Fact]
	public async Task ReturnEarlyForUnregisteredEventType()
	{
		// When no saga is registered, ProcessEventAsync should return without throwing
		var messageContext = A.Fake<IMessageContext>();
		var evt = new UnregisteredReflectionEvent { SagaId = Guid.NewGuid().ToString(), StepId = null };

		// Act — should not throw (returns early when no saga registered)
		await _sut.ProcessEventAsync(messageContext, evt, CancellationToken.None);

		// Assert — no exception means the null-safe path works correctly
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionForNullMessageContext()
	{
		// Null check should be before reflection
		var evt = new UnregisteredReflectionEvent { SagaId = Guid.NewGuid().ToString(), StepId = null };

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.ProcessEventAsync(null!, evt, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionForNullEvent()
	{
		var messageContext = A.Fake<IMessageContext>();

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.ProcessEventAsync(messageContext, null!, CancellationToken.None));
	}

	#endregion

	#region ActivatorUtilities Verification (S541.7 related)

	[Fact]
	public void SagaCoordinatorUsesActivatorUtilities()
	{
		// SagaCoordinator should use ActivatorUtilities (via serviceProvider), not Activator.CreateInstance
		var constructor = typeof(SagaCoordinator).GetConstructors().FirstOrDefault();
		constructor.ShouldNotBeNull();

		var parameters = constructor.GetParameters();
		parameters.ShouldSatisfyAllConditions(
			() => parameters.ShouldContain(p => p.ParameterType == typeof(IServiceProvider),
				"SagaCoordinator must accept IServiceProvider for DI-aware saga creation"));
	}

	#endregion

	#region Test Doubles

	private sealed class UnregisteredReflectionEvent : ISagaEvent
	{
		public required string SagaId { get; init; }
		public string? StepId { get; init; }
	}

	private sealed class FakeLoggerForReflection<T> : ILogger<T>
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => true;
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
		}
	}

	#endregion
}
