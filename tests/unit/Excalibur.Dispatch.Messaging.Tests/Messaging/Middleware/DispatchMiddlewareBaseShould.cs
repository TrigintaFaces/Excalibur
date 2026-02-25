// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;

using FakeItEasy;

using DispatchMiddlewareStage = Excalibur.Dispatch.Abstractions.DispatchMiddlewareStage;
using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="DispatchMiddlewareBase"/>.
/// </summary>
/// <remarks>
/// Tests the abstract middleware base class using concrete test implementations.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class DispatchMiddlewareBaseShould
{
	#region InvokeAsync - Null Argument Tests

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullMessage()
	{
		// Arrange
		var middleware = new TestMiddleware();
		var context = A.Fake<IMessageContext>();
		var next = CreateNextDelegate(_ => new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullContext()
	{
		// Arrange
		var middleware = new TestMiddleware();
		var message = CreateTestAction();
		var next = CreateNextDelegate(_ => new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullDelegate()
	{
		// Arrange
		var middleware = new TestMiddleware();
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region InvokeAsync - Default Processing Tests

	[Fact]
	public async Task InvokeAsync_CallsNextDelegate()
	{
		// Arrange
		var middleware = new TestMiddleware();
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var called = false;
		var next = CreateNextDelegate(_ =>
		{
			called = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		});

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		called.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ReturnsResultFromNextDelegate()
	{
		// Arrange
		var middleware = new TestMiddleware();
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var expectedResult = MessageResult.Success();
		var next = CreateNextDelegate(_ => new ValueTask<IMessageResult>(expectedResult));

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion

	#region ShouldProcess Tests

	[Fact]
	public async Task InvokeAsync_SkipsProcessingWhenShouldProcessReturnsFalse()
	{
		// Arrange
		var middleware = new SkipProcessingMiddleware();
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var nextCalled = false;
		var next = CreateNextDelegate(_ =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		});

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - Next should be called but middleware processing should be skipped
		nextCalled.ShouldBeTrue();
		middleware.ProcessCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task InvokeAsync_ProcessesWhenShouldProcessReturnsTrue()
	{
		// Arrange
		var middleware = new ProcessingMiddleware();
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var next = CreateNextDelegate(_ => new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		middleware.ProcessCalled.ShouldBeTrue();
	}

	#endregion

	#region ApplicableMessageKinds Tests

	[Fact]
	public async Task InvokeAsync_ProcessesActionWhenKindMatches()
	{
		// Arrange
		var middleware = new ActionOnlyMiddleware();
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var next = CreateNextDelegate(_ => new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		middleware.ProcessCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_SkipsActionWhenKindDoesNotMatch()
	{
		// Arrange
		var middleware = new EventOnlyMiddleware();
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var next = CreateNextDelegate(_ => new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		middleware.ProcessCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task InvokeAsync_ProcessesEventWhenKindMatches()
	{
		// Arrange
		var middleware = new EventOnlyMiddleware();
		var message = CreateTestEvent();
		var context = A.Fake<IMessageContext>();
		var next = CreateNextDelegate(_ => new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		middleware.ProcessCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ProcessesDocumentWhenKindMatches()
	{
		// Arrange
		var middleware = new DocumentOnlyMiddleware();
		var message = CreateTestDocument();
		var context = A.Fake<IMessageContext>();
		var next = CreateNextDelegate(_ => new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		middleware.ProcessCalled.ShouldBeTrue();
	}

	#endregion

	#region OnBeforeProcessAsync Tests

	[Fact]
	public async Task InvokeAsync_ShortCircuitsWhenOnBeforeReturnsResult()
	{
		// Arrange
		var shortCircuitResult = MessageResult.Failed("Short-circuited");
		var middleware = new BeforeShortCircuitMiddleware(shortCircuitResult);
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var nextCalled = false;
		var next = CreateNextDelegate(_ =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		});

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(shortCircuitResult);
		nextCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task InvokeAsync_ContinuesWhenOnBeforeReturnsNull()
	{
		// Arrange
		var middleware = new BeforeContinueMiddleware();
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var nextCalled = false;
		var next = CreateNextDelegate(_ =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		});

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		middleware.BeforeCalled.ShouldBeTrue();
	}

	#endregion

	#region OnAfterProcessAsync Tests

	[Fact]
	public async Task InvokeAsync_ReturnsModifiedResultFromOnAfter()
	{
		// Arrange
		var modifiedResult = MessageResult.Success("Modified");
		var middleware = new AfterModifyMiddleware(modifiedResult);
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var next = CreateNextDelegate(_ => new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(modifiedResult);
	}

	[Fact]
	public async Task InvokeAsync_ReturnsOriginalResultWhenOnAfterReturnsNull()
	{
		// Arrange
		var middleware = new AfterNullMiddleware();
		var originalResult = MessageResult.Success("Original");
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var next = CreateNextDelegate(_ => new ValueTask<IMessageResult>(originalResult));

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(originalResult);
		middleware.AfterCalled.ShouldBeTrue();
	}

	#endregion

	#region OnErrorAsync Tests

	[Fact]
	public async Task InvokeAsync_ReturnsErrorResultWhenOnErrorHandles()
	{
		// Arrange
		var errorResult = MessageResult.Failed("Handled error");
		var middleware = new ErrorHandlingMiddleware(errorResult);
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var next = CreateNextDelegate(_ => throw new InvalidOperationException("Test error"));

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(errorResult);
	}

	[Fact]
	public async Task InvokeAsync_ThrowsWhenOnErrorReturnsNull()
	{
		// Arrange
		var middleware = new ErrorRethrowMiddleware();
		var message = CreateTestAction();
		var context = A.Fake<IMessageContext>();
		var next = CreateNextDelegate(_ => throw new InvalidOperationException("Test error"));

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());
		ex.Message.ShouldContain("encountered an error");
		_ = ex.InnerException.ShouldNotBeNull();
		ex.InnerException.Message.ShouldBe("Test error");
	}

	#endregion

	#region Default Property Tests

	[Fact]
	public void Stage_ReturnsNullByDefault()
	{
		// Arrange
		var middleware = new TestMiddleware();

		// Assert
		middleware.Stage.ShouldBeNull();
	}

	[Fact]
	public void ApplicableMessageKinds_ReturnsAllByDefault()
	{
		// Arrange
		var middleware = new TestMiddleware();

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
	}

	[Fact]
	public void RequiredFeatures_ReturnsNullByDefault()
	{
		// Arrange
		var middleware = new TestMiddleware();

		// Assert
		middleware.RequiredFeatures.ShouldBeNull();
	}

	#endregion

	#region Custom Stage Tests

	[Fact]
	public void Stage_CanBeOverridden()
	{
		// Arrange
		var middleware = new CustomStageMiddleware(DispatchMiddlewareStage.PreProcessing);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	#endregion

	#region Required Features Tests

	[Fact]
	public void RequiredFeatures_CanBeOverridden()
	{
		// Arrange
		var features = new List<string> { "feature1", "feature2" };
		var middleware = new RequiredFeaturesMiddleware(features);

		// Assert
		middleware.RequiredFeatures.ShouldBe(features);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIDispatchMiddleware()
	{
		// Arrange
		var middleware = new TestMiddleware();

		// Assert
		_ = middleware.ShouldBeAssignableTo<IDispatchMiddleware>();
	}

	#endregion

	#region Helper Methods

	private static DispatchRequestDelegate CreateNextDelegate(
		Func<IDispatchMessage, ValueTask<IMessageResult>> handler) =>
		(message, _, _) => handler(message);

	private static TestAction CreateTestAction() => new();
	private static TestEvent CreateTestEvent() => new();
	private static TestDocument CreateTestDocument() => new();

	#endregion

	#region Test Types

	private sealed class TestMiddleware : DispatchMiddlewareBase;

	private sealed class SkipProcessingMiddleware : DispatchMiddlewareBase
	{
		public bool ProcessCalled { get; private set; }

		protected override bool ShouldProcess(IDispatchMessage message, IMessageContext context)
			=> false;

		protected override ValueTask<IMessageResult> ProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			ProcessCalled = true;
			return base.ProcessAsync(message, context, nextDelegate, cancellationToken);
		}
	}

	private sealed class ProcessingMiddleware : DispatchMiddlewareBase
	{
		public bool ProcessCalled { get; private set; }

		protected override ValueTask<IMessageResult> ProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			ProcessCalled = true;
			return base.ProcessAsync(message, context, nextDelegate, cancellationToken);
		}
	}

	private sealed class ActionOnlyMiddleware : DispatchMiddlewareBase
	{
		public bool ProcessCalled { get; private set; }

		public override MessageKinds ApplicableMessageKinds => MessageKinds.Action;

		protected override ValueTask<IMessageResult> ProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			ProcessCalled = true;
			return base.ProcessAsync(message, context, nextDelegate, cancellationToken);
		}
	}

	private sealed class EventOnlyMiddleware : DispatchMiddlewareBase
	{
		public bool ProcessCalled { get; private set; }

		public override MessageKinds ApplicableMessageKinds => MessageKinds.Event;

		protected override ValueTask<IMessageResult> ProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			ProcessCalled = true;
			return base.ProcessAsync(message, context, nextDelegate, cancellationToken);
		}
	}

	private sealed class DocumentOnlyMiddleware : DispatchMiddlewareBase
	{
		public bool ProcessCalled { get; private set; }

		public override MessageKinds ApplicableMessageKinds => MessageKinds.Document;

		protected override ValueTask<IMessageResult> ProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			ProcessCalled = true;
			return base.ProcessAsync(message, context, nextDelegate, cancellationToken);
		}
	}

	private sealed class BeforeShortCircuitMiddleware(IMessageResult shortCircuitResult) : DispatchMiddlewareBase
	{
		protected override ValueTask<IMessageResult?> OnBeforeProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
			=> ValueTask.FromResult<IMessageResult?>(shortCircuitResult);
	}

	private sealed class BeforeContinueMiddleware : DispatchMiddlewareBase
	{
		public bool BeforeCalled { get; private set; }

		protected override ValueTask<IMessageResult?> OnBeforeProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
		{
			BeforeCalled = true;
			return ValueTask.FromResult<IMessageResult?>(null);
		}
	}

	private sealed class AfterModifyMiddleware(IMessageResult modifiedResult) : DispatchMiddlewareBase
	{
		protected override ValueTask<IMessageResult?> OnAfterProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			IMessageResult result,
			CancellationToken cancellationToken)
			=> ValueTask.FromResult<IMessageResult?>(modifiedResult);
	}

	private sealed class AfterNullMiddleware : DispatchMiddlewareBase
	{
		public bool AfterCalled { get; private set; }

		protected override ValueTask<IMessageResult?> OnAfterProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			IMessageResult result,
			CancellationToken cancellationToken)
		{
			AfterCalled = true;
			return ValueTask.FromResult<IMessageResult?>(null);
		}
	}

	private sealed class ErrorHandlingMiddleware(IMessageResult errorResult) : DispatchMiddlewareBase
	{
		protected override ValueTask<IMessageResult> ProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
			=> nextDelegate(message, context, cancellationToken);

		protected override ValueTask<IMessageResult?> OnErrorAsync(
			IDispatchMessage message,
			IMessageContext context,
			Exception exception,
			CancellationToken cancellationToken)
			=> ValueTask.FromResult<IMessageResult?>(errorResult);
	}

	private sealed class ErrorRethrowMiddleware : DispatchMiddlewareBase
	{
		protected override ValueTask<IMessageResult> ProcessAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
			=> nextDelegate(message, context, cancellationToken);
	}

	private sealed class CustomStageMiddleware(DispatchMiddlewareStage stage) : DispatchMiddlewareBase
	{
		public override DispatchMiddlewareStage? Stage => stage;
	}

	private sealed class RequiredFeaturesMiddleware(IReadOnlyCollection<string> features) : DispatchMiddlewareBase
	{
		public override IReadOnlyCollection<string>? RequiredFeatures => features;
	}

	private sealed class TestAction : IDispatchAction
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestAction";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class TestEvent : IDispatchEvent
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Event;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestEvent";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class TestDocument : IDispatchDocument
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Document;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestDocument";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	#endregion
}
