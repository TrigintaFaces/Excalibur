// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
/// Integration tests for FinalDispatchHandler with IRetryPolicy implementations.
/// Sprint 44: Verifies behavioral equivalence between DefaultRetryPolicy and NoOpRetryPolicy.
/// Task: Excalibur.Dispatch-4n0b
/// </summary>
[Trait("Category", "Unit")]
public sealed class FinalDispatchHandlerRetryPolicyShould
{
	private readonly ILogger<FinalDispatchHandler> _logger;
	private readonly IMessageBusProvider _busProvider;
	private readonly IDictionary<string, IMessageBusOptions> _busOptionsMap;
	private IMessageBus _messageBus;

	public FinalDispatchHandlerRetryPolicyShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<FinalDispatchHandler>();
		_busProvider = A.Fake<IMessageBusProvider>();
		_messageBus = A.Fake<IMessageBus>();
		_busOptionsMap = new Dictionary<string, IMessageBusOptions>();

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
		context.RoutingDecision = RoutingDecision.Success(busName, [busName]);
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

		var retryPolicy = new DefaultRetryPolicy(
			new RetryPolicyOptions { MaxRetryAttempts = 5 });
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

	#region DefaultRetryPolicy Tests

	[Fact]
	public async Task RetryWithDefaultRetryPolicyOnTransientFailure()
	{
		// Arrange
		var options = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		_busOptionsMap["Local"] = options;

		var retryPolicy = new DefaultRetryPolicy(
			new RetryPolicyOptions
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
					throw new InvalidOperationException($"Transient failure {invocationCount}");
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

		var retryPolicy = new DefaultRetryPolicy(
			new RetryPolicyOptions
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
				throw new InvalidOperationException($"Persistent failure {invocationCount}");
			})
			.Returns(Task.CompletedTask);

		// Act
		var result = await handler.HandleAsync(action, context, CancellationToken.None).ConfigureAwait(false);

		// Assert - FinalDispatchHandler catches the exception after retries exhausted
		result.Succeeded.ShouldBeFalse();
		invocationCount.ShouldBe(3);
	}

	#endregion DefaultRetryPolicy Tests

	#region Integration Event Tests

	[Fact]
	public async Task HandleIntegrationEventWithRetryPolicy()
	{
		// Arrange
		var options = new TestMessageBusOptions { Name = "Local", EnableRetries = true };
		_busOptionsMap["Local"] = options;

		var retryPolicy = new DefaultRetryPolicy(
			new RetryPolicyOptions
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
					throw new InvalidOperationException("Transient failure");
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

		var retryPolicy = new DefaultRetryPolicy(
			new RetryPolicyOptions
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
					throw new InvalidOperationException("Transient failure");
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

		var retryPolicy = new DefaultRetryPolicy(
			new RetryPolicyOptions
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
					// Cancel after first failure (before retry delay completes)
					_ = Task.Run(async () =>
					{
						await Task.Delay(50).ConfigureAwait(false);
						cts.Cancel();
					});
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

		// Test with DefaultRetryPolicy
		var defaultHandler = CreateHandler(new DefaultRetryPolicy(new RetryPolicyOptions()));
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

		// Test with DefaultRetryPolicy but EnableRetries = false (falls back to NoOp)
		var defaultHandler = CreateHandler(new DefaultRetryPolicy(new RetryPolicyOptions { MaxRetryAttempts = 1 }));
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
	private sealed class TestMessageBusOptions : IMessageBusOptions;
}
