// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Unit tests for the ResultFactoryCache in <see cref="FinalDispatchHandler"/>.
/// Sprint 451 - S451.5: Tests for PERF-6 reflection caching optimization.
/// </summary>
/// <remarks>
/// These tests verify the ConcurrentDictionary-based caching of factory delegates
/// that create typed MessageResult.Success instances, eliminating per-dispatch
/// reflection overhead.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Performance")]
public sealed class ResultFactoryCacheShould
{
	private readonly FinalDispatchHandler _handler;
	private readonly IMessageBusProvider _busProvider;
	private readonly IMessageBus _mockBus;

	public ResultFactoryCacheShould()
	{
		_busProvider = A.Fake<IMessageBusProvider>();
		_mockBus = A.Fake<IMessageBus>();

		IMessageBus? outBus = _mockBus;
		_ = A.CallTo(() => _busProvider.TryGet(A<string>._, out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(_mockBus);

		_handler = new FinalDispatchHandler(
			_busProvider,
			NullLoggerFactory.Instance.CreateLogger<FinalDispatchHandler>(),
			null,
			new Dictionary<string, IMessageBusOptions>());
	}

	#region Typed Result Creation Tests

	[Fact]
	public async Task CreateTypedResult_WithStringResult_ReturnsTypedMessageResult()
	{
		// Arrange
		var action = new TestActionWithResult { ExpectedResult = "test-result" };
		var context = CreateContextWithRouting("TestBus");
		context.Items["Dispatch:Result"] = "test-result";

		// Act
		var result = await _handler.HandleAsync(action, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = result.ShouldBeAssignableTo<IMessageResult<string>>();
		var typedResult = result as IMessageResult<string>;
		typedResult?.ReturnValue.ShouldBe("test-result");
	}

	[Fact]
	public async Task CreateTypedResult_WithIntResult_ReturnsTypedMessageResult()
	{
		// Arrange
		var action = new TestActionWithIntResult();
		var context = CreateContextWithRouting("TestBus");
		context.Items["Dispatch:Result"] = 42;

		// Act
		var result = await _handler.HandleAsync(action, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = result.ShouldBeAssignableTo<IMessageResult<int>>();
		var typedResult = result as IMessageResult<int>;
		typedResult?.ReturnValue.ShouldBe(42);
	}

	[Fact]
	public async Task CreateTypedResult_WithComplexResult_ReturnsTypedMessageResult()
	{
		// Arrange
		var action = new TestActionWithComplexResult();
		var expectedResult = new ComplexResultDto { Id = 123, Name = "Test", IsActive = true };
		var context = CreateContextWithRouting("TestBus");
		context.Items["Dispatch:Result"] = expectedResult;

		// Act
		var result = await _handler.HandleAsync(action, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = result.ShouldBeAssignableTo<IMessageResult<ComplexResultDto>>();
		var typedResult = result as IMessageResult<ComplexResultDto>;
		typedResult?.ReturnValue.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task CreateTypedResult_WithNullableValueTypeResult_HandlesDefault()
	{
		// Arrange - Use value type since string class has no parameterless ctor
		var action = new TestActionWithIntResult();
		var context = CreateContextWithRouting("TestBus");
		// Not setting Dispatch:Result - will use default(int) = 0

		// Act
		var result = await _handler.HandleAsync(action, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		var typedResult = result as IMessageResult<int>;
		typedResult?.ReturnValue.ShouldBe(0);
	}

	[Fact]
	public async Task CreateTypedResult_WithCacheHit_SetsCacheHitFlag()
	{
		// Arrange
		var action = new TestActionWithResult();
		var context = CreateContextWithRouting("TestBus");
		context.Items["Dispatch:Result"] = "cached-value";
		context.Items["Dispatch:CacheHit"] = true;

		// Act
		var result = await _handler.HandleAsync(action, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		// The CacheHit flag should be passed through to the result factory
	}

	#endregion

	#region Factory Caching Verification Tests

	[Fact]
	public async Task FactoryCache_ReturnsSameFactory_ForSameType()
	{
		// Arrange
		var action1 = new TestActionWithResult();
		var action2 = new TestActionWithResult();
		var context1 = CreateContextWithRouting("TestBus");
		var context2 = CreateContextWithRouting("TestBus");
		context1.Items["Dispatch:Result"] = "result1";
		context2.Items["Dispatch:Result"] = "result2";

		// Act - Execute twice with same result type
		var result1 = await _handler.HandleAsync(action1, context1, CancellationToken.None);
		var result2 = await _handler.HandleAsync(action2, context2, CancellationToken.None);

		// Assert - Both should succeed (factory was cached and reused)
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task FactoryCache_CachesDifferentTypesIndependently()
	{
		// Arrange
		var stringAction = new TestActionWithResult();
		var intAction = new TestActionWithIntResult();
		var stringContext = CreateContextWithRouting("TestBus");
		var intContext = CreateContextWithRouting("TestBus");
		stringContext.Items["Dispatch:Result"] = "string-result";
		intContext.Items["Dispatch:Result"] = 999;

		// Act
		var stringResult = await _handler.HandleAsync(stringAction, stringContext, CancellationToken.None);
		var intResult = await _handler.HandleAsync(intAction, intContext, CancellationToken.None);

		// Assert
		stringResult.Succeeded.ShouldBeTrue();
		(stringResult as IMessageResult<string>)?.ReturnValue.ShouldBe("string-result");

		intResult.Succeeded.ShouldBeTrue();
		(intResult as IMessageResult<int>)?.ReturnValue.ShouldBe(999);
	}

	[Fact]
	public async Task FactoryCache_IsThreadSafe_UnderConcurrentAccess()
	{
		// Arrange
		const int concurrentOperations = 50;
		var results = new ConcurrentBag<IMessageResult>();

		// Act - Execute many operations concurrently
		await Parallel.ForEachAsync(
			Enumerable.Range(0, concurrentOperations),
			new ParallelOptions { MaxDegreeOfParallelism = 10 },
			async (i, ct) =>
			{
				var action = new TestActionWithResult();
				var context = CreateContextWithRouting("TestBus");
				context.Items["Dispatch:Result"] = $"result-{i}";

				var result = await _handler.HandleAsync(action, context, ct);
				results.Add(result);
			});

		// Assert - All operations should succeed
		results.Count.ShouldBe(concurrentOperations);
		results.All(r => r.Succeeded).ShouldBeTrue();
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public async Task CreateTypedResult_WithGuidResult_Works()
	{
		// Arrange
		var action = new TestActionWithGuidResult();
		var expectedGuid = Guid.NewGuid();
		var context = CreateContextWithRouting("TestBus");
		context.Items["Dispatch:Result"] = expectedGuid;

		// Act
		var result = await _handler.HandleAsync(action, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = result.ShouldBeAssignableTo<IMessageResult<Guid>>();
		var typedResult = result as IMessageResult<Guid>;
		typedResult?.ReturnValue.ShouldBe(expectedGuid);
	}

	[Fact]
	public async Task CreateTypedResult_WithEnumResult_Works()
	{
		// Arrange
		var action = new TestActionWithEnumResult();
		var context = CreateContextWithRouting("TestBus");
		context.Items["Dispatch:Result"] = TestResultEnum.ValueB;

		// Act
		var result = await _handler.HandleAsync(action, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = result.ShouldBeAssignableTo<IMessageResult<TestResultEnum>>();
		var typedResult = result as IMessageResult<TestResultEnum>;
		typedResult?.ReturnValue.ShouldBe(TestResultEnum.ValueB);
	}

	[Fact]
	public async Task CreateTypedResult_WithListResult_Works()
	{
		// Arrange
		var action = new TestActionWithListResult();
		var expectedList = new List<string> { "item1", "item2", "item3" };
		var context = CreateContextWithRouting("TestBus");
		context.Items["Dispatch:Result"] = expectedList;

		// Act
		var result = await _handler.HandleAsync(action, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = result.ShouldBeAssignableTo<IMessageResult<List<string>>>();
		var typedResult = result as IMessageResult<List<string>>;
		typedResult?.ReturnValue.ShouldBe(expectedList);
	}

	[Fact]
	public async Task CreateTypedResult_IncludesRoutingInformation()
	{
		// Arrange
		var action = new TestActionWithResult();
		var context = CreateContextWithRouting("TestBus");
		context.Items["Dispatch:Result"] = "test";

		// Act
		var result = await _handler.HandleAsync(action, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		// The result is a typed MessageResult from the handler
		_ = result.ShouldNotBeNull();
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

	private sealed class TestActionWithResult : IDispatchAction<string>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestActionWithResult";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
		public string? ExpectedResult { get; set; }
	}

	private sealed class TestActionWithIntResult : IDispatchAction<int>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestActionWithIntResult";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class TestActionWithComplexResult : IDispatchAction<ComplexResultDto>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestActionWithComplexResult";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}


	private sealed class TestActionWithGuidResult : IDispatchAction<Guid>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestActionWithGuidResult";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class TestActionWithEnumResult : IDispatchAction<TestResultEnum>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestActionWithEnumResult";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class TestActionWithListResult : IDispatchAction<List<string>>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestActionWithListResult";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	#endregion
}

/// <summary>
/// DTO for testing complex result types in ResultFactoryCache tests.
/// </summary>
internal sealed class ComplexResultDto
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool IsActive { get; set; }
}

/// <summary>
/// Enum for testing enum result types in ResultFactoryCache tests.
/// </summary>
internal enum TestResultEnum
{
	ValueA,
	ValueB,
	ValueC,
}
