// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextObservabilityMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextObservabilityMiddlewareShould : IDisposable
{
	private readonly IContextFlowTracker _fakeTracker = A.Fake<IContextFlowTracker>();
	private readonly IContextFlowMetrics _fakeMetrics = A.Fake<IContextFlowMetrics>();
	private readonly IContextTraceEnricher _fakeEnricher = A.Fake<IContextTraceEnricher>();
	private ContextObservabilityMiddleware? _middleware;

	public void Dispose() => _middleware?.Dispose();

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextObservabilityMiddleware(
				null!,
				_fakeTracker,
				_fakeMetrics,
				_fakeEnricher,
				MsOptions.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullTracker()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextObservabilityMiddleware(
				NullLogger<ContextObservabilityMiddleware>.Instance,
				null!,
				_fakeMetrics,
				_fakeEnricher,
				MsOptions.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullMetrics()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextObservabilityMiddleware(
				NullLogger<ContextObservabilityMiddleware>.Instance,
				_fakeTracker,
				null!,
				_fakeEnricher,
				MsOptions.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullTraceEnricher()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextObservabilityMiddleware(
				NullLogger<ContextObservabilityMiddleware>.Instance,
				_fakeTracker,
				_fakeMetrics,
				null!,
				MsOptions.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextObservabilityMiddleware(
				NullLogger<ContextObservabilityMiddleware>.Instance,
				_fakeTracker,
				_fakeMetrics,
				_fakeEnricher,
				null!));
	}

	[Fact]
	public void HavePreProcessingStage()
	{
		_middleware = CreateMiddleware();
		_middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

#pragma warning disable IL2026, IL3050
	[Fact]
	public async Task ThrowOnNullMessage()
	{
		_middleware = CreateMiddleware();

		await Should.ThrowAsync<ArgumentNullException>(() =>
			_middleware.InvokeAsync(
				null!,
				A.Fake<IMessageContext>(),
				CreatePassThroughDelegate(),
				CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		_middleware = CreateMiddleware();

		await Should.ThrowAsync<ArgumentNullException>(() =>
			_middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(),
				null!,
				CreatePassThroughDelegate(),
				CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowOnNullDelegate()
	{
		_middleware = CreateMiddleware();

		await Should.ThrowAsync<ArgumentNullException>(() =>
			_middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(),
				A.Fake<IMessageContext>(),
				null!,
				CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task PassThroughWhenDisabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions { Enabled = false };
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		var delegateInvoked = false;

		DispatchRequestDelegate next = (_, _, _) =>
		{
			delegateInvoked = true;
			return ValueTask.FromResult(A.Fake<IMessageResult>());
		};

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		delegateInvoked.ShouldBeTrue();
		// No tracker/metrics calls when disabled
		A.CallTo(() => _fakeTracker.RecordContextState(A<IMessageContext>._, A<string>._, A<IReadOnlyDictionary<string, object>?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task InvokeNextDelegateWhenEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions { Enabled = true };
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		var delegateInvoked = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			delegateInvoked = true;
			return ValueTask.FromResult(A.Fake<IMessageResult>());
		};

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		delegateInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task RecordContextStateBeforeAndAfterProcessing()
	{
		// Arrange
		var options = new ContextObservabilityOptions { Enabled = true };
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — RecordContextState called for both Before and After
		A.CallTo(() => _fakeTracker.RecordContextState(context, A<string>.That.Contains(".Before"), A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeTracker.RecordContextState(context, A<string>.That.Contains(".After"), A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task EnrichActivityWithContext()
	{
		// Arrange
		var options = new ContextObservabilityOptions { Enabled = true };
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeEnricher.EnrichActivity(A<System.Diagnostics.Activity?>._, context))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordMetricsOnException()
	{
		// Arrange
		var options = new ContextObservabilityOptions { Enabled = true, CaptureErrorStates = true };
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => throw new InvalidOperationException("Test error");

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());

		// Assert — error metrics were recorded
		A.CallTo(() => _fakeMetrics.RecordContextError("pipeline_exception", A<string>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeMetrics.RecordPipelineStageLatency(A<string>._, A<long>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordErrorStateOnExceptionWhenCaptureEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions { Enabled = true, CaptureErrorStates = true };
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => throw new InvalidOperationException("Test error");

		// Act
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());

		// Assert — error state was recorded via RecordContextState
		A.CallTo(() => _fakeTracker.RecordContextState(context, A<string>.That.Contains(".Error"), A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowContextIntegrityExceptionWhenFailOnViolation()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			ValidateContextIntegrity = true,
			FailOnIntegrityViolation = true,
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(false);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act & Assert
		await Should.ThrowAsync<ContextIntegrityException>(() =>
			_middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ContinueProcessingWhenIntegrityFailsButNoFailOnViolation()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			ValidateContextIntegrity = true,
			FailOnIntegrityViolation = false,
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(false);

		var delegateInvoked = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			delegateInvoked = true;
			return ValueTask.FromResult(A.Fake<IMessageResult>());
		};

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		delegateInvoked.ShouldBeTrue();
		A.CallTo(() => _fakeMetrics.RecordContextValidationFailure(A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordPreservationSuccessOnSuccessfulProcessing()
	{
		// Arrange
		var options = new ContextObservabilityOptions { Enabled = true };
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeMetrics.RecordContextPreservationSuccess(A<string>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeMetrics.RecordPipelineStageLatency(A<string>._, A<long>._))
			.MustHaveHappenedOnceExactly();
	}
#pragma warning restore IL2026, IL3050

	[Fact]
	public void ImplementIDispatchMiddleware()
	{
		_middleware = CreateMiddleware();
		_middleware.ShouldBeAssignableTo<IDispatchMiddleware>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_middleware = CreateMiddleware();
		_middleware.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		_middleware = CreateMiddleware();
		_middleware.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void DisposeWithoutError()
	{
		var middleware = CreateMiddleware();
		middleware.Dispose();
		// Second dispose should also not throw
		middleware.Dispose();
		_middleware = null;
	}

	[Fact]
	public async Task DisposeAsyncWithoutError()
	{
		var middleware = CreateMiddleware();
		await middleware.DisposeAsync();
		// Second dispose should also not throw
		await middleware.DisposeAsync();
		_middleware = null;
	}

	private static IMessageContext CreateFakeContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.MessageType).Returns("TestMessage");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => context.ContainsItem(A<string>._)).Returns(false);
		return context;
	}

	private static DispatchRequestDelegate CreatePassThroughDelegate()
	{
		return (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());
	}

	private ContextObservabilityMiddleware CreateMiddleware(ContextObservabilityOptions? options = null)
	{
		return new ContextObservabilityMiddleware(
			NullLogger<ContextObservabilityMiddleware>.Instance,
			_fakeTracker,
			_fakeMetrics,
			_fakeEnricher,
			MsOptions.Create(options ?? new ContextObservabilityOptions()));
	}
}
