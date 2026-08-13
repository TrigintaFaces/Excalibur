// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware.Validation;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

// System.ComponentModel.DataAnnotations (imported above, for the attribute-validation fixtures) and
// Excalibur.Dispatch.Validation both declare a ValidationResult; Excalibur.Dispatch.Validation ALSO
// declares a ValidationOptions distinct from Excalibur.Dispatch.Options.Middleware.ValidationOptions
// used to configure the middleware under test. Alias every needed Validation-namespace type
// individually rather than wildcard-importing the namespace -- exactly what ValidationMiddleware.cs
// itself does, for the same reason.
using ValidationException = Excalibur.Dispatch.Exceptions.ValidationException;
using IValidatorResolver = Excalibur.Dispatch.Validation.IValidatorResolver;
using IValidationResult = Excalibur.Dispatch.Validation.IValidationResult;
using NoOpValidatorResolver = Excalibur.Dispatch.Validation.NoOpValidatorResolver;
using IValidate = Excalibur.Dispatch.Validation.IValidate;
using AbstractionsValidationResult = Excalibur.Dispatch.Validation.ValidationResult;
using AbstractionsValidationError = Excalibur.Dispatch.Validation.ValidationError;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Locks the accumulate-don't-short-circuit validation pipeline: every source (registered validator,
/// self-validation, Data Annotations, <see cref="IMessageValidationService"/>) contributes its errors
/// independently, the per-property error keying <see cref="Exceptions.ValidationException"/> exposes,
/// and the 400 status code mapping that lets ASP.NET Core render a validation failure correctly instead
/// of falling back to 500.
/// </summary>
/// <remarks>
/// Every safety assertion here (a source's errors surface) is paired with a liveness twin (a passing
/// source does not block dispatch) per the project's test-quality rule: a guard asserted only on its
/// safety half is satisfied by a validator that rejects everything, or one that does nothing at all.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Middleware)]
public sealed class ValidationMiddlewareValidationSourcesShould
{
	private readonly ILogger<ValidationMiddleware> _logger;
	private readonly IMessageValidationService _validationService;
	private readonly StubValidatorResolver _resolver;
	private readonly IMessageContext _context;
	private readonly DispatchRequestDelegate _successDelegate;

