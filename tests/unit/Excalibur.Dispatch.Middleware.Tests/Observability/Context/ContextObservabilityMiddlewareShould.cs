// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

public sealed class ContextObservabilityMiddlewareShould : IDisposable
{
	private readonly IContextFlowTracker _fakeTracker;
	private readonly IContextFlowMetrics _fakeMetrics;
	private readonly IContextTraceEnricher _fakeEnricher;
	private readonly IDispatchMessage _fakeMessage;
	private readonly IMessageContext _fakeContext;
	private readonly IMessageResult _fakeResult;
	private readonly ContextObservabilityOptions _options;
	private readonly ContextObservabilityMiddleware _sut;

	public ContextObservabilityMiddlewareShould()
	{
		_fakeTracker = A.Fake<IContextFlowTracker>();
		_fakeMetrics = A.Fake<IContextFlowMetrics>();
		_fakeEnricher = A.Fake<IContextTraceEnricher>();
		_fakeMessage = A.Fake<IDispatchMessage>();
		_fakeContext = A.Fake<IMessageContext>();
		_fakeResult = A.Fake<IMessageResult>();

		A.CallTo(() => _fakeContext.MessageId).Returns("test-msg-001");
		A.CallTo(() => _fakeContext.CorrelationId).Returns("test-corr-001");
		A.CallTo(() => _fakeContext.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => _fakeContext.ContainsItem("PipelineStage")).Returns(false);
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(_fakeContext)).Returns(true);

