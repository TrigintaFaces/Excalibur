// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Documentation;

/// <summary>
/// Verification tests that key documentation code examples compile correctly.
/// </summary>
/// <remarks>
/// Sprint 447 S447.6: Verify documentation code examples compile.
/// These tests ensure that API examples in documentation match the actual implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Documentation")]
public sealed class DocumentationExamplesShould
{
	#region MessageResult API Examples (results-and-errors.md)

	[Fact]
	public void Compile_MessageResultSuccessExamples()
	{
		// Documentation: Creating Success Results
		// Simple success (no return value)
		IMessageResult result1 = MessageResult.Success();
		result1.Succeeded.ShouldBeTrue();

		// Success with a typed value
		var order = new TestOrder { Id = Guid.NewGuid() };
		IMessageResult<TestOrder> result2 = MessageResult.Success(order);
		result2.Succeeded.ShouldBeTrue();
		result2.ReturnValue.ShouldBe(order);

		// Success from cache hit
		IMessageResult result3 = MessageResult.SuccessFromCache();
		result3.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void Compile_MessageResultFailedExamples()
	{
		// Documentation: Creating Failed Results
		// Simple failure with error message
		IMessageResult result1 = MessageResult.Failed("Order not found");
		result1.Succeeded.ShouldBeFalse();
		result1.ErrorMessage.ShouldBe("Order not found");

		// Failure with problem details
		var problemDetails = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.NotFound, // "urn:dispatch:error:not-found"
			Title = "Resource Not Found",
			Status = 404,
			Detail = "Order with ID 123 was not found"
		};
		IMessageResult result2 = MessageResult.Failed(problemDetails);
		_ = result2.ProblemDetails.ShouldNotBeNull();
		// Note: Status is on concrete MessageProblemDetails, not IMessageProblemDetails interface
		(result2.ProblemDetails as MessageProblemDetails)?.Status.ShouldBe(404);

		// Typed failure
		IMessageResult<TestOrder> result3 = MessageResult.Failed<TestOrder>("Validation failed", problemDetails);
		result3.Succeeded.ShouldBeFalse();
		result3.ReturnValue.ShouldBeNull();
	}

	[Fact]
	public void Compile_MessageResultCheckingExamples()
	{
		// Documentation: Checking Results
		var result = MessageResult.Success(new TestOrder { Id = Guid.NewGuid() });

		// Check success
		if (result.Succeeded)
		{
			// Handle success
		}

		// Alternative syntax
		if (result.IsSuccess)
		{
			// Handle success
		}

		// Check for cache hit
		if (result.CacheHit)
		{
			// Result served from cache
		}

		// Access error information
		var failedResult = MessageResult.Failed<TestOrder>("Error occurred");
		if (!failedResult.Succeeded)
		{
			_ = failedResult.ErrorMessage;
			_ = failedResult.ProblemDetails?.Detail;
		}

		result.Succeeded.ShouldBeTrue();
		failedResult.Succeeded.ShouldBeFalse();
	}

	#endregion

	#region Functional Composition Examples (results-and-errors.md)

	[Fact]
	public void Compile_MapExtensionExamples()
	{
		// Documentation: Map - Transform Success Values
		var result = MessageResult.Success(new TestOrder { Id = Guid.NewGuid(), CustomerId = "C123" });

		// Sync transformation
		var dto = result.Map(order => new TestOrderDto { OrderId = order.Id });
		dto.Succeeded.ShouldBeTrue();
		_ = dto.ReturnValue.ShouldNotBeNull();
	}

