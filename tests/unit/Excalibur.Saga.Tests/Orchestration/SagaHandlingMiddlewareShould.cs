// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Unit tests for <see cref="SagaHandlingMiddleware"/>.
/// Verifies middleware stage, saga event coordination, and error handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaHandlingMiddlewareShould
{
	private readonly SagaCoordinator _coordinator;
	private readonly SagaHandlingMiddleware _sut;

	public SagaHandlingMiddlewareShould()
	{
		// SagaCoordinator is sealed, so we create a real one with faked dependencies
		var serviceProvider = A.Fake<IServiceProvider>();
		var sagaStore = A.Fake<ISagaStore>();
		var coordinatorLogger = NullLogger<SagaCoordinator>.Instance;
		_coordinator = new SagaCoordinator(serviceProvider, sagaStore, coordinatorLogger);

		var logger = NullLogger<SagaHandlingMiddleware>.Instance;
		_sut = new SagaHandlingMiddleware(_coordinator, logger);
	}

	#region Stage Tests

	[Fact]
	public void HaveEndStage()
	{
		// Assert
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.End);
	}

	#endregion

	#region InvokeAsync Argument Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(message, null!, nextDelegate, CancellationToken.None));
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

	#region Non-Saga Event Tests

	[Fact]
	public async Task PassThroughNonSagaEvent_WithoutProcessing()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>(); // Not ISagaEvent
		var context = A.Fake<IMessageContext>();
		var expectedResult = MessageResult.Success();
		var nextDelegateCalled = false;

		DispatchRequestDelegate nextDelegate = (_, _, _) =>
		{
			nextDelegateCalled = true;
			return ValueTask.FromResult(expectedResult);
		};

		// Act
		var result = await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		nextDelegateCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnResultFromNextDelegate_ForNonSagaEvent()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = MessageResult.Failed(new MessageProblemDetails { Type = "TestError" });

		DispatchRequestDelegate nextDelegate = (_, _, _) =>
			ValueTask.FromResult(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion

	#region Saga Event Tests

	[Fact]
	public async Task ProcessSagaEvent_CallsNextDelegateFirst()
	{
		// Arrange
		var sagaEvent = A.Fake<ISagaEvent>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = MessageResult.Success();
		var nextDelegateCalled = false;

		DispatchRequestDelegate nextDelegate = (_, _, _) =>
		{
			nextDelegateCalled = true;
			return ValueTask.FromResult(expectedResult);
		};

		// Act
		var result = await _sut.InvokeAsync(sagaEvent, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		nextDelegateCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnOriginalResult_WhenSagaProcessingSucceeds()
	{
		// Arrange
		var sagaEvent = A.Fake<ISagaEvent>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = MessageResult.Success();

		DispatchRequestDelegate nextDelegate = (_, _, _) =>
			ValueTask.FromResult(expectedResult);

		// Act
		// Note: Saga processing will complete without error since no saga is registered
		// for the faked event type
		var result = await _sut.InvokeAsync(sagaEvent, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PassCancellationToken_ToNextDelegate()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		using var cts = new CancellationTokenSource();
		CancellationToken? capturedToken = null;

		DispatchRequestDelegate nextDelegate = (_, _, ct) =>
		{
			capturedToken = ct;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		_ = await _sut.InvokeAsync(message, context, nextDelegate, cts.Token);

		// Assert
		capturedToken.ShouldNotBeNull();
		capturedToken.Value.ShouldBe(cts.Token);
	}

	[Fact]
	public async Task ReturnFailedResult_FromNextDelegate()
	{
		// Arrange
		var sagaEvent = A.Fake<ISagaEvent>();
		var context = A.Fake<IMessageContext>();
		var failedResult = MessageResult.Failed(new MessageProblemDetails
		{
			Type = "ValidationError",
			Title = "Validation failed"
		});

		DispatchRequestDelegate nextDelegate = (_, _, _) =>
			ValueTask.FromResult(failedResult);

		// Act
		var result = await _sut.InvokeAsync(sagaEvent, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(failedResult);
		result.IsSuccess.ShouldBeFalse();
	}

	#endregion
}
