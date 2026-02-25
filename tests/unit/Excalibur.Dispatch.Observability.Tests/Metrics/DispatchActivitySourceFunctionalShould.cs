// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Functional tests for <see cref="DispatchActivitySource"/> verifying activity creation and tagging.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Tracing")]
public sealed class DispatchActivitySourceFunctionalShould : IDisposable
{
	private readonly ActivityListener _listener;
	private readonly List<Activity> _capturedActivities = [];

	public DispatchActivitySourceFunctionalShould()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = activity => _capturedActivities.Add(activity),
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_listener.Dispose();
		foreach (var activity in _capturedActivities)
		{
			activity.Dispose();
		}
	}

	[Fact]
	public void HaveCorrectActivitySourceName()
	{
		DispatchActivitySource.Name.ShouldBe("Excalibur.Dispatch");
	}

	[Fact]
	public void HaveSingletonInstance()
	{
		DispatchActivitySource.Instance.ShouldNotBeNull();
		DispatchActivitySource.Instance.Name.ShouldBe("Excalibur.Dispatch");
	}

	[Fact]
	public void StartActivity_WithMessageTypeAndOperationTags()
	{
		var message = A.Fake<IDispatchMessage>();
		using var activity = DispatchActivitySource.StartActivity(message, "test-operation");

		activity.ShouldNotBeNull();
		activity.GetTagItem("message.type").ShouldNotBeNull();
		activity.GetTagItem("dispatch.operation").ShouldBe("test-operation");
	}

	[Fact]
	public void StartActivity_ThrowOnNullMessage()
	{
		Should.Throw<ArgumentNullException>(() => DispatchActivitySource.StartActivity(null!, "test"));
	}

	[Fact]
	public void StartPublishActivity_WithDestinationTag()
	{
		var message = A.Fake<IDispatchMessage>();
		using var activity = DispatchActivitySource.StartPublishActivity(message, "orders-topic");

		activity.ShouldNotBeNull();
		activity.GetTagItem("message.destination").ShouldBe("orders-topic");
		activity.GetTagItem("dispatch.operation").ShouldBe("publish");
	}

	[Fact]
	public void StartPublishActivity_ThrowOnNullMessage()
	{
		Should.Throw<ArgumentNullException>(() => DispatchActivitySource.StartPublishActivity(null!, "dest"));
	}

	[Fact]
	public void StartHandleActivity_WithHandlerTypeTag()
	{
		var message = A.Fake<IDispatchMessage>();
		using var activity = DispatchActivitySource.StartHandleActivity(message, typeof(string));

		activity.ShouldNotBeNull();
		activity.GetTagItem("handler.type").ShouldBe("String");
		activity.GetTagItem("dispatch.operation").ShouldBe("handle");
	}

	[Fact]
	public void StartHandleActivity_ThrowOnNullMessage()
	{
		Should.Throw<ArgumentNullException>(() => DispatchActivitySource.StartHandleActivity(null!, typeof(string)));
	}

	[Fact]
	public void StartHandleActivity_ThrowOnNullHandlerType()
	{
		var message = A.Fake<IDispatchMessage>();
		Should.Throw<ArgumentNullException>(() => DispatchActivitySource.StartHandleActivity(message, null!));
	}

	[Fact]
	public void StartMiddlewareActivity_WithMiddlewareTypeTag()
	{
		var message = A.Fake<IDispatchMessage>();
		using var activity = DispatchActivitySource.StartMiddlewareActivity(typeof(MetricsMiddleware), message);

		activity.ShouldNotBeNull();
		activity.GetTagItem("middleware.type").ShouldBe("MetricsMiddleware");
		activity.GetTagItem("dispatch.operation").ShouldBe("middleware");
	}

	[Fact]
	public void StartMiddlewareActivity_ThrowOnNullMiddlewareType()
	{
		var message = A.Fake<IDispatchMessage>();
		Should.Throw<ArgumentNullException>(() => DispatchActivitySource.StartMiddlewareActivity(null!, message));
	}

	[Fact]
	public void StartMiddlewareActivity_ThrowOnNullMessage()
	{
		Should.Throw<ArgumentNullException>(() => DispatchActivitySource.StartMiddlewareActivity(typeof(MetricsMiddleware), null!));
	}
}