	public ValidationMiddlewareValidationSourcesShould()
	{
		_logger = A.Fake<ILogger<ValidationMiddleware>>();
		_validationService = A.Fake<IMessageValidationService>();

		// A hand-written stub, not A.Fake<IValidatorResolver>(): IValidationResult declares a static
		// abstract member (Failed), and FakeItEasy's generic Returns<T>(T) cannot use that interface as
		// a type argument (CS8920 -- no most-specific implementation to dispatch the static member
		// through). A plain settable property sidesteps the generic entirely.
		_resolver = new StubValidatorResolver();
		_context = A.Fake<IMessageContext>();

		_ = A.CallTo(() => _context.MessageId).Returns("test-message-id");
		var contextItems = new Dictionary<string, object>();
		_ = A.CallTo(() => _context.Items).Returns(contextItems);
		_ = A.CallTo(() => _context.Features).Returns(new Dictionary<Type, object>());

		// Defaults so a test that doesn't care about a given source doesn't have to stub it: no
		// registered validator claims the message (matches NoOpValidatorResolver's own contract -- the
		// stub's Result starts null), and the pluggable IMessageValidationService reports success.
		_ = A.CallTo(() => _validationService.ValidateAsync(
				A<IDispatchMessage>._, A<MessageValidationContext>._, A<CancellationToken>._))
			.Returns(MessageValidationResult.Success());

		_successDelegate = (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	private ValidationMiddleware CreateSut(ValidationOptions options) =>
		new(MsOptions.Create(options), _validationService, _resolver, _logger);

	#region Anti-Short-Circuit: Every Source Accumulates, None Suppresses Another

	[Fact]
	public async Task AccumulateErrors_FromResolverAndDataAnnotations_WhenBothFail()
	{
		// Arrange
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = true, UseCustomValidation = true };
		_resolver.Result = SerializableValidationResult.Failed(
			new AbstractionsValidationError("ResolverField", "Resolver rejected the message"));
		var middleware = CreateSut(options);
		var message = new RequiredNameMessage(); // Name is null -> fails [Required]

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert -- BOTH sources' errors are present. If the resolver source short-circuited (an
		// `else`/early-return introduced between sources 1 and 3), the "Name" key would be missing.
		exception.ValidationErrors.ShouldContainKey("ResolverField");
		exception.ValidationErrors.ShouldContainKey("Name");
	}

	[Fact]
	public async Task ContinueDispatch_WhenResolverAndAnnotationsBothPass()
	{
		// Arrange -- liveness twin: a passing resolver + passing annotations must not block dispatch.
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = true, UseCustomValidation = true };
		_resolver.Result = SerializableValidationResult.Success();
		var middleware = CreateSut(options);
		var message = new ValidNameMessage();

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region IValidate: Self-Validating Messages Are Reachable and Accumulate

	[Fact]
	public async Task SurfaceErrors_FromSelfValidatingMessage_WhenIValidateFails()
	{
		// Arrange
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		var middleware = CreateSut(options);
		var message = new SelfValidatingMessage { ShouldFail = true };

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert
		exception.ValidationErrors.ShouldContainKey("SelfField");
	}

	[Fact]
	public async Task ContinueDispatch_WhenSelfValidatingMessagePasses()
	{
		// Arrange -- liveness twin: a self-validating message that reports IsValid must not block dispatch.
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		var middleware = CreateSut(options);
		var message = new SelfValidatingMessage { ShouldFail = false };

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task AccumulateErrors_FromIValidateAndDataAnnotations_WhenBothFail()
	{
		// Arrange
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = true, UseCustomValidation = true };
		var middleware = CreateSut(options);
		var message = new SelfValidatingAnnotatedMessage(); // Name is null -> fails [Required]; Validate() always fails

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert -- IValidate's error accumulates alongside DataAnnotations' error, not instead of it.
		exception.ValidationErrors.ShouldContainKey("SelfField");
		exception.ValidationErrors.ShouldContainKey("Name");
	}

	#endregion

	#region ValidationErrors Keying

	[Fact]
	public async Task KeyValidationErrors_ByPropertyName()
	{
		// Arrange
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = true, UseCustomValidation = false };
		var middleware = CreateSut(options);
		var message = new RequiredNameMessage();

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert
		exception.ValidationErrors.ShouldContainKey("Name");
		exception.ValidationErrors["Name"].Length.ShouldBe(1);
	}

	[Fact]
	public async Task CombineErrors_ForSameProperty_IntoSingleArrayEntry()
	{
		// Arrange -- two errors on the SAME property, from the same source, must land under one key as
		// a 2-element array (AddError appends to an existing key rather than overwriting it).
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		_ = A.CallTo(() => _validationService.ValidateAsync(
				A<IDispatchMessage>._, A<MessageValidationContext>._, A<CancellationToken>._))
			.Returns(MessageValidationResult.Failure(
				new ValidationError("Field1", "Error A"),
				new ValidationError("Field1", "Error B")));
		var middleware = CreateSut(options);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert
		exception.ValidationErrors.ShouldContainKey("Field1");
		exception.ValidationErrors["Field1"].ShouldBe(["Error A", "Error B"]);
	}

	[Fact]
	public async Task SurfaceResolverError_UnderEmptyKey_WhenPropertyNameIsNull()
	{
		// Arrange -- a resolver error with no property name (message-level, not field-level) must still
		// be surfaced, keyed under string.Empty, not silently dropped.
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		_resolver.Result = SerializableValidationResult.Failed(new AbstractionsValidationError("Message-level failure"));
		var middleware = CreateSut(options);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert
		exception.ValidationErrors.ShouldContainKey(string.Empty);
		exception.ValidationErrors[string.Empty].ShouldContain("Message-level failure");
	}

	#endregion

	#region Status Code Mapping