		_options = new ContextObservabilityOptions();
		_sut = new ContextObservabilityMiddleware(
			NullLogger<ContextObservabilityMiddleware>.Instance,
			_fakeTracker,
			_fakeMetrics,
			_fakeEnricher,
			Microsoft.Extensions.Options.Options.Create(_options));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextObservabilityMiddleware(
				null!,
				_fakeTracker,
				_fakeMetrics,
				_fakeEnricher,
				Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions())));
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
				Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions())));
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
				Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions())));
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
				Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions())));
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
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task PassThroughWhenDisabled()
	{
		_options.Enabled = false;
		var sut = new ContextObservabilityMiddleware(
			NullLogger<ContextObservabilityMiddleware>.Instance,
			_fakeTracker,
			_fakeMetrics,
			_fakeEnricher,
			Microsoft.Extensions.Options.Options.Create(_options));

		var result = await sut.InvokeAsync(
			_fakeMessage,
			_fakeContext,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
			CancellationToken.None);

		result.ShouldBe(_fakeResult);

		// No tracker, metrics, or enricher calls when disabled
		A.CallTo(() => _fakeTracker.RecordContextState(
			A<IMessageContext>._, A<string>._, A<IReadOnlyDictionary<string, object>?>._))
			.MustNotHaveHappened();
		A.CallTo(() => _fakeMetrics.RecordPipelineStageLatency(A<string>._, A<long>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.InvokeAsync(
				null!,
				_fakeContext,
				(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
				CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.InvokeAsync(
				_fakeMessage,
				null!,
				(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
				CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowOnNullDelegate()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.InvokeAsync(
				_fakeMessage,
				_fakeContext,
				null!,
				CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeNextDelegateAndReturnResult()
	{
		var delegateCalled = false;

		var result = await _sut.InvokeAsync(
			_fakeMessage,
			_fakeContext,
			(msg, ctx, ct) =>
			{
				delegateCalled = true;
				return new ValueTask<IMessageResult>(_fakeResult);
			},
			CancellationToken.None);

		delegateCalled.ShouldBeTrue();
		result.ShouldBe(_fakeResult);
	}

	[Fact]
	public async Task RecordContextStateBeforeAndAfterProcessing()
	{
		await _sut.InvokeAsync(
			_fakeMessage,
			_fakeContext,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
			CancellationToken.None);

		// Should record "Before" and "After" states
		A.CallTo(() => _fakeTracker.RecordContextState(
			_fakeContext,
			A<string>.That.Contains(".Before"),
			A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => _fakeTracker.RecordContextState(
			_fakeContext,
			A<string>.That.Contains(".After"),
			A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordLatencyAndPreservationOnSuccess()
	{
		await _sut.InvokeAsync(
			_fakeMessage,
			_fakeContext,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
			CancellationToken.None);

		A.CallTo(() => _fakeMetrics.RecordPipelineStageLatency(
			A<string>._, A<long>._))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => _fakeMetrics.RecordContextPreservationSuccess(A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordLatencyAndErrorOnException()
	{
		var exception = new InvalidOperationException("Pipeline failure");

		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.InvokeAsync(
				_fakeMessage,
				_fakeContext,
				(msg, ctx, ct) => throw exception,
				CancellationToken.None).AsTask());

		A.CallTo(() => _fakeMetrics.RecordPipelineStageLatency(
			A<string>._, A<long>._))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => _fakeMetrics.RecordContextError(
			"pipeline_exception", A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RethrowExceptionFromPipeline()
	{
		var exception = new InvalidOperationException("Test error");

		var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.InvokeAsync(
				_fakeMessage,
				_fakeContext,
				(msg, ctx, ct) => throw exception,
				CancellationToken.None).AsTask());

		thrown.ShouldBeSameAs(exception);
	}

	[Fact]
	public async Task CaptureErrorStateWhenEnabled()
	{
		_options.CaptureErrorStates = true;
		var sut = CreateSut();

		await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.InvokeAsync(
				_fakeMessage,
				_fakeContext,
				(msg, ctx, ct) => throw new InvalidOperationException("error"),
				CancellationToken.None).AsTask());

		// Should record error state in tracker
		A.CallTo(() => _fakeTracker.RecordContextState(
			_fakeContext,
			A<string>.That.Contains(".Error"),
			A<IReadOnlyDictionary<string, object>>.That.Matches(
				d => d.ContainsKey("error_type") && d.ContainsKey("error_message"))))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotCaptureErrorStateWhenDisabled()
	{
		_options.CaptureErrorStates = false;
		var sut = CreateSut();

		await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.InvokeAsync(
				_fakeMessage,
				_fakeContext,
				(msg, ctx, ct) => throw new InvalidOperationException("error"),
				CancellationToken.None).AsTask());

		A.CallTo(() => _fakeTracker.RecordContextState(
			_fakeContext,
			A<string>.That.Contains(".Error"),
			A<IReadOnlyDictionary<string, object>>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ValidateContextIntegrityWhenEnabled()
	{
		_options.ValidateContextIntegrity = true;
		var sut = CreateSut();

		await sut.InvokeAsync(
			_fakeMessage,
			_fakeContext,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
			CancellationToken.None);

		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(_fakeContext))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ThrowContextIntegrityExceptionWhenValidationFailsAndFailEnabled()
	{
		_options.ValidateContextIntegrity = true;
		_options.FailOnIntegrityViolation = true;
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(_fakeContext)).Returns(false);

		var sut = CreateSut();

		await Should.ThrowAsync<ContextIntegrityException>(() =>
			sut.InvokeAsync(
				_fakeMessage,
				_fakeContext,
				(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
				CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task RecordValidationFailureMetricWhenIntegrityFails()
	{
		_options.ValidateContextIntegrity = true;
		_options.FailOnIntegrityViolation = false;
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(_fakeContext)).Returns(false);

		var sut = CreateSut();

		await sut.InvokeAsync(
			_fakeMessage,
			_fakeContext,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
			CancellationToken.None);

		A.CallTo(() => _fakeMetrics.RecordContextValidationFailure(A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordContextSizeMetrics()
	{
		await _sut.InvokeAsync(
			_fakeMessage,
			_fakeContext,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
			CancellationToken.None);

		A.CallTo(() => _fakeMetrics.RecordContextSize(A<string>._, A<int>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task EnrichActivityWithContext()
	{
		await _sut.InvokeAsync(
			_fakeMessage,
			_fakeContext,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
			CancellationToken.None);

		A.CallTo(() => _fakeEnricher.EnrichActivity(
			A<System.Diagnostics.Activity>._, _fakeContext))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UsePipelineStageFromContextItemsWhenAvailable()
	{
		A.CallTo(() => _fakeContext.ContainsItem("PipelineStage")).Returns(true);
		A.CallTo(() => _fakeContext.GetItem<string>("PipelineStage")).Returns("CustomStage");

		await _sut.InvokeAsync(
			_fakeMessage,
			_fakeContext,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
			CancellationToken.None);

		// Verify "CustomStage" is used as the stage name
		A.CallTo(() => _fakeTracker.RecordContextState(
			_fakeContext,
			A<string>.That.StartsWith("CustomStage."),
			A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task FallbackToMiddlewareStageWhenNoContextItem()
	{
		A.CallTo(() => _fakeContext.ContainsItem("PipelineStage")).Returns(false);

		await _sut.InvokeAsync(
			_fakeMessage,
			_fakeContext,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(_fakeResult),
			CancellationToken.None);

		// Should use "PreProcessing" (the Stage property value)
		A.CallTo(() => _fakeTracker.RecordContextState(
			_fakeContext,
			A<string>.That.StartsWith("PreProcessing."),
			A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappened();
	}

	[Fact]
	public void NotThrowOnDispose()
	{
		Should.NotThrow(() => _sut.Dispose());
	}

	[Fact]
	public void NotThrowOnDoubleDispose()
	{
		_sut.Dispose();
		Should.NotThrow(() => _sut.Dispose());
	}

	public void Dispose() => _sut.Dispose();

	private ContextObservabilityMiddleware CreateSut() =>
		new(
			NullLogger<ContextObservabilityMiddleware>.Instance,
			_fakeTracker,
			_fakeMetrics,
			_fakeEnricher,
			Microsoft.Extensions.Options.Options.Create(_options));
}
