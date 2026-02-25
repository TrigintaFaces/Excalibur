// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Unit tests for the <see cref="FinalDispatchHandler"/> class.
/// </summary>
/// <remarks>
/// Sprint 413 - Task T413.1: FinalDispatchHandler tests (69% → 85%).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FinalDispatchHandlerShould
{
	private readonly IMessageBusProvider _busProvider;
	private readonly ILogger<FinalDispatchHandler> _logger;
	private readonly IRetryPolicy _retryPolicy;
	private readonly IDictionary<string, IMessageBusOptions> _busOptionsMap;

	public FinalDispatchHandlerShould()
	{
		_busProvider = A.Fake<IMessageBusProvider>();
		_logger = NullLoggerFactory.Instance.CreateLogger<FinalDispatchHandler>();
		_retryPolicy = A.Fake<IRetryPolicy>();
		_busOptionsMap = new Dictionary<string, IMessageBusOptions>();
	}

	private FinalDispatchHandler CreateHandler(
		IMessageBusProvider? busProvider = null,
		IRetryPolicy? retryPolicy = null,
		IDictionary<string, IMessageBusOptions>? busOptionsMap = null)
	{
		return new FinalDispatchHandler(
			busProvider ?? _busProvider,
			_logger,
			retryPolicy ?? _retryPolicy,
			busOptionsMap ?? _busOptionsMap);
	}

	#region Null Validation Tests

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new FinalDispatchHandler(_busProvider, null!, _retryPolicy, _busOptionsMap));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var handler = CreateHandler();
		var context = new FakeMessageContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			handler.HandleAsync(null!, context, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeDispatchMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			handler.HandleAsync(message, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Bus Resolution Tests

	[Fact]
	public async Task ReturnFailedResult_WhenMessageBusNotFound()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeIntegrationEvent();
		var context = CreateContextWithRouting("NonExistentBus");

		IMessageBus? outBus;
		_ = A.CallTo(() => _busProvider.TryGet("NonExistentBus", out outBus))
			.Returns(false);

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		var problemDetails = result.ProblemDetails as MessageProblemDetails;
		_ = problemDetails.ShouldNotBeNull();
		problemDetails.Status.ShouldBe(404);
		result.ProblemDetails.Detail.ShouldContain("NonExistentBus");
	}

	[Fact]
	public async Task ReturnFailedResult_WhenResolvedBusIsNull()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeIntegrationEvent();
		var context = CreateContextWithRouting("NullBus");

		IMessageBus? outBus = null;
		_ = A.CallTo(() => _busProvider.TryGet("NullBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(outBus);

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		var problemDetails = result.ProblemDetails as MessageProblemDetails;
		_ = problemDetails.ShouldNotBeNull();
		problemDetails.Status.ShouldBe(404);
	}

	[Fact]
	public async Task UseDefaultLocalBus_WhenNoRoutingResultProvided()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeIntegrationEvent();
		var context = new FakeMessageContext { RoutingDecision = null };
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("local", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => bus.PublishAsync(A<IIntegrationEvent>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	#endregion

	#region Integration Event Dispatch Tests

	[Fact]
	public async Task PublishIntegrationEvent_ToMessageBus()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeIntegrationEvent();
		var context = CreateContextWithRouting("TestBus");
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("TestBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => bus.PublishAsync(message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishIntegrationEvent_ToMultipleBuses()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeIntegrationEvent();
		var context = CreateContextWithRouting("Bus1", "Bus2");
		var bus1 = A.Fake<IMessageBus>();
		var bus2 = A.Fake<IMessageBus>();

		IMessageBus? outBus1 = bus1;
		IMessageBus? outBus2 = bus2;
		_ = A.CallTo(() => _busProvider.TryGet("Bus1", out outBus1))
			.Returns(true)
			.AssignsOutAndRefParameters(bus1);
		_ = A.CallTo(() => _busProvider.TryGet("Bus2", out outBus2))
			.Returns(true)
			.AssignsOutAndRefParameters(bus2);

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => bus1.PublishAsync(message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => bus2.PublishAsync(message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Document Dispatch Tests

	[Fact]
	public async Task PublishDocument_ToMessageBus()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeDispatchDocument();
		var context = CreateContextWithRouting("DocBus");
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("DocBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => bus.PublishAsync(A<IDispatchDocument>._, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Action Dispatch Tests

	[Fact]
	public async Task SendAction_ToFirstBusOnly()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeDispatchAction();
		var context = CreateContextWithRouting("Bus1", "Bus2");
		var bus1 = A.Fake<IMessageBus>();
		var bus2 = A.Fake<IMessageBus>();

		IMessageBus? outBus1 = bus1;
		IMessageBus? outBus2 = bus2;
		_ = A.CallTo(() => _busProvider.TryGet("Bus1", out outBus1))
			.Returns(true)
			.AssignsOutAndRefParameters(bus1);
		_ = A.CallTo(() => _busProvider.TryGet("Bus2", out outBus2))
			.Returns(true)
			.AssignsOutAndRefParameters(bus2);

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert - Only first bus should be called for actions
		_ = A.CallTo(() => bus1.PublishAsync(A<IDispatchAction>._, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => bus2.PublishAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SendAction_UseLocalBusFastPath_WhenNoRoutingDecisionProvided()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeDispatchAction();
		var context = new FakeMessageContext { RoutingDecision = null };
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("local", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => bus.PublishAsync(A<IDispatchAction>._, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipActionExecution_WhenCacheHitExists()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeDispatchAction();
		var context = CreateContextWithRouting("TestBus");
		context.Items["Dispatch:Result"] = "cached-result";
		context.Items["Dispatch:CacheHit"] = true;
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("TestBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert - Bus should not be called due to cache hit
		A.CallTo(() => bus.PublishAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Unsupported Message Type Tests

	[Fact]
	public async Task ReturnFailedResult_ForUnsupportedMessageType()
	{
		// Arrange
		var handler = CreateHandler();
		var message = new FakeDispatchMessage(); // Generic message, not event/doc/action
		var context = CreateContextWithRouting("TestBus");
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("TestBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		var problemDetails = result.ProblemDetails as MessageProblemDetails;
		_ = problemDetails.ShouldNotBeNull();
		problemDetails.Status.ShouldBe(400);
		result.ProblemDetails.Title.ShouldBe("Unsupported message type");
	}

	#endregion

	#region Retry Policy Tests

	[Fact]
	public async Task UseRetryPolicy_WhenEnableRetriesIsTrue()
	{
		// Arrange
		var busOptions = new TestMessageBusOptions { Name = "TestBus", EnableRetries = true };
		_busOptionsMap["TestBus"] = busOptions;

		_ = A.CallTo(() => _retryPolicy.ExecuteAsync(A<Func<CancellationToken, Task>>._, A<CancellationToken>._))
			.Invokes((Func<CancellationToken, Task> action, CancellationToken ct) => action(ct));

		var handler = CreateHandler();
		var message = new FakeIntegrationEvent();
		var context = CreateContextWithRouting("TestBus");
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("TestBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		// Act
		_ = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _retryPolicy.ExecuteAsync(A<Func<CancellationToken, Task>>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task SkipRetryPolicy_WhenEnableRetriesIsFalse()
	{
		// Arrange
		var busOptions = new TestMessageBusOptions { Name = "TestBus", EnableRetries = false };
		_busOptionsMap["TestBus"] = busOptions;

		var handler = CreateHandler();
		var message = new FakeIntegrationEvent();
		var context = CreateContextWithRouting("TestBus");
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("TestBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		// Act
		_ = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert - Retry policy should not be called
		A.CallTo(() => _retryPolicy.ExecuteAsync(A<Func<CancellationToken, Task>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Exception Handling Tests

	[Fact]
	public async Task ReturnFailedResult_WhenBusThrowsException()
	{
		// Arrange
		var handler = CreateHandler(retryPolicy: null);
		var message = new FakeIntegrationEvent();
		var context = CreateContextWithRouting("TestBus");
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("TestBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		_ = A.CallTo(() => bus.PublishAsync(A<IIntegrationEvent>._, A<IMessageContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Bus failure"));

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		var problemDetails = result.ProblemDetails as MessageProblemDetails;
		_ = problemDetails.ShouldNotBeNull();
		problemDetails.Status.ShouldBe(500);
		result.ProblemDetails.Detail.ShouldContain("Bus failure");
	}

	[Fact]
	public async Task ReturnFailedResult_WhenBusThrows()
	{
		// Arrange
		var handler = CreateHandler(retryPolicy: null);
		var message = new FakeIntegrationEvent();
		var context = CreateContextWithRouting("TestBus");
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("TestBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		_ = A.CallTo(() => bus.PublishAsync(A<IIntegrationEvent>._, A<IMessageContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Bus failure"));

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert - With the new RoutingDecision model, failures are reflected in the result
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnFailedResult_WhenActionDispatchThrows()
	{
		// Arrange
		var handler = CreateHandler(retryPolicy: null);
		var message = new FakeDispatchAction();
		var context = CreateContextWithRouting("TestBus");
		var bus = A.Fake<IMessageBus>();

		IMessageBus? outBus = bus;
		_ = A.CallTo(() => _busProvider.TryGet("TestBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(bus);

		_ = A.CallTo(() => bus.PublishAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Action dispatch failed"));

		// Act
		var result = await handler.HandleAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		var problemDetails = result.ProblemDetails as MessageProblemDetails;
		_ = problemDetails.ShouldNotBeNull();
		problemDetails.Status.ShouldBe(500);
		result.ProblemDetails.Detail.ShouldContain("Action dispatch failed");
	}

	#endregion

	#region Helper Methods

	private static FakeMessageContext CreateContextWithRouting(params string[] busNames)
	{
		var routingDecision = RoutingDecision.Success(
			busNames.Length > 0 ? busNames[0] : "local",
			busNames.ToList());

		return new FakeMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			CorrelationId = Guid.NewGuid().ToString(),
			RoutingDecision = routingDecision,
		};
	}

	#endregion

	#region Test Fixtures

	private sealed class TestMessageBusOptions : IMessageBusOptions
	{
		// IMessageBusOptions is an abstract class with init properties.
		// This concrete implementation allows us to set values for testing.
	}

	private sealed class FakeIntegrationEvent : IIntegrationEvent
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Event;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "FakeIntegrationEvent";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();

		// IIntegrationEvent specific
		public Guid EventId { get; } = Guid.NewGuid();
		public string? AggregateId { get; set; }
		public int? Version { get; set; }
		public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
		public string EventType => GetType().Name;
		public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
		public bool IsPersisted => false;
	}

	private sealed class FakeDispatchDocument : IDispatchDocument
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Document;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "FakeDispatchDocument";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();

		// IDispatchDocument specific
		public string DocumentId { get; set; } = Guid.NewGuid().ToString();
		public string DocumentType { get; set; } = "TestDocument";
		public string? ContentType { get; set; } = "application/json";
	}

	private sealed class FakeDispatchAction : IDispatchAction
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "FakeDispatchAction";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	#endregion
}
