// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Validation.Context;
using Excalibur.Dispatch.Options.Validation;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="ContextValidationMiddleware"/> verifying context validation rules,
/// strict vs lenient mode, and metrics recording.
/// Sprint 560 (S560.45).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class ContextValidationMiddlewareShould : UnitTestBase
{
	private readonly IContextValidator _validator;
	private readonly ContextValidationOptions _options;
	private readonly IDispatchMessage _message;
	private readonly TestMessageContext _context;

	public ContextValidationMiddlewareShould()
	{
		_validator = A.Fake<IContextValidator>();
		_options = new ContextValidationOptions { Mode = ValidationMode.Strict };
		_message = A.Fake<IDispatchMessage>();
		_context = new TestMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "TestMessage",
		};
	}

	[Fact]
	public async Task ContinuePipelineWhenValidationSucceeds()
	{
		// Arrange
		A.CallTo(() => _validator.ValidateAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<ContextValidationResult>(ContextValidationResult.Success()));

		var middleware = CreateMiddleware();
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectMessageInStrictModeWhenValidationFails()
	{
		// Arrange
		_options.Mode = ValidationMode.Strict;
		A.CallTo(() => _validator.ValidateAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<ContextValidationResult>(
				ContextValidationResult.Failure("Missing required fields")));

		var middleware = CreateMiddleware();
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		nextInvoked.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe(ProblemDetailsTypes.Validation);
	}

	[Fact]
	public async Task ContinuePipelineInLenientModeWhenValidationFails()
	{
		// Arrange
		_options.Mode = ValidationMode.Lenient;
		A.CallTo(() => _validator.ValidateAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<ContextValidationResult>(
				ContextValidationResult.Failure("Missing optional fields")));

		var middleware = CreateMiddleware();
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task RethrowExceptionInStrictModeWhenValidatorThrows()
	{
		// Arrange
		_options.Mode = ValidationMode.Strict;
		A.CallTo(() => _validator.ValidateAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Validator exploded"));

		var middleware = CreateMiddleware();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
				.ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task ContinueInLenientModeWhenValidatorThrows()
	{
		// Arrange
		_options.Mode = ValidationMode.Lenient;
		A.CallTo(() => _validator.ValidateAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Validator exploded"));

		var middleware = CreateMiddleware();
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task MergeResultsFromMultipleValidators()
	{
		// Arrange
		_options.Mode = ValidationMode.Strict;
		A.CallTo(() => _validator.ValidateAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<ContextValidationResult>(ContextValidationResult.Success()));

		var customValidator = A.Fake<IContextValidator>();
		A.CallTo(() => customValidator.ValidateAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<ContextValidationResult>(
				ContextValidationResult.FailureWithFields("Custom check failed", ["FieldA"])));

		var middleware = CreateMiddleware(customValidator);
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void SetStageToPreProcessing()
	{
		var middleware = CreateMiddleware();
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	private ContextValidationMiddleware CreateMiddleware(params IContextValidator[] customValidators)
	{
		return new ContextValidationMiddleware(
			_validator,
			NullLogger<ContextValidationMiddleware>.Instance,
			Microsoft.Extensions.Options.Options.Create(_options),
			customValidators.Length > 0 ? customValidators : null);
	}
}