	[Fact]
	public async Task SetDispatchStatusCode400_WhenValidationFails()
	{
		// Arrange -- this is what lets ASP.NET Core map a validation failure to 400 instead of falling
		// back to 500 (the old behavior, when the thrown type bound to the DataAnnotations exception and
		// GetStatusCode() had nothing to report).
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = true, UseCustomValidation = false };
		var middleware = CreateSut(options);
		var message = new RequiredNameMessage();

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert
		exception.DispatchStatusCode.ShouldBe(400);
	}

	#endregion

	#region Default Wiring Is Inert, Not Broken

	[Fact]
	public async Task DispatchSuccessfully_WithNoOpValidatorResolverAndNoAnnotations()
	{
		// Arrange -- NoOpValidatorResolver is the framework default plugged in by every preset that
		// doesn't call AddDispatchValidation(). A valid message with no configured validators must still
		// dispatch: the default wiring must be inert, not accidentally rejecting.
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		var middleware = new ValidationMiddleware(
			MsOptions.Create(options), _validationService, new NoOpValidatorResolver(), _logger);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Fail-Closed Guard: A Source Reporting Invalid With No Error Detail Must Not Let The Message Through

	[Fact]
	public async Task RejectDispatch_WhenResolverReportsInvalidWithNoErrorDetail()
	{
		// Arrange -- IValidatorResolver is a public extension point; a third-party implementation can
		// report IsValid:false with an empty Errors collection. Dropping it silently would fail OPEN: a
		// validator rejected the message and the pipeline would dispatch it anyway.
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		_resolver.Result = SerializableValidationResult.Failed(); // IsValid: false, Errors: []
		var middleware = CreateSut(options);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert -- the substituted placeholder lands under the empty-string key, same convention as a
		// null/empty property name elsewhere in this middleware.
		exception.ValidationErrors.ShouldContainKey(string.Empty);
		exception.ValidationErrors[string.Empty].Length.ShouldBe(1);
	}

	[Fact]
	public async Task ContinueDispatch_WhenResolverReportsValidWithNoErrors()
	{
		// Arrange -- liveness twin: a resolver that reports success with an empty Errors collection (the
		// ordinary case) must not be caught by the fail-closed guard.
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		_resolver.Result = SerializableValidationResult.Success();
		var middleware = CreateSut(options);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectDispatch_WhenIValidateReportsInvalidWithNoErrorDetail()
	{
		// Arrange -- same guard, the IValidate branch. A third-party self-validating message can return
		// IsValid:false with no errors just as easily as a resolver can.
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		var middleware = CreateSut(options);
		var message = new SelfValidatingMessageWithNoDetail();

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert
		exception.ValidationErrors.ShouldContainKey(string.Empty);
		exception.ValidationErrors[string.Empty].Length.ShouldBe(1);
	}

	[Fact]
	public async Task NotAppendPlaceholder_WhenResolverReportsInvalidWithErrors()
	{
		// Arrange -- the guard must not over-fire: a source that DOES supply error detail gets exactly
		// those errors, never an extra placeholder. Asserting the count means a future change that always
		// appends (rather than only substituting when the mapped set is empty) goes RED here.
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		_resolver.Result = SerializableValidationResult.Failed(
			new AbstractionsValidationError("Field1", "Explicit error"));
		var middleware = CreateSut(options);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert
		exception.ValidationErrors.Count.ShouldBe(1);
		exception.ValidationErrors.ShouldContainKey("Field1");
		exception.ValidationErrors["Field1"][0].ShouldBe("Explicit error");
	}

	#endregion

	#region IValidate XML-Doc Example (Compiler-Checked)

	[Fact]
	public async Task DocExampleImplementation_ReportsError_WhenAmountIsNotPositive()
	{
		// Arrange
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		var middleware = CreateSut(options);
		var message = new DocExampleOrderCommand(Amount: -1, Currency: "USD");

		// Act
		var exception = await Should.ThrowAsync<ValidationException>(
			middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None).AsTask());

		// Assert -- the doc's error has no property name (a message-level ValidationResult.Failure(string)
		// call), so it lands under the empty-string key, same convention as elsewhere in this middleware.
		exception.ValidationErrors.ShouldContainKey(string.Empty);
		exception.ValidationErrors[string.Empty].ShouldContain("Amount must be greater than zero");
	}

	[Fact]
	public async Task DocExampleImplementation_DispatchesSuccessfully_WhenValid()
	{
		// Arrange -- liveness twin: the doc's own "valid" path must round-trip too, not just its failure path.
		var options = new ValidationOptions { Enabled = true, UseDataAnnotations = false, UseCustomValidation = true };
		var middleware = CreateSut(options);
		var message = new DocExampleOrderCommand(Amount: 10, Currency: "USD");

		// Act
		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	/// <summary>
	/// Implements <see cref="IValidate"/> exactly as the corrected <c>&lt;example&gt;</c> block on the
	/// interface's XML doc shows -- same shape (a record implementing <c>IDispatchAction</c> and
	/// <c>IValidate</c>), same calls (<c>ValidationResult.Failure(...)</c> / <c>ValidationResult.Success()</c>).
	/// The original doc called <c>SerializableValidationResult.Failed(...)</c> where <see cref="IValidate.Validate"/>
	/// actually returns <c>ValidationResult</c> -- it never compiled, and it ships in the public NuGet
	/// .xml. This fixture makes that mismatch a compile error here instead of un-compiled prose.
	/// </summary>
	private sealed record DocExampleOrderCommand(decimal Amount, string Currency) : IDispatchAction, IValidate
	{
		public AbstractionsValidationResult Validate()
		{
			if (Amount <= 0)
			{
				return AbstractionsValidationResult.Failure("Amount must be greater than zero");
			}

			if (string.IsNullOrWhiteSpace(Currency))
			{
				return AbstractionsValidationResult.Failure("Currency is required");
			}

			return AbstractionsValidationResult.Success();
		}
	}

	#endregion

	#region Test Fixtures

	/// <summary>
	/// A hand-written <see cref="IValidatorResolver"/> test double. Not a FakeItEasy fake: FakeItEasy's
	/// generic <c>Returns&lt;T&gt;(T)</c> cannot use <see cref="IValidationResult"/> as a type argument
	/// (it declares a static abstract member, and the interface itself has no most-specific
	/// implementation to dispatch it through -- CS8920). A settable property has no such restriction.
	/// </summary>
	private sealed class StubValidatorResolver : IValidatorResolver
	{
		public IValidationResult? Result { get; set; }

		public IValidationResult? TryValidate(IDispatchMessage message) => Result;
	}

	private sealed class RequiredNameMessage : IDispatchMessage
	{
		[Required]
		public string Name { get; set; } = null!;
	}

	private sealed class ValidNameMessage : IDispatchMessage
	{
		[Required]
		public string Name { get; set; } = "valid";
	}

	private sealed class SelfValidatingMessage : IDispatchMessage, IValidate
	{
		public bool ShouldFail { get; init; }

		public AbstractionsValidationResult Validate() =>
			ShouldFail
				? AbstractionsValidationResult.Failure(new AbstractionsValidationError("SelfField", "Self validation failed"))
				: AbstractionsValidationResult.Success();
	}

	private sealed class SelfValidatingAnnotatedMessage : IDispatchMessage, IValidate
	{
		[Required]
		public string Name { get; set; } = null!;

		public AbstractionsValidationResult Validate() =>
			AbstractionsValidationResult.Failure(new AbstractionsValidationError("SelfField", "Self validation failed"));
	}

	/// <summary>
	/// Reports IsValid:false while supplying no error detail -- exactly the combination the fail-closed
	/// guard exists to catch.
	/// </summary>
	private sealed class SelfValidatingMessageWithNoDetail : IDispatchMessage, IValidate
	{
		public AbstractionsValidationResult Validate() =>
			AbstractionsValidationResult.Failure(Array.Empty<AbstractionsValidationError>()); // IsValid: false, Errors: []
	}

	#endregion
}
