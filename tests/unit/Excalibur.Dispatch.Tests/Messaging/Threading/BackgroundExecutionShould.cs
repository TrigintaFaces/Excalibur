// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Threading;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BackgroundExecutionShould
{
	// --- BackgroundExecutionMiddleware ---

	[Fact]
	public void Constructor_WithNullLogger_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new BackgroundExecutionMiddleware(null!));
	}

	[Fact]
	public void Stage_ReturnsExpectedValue()
	{
		// Arrange
		var middleware = new BackgroundExecutionMiddleware(
			NullLogger<BackgroundExecutionMiddleware>.Instance);

		// Assert
		middleware.Stage.ShouldNotBeNull();
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.End - 1);
	}

	[Fact]
	public async Task InvokeAsync_NonBackgroundMessage_PassesThrough()
	{
		// Arrange
		var middleware = new BackgroundExecutionMiddleware(
			NullLogger<BackgroundExecutionMiddleware>.Instance);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var called = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			called = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		called.ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_BackgroundMessage_ReturnsSuccess()
	{
		// Arrange
		var middleware = new BackgroundExecutionMiddleware(
			NullLogger<BackgroundExecutionMiddleware>.Instance);
		var message = new TestBackgroundMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_WithNullMessage_Throws()
	{
		// Arrange
		var middleware = new BackgroundExecutionMiddleware(
			NullLogger<BackgroundExecutionMiddleware>.Instance);
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(null!, context, next, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_WithNullContext_Throws()
	{
		// Arrange
		var middleware = new BackgroundExecutionMiddleware(
			NullLogger<BackgroundExecutionMiddleware>.Instance);
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(message, null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_WithNullNextDelegate_Throws()
	{
		// Arrange
		var middleware = new BackgroundExecutionMiddleware(
			NullLogger<BackgroundExecutionMiddleware>.Instance);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	// --- BackgroundTaskRunner ---

	[Fact]
	public async Task BackgroundTaskRunner_RunDetachedInBackground_ExecutesTask()
	{
		// Arrange
		var executed = false;
		Func<CancellationToken, Task> taskFactory = _ =>
		{
			executed = true;
			return Task.CompletedTask;
		};

		// Act
		BackgroundTaskRunner.RunDetachedInBackground(taskFactory, CancellationToken.None);
		await Task.Delay(200); // Allow background task to execute

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task BackgroundTaskRunner_RunDetachedInBackground_CallsErrorHandler()
	{
		// Arrange
		Exception? caughtException = null;
		Func<CancellationToken, Task> taskFactory = _ => throw new InvalidOperationException("test error");
		Func<Exception, Task> onError = ex =>
		{
			caughtException = ex;
			return Task.CompletedTask;
		};

		// Act
		BackgroundTaskRunner.RunDetachedInBackground(taskFactory, CancellationToken.None, onError);
		await Task.Delay(200); // Allow background task to execute

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public void BackgroundTaskRunner_RunDetachedInBackground_WithLogger_DoesNotThrow()
	{
		// Arrange
		Func<CancellationToken, Task> taskFactory = _ => throw new InvalidOperationException("test error");
		var logger = NullLogger.Instance;

		// Act & Assert - should not throw even with unhandled exception
		BackgroundTaskRunner.RunDetachedInBackground(taskFactory, CancellationToken.None, logger: logger);
	}

	// --- ThreadingServiceCollectionExtensions ---

	[Fact]
	public void AddDispatchThreading_RegistersServices()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		// Act
		services.AddDispatchThreading();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IKeyedLock));
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMiddleware));
	}

	// --- Test helpers ---

	private sealed class TestBackgroundMessage : IDispatchMessage, IExecuteInBackground
	{
		public bool PropagateExceptions => false;
	}
}
