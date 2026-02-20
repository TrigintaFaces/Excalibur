// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="InputSanitizationMiddleware"/> class.
/// </summary>
/// <remarks>
/// Sprint 554 - Task S554.42: InputSanitizationMiddleware tests.
/// Tests sanitization of message properties, pass-through of clean messages,
/// XSS prevention, path traversal prevention, and custom sanitization service.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class InputSanitizationMiddlewareShould
{
	private readonly ILogger<InputSanitizationMiddleware> _logger;

	public InputSanitizationMiddlewareShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<InputSanitizationMiddleware>();
	}

	private InputSanitizationMiddleware CreateMiddleware(
		InputSanitizationOptions options,
		ISanitizationService? sanitizationService = null)
	{
		return new InputSanitizationMiddleware(MsOptions.Create(options), sanitizationService, _logger);
	}

	private static DispatchRequestDelegate CreateSuccessDelegate()
	{
		return (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InputSanitizationMiddleware(null!, null, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new InputSanitizationOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InputSanitizationMiddleware(options, null, null!));
	}

	[Fact]
	public void NotThrow_WhenSanitizationServiceIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new InputSanitizationOptions());

		// Act & Assert - sanitization service is optional
		_ = Should.NotThrow(() =>
			new InputSanitizationMiddleware(options, null, _logger));
	}

	#endregion

	#region Stage and ApplicableMessageKinds Tests

	[Fact]
	public void HavePreProcessingStage()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions());

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void HaveActionAndEventApplicableMessageKinds()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions());

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions());
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions());
		var message = new FakeDispatchMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions());
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Disabled Middleware Tests

	[Fact]
	public async Task PassThroughDirectly_WhenDisabled()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions { Enabled = false });
		var message = new SanitizableMessage { Name = "<script>alert('xss')</script>" };
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
		// Message should NOT be sanitized when disabled
		message.Name.ShouldContain("<script>");
	}

	#endregion

	#region Sanitization of Message Properties Tests

	[Fact]
	public async Task RemoveScriptTags_FromMessageProperties()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
			Features = new SanitizationFeatures
			{
				PreventXss = true,
				RemoveHtmlTags = true,
				PreventSqlInjection = false,
				PreventPathTraversal = false,
				RemoveNullBytes = false,
				NormalizeUnicode = false,
				TrimWhitespace = false,
			},
			SanitizeContextItems = false,
			UseCustomSanitization = false,
		});
		var message = new SanitizableMessage { Name = "Hello<script>alert('xss')</script>World" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		message.Name.ShouldNotContain("<script>");
		message.Name.ShouldContain("Hello");
		message.Name.ShouldContain("World");
	}

	[Fact]
	public async Task RemoveHtmlTags_FromMessageProperties()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
			Features = new SanitizationFeatures
			{
				PreventXss = true,
				RemoveHtmlTags = true,
				PreventSqlInjection = false,
				PreventPathTraversal = false,
				RemoveNullBytes = false,
				NormalizeUnicode = false,
				TrimWhitespace = false,
			},
			SanitizeContextItems = false,
			UseCustomSanitization = false,
		});
		var message = new SanitizableMessage { Name = "Hello <b>bold</b> World" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		message.Name.ShouldNotContain("<b>");
		message.Name.ShouldNotContain("</b>");
		message.Name.ShouldContain("Hello");
		message.Name.ShouldContain("World");
	}

	[Fact]
	public async Task RemoveNullBytes_FromMessageProperties()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
			Features = new SanitizationFeatures
			{
				PreventXss = false,
				RemoveHtmlTags = false,
				PreventSqlInjection = false,
				PreventPathTraversal = false,
				RemoveNullBytes = true,
				NormalizeUnicode = false,
				TrimWhitespace = false,
			},
			SanitizeContextItems = false,
			UseCustomSanitization = false,
		});
		var message = new SanitizableMessage { Name = "Hello\0World" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		message.Name.ShouldBe("HelloWorld");
	}

	[Fact]
	public async Task RemovePathTraversal_FromMessageProperties()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
			Features = new SanitizationFeatures
			{
				PreventXss = false,
				RemoveHtmlTags = false,
				PreventSqlInjection = false,
				PreventPathTraversal = true,
				RemoveNullBytes = false,
				NormalizeUnicode = false,
				TrimWhitespace = false,
			},
			SanitizeContextItems = false,
			UseCustomSanitization = false,
		});
		var message = new SanitizableMessage { Name = "file../../etc/passwd" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		message.Name.ShouldNotContain("../");
		message.Name.ShouldNotContain("..");
	}

	[Fact]
	public async Task TrimWhitespace_FromMessageProperties()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
			Features = new SanitizationFeatures
			{
				PreventXss = false,
				RemoveHtmlTags = false,
				PreventSqlInjection = false,
				PreventPathTraversal = false,
				RemoveNullBytes = false,
				NormalizeUnicode = false,
				TrimWhitespace = true,
			},
			SanitizeContextItems = false,
			UseCustomSanitization = false,
		});
		var message = new SanitizableMessage { Name = "  hello world  " };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		message.Name.ShouldBe("hello world");
	}

	[Fact]
	public async Task EnforceMaxStringLength_WhenConfigured()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
			MaxStringLength = 10,
			Features = new SanitizationFeatures
			{
				PreventXss = false,
				RemoveHtmlTags = false,
				PreventSqlInjection = false,
				PreventPathTraversal = false,
				RemoveNullBytes = false,
				NormalizeUnicode = false,
				TrimWhitespace = false,
			},
			SanitizeContextItems = false,
			UseCustomSanitization = false,
		});
		var message = new SanitizableMessage { Name = "this is a very long string that should be truncated" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		message.Name.Length.ShouldBeLessThanOrEqualTo(10);
	}

	#endregion

	#region Pass-through of Clean Messages Tests

	[Fact]
	public async Task PassThroughCleanMessages_WithoutModification()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
			Features = new SanitizationFeatures
			{
				PreventXss = true,
				RemoveHtmlTags = true,
				PreventSqlInjection = true,
				PreventPathTraversal = true,
				RemoveNullBytes = true,
				NormalizeUnicode = false,
				TrimWhitespace = false,
			},
			SanitizeContextItems = false,
			UseCustomSanitization = false,
		});
		const string cleanName = "John Doe";
		var message = new SanitizableMessage { Name = cleanName };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		message.Name.ShouldBe(cleanName);
	}

	[Fact]
	public async Task SetSanitizationApplied_InContext_WhenValuesSanitized()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
			Features = new SanitizationFeatures
			{
				PreventXss = true,
				RemoveHtmlTags = true,
				PreventSqlInjection = false,
				PreventPathTraversal = false,
				RemoveNullBytes = false,
				NormalizeUnicode = false,
				TrimWhitespace = false,
			},
			SanitizeContextItems = false,
			UseCustomSanitization = false,
		});
		var message = new SanitizableMessage { Name = "<script>bad</script>" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		context.GetItem<bool>("Sanitization.Applied").ShouldBeTrue();
		context.GetItem<int>("Sanitization.Count").ShouldBeGreaterThan(0);
	}

	#endregion

	#region Bypass Sanitization Tests

	[Fact]
	public async Task BypassSanitization_WhenMessageTypeIsInBypassList()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
			BypassSanitizationForTypes = ["SanitizableMessage"],
		});
		var message = new SanitizableMessage { Name = "<script>alert('xss')</script>" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert - Message should NOT be sanitized
		message.Name.ShouldContain("<script>");
	}

	[Fact]
	public async Task BypassSanitization_WhenMessageHasBypassAttribute()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
		});
		var message = new BypassedSanitizationMessage { Name = "<script>alert('xss')</script>" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert - Message should NOT be sanitized
		message.Name.ShouldContain("<script>");
	}

	#endregion

	#region Custom Sanitization Service Tests

	[Fact]
	public async Task UseCustomSanitizationService_WhenConfigured()
	{
		// Arrange
		var sanitizationService = A.Fake<ISanitizationService>();
		A.CallTo(() => sanitizationService.SanitizeValueAsync(
				A<object?>._, A<string>._, A<Type>._, A<CancellationToken>._))
			.Returns(Task.FromResult<object?>("custom-sanitized"));

		var middleware = CreateMiddleware(
			new InputSanitizationOptions
			{
				Enabled = true,
				UseCustomSanitization = true,
				SanitizeContextItems = false,
				Features = new SanitizationFeatures
				{
					PreventXss = false,
					RemoveHtmlTags = false,
					PreventSqlInjection = false,
					PreventPathTraversal = false,
					RemoveNullBytes = false,
					NormalizeUnicode = false,
					TrimWhitespace = false,
				},
			},
			sanitizationService);
		var message = new SanitizableMessage { Name = "original-value" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		message.Name.ShouldBe("custom-sanitized");
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task LogAndContinue_WhenPerPropertySanitizationErrorOccurs_EvenWithThrowOnErrorTrue()
	{
		// Arrange
		// Per-property sanitization errors are caught and logged in SanitizeMessagePropertiesAsync,
		// not propagated to the outer catch block. ThrowOnSanitizationError only applies to
		// infrastructure-level failures that escape the per-property try/catch.
		var sanitizationService = A.Fake<ISanitizationService>();
		A.CallTo(() => sanitizationService.SanitizeValueAsync(
				A<object?>._, A<string>._, A<Type>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("sanitization error"));

		var middleware = CreateMiddleware(
			new InputSanitizationOptions
			{
				Enabled = true,
				ThrowOnSanitizationError = true,
				UseCustomSanitization = true,
				SanitizeContextItems = false,
			},
			sanitizationService);
		var message = new SanitizableMessage { Name = "test" };
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var nextCalled = false;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act - Per-property errors are swallowed, pipeline continues
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ContinueWithoutSanitization_WhenErrorOccursAndThrowOnErrorIsFalse()
	{
		// Arrange
		var sanitizationService = A.Fake<ISanitizationService>();
		A.CallTo(() => sanitizationService.SanitizeValueAsync(
				A<object?>._, A<string>._, A<Type>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("sanitization error"));

		var middleware = CreateMiddleware(
			new InputSanitizationOptions
			{
				Enabled = true,
				ThrowOnSanitizationError = false,
				UseCustomSanitization = true,
				SanitizeContextItems = false,
			},
			sanitizationService);
		var message = new SanitizableMessage { Name = "test" };
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

	#region Context Item Sanitization Tests

	[Fact]
	public async Task SanitizeContextItems_WhenEnabled()
	{
		// Arrange
		var middleware = CreateMiddleware(new InputSanitizationOptions
		{
			Enabled = true,
			SanitizeContextItems = true,
			UseCustomSanitization = false,
			Features = new SanitizationFeatures
			{
				PreventXss = false,
				RemoveHtmlTags = false,
				PreventSqlInjection = false,
				PreventPathTraversal = false,
				RemoveNullBytes = false,
				NormalizeUnicode = false,
				TrimWhitespace = false,
			},
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		// Inject CRLF into UserAgent header (a known sanitization target)
		context.SetItem("UserAgent", "Mozilla\r\nInjected-Header: evil");

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert - CRLF characters should be stripped
		var sanitizedUA = context.GetItem<string>("UserAgent");
		sanitizedUA.ShouldNotBeNull();
		sanitizedUA.ShouldNotContain("\r");
		sanitizedUA.ShouldNotContain("\n");
	}

	#endregion

	#region Default Options Tests

	[Fact]
	public void HaveCorrectDefaultOptionValues()
	{
		// Arrange
		var options = new InputSanitizationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.MaxStringLength.ShouldBe(0);
		options.SanitizeContextItems.ShouldBeTrue();
		options.UseCustomSanitization.ShouldBeTrue();
		options.ThrowOnSanitizationError.ShouldBeFalse();
		options.BypassSanitizationForTypes.ShouldBeNull();
		options.ExcludeProperties.ShouldBeNull();
	}

	[Fact]
	public void HaveCorrectDefaultFeatureValues()
	{
		// Arrange
		var features = new SanitizationFeatures();

		// Assert
		features.PreventXss.ShouldBeTrue();
		features.RemoveHtmlTags.ShouldBeTrue();
		features.PreventSqlInjection.ShouldBeTrue();
		features.PreventPathTraversal.ShouldBeTrue();
		features.RemoveNullBytes.ShouldBeTrue();
		features.NormalizeUnicode.ShouldBeTrue();
		features.TrimWhitespace.ShouldBeTrue();
	}

	#endregion

	#region Test Message Types

	/// <summary>
	/// Test message with writable string properties for sanitization.
	/// </summary>
	private sealed class SanitizableMessage : IDispatchMessage
	{
		public string Name { get; set; } = string.Empty;
		public string? Description { get; set; }
	}

	/// <summary>
	/// Test message with [BypassSanitization] attribute.
	/// </summary>
	[BypassSanitization]
	private sealed class BypassedSanitizationMessage : IDispatchMessage
	{
		public string Name { get; set; } = string.Empty;
	}

	#endregion
}
