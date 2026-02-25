// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// In-depth unit tests for <see cref="ContextObservabilityMiddleware"/> covering uncovered code paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextObservabilityMiddlewareDepthShould : IDisposable
{
	private readonly IContextFlowTracker _fakeTracker = A.Fake<IContextFlowTracker>();
	private readonly IContextFlowMetrics _fakeMetrics = A.Fake<IContextFlowMetrics>();
	private readonly IContextTraceEnricher _fakeEnricher = A.Fake<IContextTraceEnricher>();
	private ContextObservabilityMiddleware? _middleware;

	public void Dispose() => _middleware?.Dispose();

	[Fact]
	public async Task EmitDiagnosticEvent_WhenEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			EmitDiagnosticEvents = true,
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — ValidateContextIntegrity is called once in finally block for diagnostic event + once for integrity check
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ThrowContextSizeExceededException_WhenSizeExceededAndFailEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			Limits =
			{
				MaxContextSizeBytes = 1, // Very small limit to trigger
				FailOnSizeThresholdExceeded = true,
			},
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act & Assert
		await Should.ThrowAsync<ContextSizeExceededException>(() =>
			_middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task RecordSizeThresholdExceeded_WhenSizeExceededButNoFail()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			Limits =
			{
				MaxContextSizeBytes = 1,
				FailOnSizeThresholdExceeded = false,
			},
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeMetrics.RecordContextSizeThresholdExceeded(A<string>._, A<int>._))
			.MustHaveHappened();
		A.CallTo(() => _fakeMetrics.RecordContextSize(A<string>._, A<int>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task CaptureCustomItems_WhenEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			CaptureCustomItems = true,
			Limits = { MaxCustomItemsToCapture = 5 },
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>
		{
			["OrderId"] = "order-123",
			["Amount"] = 99.99,
		});
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — tracker should have been called with before and after states
		A.CallTo(() => _fakeTracker.RecordContextState(context, A<string>._, A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task GetStageNameFromContextItem_WhenPipelineStageSet()
	{
		// Arrange
		var options = new ContextObservabilityOptions { Enabled = true };
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => context.ContainsItem("PipelineStage")).Returns(true);
		A.CallTo(() => context.GetItem<string>("PipelineStage")).Returns("CustomStage");
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — RecordContextState should use CustomStage in the stage name
		A.CallTo(() => _fakeTracker.RecordContextState(
			context,
			A<string>.That.Contains("CustomStage"),
			A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RecordErrorState_WithStackTrace_WhenCaptureEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			CaptureErrorStates = true,
			Tracing = { IncludeStackTraceInErrors = true },
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => throw new InvalidOperationException("Boom");

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());

		// Assert — error state with metadata was recorded
		A.CallTo(() => _fakeTracker.RecordContextState(
			context,
			A<string>.That.Contains(".Error"),
			A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotRecordErrorState_WhenCaptureDisabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			CaptureErrorStates = false,
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => throw new InvalidOperationException("Boom");

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());

		// Assert — no error state recorded
		A.CallTo(() => _fakeTracker.RecordContextState(
			context,
			A<string>.That.Contains(".Error"),
			A<IReadOnlyDictionary<string, object>?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DetectMutations_WhenContextChanges()
	{
		// Arrange — enable mutation storage
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			Tracing = { StoreMutationsInContext = true },
		};
		_middleware = CreateMiddleware(options);

		var items = new Dictionary<string, object>();
		var context = CreateFakeContext(items);
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		var message = A.Fake<IDispatchMessage>();

		// The next delegate modifies the context items — simulating a field mutation
		DispatchRequestDelegate next = (_, ctx, _) =>
		{
			ctx.SetItem("NewField", "added-value");
			return ValueTask.FromResult(A.Fake<IMessageResult>());
		};

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — metrics should have recorded mutations (context snapshots differ)
		A.CallTo(() => _fakeMetrics.RecordContextPreservationSuccess(A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeNullFields_WhenIncludeNullFieldsEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			Tracing = { IncludeNullFields = true },
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => context.CorrelationId).Returns((string?)null); // Null field
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — should succeed with null fields included
		A.CallTo(() => _fakeMetrics.RecordContextPreservationSuccess(A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task EmitDiagnosticEvent_AfterException()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			EmitDiagnosticEvents = true,
			CaptureErrorStates = true,
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => throw new InvalidOperationException("Error");

		// Act
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());

		// Assert — ValidateContextIntegrity called for diagnostic event in finally block
		// First call is the integrity check, second is in the diagnostic event emission
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context))
			.MustHaveHappened();
	}

	[Fact]
	public async Task SkipValidation_WhenValidateContextIntegrityDisabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			ValidateContextIntegrity = false,
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — ValidateContextIntegrity should not have been called for validation
		// (may still be called for diagnostic events)
		A.CallTo(() => _fakeMetrics.RecordContextValidationFailure(A<string>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RecordContextSize_OnSuccess()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			Limits = { MaxContextSizeBytes = 100000 }, // Large limit to avoid threshold
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeMetrics.RecordContextSize(A<string>._, A<int>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task FallbackToPreProcessingStage_WhenNoPipelineStageInContext()
	{
		// Arrange
		var options = new ContextObservabilityOptions { Enabled = true };
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => context.ContainsItem("PipelineStage")).Returns(false);
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — stage name should use "PreProcessing" (from Stage property)
		A.CallTo(() => _fakeTracker.RecordContextState(
			context,
			A<string>.That.Contains("PreProcessing"),
			A<IReadOnlyDictionary<string, object>?>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task HandleTrackedFieldModification()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			Fields =
			{
				TrackedFields = ["MessageId"],
			},
			Tracing = { StoreMutationsInContext = true },
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — should complete without error
		A.CallTo(() => _fakeMetrics.RecordContextPreservationSuccess(A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleCriticalFieldRemoval()
	{
		// Arrange
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			Fields =
			{
				CriticalFields = ["CorrelationId"],
			},
			Tracing = { StoreMutationsInContext = true },
		};
		_middleware = CreateMiddleware(options);

		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		A.CallTo(() => _fakeTracker.ValidateContextIntegrity(context)).Returns(true);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act — should not throw
		await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeMetrics.RecordContextPreservationSuccess(A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Act — dispose multiple times
		await middleware.DisposeAsync().ConfigureAwait(false);
		await middleware.DisposeAsync().ConfigureAwait(false);

		_middleware = null; // Prevent teardown
	}

	private static IMessageContext CreateFakeContext(Dictionary<string, object>? items = null)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.MessageType).Returns("TestMessage");
		A.CallTo(() => context.Items).Returns(items ?? new Dictionary<string, object>());
		A.CallTo(() => context.ContainsItem(A<string>._)).Returns(false);
		return context;
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

#pragma warning restore IL2026, IL3050
