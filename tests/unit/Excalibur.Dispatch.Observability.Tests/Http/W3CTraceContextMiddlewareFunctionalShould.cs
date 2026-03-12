// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Observability.Http;

namespace Excalibur.Dispatch.Observability.Tests.Http;

/// <summary>
/// Functional tests for <see cref="W3CTraceContextMiddleware"/> verifying W3C traceparent/tracestate propagation.
/// Uses unique traceIds per test to avoid Activity contamination from parallel xUnit execution.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Http")]
public sealed class W3CTraceContextMiddlewareFunctionalShould : IDisposable
{
	private readonly ActivityListener _listener;
	private readonly ConcurrentBag<Activity> _activities = [];

	public W3CTraceContextMiddlewareFunctionalShould()
	{
		_listener = new ActivityListener
		{
			// Exact match to the W3C middleware's ActivitySource name
			ShouldListenTo = source => source.Name == W3CTraceContextTelemetryConstants.ActivitySourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = activity => _activities.Add(activity),
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_listener.Dispose();
		foreach (var activity in _activities)
		{
			activity.Dispose();
		}
	}

	/// <summary>
	/// Creates a unique W3C traceparent string and returns the traceId hex for filtering.
	/// </summary>
	private static (string traceparent, string traceIdHex) CreateUniqueTraceparent()
	{
		var traceIdHex = Guid.NewGuid().ToString("N"); // 32 hex chars
		return ($"00-{traceIdHex}-00f067aa0ba902b7-01", traceIdHex);
	}

	/// <summary>
	/// Finds the propagate activity matching the given traceId to isolate from parallel tests.
	/// </summary>
	private Activity? FindPropagateActivity(string traceIdHex)
	{
		var traceId = ActivityTraceId.CreateFromString(traceIdHex.AsSpan());
		return _activities.FirstOrDefault(a =>
			a.OperationName == "dispatch.w3c.propagate" &&
			a.TraceId == traceId);
	}

	/// <summary>
	/// Creates a fake <see cref="IMessageContext"/> backed by a real Items dictionary
	/// so that extension methods (GetItem, SetItem, ContainsItem) work correctly.
	/// </summary>
	private static IMessageContext CreateFakeContext(Dictionary<string, object>? items = null)
	{
		var context = A.Fake<IMessageContext>();
		var itemsDict = items ?? new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(itemsDict);
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		return context;
	}

	[Fact]
	public void HavePreProcessingStage()
	{
		var middleware = new W3CTraceContextMiddleware();
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		var middleware = new W3CTraceContextMiddleware();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(null!, context, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		var middleware = new W3CTraceContextMiddleware();
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(message, null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullNextDelegate()
	{
		var middleware = new W3CTraceContextMiddleware();

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), null!, CancellationToken.None));
	}

	[Fact]
	public async Task PassthroughWhenNoTraceparent()
	{
		var middleware = new W3CTraceContextMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		// No traceparent or headers in Items

		var expectedResult = A.Fake<IMessageResult>();
		var nextInvoked = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};

		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		nextInvoked.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task CreateActivityWhenTraceparentPresent()
	{
		var middleware = new W3CTraceContextMiddleware();
		var message = A.Fake<IDispatchMessage>();

		var (traceparent, traceIdHex) = CreateUniqueTraceparent();
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			[W3CTraceContextMiddleware.TraceparentKey] = traceparent,
		};
		var context = CreateFakeContext(items);

		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		result.ShouldBe(expectedResult);

		var propagateActivity = FindPropagateActivity(traceIdHex);
		propagateActivity.ShouldNotBeNull();
		propagateActivity.GetTagItem("dispatch.trace.propagation")?.ToString().ShouldBe("w3c");
	}

	[Fact]
	public async Task SetTracestateOnActivity()
	{
		var middleware = new W3CTraceContextMiddleware();
		var message = A.Fake<IDispatchMessage>();

		var (traceparent, traceIdHex) = CreateUniqueTraceparent();
		var tracestate = "congo=lZWRzIHRoNhcm5hbWVkT2";

		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			[W3CTraceContextMiddleware.TraceparentKey] = traceparent,
			[W3CTraceContextMiddleware.TracestateKey] = tracestate,
		};
		var context = CreateFakeContext(items);

		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		var propagateActivity = FindPropagateActivity(traceIdHex);
		propagateActivity.ShouldNotBeNull();
		propagateActivity.TraceStateString.ShouldBe(tracestate);
	}

	[Fact]
	public async Task ExtractTraceparentFromHeaders()
	{
		var middleware = new W3CTraceContextMiddleware();
		var message = A.Fake<IDispatchMessage>();

		// Provide headers dictionary with unique traceparent
		var (traceparent, traceIdHex) = CreateUniqueTraceparent();
		var headers = new Dictionary<string, string>
		{
			["traceparent"] = traceparent,
		};
		// No direct traceparent in Items, only in Headers dict
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Headers"] = headers,
		};
		var context = CreateFakeContext(items);

		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		var propagateActivity = FindPropagateActivity(traceIdHex);
		propagateActivity.ShouldNotBeNull();
	}

	[Fact]
	public async Task SetMessageTypeTag()
	{
		var middleware = new W3CTraceContextMiddleware();
		var message = A.Fake<IDispatchMessage>();

		var (traceparent, traceIdHex) = CreateUniqueTraceparent();
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			[W3CTraceContextMiddleware.TraceparentKey] = traceparent,
		};
		var context = CreateFakeContext(items);

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		var propagateActivity = FindPropagateActivity(traceIdHex);
		propagateActivity.ShouldNotBeNull();
		propagateActivity.GetTagItem("message.type").ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleMalformedTraceparent()
	{
		var middleware = new W3CTraceContextMiddleware();
		var message = A.Fake<IDispatchMessage>();

		// Malformed traceparent (too few parts)
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			[W3CTraceContextMiddleware.TraceparentKey] = "00-short-01",
		};
		var context = CreateFakeContext(items);

		var expectedResult = A.Fake<IMessageResult>();
		var nextInvoked = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};

		// Should not throw, even with malformed traceparent
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		nextInvoked.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public void ExposeTraceparentKeyConstant()
	{
		W3CTraceContextMiddleware.TraceparentKey.ShouldBe("traceparent");
	}

	[Fact]
	public void ExposeTracestateKeyConstant()
	{
		W3CTraceContextMiddleware.TracestateKey.ShouldBe("tracestate");
	}
}
