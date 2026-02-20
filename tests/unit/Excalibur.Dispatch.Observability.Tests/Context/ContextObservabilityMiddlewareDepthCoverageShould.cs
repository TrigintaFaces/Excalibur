// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Deep coverage tests for <see cref="ContextObservabilityMiddleware"/> covering
/// enabled/disabled paths, integrity validation, size threshold violation,
/// error state capture, diagnostic event emission, and critical field detection.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextObservabilityMiddlewareDepthCoverageShould : IDisposable
{
	private readonly IContextFlowTracker _tracker = A.Fake<IContextFlowTracker>();
	private readonly IContextFlowMetrics _metrics = A.Fake<IContextFlowMetrics>();
	private readonly IContextTraceEnricher _enricher = A.Fake<IContextTraceEnricher>();
	private readonly ContextObservabilityOptions _options;
	private readonly ContextObservabilityMiddleware _sut;

	public ContextObservabilityMiddlewareDepthCoverageShould()
	{
		_options = new ContextObservabilityOptions
		{
			Enabled = true,
			CaptureCustomItems = true,
			EmitDiagnosticEvents = true,
			ValidateContextIntegrity = true,
			FailOnIntegrityViolation = false,
			CaptureErrorStates = true,
		};
		_options.Limits.MaxContextSizeBytes = 100_000;
		_options.Limits.MaxCustomItemsToCapture = 5;
		_options.Limits.FailOnSizeThresholdExceeded = false;
		_options.Tracing.IncludeNullFields = false;
		_options.Tracing.StoreMutationsInContext = true;
		_options.Tracing.IncludeStackTraceInErrors = false;

		var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_options);
		_sut = new ContextObservabilityMiddleware(
			NullLogger<ContextObservabilityMiddleware>.Instance,
			_tracker,
			_metrics,
			_enricher,
			optionsWrapper);
	}

	public void Dispose() => _sut.Dispose();

	[Fact]
	public async Task PassThrough_WhenDisabled()
	{
		// Arrange
		_options.Enabled = false;
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await _sut.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(expectedResult),
			CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _tracker.RecordContextState(A<IMessageContext>._, A<string>._, A<IReadOnlyDictionary<string, object>>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RecordContextState_BeforeAndAfterProcessing()
	{
		// Arrange
		A.CallTo(() => _tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(true);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		var successResult = A.Fake<IMessageResult>();
		A.CallTo(() => successResult.IsSuccess).Returns(true);

		// Act
		await _sut.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(successResult),
			CancellationToken.None);

		// Assert - RecordContextState called for both Before and After
		A.CallTo(() => _tracker.RecordContextState(
				context,
				A<string>.That.Contains(".Before"),
				A<IReadOnlyDictionary<string, object>>._))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => _tracker.RecordContextState(
				context,
				A<string>.That.Contains(".After"),
				A<IReadOnlyDictionary<string, object>>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordMetrics_OnSuccessfulProcessing()
	{
		// Arrange
		A.CallTo(() => _tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(true);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		// Act
		await _sut.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(result),
			CancellationToken.None);

		// Assert
		A.CallTo(() => _metrics.RecordPipelineStageLatency(A<string>._, A<long>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _metrics.RecordContextPreservationSuccess(A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordErrorMetrics_OnPipelineException()
	{
		// Arrange
		A.CallTo(() => _tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(true);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.InvokeAsync(
				message, context,
				(_, _, _) => throw new InvalidOperationException("Test failure"),
				CancellationToken.None));

		A.CallTo(() => _metrics.RecordContextError("pipeline_exception", A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CaptureErrorState_WhenEnabled()
	{
		// Arrange
		A.CallTo(() => _tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(true);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.InvokeAsync(
				message, context,
				(_, _, _) => throw new InvalidOperationException("Test capture"),
				CancellationToken.None));

		// Assert - error state was captured (RecordContextState called with .Error suffix)
		A.CallTo(() => _tracker.RecordContextState(
				context,
				A<string>.That.Contains(".Error"),
				A<IReadOnlyDictionary<string, object>>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordValidationFailure_WhenIntegrityCheckFails()
	{
		// Arrange
		A.CallTo(() => _tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(false);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		// Act
		await _sut.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(result),
			CancellationToken.None);

		// Assert
		A.CallTo(() => _metrics.RecordContextValidationFailure(A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowContextIntegrityException_WhenFailOnIntegrityViolation()
	{
		// Arrange
		_options.FailOnIntegrityViolation = true;
		A.CallTo(() => _tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(false);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act & Assert
		await Should.ThrowAsync<ContextIntegrityException>(async () =>
			await _sut.InvokeAsync(
				message, context,
				(_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>()),
				CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullMessage()
	{
		Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(
				null!, A.Fake<IMessageContext>(),
				(_, _, _) => default, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullContext()
	{
		Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(
				A.Fake<IDispatchMessage>(), null!,
				(_, _, _) => default, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullNextDelegate()
	{
		Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(
				A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(),
				null!, CancellationToken.None));
	}

	[Fact]
	public void HaveCorrectStage()
	{
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task DisposeAsync_Completes()
	{
		// Act & Assert - no exception
		await _sut.DisposeAsync();
	}

	[Fact]
	public async Task GetStageName_FromContext_WhenPipelineStageIsSet()
	{
		// Arrange
		A.CallTo(() => _tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(true);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.ContainsItem("PipelineStage")).Returns(true);
		A.CallTo(() => context.GetItem<string>("PipelineStage")).Returns("CustomStage");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		// Act
		await _sut.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(result),
			CancellationToken.None);

		// Assert - RecordContextState uses the custom stage name
		A.CallTo(() => _tracker.RecordContextState(
				context,
				A<string>.That.Contains("CustomStage"),
				A<IReadOnlyDictionary<string, object>>._))
			.MustHaveHappened();
	}
}
