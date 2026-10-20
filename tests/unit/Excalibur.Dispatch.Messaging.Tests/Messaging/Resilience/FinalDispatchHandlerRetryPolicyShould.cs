// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Telemetry;
using Tests.Shared.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
/// Integration tests for FinalDispatchHandler with IRetryPolicy implementations.
/// Verifies FinalDispatchHandler behaves equivalently under a retrying policy and a no-op one.
/// Task: Excalibur.Dispatch-4n0b
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class FinalDispatchHandlerRetryPolicyShould
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task PreserveOuterRetryBudgetAndOriginalFailure(bool transient)
	{
		_busOptionsMap["Local"] = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		var policy = CreatePollyPolicy();
		var handler = CreateHandler(policy);
		var middleware = CreateRetryMiddleware();
		var action = A.Fake<IDispatchAction>();
		var context = CreateContext();
		var calls = 0;
		Exception fault = transient ? new IOException("before effect") : new ArgumentException("permanent");
		A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._))
			.Invokes(() => { calls++; throw fault; });
		Exception? observed = null;
		try
		{
			await middleware.InvokeAsync(action, context, (message, ctx, token) => handler.HandleAsync(message, ctx, token), CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			observed = exception;
		}
		calls.ShouldBe(transient ? 2 : 1, "one owner controls the total downstream attempt budget");
		observed.ShouldBeSameAs(fault, "classification must see the original fault, not a generic 500 result");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RecoverWithOneEffectWithOrWithoutOuterOwner(bool outerOwner)
	{
		_busOptionsMap["Local"] = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		var handler = CreateHandler(CreatePollyPolicy());
		var action = A.Fake<IDispatchAction>();
		var context = CreateContext();
		var calls = 0;
		var effects = 0;
		A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._))
			.Invokes(() =>
			{
				calls++;
				if (calls == 1) { throw new IOException("before effect"); }
				effects++;
			}).Returns(Task.CompletedTask);
		var result = outerOwner
			? await CreateRetryMiddleware().InvokeAsync(action, context, (message, ctx, token) => handler.HandleAsync(message, ctx, token), CancellationToken.None).ConfigureAwait(false)
			: await handler.HandleAsync(action, context, CancellationToken.None).ConfigureAwait(false);
		result.Succeeded.ShouldBeTrue();
		calls.ShouldBe(2);
		effects.ShouldBe(1);
	}

	private static PollyRetryPolicyAdapter CreatePollyPolicy() => new(
		Microsoft.Extensions.Options.Options.Create(new PollyRetryOptions
		{
			MaxRetryAttempts = 1,
			BaseDelay = TimeSpan.Zero,
			UseJitter = false,
			ShouldRetry = static exception => exception is IOException,
		}));

	[Fact]
	public async Task PropagateCallerCancellationWithoutAnotherAttempt()
	{
		_busOptionsMap["Local"] = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		var handler = CreateHandler(CreatePollyPolicy());
		var action = A.Fake<IDispatchAction>();
		var context = CreateContext();
		using var caller = new CancellationTokenSource();
		var calls = 0;
		A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				calls++;
				caller.Cancel();
				return Task.FromCanceled(caller.Token);
			});
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await CreateRetryMiddleware().InvokeAsync(action, context, (message, ctx, token) => handler.HandleAsync(message, ctx, token), caller.Token).ConfigureAwait(false));
		calls.ShouldBe(1);
		RetryOwnershipScope.Owns(action, context).ShouldBeFalse();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task OwnEventAndDocumentRetriesEvenWithPriorActionResult(bool dispatchEvent)
	{
		_busOptionsMap["Local"] = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		var handler = CreateHandler(CreatePollyPolicy());
		var context = CreateContext();
		context.Result = "previous action";
		var calls = 0;
		Task Publish()
		{
			calls++;
			return Task.FromException(new IOException("before effect"));
		}
		IDispatchMessage message;
		if (dispatchEvent)
		{
			var evt = A.Fake<IDispatchEvent>();
			message = evt;
			A.CallTo(() => _messageBus.PublishAsync(evt, context, A<CancellationToken>._)).ReturnsLazily(Publish);
		}
		else
		{
			var doc = A.Fake<IDispatchDocument>();
			message = doc;
			A.CallTo(() => _messageBus.PublishAsync(doc, context, A<CancellationToken>._)).ReturnsLazily(Publish);
		}
		await Should.ThrowAsync<IOException>(async () =>
			await CreateRetryMiddleware().InvokeAsync(message, context, (payload, ctx, token) => handler.HandleAsync(payload, ctx, token), CancellationToken.None).ConfigureAwait(false));
		calls.ShouldBe(2);
	}

	private static RetryMiddleware CreateRetryMiddleware() => new(
		Microsoft.Extensions.Options.Options.Create(new RetryOptions
		{
			MaxRetryAttempts = 1,
			BaseDelay = TimeSpan.Zero,
			UseJitter = false,
		}), NullTelemetrySanitizer.Instance, NullLogger<RetryMiddleware>.Instance);

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RetryOnlyFailedFanoutDestinationWithinOneBudget(bool persistent)
	{
		var message = A.Fake<IDispatchEvent>();
		var context = CreateContext();
		context.GetOrCreateRoutingFeature().RoutingDecision = RoutingDecision.Success("A", ["A", "B"]);
		var first = A.Fake<IMessageBus>();
		var second = A.Fake<IMessageBus>();
		IMessageBus? firstOut = first;
		IMessageBus? secondOut = second;
		A.CallTo(() => _busProvider.TryGet("A", out firstOut)).Returns(true).AssignsOutAndRefParameters(first);
		A.CallTo(() => _busProvider.TryGet("B", out secondOut)).Returns(true).AssignsOutAndRefParameters(second);
		_busOptionsMap["A"] = new TestMessageBusOptions { Name = "A", EnableRetries = true };
		_busOptionsMap["B"] = new TestMessageBusOptions { Name = "B", EnableRetries = true };
		var firstCalls = 0;
		var secondCalls = 0;
		A.CallTo(() => first.PublishAsync(message, context, A<CancellationToken>._))
			.Invokes(() => firstCalls++).Returns(Task.CompletedTask);
		A.CallTo(() => second.PublishAsync(message, context, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				secondCalls++;
				return persistent || secondCalls == 1 ? Task.FromException(new IOException("before effect")) : Task.CompletedTask;
			});
		var handler = CreateHandler(CreatePollyPolicy());
		var middleware = CreateRetryMiddleware();
		var result = await middleware.InvokeAsync(message, context,
			(payload, ctx, token) => handler.HandleAsync(payload, ctx, token), CancellationToken.None).ConfigureAwait(false);
		result.Succeeded.ShouldBe(!persistent);
		firstCalls.ShouldBe(1, "a destination already delivered in this retry sequence must not be replayed");
		secondCalls.ShouldBe(2, "the outer budget must not multiply the inner Polly budget");
		if (!persistent)
		{
			// The same message/context in a NEW logical dispatch must publish again.
			var next = await middleware.InvokeAsync(message, context,
				(payload, ctx, token) => handler.HandleAsync(payload, ctx, token), CancellationToken.None).ConfigureAwait(false);
			next.Succeeded.ShouldBeTrue();
			firstCalls.ShouldBe(2);
			secondCalls.ShouldBe(3);
		}
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RetryIndependentFailureAfterFanoutWithoutRepublishing(bool returnedFailure)
	{
		var message = A.Fake<IDispatchEvent>();
		var context = CreateContext();
		context.GetOrCreateRoutingFeature().RoutingDecision = RoutingDecision.Success("A", ["A", "B"]);
		var first = A.Fake<IMessageBus>();
		var second = A.Fake<IMessageBus>();
		IMessageBus? firstOut = first;
		IMessageBus? secondOut = second;
		A.CallTo(() => _busProvider.TryGet("A", out firstOut)).Returns(true).AssignsOutAndRefParameters(first);
		A.CallTo(() => _busProvider.TryGet("B", out secondOut)).Returns(true).AssignsOutAndRefParameters(second);
		var firstCalls = 0;
		var secondCalls = 0;
		A.CallTo(() => first.PublishAsync(message, context, A<CancellationToken>._)).Invokes(() => firstCalls++).Returns(Task.CompletedTask);
		A.CallTo(() => second.PublishAsync(message, context, A<CancellationToken>._)).Invokes(() => secondCalls++).Returns(Task.CompletedTask);
		var handler = CreateHandler(CreatePollyPolicy());
		var callbacks = 0;
		var result = await CreateRetryMiddleware().InvokeAsync(message, context, async (payload, ctx, token) =>
		{
			callbacks++;
			var delivered = await handler.HandleAsync(payload, ctx, token).ConfigureAwait(false);
			if (callbacks == 1)
			{
				if (returnedFailure)
				{
					return MessageResult.Failed(new MessageProblemDetails { Status = 503, Title = "Independent upstream fault" });
				}
				throw new IOException("independent upstream fault");
			}
			return delivered;
		}, CancellationToken.None).ConfigureAwait(false);
		result.Succeeded.ShouldBeTrue();
		callbacks.ShouldBe(2);
		firstCalls.ShouldBe(1);
		secondCalls.ShouldBe(1);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RetainPermanentFanoutFailureWhileRecoveringTransientDestination(bool transient)
	{
		var (message, context, first, second, third) = ConfigureThreeDestinationFanout();
		var firstCalls = 0;
		var secondCalls = 0;
		var thirdCalls = 0;
		A.CallTo(() => first.PublishAsync(message, context, A<CancellationToken>._)).ReturnsLazily(() =>
		{
			firstCalls++;
			return Task.FromException(new ArgumentException("PERMANENT-A"));
		});
		A.CallTo(() => second.PublishAsync(message, context, A<CancellationToken>._)).ReturnsLazily(() =>
		{
			secondCalls++;
			return transient && secondCalls == 1 ? Task.FromException(new IOException("before effect")) : Task.CompletedTask;
		});
		A.CallTo(() => third.PublishAsync(message, context, A<CancellationToken>._)).Invokes(() => thirdCalls++).Returns(Task.CompletedTask);
		var handler = CreateHandler(CreatePollyPolicy());
		var attempts = 0;
		var result = await CreateRetryMiddleware().InvokeAsync(message, context, (payload, ctx, token) =>
		{
			attempts++;
			return handler.HandleAsync(payload, ctx, token);
		}, CancellationToken.None).ConfigureAwait(false);
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull().Detail.ShouldContain("PERMANENT-A");
		firstCalls.ShouldBe(1);
		secondCalls.ShouldBe(transient ? 2 : 1);
		thirdCalls.ShouldBe(1);
		attempts.ShouldBe(transient ? 2 : 1);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task StopPendingFanoutDestinationsOnCallerCancellation(bool beforeEffect)
	{
		var (message, context, first, second, third) = ConfigureThreeDestinationFanout();
		using var caller = new CancellationTokenSource();
		var firstCalls = 0;
		var effects = 0;
		A.CallTo(() => first.PublishAsync(message, context, A<CancellationToken>._)).ReturnsLazily(() =>
		{
			firstCalls++;
			caller.Cancel();
			if (beforeEffect) { return Task.FromCanceled(caller.Token); }
			effects++;
			return Task.CompletedTask;
		});
		var handler = CreateHandler(CreatePollyPolicy());
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await CreateRetryMiddleware().InvokeAsync(message, context, (payload, ctx, token) => handler.HandleAsync(payload, ctx, token), caller.Token).ConfigureAwait(false));
		firstCalls.ShouldBe(1);
		effects.ShouldBe(beforeEffect ? 0 : 1);
		A.CallTo(() => second.PublishAsync(message, context, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => third.PublishAsync(message, context, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task RetainUncancelledDestinationCancellationAsPermanentFanoutFailure()
	{
		var (message, context, first, second, third) = ConfigureThreeDestinationFanout();
		var firstCalls = 0;
		A.CallTo(() => first.PublishAsync(message, context, A<CancellationToken>._)).ReturnsLazily(() =>
		{
			firstCalls++;
			return Task.FromException(new OperationCanceledException("attempt-local cancellation"));
		});
		var handler = CreateHandler(CreatePollyPolicy());
		var result = await CreateRetryMiddleware().InvokeAsync(message, context,
			(payload, ctx, token) => handler.HandleAsync(payload, ctx, token), CancellationToken.None).ConfigureAwait(false);
		result.Succeeded.ShouldBeFalse();
		firstCalls.ShouldBe(1);
		A.CallTo(() => second.PublishAsync(message, context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => third.PublishAsync(message, context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	private (IDispatchEvent Message, FakeMessageContext Context, IMessageBus First, IMessageBus Second, IMessageBus Third) ConfigureThreeDestinationFanout()
	{
		var message = A.Fake<IDispatchEvent>();
		var context = CreateContext();
		context.GetOrCreateRoutingFeature().RoutingDecision = RoutingDecision.Success("A", ["A", "B", "C"]);
		var first = A.Fake<IMessageBus>();
		var second = A.Fake<IMessageBus>();
		var third = A.Fake<IMessageBus>();
		IMessageBus? firstOut = first;
		IMessageBus? secondOut = second;
		IMessageBus? thirdOut = third;
		A.CallTo(() => _busProvider.TryGet("A", out firstOut)).Returns(true).AssignsOutAndRefParameters(first);
		A.CallTo(() => _busProvider.TryGet("B", out secondOut)).Returns(true).AssignsOutAndRefParameters(second);
		A.CallTo(() => _busProvider.TryGet("C", out thirdOut)).Returns(true).AssignsOutAndRefParameters(third);
		foreach (var name in new[] { "A", "B", "C" })
		{
			_busOptionsMap[name] = new TestMessageBusOptions { Name = name, EnableRetries = true };
		}
		return (message, context, first, second, third);
	}

	[Fact]
	public async Task PropagateClassifierFaultWithoutReplayingSuccessfulDestination()
	{
		var (message, context, first, second, third) = ConfigureThreeDestinationFanout();
		var fault = new InvalidOperationException("classifier configuration fault");
		var classifier = A.Fake<IMessageFailureClassifier>();
		A.CallTo(() => classifier.Classify(A<Exception>._)).Throws(fault);
		A.CallTo(() => second.PublishAsync(message, context, A<CancellationToken>._))
			.Returns(Task.FromException(new IOException("before effect")));
		var middleware = new RetryMiddleware(Microsoft.Extensions.Options.Options.Create(new RetryOptions
		{
			MaxRetryAttempts = 1,
			BaseDelay = TimeSpan.Zero,
			UseJitter = false,
		}), NullTelemetrySanitizer.Instance, NullLogger<RetryMiddleware>.Instance, classifier);
		var handler = CreateHandler(CreatePollyPolicy());
		var observed = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await middleware.InvokeAsync(message, context, (payload, ctx, token) => handler.HandleAsync(payload, ctx, token), CancellationToken.None).ConfigureAwait(false));
		observed.ShouldBeSameAs(fault);
		A.CallTo(() => first.PublishAsync(message, context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => second.PublishAsync(message, context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => third.PublishAsync(message, context, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task PreserveCachedActionWithoutPublishing(bool namedEndpoint)
	{
		var handler = CreateHandler(CreatePollyPolicy());
		var action = A.Fake<IDispatchAction<string>>();
		var context = CreateContext();
		if (!namedEndpoint)
		{
			context.GetOrCreateRoutingFeature().RoutingDecision = RoutingDecision.Success("local", []);
		}
		context.Result = "cached payload";
		var result = await CreateRetryMiddleware().InvokeAsync(action, context,
			(message, ctx, token) => handler.HandleAsync(message, ctx, token), CancellationToken.None).ConfigureAwait(false);
		result.Succeeded.ShouldBeTrue();
		result.ShouldBeAssignableTo<IMessageResult<string>>().ReturnValue.ShouldBe("cached payload");
		A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task PreserveStandalonePolicyForNestedDifferentMessage()
	{
		_busOptionsMap["Local"] = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		var handler = CreateHandler(CreatePollyPolicy());
		var outer = A.Fake<IDispatchAction>();
		var nested = A.Fake<IDispatchAction>();
		var context = CreateContext();
		var calls = 0;
		A.CallTo(() => _messageBus.PublishAsync(nested, context, A<CancellationToken>._))
			.Invokes(() => { if (++calls == 1) { throw new IOException("before effect"); } })
			.Returns(Task.CompletedTask);
		var result = await CreateRetryMiddleware().InvokeAsync(outer, context,
			(_, ctx, token) => handler.HandleAsync(nested, ctx, token), CancellationToken.None).ConfigureAwait(false);
		result.Succeeded.ShouldBeTrue();
		calls.ShouldBe(2, "a different message retains its independent policy even with the same context");
	}

	private readonly ILogger<FinalDispatchHandler> _logger;
	private readonly IMessageBusProvider _busProvider;
	private readonly IDictionary<string, MessageBusOptions> _busOptionsMap;
	private IMessageBus _messageBus;

	public FinalDispatchHandlerRetryPolicyShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<FinalDispatchHandler>();
		_busProvider = A.Fake<IMessageBusProvider>();
		_messageBus = A.Fake<IMessageBus>();
		_busOptionsMap = new Dictionary<string, MessageBusOptions>();

		// Default setup: bus provider returns the fake message bus
		SetupBusProvider(_messageBus);
	}

	private void SetupBusProvider(IMessageBus bus)
	{
		_messageBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet(A<string>._, out bus!))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);
	}

	private FakeMessageContext CreateContext(string busName = "Local")
	{
		var context = new FakeMessageContext();
		context.GetOrCreateRoutingFeature().RoutingDecision = RoutingDecision.Success(busName, [busName]);
		return context;
	}

	private FinalDispatchHandler CreateHandler(IRetryPolicy? retryPolicy = null)
	{
		return new FinalDispatchHandler(_busProvider, _logger, retryPolicy, _busOptionsMap);
	}

	#region NoOpRetryPolicy Tests

	[Fact]
	public async Task UseNoOpRetryPolicyWhenRetryPolicyIsNull()
	{
		// Arrange
		var handler = CreateHandler(retryPolicy: null);
		var action = A.Fake<IDispatchAction>();
		var context = CreateContext();

		_ = A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Act
		var result = await handler.HandleAsync(action, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseNoOpRetryPolicyWhenRetriesDisabled()
	{
		// Arrange
		var options = new TestMessageBusOptions { Name = "Local", EnableRetries = false };
		_busOptionsMap["Local"] = options;

		var retryPolicy = new TestRetryPolicy(
			new RetryOptions { MaxRetryAttempts = 5 });
		var handler = CreateHandler(retryPolicy);
		var action = A.Fake<IDispatchAction>();
		var context = CreateContext();
		var invocationCount = 0;

		_ = A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._))
			.Invokes(() =>
			{
				invocationCount++;
				if (invocationCount == 1)
				{
					throw new InvalidOperationException("First failure");
				}
			})
			.Returns(Task.CompletedTask);

		// Act
		var result = await handler.HandleAsync(action, context, CancellationToken.None).ConfigureAwait(false);

		// Assert - Should fail immediately without retry since EnableRetries is false
		result.Succeeded.ShouldBeFalse();
		invocationCount.ShouldBe(1);
	}

	[Fact]
	public async Task PropagateExceptionImmediatelyWithNoOpPolicy()
	{
		// Arrange
		var handler = CreateHandler(NoOpRetryPolicy.Instance);
		var action = A.Fake<IDispatchAction>();
		var context = CreateContext();

		_ = A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Bus failure"));

		// Act
		var result = await handler.HandleAsync(action, context, CancellationToken.None).ConfigureAwait(false);

		// Assert - FinalDispatchHandler catches exceptions and returns failed result
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Detail.ShouldBe("Bus failure");
	}

	#endregion NoOpRetryPolicy Tests

	#region Retrying-policy Tests

	[Fact]
	public async Task RetryWithARetryingPolicyOnTransientFailure()
	{
		// Arrange
		var options = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		_busOptionsMap["Local"] = options;

		var retryPolicy = new TestRetryPolicy(
			new RetryOptions
			{
				MaxRetryAttempts = 3,
				BaseDelay = TimeSpan.FromMilliseconds(10),
			});
		var handler = CreateHandler(retryPolicy);
		var action = A.Fake<IDispatchAction>();
		var context = CreateContext();
		var invocationCount = 0;

		_ = A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._))
			.Invokes(() =>
			{
				invocationCount++;
				if (invocationCount < 3)
				{
					throw new TimeoutException($"Transient failure {invocationCount}");
				}
			})
			.Returns(Task.CompletedTask);

		// Act
		var result = await handler.HandleAsync(action, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		invocationCount.ShouldBe(3);
	}

	[Fact]
	public async Task FailAfterMaxRetriesExceeded()
	{
		// Arrange
		var options = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		_busOptionsMap["Local"] = options;

		var retryPolicy = new TestRetryPolicy(
			new RetryOptions
			{
				MaxRetryAttempts = 3,
				BaseDelay = TimeSpan.FromMilliseconds(5),
			});
		var handler = CreateHandler(retryPolicy);
		var action = A.Fake<IDispatchAction>();
		var context = CreateContext();
		var invocationCount = 0;

		_ = A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._))
			.Invokes(() =>
			{
				invocationCount++;
				throw new TimeoutException($"Persistent failure {invocationCount}");
			})
			.Returns(Task.CompletedTask);

		// Act
		var result = await handler.HandleAsync(action, context, CancellationToken.None).ConfigureAwait(false);

		// Assert - FinalDispatchHandler catches the exception after retries exhausted
		result.Succeeded.ShouldBeFalse();
		invocationCount.ShouldBe(3);
	}

	#endregion Retrying-policy Tests

	#region Integration Event Tests

	[Fact]
	public async Task HandleIntegrationEventWithRetryPolicy()
	{
		// Arrange
		var options = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		_busOptionsMap["Local"] = options;

		var retryPolicy = new TestRetryPolicy(
			new RetryOptions
			{
				MaxRetryAttempts = 2,
				BaseDelay = TimeSpan.FromMilliseconds(5),
			});
		var handler = CreateHandler(retryPolicy);
		var integrationEvent = A.Fake<IIntegrationEvent>();
		var context = CreateContext();
		var invocationCount = 0;

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchEvent>._, context, A<CancellationToken>._))
			.Invokes(() =>
			{
				invocationCount++;
				if (invocationCount == 1)
				{
					throw new TimeoutException("Transient failure");
				}
			})
			.Returns(Task.CompletedTask);

		// Act
		var result = await handler.HandleAsync(integrationEvent, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		invocationCount.ShouldBe(2);
	}

	#endregion Integration Event Tests

	#region Document Tests

	[Fact]
	public async Task HandleDocumentWithRetryPolicy()
	{
		// Arrange
		var options = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		_busOptionsMap["Local"] = options;

		var retryPolicy = new TestRetryPolicy(
			new RetryOptions
			{
				MaxRetryAttempts = 2,
				BaseDelay = TimeSpan.FromMilliseconds(5),
			});
		var handler = CreateHandler(retryPolicy);
		var document = A.Fake<IDispatchDocument>();
		var context = CreateContext();
		var invocationCount = 0;

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchDocument>._, context, A<CancellationToken>._))
			.Invokes(() =>
			{
				invocationCount++;
				if (invocationCount == 1)
				{
					throw new TimeoutException("Transient failure");
				}
			})
			.Returns(Task.CompletedTask);

		// Act
		var result = await handler.HandleAsync(document, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		invocationCount.ShouldBe(2);
	}

	#endregion Document Tests

	#region No Message Bus Found Tests

	[Fact]
	public async Task ReturnFailedResultWhenMessageBusNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _busProvider.TryGet(A<string>._, out _messageBus!))
			.Returns(false)
			.AssignsOutAndRefParameters((IMessageBus?)null);

		var handler = CreateHandler(NoOpRetryPolicy.Instance);
		var action = A.Fake<IDispatchAction>();
		var context = CreateContext("NonExistent");

		// Act
		var result = await handler.HandleAsync(action, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails!.Title.ShouldBe("Routing failed");
	}

	#endregion No Message Bus Found Tests

	#region Cancellation Tests

	[Fact]
	public async Task RespectCancellationTokenDuringRetry()
	{
		// Arrange
		var options = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		_busOptionsMap["Local"] = options;

		var retryPolicy = new TestRetryPolicy(
			new RetryOptions
			{
				MaxRetryAttempts = 5,
				BaseDelay = TimeSpan.FromSeconds(10), // Long delay
			});
		var handler = CreateHandler(retryPolicy);
		var action = A.Fake<IDispatchAction>();
		using var cts = new CancellationTokenSource();
		var context = CreateContext();
		var invocationCount = 0;

		_ = A.CallTo(() => _messageBus.PublishAsync(action, context, A<CancellationToken>._))
			.Invokes(() =>
			{
				invocationCount++;
				if (invocationCount == 1)
				{
					// Cancel immediately after first failure so retry delay observes cancellation deterministically.
					cts.Cancel();
					throw new InvalidOperationException("First failure");
				}
			})
			.Returns(Task.CompletedTask);

		// Act
		var result = await handler.HandleAsync(action, context, cts.Token).ConfigureAwait(false);

		// Assert - Should fail due to cancellation during retry wait
		result.Succeeded.ShouldBeFalse();
	}

	#endregion Cancellation Tests

	#region Behavioral Equivalence Tests

	[Fact]
	public async Task ProduceSameResultWithNoOpAndDefaultPolicyOnSuccess()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		_ = A.CallTo(() => _messageBus.PublishAsync(action, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Test with NoOpRetryPolicy
		var noOpHandler = CreateHandler(NoOpRetryPolicy.Instance);
		var noOpContext = CreateContext();
		var noOpResult = await noOpHandler.HandleAsync(action, noOpContext, CancellationToken.None).ConfigureAwait(false);

		// Test with a retrying policy
		var defaultHandler = CreateHandler(new TestRetryPolicy(new RetryOptions()));
		var defaultContext = CreateContext();
		var defaultResult = await defaultHandler.HandleAsync(action, defaultContext, CancellationToken.None).ConfigureAwait(false);

		// Assert - Both should succeed
		noOpResult.Succeeded.ShouldBeTrue();
		defaultResult.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ProduceSameFailureResultOnPersistentFailure()
	{
		// Arrange
		var options = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		_busOptionsMap["Local"] = options;

		var action = A.Fake<IDispatchAction>();
		var exception = new InvalidOperationException("Persistent failure");

		_ = A.CallTo(() => _messageBus.PublishAsync(action, A<IMessageContext>._, A<CancellationToken>._))
			.Throws(exception);

		// Test with NoOpRetryPolicy
		var noOpHandler = CreateHandler(NoOpRetryPolicy.Instance);
		var noOpContext = CreateContext();
		var noOpResult = await noOpHandler.HandleAsync(action, noOpContext, CancellationToken.None).ConfigureAwait(false);

		// Clear options to use NoOp fallback
		_busOptionsMap.Clear();

		// Test with a retrying policy but EnableRetries = false (falls back to NoOp)
		var defaultHandler = CreateHandler(new TestRetryPolicy(new RetryOptions { MaxRetryAttempts = 1 }));
		var defaultContext = CreateContext();
		var defaultResult = await defaultHandler.HandleAsync(action, defaultContext, CancellationToken.None).ConfigureAwait(false);

		// Assert - Both should fail with same error detail
		noOpResult.Succeeded.ShouldBeFalse();
		defaultResult.Succeeded.ShouldBeFalse();
		noOpResult.ProblemDetails.Detail.ShouldBe(defaultResult.ProblemDetails.Detail);
	}

	#endregion Behavioral Equivalence Tests

	/// <summary>
	/// Test message bus options.
	/// </summary>
	private sealed class TestMessageBusOptions : MessageBusOptions;
}

/// <summary>
/// A retrying <see cref="IRetryPolicy"/> local to these tests.
/// </summary>
/// <remarks>
/// These tests are about how <c>FinalDispatchHandler</c> behaves when it is GIVEN a policy that
/// retries -- not about any particular policy implementation. They previously used a production type
/// that nothing registered; a local fake states that intent and stops handler coverage from depending
/// on which policy the framework happens to ship. It counts MaxRetryAttempts as TOTAL attempts, which
/// is what the assertions below were written against.
/// </remarks>
internal sealed class TestRetryPolicy(RetryOptions options) : IRetryPolicy
{
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		var attempt = 0;
		while (true)
		{
			attempt++;
			try
			{
				return await action(cancellationToken).ConfigureAwait(false);
			}
			// Cancellation is the caller's decision, never a transient fault -- it must propagate on the
			// first occurrence rather than being retried.
			catch (Exception ex) when (ex is not OperationCanceledException && attempt < options.MaxRetryAttempts)
			{
				// Retry until the attempt cap; the final failure propagates to the caller. A real policy
				// observes cancellation while awaiting its backoff delay, so check it here rather than
				// sleeping -- same observable behaviour, no wall-clock time in the suite.
				cancellationToken.ThrowIfCancellationRequested();
			}
		}
	}

	public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		_ = await ExecuteAsync<object?>(
			async ct => { await action(ct).ConfigureAwait(false); return null; },
			cancellationToken).ConfigureAwait(false);
	}
}
