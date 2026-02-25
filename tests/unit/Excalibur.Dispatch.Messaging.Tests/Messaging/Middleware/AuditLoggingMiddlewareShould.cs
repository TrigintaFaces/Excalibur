// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="AuditLoggingMiddleware"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AuditLoggingMiddlewareShould
{
	private readonly ILogger<AuditLoggingMiddleware> _logger;

	public AuditLoggingMiddlewareShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<AuditLoggingMiddleware>();
	}

	private static AuditLoggingMiddleware CreateMiddleware(
		AuditLoggingOptions options,
		ILogger<AuditLoggingMiddleware> logger)
	{
		return new AuditLoggingMiddleware(MsOptions.Create(options), NullTelemetrySanitizer.Instance, logger);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new AuditLoggingMiddleware(null!, NullTelemetrySanitizer.Instance, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new AuditLoggingOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new AuditLoggingMiddleware(options, NullTelemetrySanitizer.Instance, null!));
	}

	#endregion

	#region Stage Tests

	[Fact]
	public void HaveLoggingStage()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Logging);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Delegate Invocation Tests

	[Fact]
	public async Task CallNextDelegate()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
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
	public async Task ReturnResultFromNextDelegate()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var expectedResult = MessageResult.Failed("expected error");

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(expectedResult);
		result.IsSuccess.ShouldBeFalse();
	}

	#endregion

	#region Exception Handling Tests

	[Fact]
	public async Task RethrowException_WhenNextDelegateThrows()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			throw new InvalidOperationException("Test pipeline failure");

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task PreserveExceptionDetails_WhenNextDelegateThrows()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var expectedException = new InvalidOperationException("Specific error message");

		DispatchRequestDelegate next = (msg, ctx, ct) => throw expectedException;

		// Act & Assert
		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());

		thrown.Message.ShouldBe("Specific error message");
		thrown.ShouldBeSameAs(expectedException);
	}

	#endregion

	#region User ID Extraction Tests

	[Fact]
	public async Task ExtractUserId_FromContextProperties()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };
		context.AddProperty("UserId", "user-abc-123");

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act - should not throw; the middleware extracts UserId internally for logging
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ExtractUserId_FromCustomExtractor()
	{
		// Arrange
		var extractorCalled = false;
		var options = new AuditLoggingOptions
		{
			UserIdExtractor = ctx =>
			{
				extractorCalled = true;
				return "custom-user-id";
			}
		};
		var middleware = CreateMiddleware(options, _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		extractorCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ExtractUserId_FromHeaders_WhenNotInProperties()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };
		context.AddProperty("Headers", new Dictionary<string, object> { { "UserId", "header-user-id" } });

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Correlation ID Extraction Tests

	[Fact]
	public async Task ExtractCorrelationId_FromContextProperties()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };
		context.AddProperty("CorrelationId", "corr-123");

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ExtractCorrelationId_FromCustomExtractor()
	{
		// Arrange
		var extractorCalled = false;
		var options = new AuditLoggingOptions
		{
			CorrelationIdExtractor = ctx =>
			{
				extractorCalled = true;
				return "custom-corr-id";
			}
		};
		var middleware = CreateMiddleware(options, _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		extractorCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ExtractCorrelationId_FromHeaders_WhenNotInProperties()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };
		context.AddProperty("Headers", new Dictionary<string, object> { { "CorrelationId", "header-corr-id" } });

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Payload Logging Tests

	[Fact]
	public async Task NotLogPayload_WhenLogMessagePayloadIsFalse()
	{
		// Arrange
		var options = new AuditLoggingOptions { LogMessagePayload = false };
		var middleware = CreateMiddleware(options, _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act - should not throw and should succeed without payload logging
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task LogPayload_WhenLogMessagePayloadIsTrue()
	{
		// Arrange
		var options = new AuditLoggingOptions { LogMessagePayload = true };
		var middleware = CreateMiddleware(options, _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act - middleware should not throw when serializing the payload
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task RespectPayloadFilter_WhenConfigured()
	{
		// Arrange
		var filterCalled = false;
		var options = new AuditLoggingOptions
		{
			LogMessagePayload = true,
			PayloadFilter = msg =>
			{
				filterCalled = true;
				return false; // Don't log this payload
			}
		};
		var middleware = CreateMiddleware(options, _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		filterCalled.ShouldBeTrue();
	}

	#endregion

	#region Success Result Flow Tests

	[Fact]
	public async Task ReturnSuccessResult_WhenNextDelegateSucceeds()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Failure Result Flow Tests

	[Fact]
	public async Task ReturnFailedResult_WhenNextDelegateReturnsFailed()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };
		var failedResult = MessageResult.Failed("Processing failed");

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(failedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ShouldBeSameAs(failedResult);
	}

	#endregion

	#region Message Context Handling Tests

	[Fact]
	public async Task HandleNullMessageId_InContext()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = null };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act - should not throw even with null MessageId
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleValidGuidMessageId_InContext()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var validGuid = Guid.NewGuid();
		var context = new FakeMessageContext { MessageId = validGuid.ToString() };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleNonGuidMessageId_InContext()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "not-a-guid" };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act - the middleware uses Guid.TryParse and falls back to Guid.Empty
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region CancellationToken Tests

	[Fact]
	public async Task PassCancellationToken_ToNextDelegate()
	{
		// Arrange
		var middleware = CreateMiddleware(new AuditLoggingOptions(), _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };
		using var cts = new CancellationTokenSource();
		CancellationToken capturedToken = default;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			capturedToken = ct;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, next, cts.Token);

		// Assert
		capturedToken.ShouldBe(cts.Token);
	}

	#endregion

	#region Combined Extractor and Payload Tests

	[Fact]
	public async Task HandleBothExtractors_WithPayloadLogging()
	{
		// Arrange
		var options = new AuditLoggingOptions
		{
			UserIdExtractor = _ => "extracted-user",
			CorrelationIdExtractor = _ => "extracted-corr",
			LogMessagePayload = true,
			MaxPayloadSize = 50000,
			MaxPayloadDepth = 3,
		};
		var middleware = CreateMiddleware(options, _logger);
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = Guid.NewGuid().ToString() };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion
}
