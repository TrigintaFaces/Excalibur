// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for the <see cref="FilteredDispatchMiddlewareInvoker"/> class.
/// </summary>
/// <remarks>
/// Sprint 461 - Task S461.1: Remaining 0% Coverage Tests.
/// Tests the filtered middleware invoker that combines applicability evaluation with execution.
/// Implements requirements R2.4-R2.6.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class FilteredDispatchMiddlewareInvokerShould
{
	private static readonly int[] ExpectedOrder123 = [1, 2, 3];
	private static readonly int[] ExpectedOrder13 = [1, 3];

	private readonly IOptions<FilteredInvokerOptions> _options;
	private readonly ILogger<FilteredDispatchMiddlewareInvoker> _logger;

	public FilteredDispatchMiddlewareInvokerShould()
	{
		_options = Microsoft.Extensions.Options.Options.Create(new FilteredInvokerOptions());
		_logger = NullLogger<FilteredDispatchMiddlewareInvoker>.Instance;
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsOnNullMiddleware()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		var features = new HashSet<DispatchFeatures>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new FilteredDispatchMiddlewareInvoker(
			null!,
			evaluator,
			features,
			_options,
			_logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullApplicabilityEvaluator()
	{
		// Arrange
		var middleware = Array.Empty<IDispatchMiddleware>();
		var features = new HashSet<DispatchFeatures>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new FilteredDispatchMiddlewareInvoker(
			middleware,
			null!,
			features,
			_options,
			_logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullEnabledFeatures()
	{
		// Arrange
		var middleware = Array.Empty<IDispatchMiddleware>();
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new FilteredDispatchMiddlewareInvoker(
			middleware,
			evaluator,
			null!,
			_options,
			_logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullOptions()
	{
		// Arrange
		var middleware = Array.Empty<IDispatchMiddleware>();
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		var features = new HashSet<DispatchFeatures>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new FilteredDispatchMiddlewareInvoker(
			middleware,
			evaluator,
			features,
			null!,
			_logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullLogger()
	{
		// Arrange
		var middleware = Array.Empty<IDispatchMiddleware>();
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		var features = new HashSet<DispatchFeatures>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new FilteredDispatchMiddlewareInvoker(
			middleware,
			evaluator,
			features,
			_options,
			null!));
	}

	[Fact]
	public void Constructor_AcceptsValidParameters()
	{
		// Arrange
		var middleware = Array.Empty<IDispatchMiddleware>();
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		var features = new HashSet<DispatchFeatures>();

		// Act
		var sut = new FilteredDispatchMiddlewareInvoker(
			middleware,
			evaluator,
			features,
			_options,
			_logger);

		// Assert
		_ = sut.ShouldNotBeNull();
	}

	#endregion

	#region Stage Property Tests

	[Fact]
	public void Stage_ReturnsProcessing()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.Stage;

		// Assert
		result.ShouldBe(DispatchMiddlewareStage.Processing);
	}

	#endregion

	#region ApplicableMessageKinds Property Tests

	[Fact]
	public void ApplicableMessageKinds_ReturnsAll()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.ApplicableMessageKinds;

		// Assert
		result.ShouldBe(MessageKinds.All);
	}

	#endregion

	#region InvokeAsync Tests - Parameter Validation

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullMessage()
	{
		// Arrange
		var sut = CreateSut();
		var context = A.Fake<IMessageContext>();
		var nextDelegate = CreatePassThroughDelegate();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.InvokeAsync(null!, context, nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullContext()
	{
		// Arrange
		var sut = CreateSut();
		var message = new TestCommand();
		var nextDelegate = CreatePassThroughDelegate();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.InvokeAsync(message, null!, nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullNextDelegate()
	{
		// Arrange
		var sut = CreateSut();
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	#endregion

	#region InvokeAsync Tests - No Middleware

	[Fact]
	public async Task InvokeAsync_CallsNextDelegateWhenNoMiddleware()
	{
		// Arrange
		var sut = CreateSut();
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var nextDelegateCalled = false;
		DispatchRequestDelegate nextDelegate = (msg, ctx, ct) =>
		{
			nextDelegateCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextDelegateCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ReturnsNextDelegateResultWhenNoApplicableMiddleware()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		var middleware = new[] { A.Fake<IDispatchMiddleware>() };

		// Configure evaluator to reject all middleware
		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Returns(false);

		var sut = CreateSut(middleware: middleware, evaluator: evaluator);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = MessageResult.Success();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion

	#region InvokeAsync Tests - With Middleware

	[Fact]
	public async Task InvokeAsync_ExecutesApplicableMiddleware()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		var middleware = A.Fake<IDispatchMiddleware>();
		var middlewareInvoked = false;

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Returns(true);

		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				middlewareInvoked = true;
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return await next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		var sut = CreateSut(middleware: new[] { middleware }, evaluator: evaluator);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		middlewareInvoked.ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ExecutesMiddlewareInOrder()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		var executionOrder = new List<int>();

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Returns(true);

		var middleware1 = CreateOrderedMiddleware(1, executionOrder);
		var middleware2 = CreateOrderedMiddleware(2, executionOrder);
		var middleware3 = CreateOrderedMiddleware(3, executionOrder);

		var sut = CreateSut(middleware: new[] { middleware1, middleware2, middleware3 }, evaluator: evaluator);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		executionOrder.ShouldBe(ExpectedOrder123);
	}

	[Fact]
	public async Task InvokeAsync_SkipsNonApplicableMiddleware()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		var executionOrder = new List<int>();

		var middleware1 = new OrderedMiddleware1(executionOrder);
		var middleware2 = new OrderedMiddleware2(executionOrder);
		var middleware3 = new OrderedMiddleware3(executionOrder);

		// Only middleware 1 and 3 are applicable (using concrete types)
		_ = A.CallTo(() => evaluator.IsApplicable(typeof(OrderedMiddleware1), A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Returns(true);
		_ = A.CallTo(() => evaluator.IsApplicable(typeof(OrderedMiddleware2), A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Returns(false);
		_ = A.CallTo(() => evaluator.IsApplicable(typeof(OrderedMiddleware3), A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Returns(true);

		var sut = CreateSut(middleware: new IDispatchMiddleware[] { middleware1, middleware2, middleware3 }, evaluator: evaluator);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		executionOrder.ShouldBe(ExpectedOrder13);
	}

	#endregion

	#region Message Kind Detection Tests

	[Fact]
	public async Task InvokeAsync_DetectsCommandByNamingConvention()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		MessageKinds? detectedKind = null;

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Invokes(call => detectedKind = call.GetArgument<MessageKinds>(1))
			.Returns(false);

		var sut = CreateSut(middleware: new[] { A.Fake<IDispatchMiddleware>() }, evaluator: evaluator);
		var message = new TestCommand(); // Ends with "Command"
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		detectedKind.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public async Task InvokeAsync_DetectsEventByNamingConvention()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		MessageKinds? detectedKind = null;

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Invokes(call => detectedKind = call.GetArgument<MessageKinds>(1))
			.Returns(false);

		var sut = CreateSut(middleware: new[] { A.Fake<IDispatchMiddleware>() }, evaluator: evaluator);
		var message = new TestEvent(); // Ends with "Event"
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		detectedKind.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public async Task InvokeAsync_DetectsDocumentByNamingConvention()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		MessageKinds? detectedKind = null;

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Invokes(call => detectedKind = call.GetArgument<MessageKinds>(1))
			.Returns(false);

		var sut = CreateSut(middleware: new[] { A.Fake<IDispatchMiddleware>() }, evaluator: evaluator);
		var message = new TestDocument(); // Ends with "Document"
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		detectedKind.ShouldBe(MessageKinds.Document);
	}

	[Fact]
	public async Task InvokeAsync_DetectsQueryAsDocument()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		MessageKinds? detectedKind = null;

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Invokes(call => detectedKind = call.GetArgument<MessageKinds>(1))
			.Returns(false);

		var sut = CreateSut(middleware: new[] { A.Fake<IDispatchMiddleware>() }, evaluator: evaluator);
		var message = new TestQuery(); // Ends with "Query"
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		detectedKind.ShouldBe(MessageKinds.Document);
	}

	[Fact]
	public async Task InvokeAsync_DetectsNotificationAsEvent()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		MessageKinds? detectedKind = null;

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Invokes(call => detectedKind = call.GetArgument<MessageKinds>(1))
			.Returns(false);

		var sut = CreateSut(middleware: new[] { A.Fake<IDispatchMiddleware>() }, evaluator: evaluator);
		var message = new TestNotification(); // Ends with "Notification"
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		detectedKind.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public async Task InvokeAsync_DefaultsToActionForUnknownType()
	{
		// Arrange
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		MessageKinds? detectedKind = null;

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Invokes(call => detectedKind = call.GetArgument<MessageKinds>(1))
			.Returns(false);

		var sut = CreateSut(middleware: new[] { A.Fake<IDispatchMiddleware>() }, evaluator: evaluator);
		var message = new TestMessage(); // Generic name, no convention
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		detectedKind.ShouldBe(MessageKinds.Action);
	}

	#endregion

	#region Caching Tests

	[Fact]
	public async Task InvokeAsync_CachesFilteredMiddlewareWhenCachingEnabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new FilteredInvokerOptions { EnableCaching = true });
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Returns(true);

		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return await next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		var sut = CreateSut(middleware: new[] { middleware }, evaluator: evaluator, options: options);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act - Call twice with same message kind
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert - Evaluator should only be called once per middleware (cached)
		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_DoesNotCacheWhenCachingDisabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new FilteredInvokerOptions { EnableCaching = false });
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Returns(true);

		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return await next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		var sut = CreateSut(middleware: new[] { middleware }, evaluator: evaluator, options: options);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act - Call twice with same message kind
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert - Evaluator should be called twice (not cached)
		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.MustHaveHappenedTwiceExactly();
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task InvokeAsync_IncludesMiddlewareOnFilterErrorWhenConfigured()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new FilteredInvokerOptions { IncludeMiddlewareOnFilterError = true });
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		var middlewareExecuted = false;

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Throws(new InvalidOperationException("Evaluation error"));

		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				middlewareExecuted = true;
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return await next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		var sut = CreateSut(middleware: new[] { middleware }, evaluator: evaluator, options: options);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		middlewareExecuted.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ExcludesMiddlewareOnFilterErrorByDefault()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new FilteredInvokerOptions { IncludeMiddlewareOnFilterError = false });
		var evaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		var middlewareExecuted = false;

		_ = A.CallTo(() => evaluator.IsApplicable(A<Type>._, A<MessageKinds>._, A<IReadOnlySet<DispatchFeatures>>._))
			.Throws(new InvalidOperationException("Evaluation error"));

		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				middlewareExecuted = true;
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return await next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		var sut = CreateSut(middleware: new[] { middleware }, evaluator: evaluator, options: options);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		middlewareExecuted.ShouldBeFalse();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void Invoker_ImplementsIDispatchMiddleware()
	{
		// Arrange
		var sut = CreateSut();

		// Assert
		_ = sut.ShouldBeAssignableTo<IDispatchMiddleware>();
	}

	#endregion

	#region Helper Methods

	private FilteredDispatchMiddlewareInvoker CreateSut(
		IEnumerable<IDispatchMiddleware>? middleware = null,
		IDispatchMiddlewareApplicabilityEvaluator? evaluator = null,
		IReadOnlySet<DispatchFeatures>? features = null,
		IOptions<FilteredInvokerOptions>? options = null)
	{
		return new FilteredDispatchMiddlewareInvoker(
			middleware ?? Array.Empty<IDispatchMiddleware>(),
			evaluator ?? A.Fake<IDispatchMiddlewareApplicabilityEvaluator>(),
			features ?? new HashSet<DispatchFeatures>(),
			options ?? _options,
			_logger);
	}

	private static DispatchRequestDelegate CreatePassThroughDelegate()
	{
		return (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	private static IDispatchMiddleware CreateOrderedMiddleware(int order, List<int> executionOrder)
	{
		var middleware = A.Fake<IDispatchMiddleware>();

		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				executionOrder.Add(order);
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return await next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		return middleware;
	}

	#endregion

	#region Test Types

	private sealed class TestCommand : IDispatchMessage { }

	private sealed class TestEvent : IDispatchMessage { }

	private sealed class TestDocument : IDispatchMessage { }

	private sealed class TestQuery : IDispatchMessage { }

	private sealed class TestNotification : IDispatchMessage { }

	private sealed class TestMessage : IDispatchMessage { }

	/// <summary>
	/// Concrete middleware implementation for testing type-based filtering.
	/// </summary>
	private sealed class OrderedMiddleware1(List<int> executionOrder) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			executionOrder.Add(1);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Concrete middleware implementation for testing type-based filtering.
	/// </summary>
	private sealed class OrderedMiddleware2(List<int> executionOrder) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			executionOrder.Add(2);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Concrete middleware implementation for testing type-based filtering.
	/// </summary>
	private sealed class OrderedMiddleware3(List<int> executionOrder) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			executionOrder.Add(3);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	#endregion
}
