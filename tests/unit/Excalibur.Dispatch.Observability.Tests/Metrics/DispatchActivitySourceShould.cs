// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="DispatchActivitySource"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Tracing")]
public sealed class DispatchActivitySourceShould : IDisposable
{
	private readonly ActivityListener _listener;

	public DispatchActivitySourceShould()
	{
		// Set up an activity listener to capture activities
		_listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_listener.Dispose();
	}

	#region Constants Tests

	[Fact]
	public void HaveCorrectNameConstant()
	{
		// Assert
		DispatchActivitySource.Name.ShouldBe("Excalibur.Dispatch");
	}

	[Fact]
	public void HaveStaticInstance()
	{
		// Assert
		DispatchActivitySource.Instance.ShouldNotBeNull();
	}

	[Fact]
	public void HaveInstanceWithCorrectName()
	{
		// Assert
		DispatchActivitySource.Instance.Name.ShouldBe("Excalibur.Dispatch");
	}

	#endregion

	#region StartActivity Tests

	[Fact]
	public void StartActivity_ReturnsActivity()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartActivity(message, "test.activity");

		// Assert
		activity.ShouldNotBeNull();
	}

	[Fact]
	public void StartActivity_SetsMessageTypeTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartActivity(message, "test.activity");

		// Assert
		activity.ShouldNotBeNull();
		var messageTypeTag = activity.GetTagItem("message.type");
		messageTypeTag.ShouldNotBeNull();
	}

	[Fact]
	public void StartActivity_SetsDispatchOperationTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartActivity(message, "custom.operation");

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.operation").ShouldBe("custom.operation");
	}

	[Fact]
	public void StartActivity_ThrowsOnNullMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartActivity(null!, "test.activity"));
	}

	#endregion

	#region StartPublishActivity Tests

	[Fact]
	public void StartPublishActivity_ReturnsActivity()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartPublishActivity(message, "test-queue");

		// Assert
		activity.ShouldNotBeNull();
	}

	[Fact]
	public void StartPublishActivity_SetsDestinationTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartPublishActivity(message, "orders-queue");

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("message.destination").ShouldBe("orders-queue");
	}

	[Fact]
	public void StartPublishActivity_SetsPublishOperation()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartPublishActivity(message, "test-queue");

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.operation").ShouldBe("publish");
	}

	[Fact]
	public void StartPublishActivity_ThrowsOnNullMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartPublishActivity(null!, "test-queue"));
	}

	#endregion

	#region StartHandleActivity Tests

	[Fact]
	public void StartHandleActivity_ReturnsActivity()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartHandleActivity(message, typeof(TestHandler));

		// Assert
		activity.ShouldNotBeNull();
	}

	[Fact]
	public void StartHandleActivity_SetsHandlerTypeTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartHandleActivity(message, typeof(TestHandler));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("handler.type").ShouldBe("TestHandler");
	}

	[Fact]
	public void StartHandleActivity_SetsHandleOperation()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartHandleActivity(message, typeof(TestHandler));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.operation").ShouldBe("handle");
	}

	[Fact]
	public void StartHandleActivity_ThrowsOnNullMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartHandleActivity(null!, typeof(TestHandler)));
	}

	[Fact]
	public void StartHandleActivity_ThrowsOnNullHandlerType()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartHandleActivity(message, null!));
	}

	#endregion

	#region StartMiddlewareActivity Tests

	[Fact]
	public void StartMiddlewareActivity_ReturnsActivity()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartMiddlewareActivity(typeof(TestMiddleware), message);

		// Assert
		activity.ShouldNotBeNull();
	}

	[Fact]
	public void StartMiddlewareActivity_SetsMiddlewareTypeTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartMiddlewareActivity(typeof(TestMiddleware), message);

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("middleware.type").ShouldBe("TestMiddleware");
	}

	[Fact]
	public void StartMiddlewareActivity_SetsMessageTypeTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartMiddlewareActivity(typeof(TestMiddleware), message);

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("message.type").ShouldNotBeNull();
	}

	[Fact]
	public void StartMiddlewareActivity_SetsMiddlewareOperation()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartMiddlewareActivity(typeof(TestMiddleware), message);

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.operation").ShouldBe("middleware");
	}

	[Fact]
	public void StartMiddlewareActivity_ThrowsOnNullMiddlewareType()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartMiddlewareActivity(null!, message));
	}

	[Fact]
	public void StartMiddlewareActivity_ThrowsOnNullMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartMiddlewareActivity(typeof(TestMiddleware), null!));
	}

	#endregion

	#region Test Helpers

	private sealed class TestHandler
	{
	}

	private sealed class TestMiddleware
	{
	}

	#endregion
}
