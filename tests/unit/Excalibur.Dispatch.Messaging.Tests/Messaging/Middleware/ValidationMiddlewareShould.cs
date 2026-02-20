// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="ValidationMiddleware"/> class.
/// </summary>
/// <remarks>
/// Sprint 414 - Task T414.5: ValidationMiddleware tests (0% → 50%+).
/// Tests validation middleware implementation including Data Annotations and custom validation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class ValidationMiddlewareShould
{
	private readonly ILogger<ValidationMiddleware> _logger;
	private readonly IValidationService _validationService;
	private readonly IMessageContext _context;
	private readonly DispatchRequestDelegate _successDelegate;

	public ValidationMiddlewareShould()
	{
		_logger = A.Fake<ILogger<ValidationMiddleware>>();
		_validationService = A.Fake<IValidationService>();
		_context = A.Fake<IMessageContext>();

		_ = A.CallTo(() => _context.MessageId).Returns("test-message-id");

		_successDelegate = (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ValidationMiddleware(null!, _validationService, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenValidationServiceIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ValidationMiddleware(options, null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new ValidationMiddleware(options, _validationService, null!));
	}

	#endregion

	#region Stage Tests

	[Fact]
	public void HaveValidationStage()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions());
		var middleware = new ValidationMiddleware(options, _validationService, _logger);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Validation);
	}

	[Fact]
	public void HaveActionApplicableMessageKinds()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions());
		var middleware = new ValidationMiddleware(options, _validationService, _logger);

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions());
		var middleware = new ValidationMiddleware(options, _validationService, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions());
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions());
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, _context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Disabled Validation Tests

	[Fact]
	public async Task PassThroughDirectly_WhenDisabled()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions { Enabled = false });
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	#endregion

	#region Custom Validation Tests

	[Fact]
	public async Task UseCustomValidation_WhenEnabled()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = false,
			UseCustomValidation = true
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._))
			.Returns(MessageValidationResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		_ = A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowValidationException_WhenCustomValidationFails()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = false,
			UseCustomValidation = true
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._))
			.Returns(MessageValidationResult.Failure(new ValidationError("PropertyName", "Validation failed")));

		// Act & Assert
		_ = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task SkipCustomValidation_WhenDisabled()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = false,
			UseCustomValidation = false
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	#endregion

	#region Data Annotations Validation Tests

	[Fact]
	public async Task PassValidation_WhenDataAnnotationsAreValid()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = true,
			UseCustomValidation = false
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = new ValidTestMessage { Name = "Test", Value = 5 };

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowValidationException_WhenDataAnnotationsAreInvalid()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = true,
			UseCustomValidation = false
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = new InvalidTestMessage { Name = null!, Value = -1 };

		// Act & Assert
		_ = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	#endregion

	#region Combined Validation Tests

	[Fact]
	public async Task CombineDataAnnotationsAndCustomValidation_WhenBothEnabled()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = true,
			UseCustomValidation = true
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = new ValidTestMessage { Name = "Test", Value = 5 };

		_ = A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._))
			.Returns(MessageValidationResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		_ = A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region StopOnFirstError Tests

	[Fact]
	public async Task StopOnFirstError_WhenConfigured()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = false,
			UseCustomValidation = true,
			StopOnFirstError = true
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._))
			.Returns(MessageValidationResult.Failure(
				new ValidationError("Property1", "Error 1"),
				new ValidationError("Property2", "Error 2")));

		// Act & Assert
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// StopOnFirstError should only report one error in the exception message
		// The exception message contains the error summary, which should have only one error
		exception.Message.ShouldContain("Error 1");
		exception.Message.ShouldNotContain("Error 2");
	}

	#endregion

	#region Validation Context Tests

	[Fact]
	public async Task CreateValidationContextWithTenantId_WhenAvailable()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = false,
			UseCustomValidation = true
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _context.GetItem<object>("TenantId")).Returns("tenant-123");

		MessageValidationContext? capturedContext = null;
		_ = A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._))
			.Invokes((IDispatchMessage _, MessageValidationContext ctx, CancellationToken _) =>
				capturedContext = ctx)
			.Returns(MessageValidationResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.TenantId.ShouldBe("tenant-123");
	}

	[Fact]
	public async Task CreateValidationContextWithUserId_WhenAvailable()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = false,
			UseCustomValidation = true
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _context.GetItem<object>("UserId")).Returns("user-456");

		MessageValidationContext? capturedContext = null;
		_ = A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._))
			.Invokes((IDispatchMessage _, MessageValidationContext ctx, CancellationToken _) =>
				capturedContext = ctx)
			.Returns(MessageValidationResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.UserId.ShouldBe("user-456");
	}

	[Fact]
	public async Task CreateValidationContextWithCorrelationId_WhenAvailable()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = false,
			UseCustomValidation = true
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _context.GetItem<object>("CorrelationId")).Returns("correlation-789");

		MessageValidationContext? capturedContext = null;
		_ = A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._))
			.Invokes((IDispatchMessage _, MessageValidationContext ctx, CancellationToken _) =>
				capturedContext = ctx)
			.Returns(MessageValidationResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.CorrelationId.ShouldBe("correlation-789");
	}

	#endregion

	#region Exception Handling Tests

	[Fact]
	public async Task RethrowNonValidationExceptions()
	{
		// Arrange
		var options = MsOptions.Create(new ValidationOptions
		{
			Enabled = true,
			UseDataAnnotations = false,
			UseCustomValidation = true
		});
		var middleware = new ValidationMiddleware(options, _validationService, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _validationService.ValidateAsync(
			A<IDispatchMessage>._,
			A<MessageValidationContext>._,
			A<CancellationToken>._))
			.Throws(new InvalidOperationException("Service error"));

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	#endregion

	#region Test Fixtures

	/// <summary>
	/// Test message with valid data annotations.
	/// </summary>
	private sealed class ValidTestMessage : IDispatchMessage
	{
		[Required]
		public string Name { get; set; } = string.Empty;

		[Range(0, 10)]
		public int Value { get; set; }
	}

	/// <summary>
	/// Test message that will fail data annotation validation.
	/// </summary>
	private sealed class InvalidTestMessage : IDispatchMessage
	{
		[Required]
		public string Name { get; set; } = null!;

		[Range(0, 10)]
		public int Value { get; set; } = -1;
	}

	#endregion
}
