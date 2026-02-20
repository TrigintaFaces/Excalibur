// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;
using AuthorizationResult = Excalibur.Dispatch.Middleware.AuthorizationResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="AuthorizationMiddleware"/> class.
/// </summary>
/// <remarks>
/// Sprint 414 - Task T414.6: AuthorizationMiddleware tests (0% → 50%+).
/// Tests authorization middleware implementation including role-based and policy-based authorization.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class AuthorizationMiddlewareShould
{
	private readonly ILogger<AuthorizationMiddleware> _logger;
	private readonly IAuthorizationService _authorizationService;
	private readonly IMessageContext _context;
	private readonly DispatchRequestDelegate _successDelegate;

	public AuthorizationMiddlewareShould()
	{
		_logger = A.Fake<ILogger<AuthorizationMiddleware>>();
		_authorizationService = A.Fake<IAuthorizationService>();
		_context = A.Fake<IMessageContext>();

		_ = A.CallTo(() => _context.MessageId).Returns("test-message-id");

		// Configure logger to return a disposable scope (required by middleware)
		_ = A.CallTo(() => _logger.BeginScope(A<Dictionary<string, object>>._))
			.Returns(A.Fake<IDisposable>());
		_ = A.CallTo(() => _logger.BeginScope(A<object>._))
			.Returns(A.Fake<IDisposable>());

		// Configure context.GetItem to return null by default (prevents FakeItEasy from creating fake objects)
		_ = A.CallTo(() => _context.GetItem<object>(A<string>._)).Returns(null);

		_successDelegate = (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new AuthorizationMiddleware(null!, _authorizationService, NullTelemetrySanitizer.Instance, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenAuthorizationServiceIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new AuthorizationMiddleware(options, null!, NullTelemetrySanitizer.Instance, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, null!));
	}

	#endregion

	#region Stage Tests

	[Fact]
	public void HaveAuthorizationStage()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions());
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Authorization);
	}

	[Fact]
	public void HaveActionApplicableMessageKinds()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions());
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions());
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions());
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions());
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, _context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Disabled Authorization Tests

	[Fact]
	public async Task PassThroughDirectly_WhenDisabled()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions { Enabled = false });
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	#endregion

	#region Authorization Service Tests

	[Fact]
	public async Task CallAuthorizationService_WhenEnabled()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions
		{
			Enabled = true,
			AllowAnonymousAccess = false
		});
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();

		// Setup a subject to avoid anonymous check
		_ = A.CallTo(() => _context.GetItem<object>("UserId")).Returns("user-123");

		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._))
			.Returns(AuthorizationResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowUnauthorizedAccessException_WhenAuthorizationFails()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions
		{
			Enabled = true,
			AllowAnonymousAccess = false
		});
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _context.GetItem<object>("UserId")).Returns("user-123");

		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._))
			.Returns(AuthorizationResult.Failure("Access denied"));

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	#endregion

	#region Anonymous Access Tests

	[Fact]
	public async Task AllowAnonymousAccess_WhenConfiguredAndNoSubject()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions
		{
			Enabled = true,
			AllowAnonymousAccess = true
		});
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		// Use concrete message type instead of fake - fakes can have unexpected behavior with GetType()
		var message = new TestActionMessage();

		// No user/subject set - GetItem returns null by default (configured in constructor)

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		// Authorization service should not be called when anonymous access is allowed and no subject
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowUnauthorizedAccessException_WhenNoSubjectAndAnonymousNotAllowed()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions
		{
			Enabled = true,
			AllowAnonymousAccess = false
		});
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		// Use concrete message type instead of fake
		var message = new TestActionMessage();

		// No user/subject set - all GetItem calls return null

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	#endregion

	#region Bypass Authorization Tests

	[Fact]
	public async Task BypassAuthorization_WhenMessageTypeIsInBypassList()
	{
		// Arrange
		var message = new TestActionMessage();
		var options = MsOptions.Create(new AuthorizationOptions
		{
			Enabled = true,
			BypassAuthorizationForTypes = [nameof(TestActionMessage)]
		});
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);

		// No user/subject set but should still pass due to bypass

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task BypassAuthorization_WhenMessageHasAllowAnonymousAttribute()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions
		{
			Enabled = true,
			AllowAnonymousAccess = false
		});
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		var message = new AllowAnonymousTestMessage();

		// No user/subject set but should still pass due to attribute

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	#endregion

	#region Context Extraction Tests

	[Fact]
	public async Task ExtractSubjectIdFromUserId()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions
		{
			Enabled = true
		});
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _context.GetItem<object>("UserId")).Returns("user-from-userid");

		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._))
			.Returns(AuthorizationResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert - Authorization service was called (subject was extracted)
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExtractSubjectIdFromSubjectId_WhenUserIdNotPresent()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions
		{
			Enabled = true
		});
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _context.GetItem<object>("UserId")).Returns(null);
		_ = A.CallTo(() => _context.GetItem<object>("SubjectId")).Returns("subject-id-value");

		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._))
			.Returns(AuthorizationResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert - Authorization service was called (subject was extracted)
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExtractSubjectIdFromServiceId_WhenOthersNotPresent()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions
		{
			Enabled = true
		});
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _context.GetItem<object>("UserId")).Returns(null);
		_ = A.CallTo(() => _context.GetItem<object>("SubjectId")).Returns(null);
		_ = A.CallTo(() => _context.GetItem<object>("ServiceId")).Returns("service-id-value");

		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._))
			.Returns(AuthorizationResult.Success());

		// Act
		_ = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert - Authorization service was called (subject was extracted)
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Exception Handling Tests

	[Fact]
	public async Task RethrowNonUnauthorizedAccessExceptions()
	{
		// Arrange
		var options = MsOptions.Create(new AuthorizationOptions
		{
			Enabled = true
		});
		var middleware = new AuthorizationMiddleware(options, _authorizationService, NullTelemetrySanitizer.Instance, _logger);
		var message = A.Fake<IDispatchMessage>();

		_ = A.CallTo(() => _context.GetItem<object>("UserId")).Returns("user-123");

		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<object>._,
			A<CancellationToken>._))
			.Throws(new InvalidOperationException("Service error"));

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	#endregion

	#region Test Fixtures

	/// <summary>
	/// Test action message for authorization tests.
	/// </summary>
	private sealed class TestActionMessage : IDispatchMessage;

	/// <summary>
	/// Test message with AllowAnonymous attribute.
	/// </summary>
	[AllowAnonymous]
	private sealed class AllowAnonymousTestMessage : IDispatchMessage;

	#endregion
}
