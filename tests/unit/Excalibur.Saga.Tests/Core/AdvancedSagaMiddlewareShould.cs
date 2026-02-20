// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.Core;

/// <summary>
/// Unit tests for <see cref="AdvancedSagaMiddleware"/>.
/// Verifies saga message handling, compensation, and state management.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class AdvancedSagaMiddlewareShould
{
	private readonly ISagaOrchestrator _orchestrator;
	private readonly ISagaStateStore _stateStore;
	private readonly IOptions<AdvancedSagaOptions> _options;
	private readonly AdvancedSagaMiddleware _sut;

	public AdvancedSagaMiddlewareShould()
	{
		_orchestrator = A.Fake<ISagaOrchestrator>();
		_stateStore = A.Fake<ISagaStateStore>();
		_options = Options.Create(new AdvancedSagaOptions());
		var logger = NullLogger<AdvancedSagaMiddleware>.Instance;
		_sut = new AdvancedSagaMiddleware(_orchestrator, _stateStore, _options, logger);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOrchestratorIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new AdvancedSagaMiddleware(
				null!,
				_stateStore,
				_options,
				NullLogger<AdvancedSagaMiddleware>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenStateStoreIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new AdvancedSagaMiddleware(
				_orchestrator,
				null!,
				_options,
				NullLogger<AdvancedSagaMiddleware>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new AdvancedSagaMiddleware(
				_orchestrator,
				_stateStore,
				null!,
				NullLogger<AdvancedSagaMiddleware>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new AdvancedSagaMiddleware(
				_orchestrator,
				_stateStore,
				_options,
				null!));
	}

	#endregion

	#region Stage and MessageKinds Tests

	[Fact]
	public void HaveProcessingStage()
	{
		// Assert
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.Processing);
	}

	[Fact]
	public void ApplyToActionsAndEvents()
	{
		// Assert
		_sut.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
	}

	#endregion

	#region InvokeAsync Argument Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(null!, context, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(message, null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	#endregion

	#region Non-Saga Message Tests

	[Fact]
	public async Task PassThrough_WhenNotSagaMessage()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = MessageResult.Success();
		var nextCalled = false;

		A.CallTo(() => context.GetItem<bool?>("IsSagaMessage")).Returns(null);

		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(expectedResult);
		};

		// Act
		var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		nextCalled.ShouldBeTrue();
	}

	#endregion

	#region Saga Message Tests

	[Fact]
	public async Task ProcessSagaMessage_WhenContextMarkedAsSaga()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = MessageResult.Success();

		A.CallTo(() => context.GetItem<bool?>("IsSagaMessage")).Returns(true);
		A.CallTo(() => context.GetItem<string>("SagaId")).Returns("saga-123");

		DispatchRequestDelegate next = (_, _, _) =>
			ValueTask.FromResult(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task LoadExistingState_WhenSagaIdPresent()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var sagaId = "saga-456";

		A.CallTo(() => context.GetItem<bool?>("IsSagaMessage")).Returns(true);
		A.CallTo(() => context.GetItem<string>("SagaId")).Returns(sagaId);
		// Note: FakeItEasy returns completed Task with null by default for Task<T?>

		DispatchRequestDelegate next = (_, _, _) =>
			ValueTask.FromResult(MessageResult.Success());

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - Verify state store was called with the saga ID
		A.CallTo(() => _stateStore.GetStateAsync(sagaId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FallbackToCorrelationId_WhenNoSagaId()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var correlationId = "corr-789";

		A.CallTo(() => context.GetItem<bool?>("IsSagaMessage")).Returns(true);
		A.CallTo(() => context.GetItem<string>("SagaId")).Returns((string?)null);
		A.CallTo(() => context.CorrelationId).Returns(correlationId);

		DispatchRequestDelegate next = (_, _, _) =>
			ValueTask.FromResult(MessageResult.Success());

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - State store should be called with correlation ID
		A.CallTo(() => _stateStore.GetStateAsync(correlationId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Compensation Tests

	[Fact]
	public async Task TriggerCompensation_WhenHandlerFails_AndAutoCompensationEnabled()
	{
		// Arrange
		var options = Options.Create(new AdvancedSagaOptions { EnableAutoCompensation = true });
		var sut = new AdvancedSagaMiddleware(
			_orchestrator, _stateStore, options, NullLogger<AdvancedSagaMiddleware>.Instance);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var sagaId = "saga-fail";
		var failedResult = MessageResult.Failed("Handler failed");

		A.CallTo(() => context.GetItem<bool?>("IsSagaMessage")).Returns(true);
		A.CallTo(() => context.GetItem<string>("SagaId")).Returns(sagaId);

		DispatchRequestDelegate next = (_, _, _) =>
			ValueTask.FromResult(failedResult);

		// Act
		await sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _orchestrator.CancelSagaAsync(sagaId, A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotTriggerCompensation_WhenAutoCompensationDisabled()
	{
		// Arrange
		var options = Options.Create(new AdvancedSagaOptions { EnableAutoCompensation = false });
		var sut = new AdvancedSagaMiddleware(
			_orchestrator, _stateStore, options, NullLogger<AdvancedSagaMiddleware>.Instance);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var failedResult = MessageResult.Failed("Handler failed");

		A.CallTo(() => context.GetItem<bool?>("IsSagaMessage")).Returns(true);
		A.CallTo(() => context.GetItem<string>("SagaId")).Returns("saga-fail");

		DispatchRequestDelegate next = (_, _, _) =>
			ValueTask.FromResult(failedResult);

		// Act
		await sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _orchestrator.CancelSagaAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Exception Handling Tests

	[Fact]
	public async Task ReturnFailedResult_WhenExceptionThrown()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		A.CallTo(() => context.GetItem<bool?>("IsSagaMessage")).Returns(true);
		A.CallTo(() => context.GetItem<string>("SagaId")).Returns("saga-error");

		DispatchRequestDelegate next = (_, _, _) =>
			throw new InvalidOperationException("Test error");

		// Act
		var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task RethrowOperationCanceledException_WhenCancelled()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		A.CallTo(() => context.GetItem<bool?>("IsSagaMessage")).Returns(true);
		A.CallTo(() => context.GetItem<string>("SagaId")).Returns("saga-cancel");

		DispatchRequestDelegate next = (_, _, ct) =>
		{
			ct.ThrowIfCancellationRequested();
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await _sut.InvokeAsync(message, context, next, cts.Token));
	}

	[Fact]
	public async Task CancelSaga_WhenOperationCancelled()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var sagaId = "saga-cancelled";
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		A.CallTo(() => context.GetItem<bool?>("IsSagaMessage")).Returns(true);
		A.CallTo(() => context.GetItem<string>("SagaId")).Returns(sagaId);

		DispatchRequestDelegate next = (_, _, ct) =>
		{
			ct.ThrowIfCancellationRequested();
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		try
		{
			await _sut.InvokeAsync(message, context, next, cts.Token);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		A.CallTo(() => _orchestrator.CancelSagaAsync(sagaId, "Operation cancelled", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region SagaMessageAttribute Tests

	[Fact]
	public void SagaMessageAttribute_HasDefaultValues()
	{
		// Arrange & Act
		var attribute = new SagaMessageAttribute();

		// Assert
		attribute.SagaType.ShouldBeNull();
	}

	[Fact]
	public void SagaMessageAttribute_AllowsSagaTypeSetting()
	{
		// Arrange & Act
		var attribute = new SagaMessageAttribute { SagaType = "OrderSaga" };

		// Assert
		attribute.SagaType.ShouldBe("OrderSaga");
	}

	#endregion

	#region Test Types

	internal sealed class TestSagaState : ISagaData
	{
		public Guid Id { get; set; }
		public int Version { get; set; }
	}

	#endregion
}
