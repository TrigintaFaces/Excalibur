// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="TimeoutMiddleware"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TimeoutMiddlewareShould : IAsyncDisposable
{
	private readonly ILogger<TimeoutMiddleware> _logger;
	private readonly TimeoutMiddleware _defaultMiddleware;

	public TimeoutMiddlewareShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<TimeoutMiddleware>();
		_defaultMiddleware = CreateMiddleware(new TimeoutOptions());
	}

	public async ValueTask DisposeAsync()
	{
		await _defaultMiddleware.DisposeAsync().ConfigureAwait(false);
	}

	private TimeoutMiddleware CreateMiddleware(TimeoutOptions options)
	{
		return new TimeoutMiddleware(_logger, MsOptions.Create(options));
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TimeoutMiddleware(null!, options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TimeoutMiddleware(_logger, null!));
	}

	#endregion

	#region Stage Tests

	[Fact]
	public void HaveProcessingStage()
	{
		// Assert
		_defaultMiddleware.Stage.ShouldBe(DispatchMiddlewareStage.Processing);
	}

	[Fact]
	public void HaveActionAndEventApplicableMessageKinds()
	{
		// Assert
		_defaultMiddleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			_defaultMiddleware.InvokeAsync(null!, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var message = new FakeDispatchMessage();
		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			_defaultMiddleware.InvokeAsync(message, null!, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			_defaultMiddleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Disabled Middleware Tests

	[Fact]
	public async Task PassThroughDirectly_WhenDisabled()
	{
		// Arrange
		await using var middleware = CreateMiddleware(new TimeoutOptions { Enabled = false });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var nextCalled = false;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Normal Execution Tests

	[Fact]
	public async Task CallNextDelegate_WhenWithinTimeout()
	{
		// Arrange
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromSeconds(30),
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var nextCalled = false;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnResult_FromNextDelegate()
	{
		// Arrange
		await using var middleware = CreateMiddleware(new TimeoutOptions { Enabled = true });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var expectedResult = MessageResult.Failed("expected error");

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(expectedResult);
	}

	#endregion

	#region Timeout Behavior Tests

	[Fact]
	public async Task ThrowMessageTimeoutException_WhenTimeoutExceeded_AndThrowOnTimeoutIsTrue()
	{
		// Arrange — use 200ms timeout with long-running task to avoid flaky races under load
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromMilliseconds(200),
			ActionTimeout = TimeSpan.FromMilliseconds(200),
			ThrowOnTimeout = true,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-timeout" };

		DispatchRequestDelegate next = async (msg, ctx, ct) =>
		{
			// Wait for the cancellation token to fire (the timeout), then throw
			await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
			return MessageResult.Success();
		};

		// Act & Assert
		_ = await Should.ThrowAsync<MessageTimeoutException>(
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ReturnTimeoutResult_WhenTimeoutExceeded_AndThrowOnTimeoutIsFalse()
	{
		// Arrange — use 200ms timeout with long-running task to avoid flaky races under load
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromMilliseconds(200),
			ActionTimeout = TimeSpan.FromMilliseconds(200),
			ThrowOnTimeout = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-timeout" };

		DispatchRequestDelegate next = async (msg, ctx, ct) =>
		{
			// Wait for the cancellation token to fire (the timeout), then throw
			await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
			return MessageResult.Success();
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNullOrEmpty();
		result.ErrorMessage.ShouldContain("timed out");
	}

	[Fact]
	public async Task SetTimeoutContextItems_WhenTimeoutExceeded()
	{
		// Arrange — use 200ms timeout with long-running task to avoid flaky races under load
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromMilliseconds(200),
			ActionTimeout = TimeSpan.FromMilliseconds(200),
			ThrowOnTimeout = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-timeout" };

		DispatchRequestDelegate next = async (msg, ctx, ct) =>
		{
			// Wait for the cancellation token to fire (the timeout), then throw
			await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
			return MessageResult.Success();
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		context.GetItem<bool>("Timeout.Exceeded").ShouldBeTrue();
		context.ContainsItem("Timeout.ElapsedTime").ShouldBeTrue();
		context.ContainsItem("Timeout.ConfiguredTimeout").ShouldBeTrue();
	}

	#endregion

	#region Timeout Configuration Tests

	[Fact]
	public async Task UseConfiguredTimeout()
	{
		// Arrange
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromMilliseconds(100),
			ThrowOnTimeout = true,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		DispatchRequestDelegate next = async (msg, ctx, ct) =>
		{
			// This should complete well within 100ms
			await Task.Delay(1, ct).ConfigureAwait(false);
			return MessageResult.Success();
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert - should succeed because we finished within the timeout
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task UseContextOverrideTimeout_WhenPresent()
	{
		// Arrange
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromMilliseconds(50),
			ThrowOnTimeout = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		// Set a generous context override timeout
		context.SetItem("Timeout.Override", TimeSpan.FromSeconds(30));

		DispatchRequestDelegate next = async (msg, ctx, ct) =>
		{
			// This 100ms delay would fail the 50ms default but passes the 30s override
			await Task.Delay(100, ct).ConfigureAwait(false);
			return MessageResult.Success();
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task UseMessageTypeSpecificTimeout_WhenConfigured()
	{
		// Arrange
		var options = new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromMilliseconds(50),
			ThrowOnTimeout = false,
		};
		options.MessageTypeTimeouts["FakeDispatchMessage"] = TimeSpan.FromSeconds(30);

		await using var middleware = CreateMiddleware(options);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		DispatchRequestDelegate next = async (msg, ctx, ct) =>
		{
			// This 100ms delay would fail the 50ms default but passes the type-specific 30s
			await Task.Delay(100, ct).ConfigureAwait(false);
			return MessageResult.Success();
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task UseActionTimeout_ForActionMessages()
	{
		// Arrange
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			ActionTimeout = TimeSpan.FromSeconds(30),
			DefaultTimeout = TimeSpan.FromMilliseconds(50),
			ThrowOnTimeout = false,
		});
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		DispatchRequestDelegate next = async (msg, ctx, ct) =>
		{
			// This 100ms delay would fail the 50ms default but passes the 30s action timeout
			await Task.Delay(100, ct).ConfigureAwait(false);
			return MessageResult.Success();
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task UseEventTimeout_ForEventMessages()
	{
		// Arrange
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			EventTimeout = TimeSpan.FromSeconds(120),
			DefaultTimeout = TimeSpan.FromMilliseconds(50),
			ThrowOnTimeout = false,
		});
		var message = new FakeEventMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		DispatchRequestDelegate next = async (msg, ctx, ct) =>
		{
			// This 100ms delay would fail the 50ms default but passes the 30s event timeout
			await Task.Delay(100, ct).ConfigureAwait(false);
			return MessageResult.Success();
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region CancellationToken Propagation Tests

	[Fact]
	public async Task PropagateCancellationToken_ThroughLinkedSource()
	{
		// Arrange
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromSeconds(30),
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		using var cts = new CancellationTokenSource();
		CancellationToken capturedToken = default;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			capturedToken = ct;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, next, cts.Token);

		// Assert - the middleware creates a linked token source, so the token should be different
		// but cancelling the original should also cancel the linked one
		capturedToken.CanBeCanceled.ShouldBeTrue();
	}

	[Fact]
	public async Task RethrowOperationCanceledException_WhenExternalCancellation()
	{
		// Arrange
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromSeconds(30),
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		DispatchRequestDelegate next = async (msg, ctx, ct) =>
		{
			ct.ThrowIfCancellationRequested();
			await Task.Yield();
			return MessageResult.Success();
		};

		// Act & Assert - external cancellation should propagate as OperationCanceledException,
		// not wrapped as MessageTimeoutException
		_ = await Should.ThrowAsync<OperationCanceledException>(
			middleware.InvokeAsync(message, context, next, cts.Token).AsTask());
	}

	#endregion

	#region Exception Propagation Tests

	[Fact]
	public async Task RethrowNonTimeoutExceptions()
	{
		// Arrange
		await using var middleware = CreateMiddleware(new TimeoutOptions { Enabled = true });
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			throw new InvalidOperationException("Test pipeline error");

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());
	}

	#endregion

	#region MessageTimeoutException Details Tests

	[Fact]
	public async Task IncludeMessageId_InTimeoutException()
	{
		// Arrange — use 200ms timeout with long-running task to avoid flaky races under load
		await using var middleware = CreateMiddleware(new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromMilliseconds(200),
			ActionTimeout = TimeSpan.FromMilliseconds(200),
			ThrowOnTimeout = true,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "msg-detail-test" };

		DispatchRequestDelegate next = async (msg, ctx, ct) =>
		{
			// Wait for the cancellation token to fire (the timeout), then throw
			await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
			return MessageResult.Success();
		};

		// Act
		var ex = await Should.ThrowAsync<MessageTimeoutException>(
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());

		// Assert
		ex.MessageId.ShouldBe("msg-detail-test");
		ex.MessageType.ShouldBe("FakeDispatchMessage");
		ex.TimeoutDuration.ShouldBe(TimeSpan.FromMilliseconds(200));
		ex.ElapsedTime.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	#endregion

	#region IAsyncDisposable Tests

	[Fact]
	public async Task DisposeWithoutError()
	{
		// Arrange
		var middleware = CreateMiddleware(new TimeoutOptions());

		// Act & Assert - should not throw
		await middleware.DisposeAsync().ConfigureAwait(false);
	}

	#endregion

	#region Default Options Tests

	[Fact]
	public void HaveCorrectDefaultOptionValues()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.ActionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.EventTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.DocumentTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.ThrowOnTimeout.ShouldBeTrue();
		options.MessageTypeTimeouts.ShouldBeEmpty();
	}

	#endregion

	#region Test Message Types

	/// <summary>
	/// Test message implementing IDispatchAction for action-specific timeout tests.
	/// </summary>
	private sealed class FakeActionMessage : IDispatchAction;

	/// <summary>
	/// Test message implementing IDispatchEvent for event-specific timeout tests.
	/// </summary>
	private sealed class FakeEventMessage : IDispatchEvent;

	#endregion
}