	[Fact]
	public async Task Compile_MapAsyncExtensionExamples()
	{
		// Documentation: Async transformation
		var result = MessageResult.Success(new TestOrder { Id = Guid.NewGuid() });

		var dto = await result.MapAsync(async order =>
		{
			await Task.Delay(1); // Simulate async work
			return new TestOrderDto { OrderId = order.Id };
		});

		dto.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Compile_BindExtensionExamples()
	{
		// Documentation: Bind - Chain Result Operations
		var orderResult = MessageResult.Success(new TestOrder { Id = Guid.NewGuid(), Status = "Active" });

		// Chain sync operations
		var finalResult = orderResult.Bind(order =>
		{
			if (order.Status == "Cancelled")
				return MessageResult.Failed<TestShippingInfo>("Cannot ship cancelled order");

			return MessageResult.Success(new TestShippingInfo { TrackingNumber = "TRK123" });
		});

		finalResult.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task Compile_BindAsyncExtensionExamples()
	{
		// Documentation: Chain async operations
		var orderResult = MessageResult.Success(new TestOrder { Id = Guid.NewGuid() });

		var finalResult = await orderResult.BindAsync(async order =>
		{
			await Task.Delay(1); // Simulate async check
			return MessageResult.Success(new TestShipmentResult { Success = true });
		});

		finalResult.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Compile_MatchExtensionExamples()
	{
		// Documentation: Match - Pattern Matching
		var result = MessageResult.Success(new TestOrder { Id = Guid.NewGuid() });

		// Sync match
		var response = result.Match(
			onSuccess: order => $"Found order {order.Id}",
			onFailure: problem => $"Error: {problem?.Detail ?? "Unknown error"}"
		);

		response.ShouldStartWith("Found order");
	}

	[Fact]
	public async Task Compile_MatchAsyncExtensionExamples()
	{
		// Documentation: Async match
		var resultTask = Task.FromResult(MessageResult.Success(new TestOrder { Id = Guid.NewGuid() }));

		var response = await resultTask.Match(
			onSuccess: order => $"Found order {order.Id}",
			onFailure: problem => $"Error: {problem?.Detail ?? "Unknown error"}"
		);

		response.ShouldStartWith("Found order");
	}

	[Fact]
	public void Compile_TapExtensionExamples()
	{
		// Documentation: Tap - Side Effects
		var logs = new List<string>();
		var result = MessageResult.Success(new TestOrder { Id = Guid.NewGuid() });

		// Sync tap (logging, metrics)
		var tappedResult = result.Tap(order => logs.Add($"Order {order.Id} retrieved"));

		logs.Count.ShouldBe(1);
		tappedResult.ShouldBe(result); // Returns same result
	}

	[Fact]
	public void Compile_GetValueOrDefaultExamples()
	{
		// Documentation: GetValueOrDefault - Safe Value Access
		var result = MessageResult.Success(new TestOrder { Id = Guid.NewGuid() });

		// With default value
		var order = result.GetValueOrDefault(TestOrder.Empty);
		_ = order.ShouldNotBeNull();

		// With null default
		var order2 = result.GetValueOrDefault();
		_ = order2.ShouldNotBeNull();

		// Failed result uses default
		var failed = MessageResult.Failed<TestOrder>("Not found");
		var order3 = failed.GetValueOrDefault(TestOrder.Empty);
		order3.ShouldBe(TestOrder.Empty);
	}

	[Fact]
	public void Compile_GetValueOrThrowExamples()
	{
		// Documentation: GetValueOrThrow - Fail Fast
		var result = MessageResult.Success(new TestOrder { Id = Guid.NewGuid() });

		// Throws InvalidOperationException if failed
		var order = result.GetValueOrThrow();
		_ = order.ShouldNotBeNull();

		// Verify it throws for failed result
		var failed = MessageResult.Failed<TestOrder>("Not found");
		_ = Should.Throw<InvalidOperationException>(() => failed.GetValueOrThrow());
	}

	#endregion

	#region Problem Details Examples (results-and-errors.md)

	[Fact]
	public void Compile_ProblemDetailsTypeConstants()
	{
		// Documentation: Standard Problem Details Type URIs
		// Verify all documented constants exist and have correct values

		ProblemDetailsTypes.Validation.ShouldBe("urn:dispatch:error:validation");
		ProblemDetailsTypes.NotFound.ShouldBe("urn:dispatch:error:not-found");
		ProblemDetailsTypes.Conflict.ShouldBe("urn:dispatch:error:conflict");
		ProblemDetailsTypes.Forbidden.ShouldBe("urn:dispatch:error:forbidden");
		ProblemDetailsTypes.Unauthorized.ShouldBe("urn:dispatch:error:unauthorized");
		ProblemDetailsTypes.Timeout.ShouldBe("urn:dispatch:error:timeout");
		ProblemDetailsTypes.RateLimited.ShouldBe("urn:dispatch:error:rate-limited");
		ProblemDetailsTypes.Internal.ShouldBe("urn:dispatch:error:internal");
		ProblemDetailsTypes.Routing.ShouldBe("urn:dispatch:error:routing");
		ProblemDetailsTypes.Transport.ShouldBe("urn:dispatch:error:transport");
		ProblemDetailsTypes.Serialization.ShouldBe("urn:dispatch:error:serialization");
		ProblemDetailsTypes.Concurrency.ShouldBe("urn:dispatch:error:concurrency");
		ProblemDetailsTypes.HandlerNotFound.ShouldBe("urn:dispatch:error:handler-not-found");
		ProblemDetailsTypes.HandlerError.ShouldBe("urn:dispatch:error:handler-error");
		ProblemDetailsTypes.MappingFailed.ShouldBe("urn:dispatch:error:mapping-failed");
		ProblemDetailsTypes.BackgroundExecution.ShouldBe("urn:dispatch:error:background-execution");
	}

	[Fact]
	public void Compile_MessageProblemDetailsCreation()
	{
		// Documentation: Creating Problem Details
		var problemDetails = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.Validation, // "urn:dispatch:error:validation"
			Title = "Insufficient Funds",
			Status = 402,
			Detail = "Account 123 has insufficient funds. Required: $100, Available: $50",
			Instance = "/orders/456"
		};

		problemDetails.Type.ShouldBe(ProblemDetailsTypes.Validation);
		problemDetails.Status.ShouldBe(402);
	}

	#endregion

	#region Inbox Configuration Examples (inbox.md)

	[Fact]
	public void Compile_IdempotentAttributeExists()
	{
		// Documentation: [Idempotent] attribute
		// Verify the attribute exists and can be applied
		var attribute = new IdempotentAttribute();
		_ = attribute.ShouldNotBeNull();
	}

	[Fact]
	public void Compile_InboxConfigurationBuilderFluentApi()
	{
		// Documentation: ConfigureInbox() fluent API
		var builder = new InboxConfigurationBuilder();

		// ForHandler<T>
		_ = builder.ForHandler<TestHandler>()
			.WithRetention(TimeSpan.FromHours(24));

		// ForHandlersMatching with predicate
		_ = builder.ForHandlersMatching(
			t => t.Name.EndsWith("Handler"),
			cfg => cfg.UseInMemory());

		// ForNamespace
		_ = builder.ForNamespace("MyApp.Handlers.Financial",
			cfg => cfg.WithRetention(TimeSpan.FromDays(7)));

		// Build and verify
		var provider = builder.Build(new[] { typeof(TestHandler) });
		_ = provider.ShouldNotBeNull();
	}

	[Fact]
	public void Compile_InboxHandlerConfigurationMethods()
	{
		// Documentation: IInboxHandlerConfiguration fluent methods
		var config = new InboxHandlerConfiguration();

		// WithRetention
		_ = config.WithRetention(TimeSpan.FromHours(48));
		var settings = config.Build();
		settings.Retention.ShouldBe(TimeSpan.FromHours(48));

		// UseInMemory
		config = new InboxHandlerConfiguration();
		_ = config.UseInMemory();
		settings = config.Build();
		settings.UseInMemory.ShouldBeTrue();

		// UsePersistent
		config = new InboxHandlerConfiguration();
		_ = config.UsePersistent();
		settings = config.Build();
		settings.UseInMemory.ShouldBeFalse();

		// WithStrategy
		config = new InboxHandlerConfiguration();
		_ = config.WithStrategy(MessageIdStrategy.FromCorrelationId);
		settings = config.Build();
		settings.Strategy.ShouldBe(MessageIdStrategy.FromCorrelationId);

		// WithHeaderName
		config = new InboxHandlerConfiguration();
		_ = config.WithHeaderName("X-Idempotency-Key");
		settings = config.Build();
		settings.HeaderName.ShouldBe("X-Idempotency-Key");

		// WithMessageIdProvider<T>
		config = new InboxHandlerConfiguration();
		_ = config.WithMessageIdProvider<TestMessageIdProvider>();
		settings = config.Build();
		settings.MessageIdProviderType.ShouldBe(typeof(TestMessageIdProvider));
		settings.Strategy.ShouldBe(MessageIdStrategy.Custom);
	}

	[Fact]
	public void Compile_MessageIdStrategyValues()
	{
		// Documentation: All MessageIdStrategy values
		var strategies = new[]
		{
			MessageIdStrategy.FromHeader,
			MessageIdStrategy.FromCorrelationId,
			MessageIdStrategy.CompositeKey,
			MessageIdStrategy.Custom
		};

		strategies.Length.ShouldBe(4);
	}

	#endregion

	#region Getting Started Examples (getting-started.md)

	[Fact]
	public void Compile_ActionDefinitionExamples()
	{
		// Documentation: Define an Action
		// Action without return value - uses IDispatchAction
		var createOrder = new CreateOrderTestAction("C123", new List<string> { "Item1", "Item2" });
		(createOrder is IDispatchAction).ShouldBeTrue();

		// Action with return value - uses IDispatchAction<T>
		var getOrder = new GetOrderTestAction(Guid.NewGuid());
		(getOrder is IDispatchAction<TestOrder>).ShouldBeTrue();
	}

	[Fact]
	public void Compile_HandlerInterfaceExamples()
	{
		// Documentation: Create a Handler
		// IActionHandler<TAction> for commands
		var createHandler = new CreateOrderTestHandler();
		(createHandler is IActionHandler<CreateOrderTestAction>).ShouldBeTrue();

		// IActionHandler<TAction, TResult> for queries
		var getHandler = new GetOrderTestHandler();
		(getHandler is IActionHandler<GetOrderTestAction, TestOrder>).ShouldBeTrue();
	}

	#endregion

	#region Test Fixtures

	private sealed class TestOrder
	{
		public static TestOrder Empty { get; } = new();
		public Guid Id { get; init; }
		public string CustomerId { get; init; } = string.Empty;
		public string Status { get; init; } = "Active";
		public List<string> Items { get; init; } = [];
	}

	private sealed class TestOrderDto
	{
		public Guid OrderId { get; init; }
	}

	private sealed class TestShippingInfo
	{
		public string TrackingNumber { get; init; } = string.Empty;
	}

	private sealed class TestShipmentResult
	{
		public bool Success { get; init; }
	}

	private sealed class TestHandler { }

	private sealed class TestMessageIdProvider : IMessageIdProvider
	{
		public string? GetMessageId(IDispatchMessage message, IMessageContext context)
			=> Guid.NewGuid().ToString();
	}

	// Getting Started action examples
	private record CreateOrderTestAction(string CustomerId, List<string> Items) : IDispatchAction;

	private record GetOrderTestAction(Guid OrderId) : IDispatchAction<TestOrder>;

	// Getting Started handler examples
	private sealed class CreateOrderTestHandler : IActionHandler<CreateOrderTestAction>
	{
		public Task HandleAsync(CreateOrderTestAction action, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class GetOrderTestHandler : IActionHandler<GetOrderTestAction, TestOrder>
	{
		public Task<TestOrder> HandleAsync(GetOrderTestAction action, CancellationToken cancellationToken)
			=> Task.FromResult(new TestOrder { Id = action.OrderId });
	}

	#endregion
}
