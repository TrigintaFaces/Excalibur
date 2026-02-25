// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Metrics;

/// <summary>
/// Unit tests for <see cref="DispatchActivitySource"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class DispatchActivitySourceShould : UnitTestBase
{
	private readonly ActivityListener _listener;

	public DispatchActivitySourceShould()
	{
		// Set up listener to ensure activities are created
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == DispatchActivitySource.Name,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
		};
		ActivitySource.AddActivityListener(_listener);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_listener.Dispose();
		}

		base.Dispose(disposing);
	}

	#region Constants and Instance Tests

	[Fact]
	public void HaveCorrectNameConstant()
	{
		// Assert
		DispatchActivitySource.Name.ShouldBe("Excalibur.Dispatch");
	}

	[Fact]
	public void HaveNonNullInstance()
	{
		// Assert
		DispatchActivitySource.Instance.ShouldNotBeNull();
	}

	[Fact]
	public void InstanceHaveCorrectName()
	{
		// Assert
		DispatchActivitySource.Instance.Name.ShouldBe(DispatchActivitySource.Name);
	}

	#endregion

	#region StartActivity Tests

	[Fact]
	public void StartActivity_WithValidParameters()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartActivity(message, "test.operation");

		// Assert
		activity.ShouldNotBeNull();
	}

	[Fact]
	public void StartActivity_SetMessageTypeTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartActivity(message, "test.operation");

		// Assert
		activity.ShouldNotBeNull();
		activity.Tags.ShouldContain(t => t.Key == "message.type");
	}

	[Fact]
	public void StartActivity_SetOperationTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartActivity(message, "my.custom.operation");

		// Assert
		activity.ShouldNotBeNull();
		activity.Tags.ShouldContain(t => t.Key == "dispatch.operation" && t.Value == "my.custom.operation");
	}

	[Fact]
	public void StartActivity_ThrowOnNullMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartActivity(null!, "test.operation"));
	}

	#endregion

	#region StartPublishActivity Tests

	[Fact]
	public void StartPublishActivity_WithValidParameters()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartPublishActivity(message, "orders-topic");

		// Assert
		activity.ShouldNotBeNull();
	}

	[Fact]
	public void StartPublishActivity_SetDestinationTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartPublishActivity(message, "rabbitmq://orders");

		// Assert
		activity.ShouldNotBeNull();
		activity.Tags.ShouldContain(t => t.Key == "message.destination" && t.Value == "rabbitmq://orders");
	}

	[Fact]
	public void StartPublishActivity_SetPublishOperation()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		using var activity = DispatchActivitySource.StartPublishActivity(message, "test-destination");

		// Assert
		activity.ShouldNotBeNull();
		activity.Tags.ShouldContain(t => t.Key == "dispatch.operation" && t.Value == "publish");
	}

	[Fact]
	public void StartPublishActivity_ThrowOnNullMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartPublishActivity(null!, "destination"));
	}

	#endregion

	#region StartHandleActivity Tests

	[Fact]
	public void StartHandleActivity_WithValidParameters()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var handlerType = typeof(TestHandler);

		// Act
		using var activity = DispatchActivitySource.StartHandleActivity(message, handlerType);

		// Assert
		activity.ShouldNotBeNull();
	}

	[Fact]
	public void StartHandleActivity_SetHandlerTypeTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var handlerType = typeof(TestHandler);

		// Act
		using var activity = DispatchActivitySource.StartHandleActivity(message, handlerType);

		// Assert
		activity.ShouldNotBeNull();
		activity.Tags.ShouldContain(t => t.Key == "handler.type" && t.Value == "TestHandler");
	}

	[Fact]
	public void StartHandleActivity_SetHandleOperation()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var handlerType = typeof(TestHandler);

		// Act
		using var activity = DispatchActivitySource.StartHandleActivity(message, handlerType);

		// Assert
		activity.ShouldNotBeNull();
		activity.Tags.ShouldContain(t => t.Key == "dispatch.operation" && t.Value == "handle");
	}

	[Fact]
	public void StartHandleActivity_ThrowOnNullMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartHandleActivity(null!, typeof(TestHandler)));
	}

	[Fact]
	public void StartHandleActivity_ThrowOnNullHandlerType()
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
	public void StartMiddlewareActivity_WithValidParameters()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var middlewareType = typeof(TestMiddleware);

		// Act
		using var activity = DispatchActivitySource.StartMiddlewareActivity(middlewareType, message);

		// Assert
		activity.ShouldNotBeNull();
	}

	[Fact]
	public void StartMiddlewareActivity_SetMiddlewareTypeTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var middlewareType = typeof(TestMiddleware);

		// Act
		using var activity = DispatchActivitySource.StartMiddlewareActivity(middlewareType, message);

		// Assert
		activity.ShouldNotBeNull();
		activity.Tags.ShouldContain(t => t.Key == "middleware.type" && t.Value == "TestMiddleware");
	}

	[Fact]
	public void StartMiddlewareActivity_SetMiddlewareOperation()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var middlewareType = typeof(TestMiddleware);

		// Act
		using var activity = DispatchActivitySource.StartMiddlewareActivity(middlewareType, message);

		// Assert
		activity.ShouldNotBeNull();
		activity.Tags.ShouldContain(t => t.Key == "dispatch.operation" && t.Value == "middleware");
	}

	[Fact]
	public void StartMiddlewareActivity_ThrowOnNullMiddlewareType()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartMiddlewareActivity(null!, message));
	}

	[Fact]
	public void StartMiddlewareActivity_ThrowOnNullMessage()
	{
		// Arrange
		var middlewareType = typeof(TestMiddleware);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchActivitySource.StartMiddlewareActivity(middlewareType, null!));
	}

	#endregion

	#region Test Helpers

	private sealed class TestHandler;

	private sealed class TestMiddleware;

	#endregion
}
