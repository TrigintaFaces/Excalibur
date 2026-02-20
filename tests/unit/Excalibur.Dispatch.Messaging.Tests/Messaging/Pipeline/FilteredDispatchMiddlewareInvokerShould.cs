// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Pipeline;

/// <summary>
/// Unit tests for <see cref="FilteredDispatchMiddlewareInvoker" /> covering middleware filtering,
/// applicability evaluation, and execution chain behavior.
/// </summary>
/// <remarks>
/// Sprint 461 - S461.1: Coverage tests for 0% coverage classes.
/// Target: Increase FilteredDispatchMiddlewareInvoker coverage from 0% to 80%+.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class FilteredDispatchMiddlewareInvokerShould
{
	private readonly ILogger<FilteredDispatchMiddlewareInvoker> _logger;
	private readonly IDispatchMiddlewareApplicabilityEvaluator _applicabilityEvaluator;
	private readonly IReadOnlySet<DispatchFeatures> _enabledFeatures;
	private readonly IOptions<FilteredInvokerOptions> _options;

	public FilteredDispatchMiddlewareInvokerShould()
	{
		_logger = A.Fake<ILogger<FilteredDispatchMiddlewareInvoker>>();
		_applicabilityEvaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		_enabledFeatures = new HashSet<DispatchFeatures>();
		_options = Microsoft.Extensions.Options.Options.Create(new FilteredInvokerOptions());

		// Default: all middleware applicable
		_ = A.CallTo(() => _applicabilityEvaluator.IsApplicable(
			A<Type>._,
			A<MessageKinds>._,
			A<IReadOnlySet<DispatchFeatures>>._))
			.Returns(true);
	}

	private FilteredDispatchMiddlewareInvoker CreateInvoker(
		IEnumerable<IDispatchMiddleware>? middleware = null,
		FilteredInvokerOptions? options = null)
	{
		var actualMiddleware = middleware ?? Enumerable.Empty<IDispatchMiddleware>();
		var actualOptions = options != null
			? Microsoft.Extensions.Options.Options.Create(options)
			: _options;

		return new FilteredDispatchMiddlewareInvoker(
			actualMiddleware,
			_applicabilityEvaluator,
			_enabledFeatures,
			actualOptions,
			_logger);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_Should_Throw_When_Middleware_Is_Null()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new FilteredDispatchMiddlewareInvoker(
				null!,
				_applicabilityEvaluator,
				_enabledFeatures,
				_options,
				_logger));
	}

	[Fact]
	public void Constructor_Should_Throw_When_ApplicabilityEvaluator_Is_Null()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new FilteredDispatchMiddlewareInvoker(
				Enumerable.Empty<IDispatchMiddleware>(),
				null!,
				_enabledFeatures,
				_options,
				_logger));
	}

	[Fact]
	public void Constructor_Should_Throw_When_EnabledFeatures_Is_Null()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new FilteredDispatchMiddlewareInvoker(
				Enumerable.Empty<IDispatchMiddleware>(),
				_applicabilityEvaluator,
				null!,
				_options,
				_logger));
	}

	[Fact]
	public void Constructor_Should_Throw_When_Options_Is_Null()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new FilteredDispatchMiddlewareInvoker(
				Enumerable.Empty<IDispatchMiddleware>(),
				_applicabilityEvaluator,
				_enabledFeatures,
				null!,
				_logger));
	}

	[Fact]
	public void Constructor_Should_Throw_When_Logger_Is_Null()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new FilteredDispatchMiddlewareInvoker(
				Enumerable.Empty<IDispatchMiddleware>(),
				_applicabilityEvaluator,
				_enabledFeatures,
				_options,
				null!));
	}

	[Fact]
	public void Constructor_Should_Accept_Valid_Parameters()
	{
		// Act
		var invoker = CreateInvoker();

		// Assert
		_ = invoker.ShouldNotBeNull();
	}

	#endregion

	#region Stage and ApplicableMessageKinds Properties

	[Fact]
	public void Stage_Should_Return_Processing()
	{
		// Arrange
		var invoker = CreateInvoker();

		// Act
		var stage = invoker.Stage;

		// Assert
		stage.ShouldBe(DispatchMiddlewareStage.Processing);
	}

	[Fact]
	public void ApplicableMessageKinds_Should_Return_All()
	{
		// Arrange
		var invoker = CreateInvoker();

		// Act
		var kinds = invoker.ApplicableMessageKinds;

		// Assert
		kinds.ShouldBe(MessageKinds.All);
	}

	#endregion

	#region InvokeAsync Tests

	[Fact]
	public async Task InvokeAsync_Should_Throw_When_Message_Is_Null()
	{
		// Arrange
		var invoker = CreateInvoker();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			invoker.InvokeAsync(null!, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_Should_Throw_When_Context_Is_Null()
	{
		// Arrange
		var invoker = CreateInvoker();
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			invoker.InvokeAsync(message, null!, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_Should_Throw_When_NextDelegate_Is_Null()
	{
		// Arrange
		var invoker = CreateInvoker();
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			invoker.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_Should_Call_NextDelegate_When_No_Middleware_Applicable()
	{
		// Arrange - No middleware
		var invoker = CreateInvoker(Enumerable.Empty<IDispatchMiddleware>());
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		var nextCalled = false;

		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};

		// Act
		var result = await invoker.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task InvokeAsync_Should_Execute_Applicable_Middleware()
	{
		// Arrange
		var middlewareCalled = false;
		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Invokes(() => middlewareCalled = true)
			.ReturnsLazily(call =>
			{
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		var invoker = CreateInvoker(new[] { middleware });
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await invoker.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		middlewareCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_Should_Skip_Middleware_When_Not_Applicable()
	{
		// Arrange - Create an evaluator that returns false for all middleware
		var strictEvaluator = A.Fake<IDispatchMiddlewareApplicabilityEvaluator>();
		_ = A.CallTo(() => strictEvaluator.IsApplicable(
			A<Type>._,
			A<MessageKinds>._,
			A<IReadOnlySet<DispatchFeatures>>._))
			.Returns(false);

		var middleware = A.Fake<IDispatchMiddleware>();
		var middlewareCalled = false;
		_ = A.CallTo(() => middleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Invokes(() => middlewareCalled = true)
			.ReturnsLazily(call =>
			{
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		// Create invoker with the strict evaluator
		var invoker = new FilteredDispatchMiddlewareInvoker(
			new[] { middleware },
			strictEvaluator,
			_enabledFeatures,
			_options,
			_logger);

		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		_ = await invoker.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - middleware should NOT be called because evaluator returned false
		middlewareCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task InvokeAsync_Should_Execute_Multiple_Middleware_In_Order()
	{
		// Arrange
		var executionOrder = new List<int>();
		var middleware1 = CreateTrackingMiddleware(1, executionOrder);
		var middleware2 = CreateTrackingMiddleware(2, executionOrder);
		var middleware3 = CreateTrackingMiddleware(3, executionOrder);

		var invoker = CreateInvoker(new[] { middleware1, middleware2, middleware3 });
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		_ = await invoker.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		executionOrder.Count.ShouldBe(3);
		executionOrder[0].ShouldBe(1);
		executionOrder[1].ShouldBe(2);
		executionOrder[2].ShouldBe(3);
	}

	#endregion

	#region Caching Tests

	[Fact]
	public async Task InvokeAsync_Should_Cache_Filtered_Middleware_When_Caching_Enabled()
	{
		// Arrange
		var options = new FilteredInvokerOptions { EnableCaching = true };
		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		var invoker = CreateInvoker(new[] { middleware }, options);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		// Act - invoke twice
		_ = await invoker.InvokeAsync(message, context, next, CancellationToken.None);
		_ = await invoker.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - applicability should only be evaluated once due to caching
		_ = A.CallTo(() => _applicabilityEvaluator.IsApplicable(
			A<Type>._,
			A<MessageKinds>._,
			A<IReadOnlySet<DispatchFeatures>>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_Should_Not_Cache_When_Caching_Disabled()
	{
		// Arrange
		var options = new FilteredInvokerOptions { EnableCaching = false };
		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		var invoker = CreateInvoker(new[] { middleware }, options);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		// Act - invoke twice
		_ = await invoker.InvokeAsync(message, context, next, CancellationToken.None);
		_ = await invoker.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - applicability should be evaluated for each call
		_ = A.CallTo(() => _applicabilityEvaluator.IsApplicable(
			A<Type>._,
			A<MessageKinds>._,
			A<IReadOnlySet<DispatchFeatures>>._))
			.MustHaveHappenedTwiceExactly();
	}

	#endregion

	#region Message Kind Classification Tests

	[Fact]
	public async Task InvokeAsync_Should_Classify_Commands_As_Action()
	{
		// Arrange - use naming convention for classification (TestCommand ends with "Command")
		var invoker = CreateInvoker(Enumerable.Empty<IDispatchMiddleware>());
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};

		// Act
		_ = await invoker.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_Should_Classify_Events_As_Event()
	{
		// Arrange
		var invoker = CreateInvoker(Enumerable.Empty<IDispatchMiddleware>());
		var message = new TestEvent(); // Ends with "Event"
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};

		// Act
		_ = await invoker.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task InvokeAsync_Should_Include_Middleware_On_Error_When_Configured()
	{
		// Arrange
		var options = new FilteredInvokerOptions { IncludeMiddlewareOnFilterError = true, EnableCaching = false };
		var middleware = A.Fake<IDispatchMiddleware>();
		var middlewareCalled = false;

		_ = A.CallTo(() => middleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Invokes(() => middlewareCalled = true)
			.ReturnsLazily(call =>
			{
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		// Make evaluator throw
		_ = A.CallTo(() => _applicabilityEvaluator.IsApplicable(
			A<Type>._,
			A<MessageKinds>._,
			A<IReadOnlySet<DispatchFeatures>>._))
			.Throws(new InvalidOperationException("Test exception"));

		var invoker = CreateInvoker(new[] { middleware }, options);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		_ = await invoker.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - middleware should still be called due to IncludeMiddlewareOnFilterError
		middlewareCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_Should_Exclude_Middleware_On_Error_When_Not_Configured()
	{
		// Arrange
		var options = new FilteredInvokerOptions { IncludeMiddlewareOnFilterError = false, EnableCaching = false };
		var middleware = A.Fake<IDispatchMiddleware>();
		var middlewareCalled = false;

		_ = A.CallTo(() => middleware.InvokeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<DispatchRequestDelegate>._,
			A<CancellationToken>._))
			.Invokes(() => middlewareCalled = true)
			.ReturnsLazily(call =>
			{
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				return next(
					call.GetArgument<IDispatchMessage>(0),
					call.GetArgument<IMessageContext>(1),
					call.GetArgument<CancellationToken>(3));
			});

		// Make evaluator throw
		_ = A.CallTo(() => _applicabilityEvaluator.IsApplicable(
			A<Type>._,
			A<MessageKinds>._,
			A<IReadOnlySet<DispatchFeatures>>._))
			.Throws(new InvalidOperationException("Test exception"));

		var invoker = CreateInvoker(new[] { middleware }, options);
		var message = new TestCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		_ = await invoker.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - middleware should NOT be called
		middlewareCalled.ShouldBeFalse();
	}

	#endregion

	#region Helper Methods and Test Classes

	private static IDispatchMiddleware CreateTrackingMiddleware(int order, List<int> executionOrder)
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

	private sealed class TestCommand : IDispatchMessage
	{
		public Guid MessageId { get; } = Guid.NewGuid();
		public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
	}

	private sealed class TestEvent : IDispatchMessage
	{
		public Guid MessageId { get; } = Guid.NewGuid();
		public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
	}

	#endregion
}
