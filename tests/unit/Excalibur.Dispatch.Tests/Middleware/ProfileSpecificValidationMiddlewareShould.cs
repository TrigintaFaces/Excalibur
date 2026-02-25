// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="ProfileSpecificValidationMiddleware"/> verifying profile-specific
/// validation rules, validation levels, and field constraint enforcement.
/// Sprint 560 (S560.45).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class ProfileSpecificValidationMiddlewareShould : UnitTestBase
{
	private readonly IValidatorResolver _resolver;
	private readonly ProfileValidationOptions _options;
	private readonly TestMessageContext _context;

	public ProfileSpecificValidationMiddlewareShould()
	{
		_resolver = new NullValidatorResolver();
		_options = new ProfileValidationOptions
		{
			DefaultProfile = "default",
			DefaultValidationLevel = ValidationLevel.Standard,
		};
		_context = new TestMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "TestMessage",
		};
	}

	[Fact]
	public async Task PassThroughWhenNoProfileRulesExist()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new TestValidatableMessage { Name = "Valid" };
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task SkipValidationForNoneLevel()
	{
		// Arrange
		var rules = CreateProfileRules("test-profile", ValidationLevel.None);
		var middleware = CreateMiddleware(rules);
		_context.Items["DispatchProfile"] = "test-profile";

		var message = A.Fake<IDispatchMessage>();
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task UseDefaultProfileWhenNoProfileInContext()
	{
		// Arrange — no DispatchProfile in context, should use default
		var middleware = CreateMiddleware();
		var message = new TestValidatableMessage { Name = "Valid" };
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectMessageWhenRequiredFieldIsMissing()
	{
		// Arrange
		var rules = CreateProfileRules("strict-profile", ValidationLevel.Basic, requiredFields: ["Name"]);
		var middleware = CreateMiddleware(rules);
		_context.Items["DispatchProfile"] = "strict-profile";

		var message = new TestValidatableMessage { Name = null };
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe(ProblemDetailsTypes.Validation);
	}

	[Fact]
	public async Task EnforceFieldConstraints()
	{
		// Arrange
		var constraint = A.Fake<IFieldConstraint>();
		A.CallTo(() => constraint.IsSatisfied(A<IDispatchMessage>._)).Returns(false);
		A.CallTo(() => constraint.ErrorMessage).Returns("Field constraint violated");
		A.CallTo(() => constraint.FieldName).Returns("Name");

		var rules = CreateProfileRules("constrained", ValidationLevel.Basic, fieldConstraints: [constraint]);
		var middleware = CreateMiddleware(rules);
		_context.Items["DispatchProfile"] = "constrained";

		var message = new TestValidatableMessage { Name = "Value" };
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void SetStageToValidation()
	{
		var middleware = CreateMiddleware();
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Validation);
	}

	[Fact]
	public void AcceptAllMessageKinds()
	{
		var middleware = CreateMiddleware();
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
	}

	private ProfileSpecificValidationMiddleware CreateMiddleware(params IProfileValidationRules[] rules)
	{
		return new ProfileSpecificValidationMiddleware(
			_resolver,
			Microsoft.Extensions.Options.Options.Create(_options),
			rules,
			NullLogger<ProfileSpecificValidationMiddleware>.Instance);
	}

	private static IProfileValidationRules CreateProfileRules(
		string profileName,
		ValidationLevel level,
		IReadOnlyList<string>? requiredFields = null,
		IReadOnlyList<IFieldConstraint>? fieldConstraints = null)
	{
		var rules = A.Fake<IProfileValidationRules>();
		A.CallTo(() => rules.ProfileName).Returns(profileName);
		A.CallTo(() => rules.ValidationLevel).Returns(level);
		A.CallTo(() => rules.MaxMessageSize).Returns(1_048_576);
		A.CallTo(() => rules.RequiredFields).Returns(requiredFields ?? Array.Empty<string>());
		A.CallTo(() => rules.CustomValidators).Returns(Array.Empty<ICustomValidator>());
		A.CallTo(() => rules.FieldConstraints).Returns(fieldConstraints ?? Array.Empty<IFieldConstraint>());
		return rules;
	}

	private sealed class TestValidatableMessage : IDispatchMessage
	{
		public string? Name { get; set; }
	}

	private sealed class NullValidatorResolver : IValidatorResolver
	{
		public IValidationResult? TryValidate(IDispatchMessage message) => null;
	}
}
