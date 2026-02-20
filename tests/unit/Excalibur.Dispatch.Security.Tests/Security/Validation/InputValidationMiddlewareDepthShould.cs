// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Validation;

/// <summary>
/// Depth tests for <see cref="InputValidationMiddleware"/>.
/// Covers disabled validation bypass, context validation (correlation ID, message ID, timestamps),
/// custom validator failure propagation, and the middleware stage property.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class InputValidationMiddlewareDepthShould
{
	private readonly ILogger<InputValidationMiddleware> _logger;
	private readonly ISecurityEventLogger _securityEventLogger;
	private readonly IDispatchMessage _message;
	private readonly DispatchRequestDelegate _nextDelegate;
	private readonly IMessageResult _successResult;

	public InputValidationMiddlewareDepthShould()
	{
		_logger = NullLogger<InputValidationMiddleware>.Instance;
		_securityEventLogger = A.Fake<ISecurityEventLogger>();
		_message = A.Fake<IDispatchMessage>();
		_nextDelegate = A.Fake<DispatchRequestDelegate>();
		_successResult = A.Fake<IMessageResult>();

		A.CallTo(() => _successResult.Succeeded).Returns(true);
		A.CallTo(() => _nextDelegate(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(_successResult));

		A.CallTo(() => _securityEventLogger.LogSecurityEventAsync(
			A<SecurityEventType>._, A<string>._, A<SecuritySeverity>._, A<CancellationToken>._, A<IMessageContext?>._))
			.Returns(Task.CompletedTask);
	}

	private static IMessageContext CreateValidContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		return context;
	}

	private static InputValidationOptions CreatePermissiveOptions() => new()
	{
		EnableValidation = true,
		AllowNullProperties = true,
		AllowEmptyStrings = true,
		BlockSqlInjection = false,
		BlockNoSqlInjection = false,
		BlockCommandInjection = false,
		BlockPathTraversal = false,
		BlockLdapInjection = false,
		BlockHtmlContent = false,
		BlockControlCharacters = false,
		RequireCorrelationId = false,
		MaxMessageSizeBytes = int.MaxValue,
		MaxObjectDepth = 100,
	};

	[Fact]
	public void HaveValidationStage()
	{
		var sut = new InputValidationMiddleware(_logger, new InputValidationOptions(), [], _securityEventLogger);
		sut.Stage.ShouldBe(DispatchMiddlewareStage.Validation);
	}

	[Fact]
	public async Task PassWhenValidationDisabled()
	{
		// Arrange
		var options = new InputValidationOptions { EnableValidation = false };
		var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);
		var context = CreateValidContext();

		// Act
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert - next delegate should be called directly
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => _nextDelegate(_message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowInputValidationExceptionWhenMissingCorrelationId()
	{
		// Arrange - require correlation ID but don't provide one
		var options = CreatePermissiveOptions();
		options.RequireCorrelationId = true;
		var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(null as string);
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act & Assert
		await Should.ThrowAsync<InputValidationException>(async () =>
			await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowInputValidationExceptionWhenMissingMessageId()
	{
		// Arrange
		var options = CreatePermissiveOptions();
		var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(null as string);
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act & Assert
		await Should.ThrowAsync<InputValidationException>(async () =>
			await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowInputValidationExceptionWhenTimestampTooOld()
	{
		// Arrange
		var options = CreatePermissiveOptions();
		options.MaxMessageAgeDays = 1;
		var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow.AddDays(-5));
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow.AddDays(-5));
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act & Assert
		await Should.ThrowAsync<InputValidationException>(async () =>
			await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowInputValidationExceptionWhenTimestampInFuture()
	{
		// Arrange
		var options = CreatePermissiveOptions();
		var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		// More than 5 minutes in the future
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow.AddMinutes(10));
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow.AddMinutes(10));
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act & Assert
		await Should.ThrowAsync<InputValidationException>(async () =>
			await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task PropagateCustomValidatorErrors()
	{
		// Arrange
		var validator = A.Fake<IInputValidator>();
		A.CallTo(() => validator.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
			.Returns(Task.FromResult(InputValidationResult.Failure("Custom error 1", "Custom error 2")));

		var options = CreatePermissiveOptions();
		var sut = new InputValidationMiddleware(_logger, options, [validator], _securityEventLogger);
		var context = CreateValidContext();

		// Act & Assert
		await Should.ThrowAsync<InputValidationException>(async () =>
			await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task HandleCustomValidatorExceptionWhenFailOnExceptionEnabled()
	{
		// Arrange
		var validator = A.Fake<IInputValidator>();
		A.CallTo(() => validator.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
			.Throws(new InvalidOperationException("Validator blew up"));

		var options = CreatePermissiveOptions();
		options.FailOnValidatorException = true;
		var sut = new InputValidationMiddleware(_logger, options, [validator], _securityEventLogger);
		var context = CreateValidContext();

		// Act & Assert
		await Should.ThrowAsync<InputValidationException>(async () =>
			await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task ContinueWhenCustomValidatorThrowsAndFailOnExceptionDisabled()
	{
		// Arrange
		var validator = A.Fake<IInputValidator>();
		A.CallTo(() => validator.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
			.Throws(new InvalidOperationException("Validator blew up"));

		var options = CreatePermissiveOptions();
		options.FailOnValidatorException = false;
		var sut = new InputValidationMiddleware(_logger, options, [validator], _securityEventLogger);
		var context = CreateValidContext();

		// Act - should proceed despite validator exception
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task SetValidationPassedInContextItemsOnSuccess()
	{
		// Arrange
		var options = CreatePermissiveOptions();
		var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		items.ShouldContainKey("Validation:Passed");
		items["Validation:Passed"].ShouldBe(true);
		items.ShouldContainKey("Validation:Timestamp");
	}

	[Fact]
	public async Task LogSecurityEventOnValidationFailure()
	{
		// Arrange - force a validation failure (missing message ID)
		var options = CreatePermissiveOptions();
		var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(null as string);
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act
		try
		{
			await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);
		}
		catch (InputValidationException)
		{
			// Expected
		}

		// Assert - security event should have been logged
		A.CallTo(() => _securityEventLogger.LogSecurityEventAsync(
			SecurityEventType.ValidationFailure,
			A<string>._,
			A<SecuritySeverity>._,
			A<CancellationToken>._,
			A<IMessageContext?>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RunMultipleCustomValidatorsInSequence()
	{
		// Arrange
		var validator1 = A.Fake<IInputValidator>();
		var validator2 = A.Fake<IInputValidator>();
		A.CallTo(() => validator1.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
			.Returns(Task.FromResult(InputValidationResult.Success()));
		A.CallTo(() => validator2.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
			.Returns(Task.FromResult(InputValidationResult.Success()));

		var options = CreatePermissiveOptions();
		var sut = new InputValidationMiddleware(_logger, options, [validator1, validator2], _securityEventLogger);
		var context = CreateValidContext();

		// Act
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert - both validators should be called
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => validator1.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => validator2.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ValidateInvalidUserIdFormat()
	{
		// Arrange
		var options = CreatePermissiveOptions();
		var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);

		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["User:MessageId"] = "invalid user id with spaces!!!",
		};
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.Items).Returns(items);

		// Act & Assert - invalid user ID should cause validation failure
		await Should.ThrowAsync<InputValidationException>(async () =>
			await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task AcceptValidGuidUserId()
	{
		// Arrange
		var options = CreatePermissiveOptions();
		var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);

		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["User:MessageId"] = Guid.NewGuid().ToString(),
		};
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task AcceptValidAlphanumericUserId()
	{
		// Arrange
		var options = CreatePermissiveOptions();
		var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);

		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["User:MessageId"] = "user12345",
		};
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}
}
