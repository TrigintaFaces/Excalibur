// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Validation;

/// <summary>
/// pfb7s4 engage-tests (author≠impl): the schema-validation seam in <see cref="DefaultValidationService"/>
/// MUST be wired — never a silent no-op when <see cref="ValidationOptions.ValidateSchemas"/> is enabled.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DefaultValidationServiceSchemaEngageShould
{
	/// <summary>
	/// Fail-loud backstop: when schema validation is enabled but NO <see cref="IMessageSchemaValidator"/>
	/// is registered, validation MUST surface an error rather than silently pass.
	/// </summary>
	/// <remarks>
	/// Structural RED argument: pre-fix, <c>ValidateSchemasAsync</c> was <c>await ValueTask.CompletedTask</c>
	/// (no-op) so the result was <c>Success</c>. Reverting the null-validator branch to a silent return
	/// (no error added) makes <c>IsValid</c> true again — RED.
	/// </remarks>
	[Fact]
	public async Task SurfaceError_WhenSchemaValidationEnabled_ButNoValidatorRegistered()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ValidationOptions { Enabled = true, ValidateSchemas = true });
		var sut = new DefaultValidationService(options, NullLogger<DefaultValidationService>.Instance);
		var context = new MessageValidationContext("msg-pfb7s4-1", typeof(SchemaEngageMessage));

		// Act
		var result = await sut.ValidateAsync(new SchemaEngageMessage(), context, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Schema validation is enabled", StringComparison.Ordinal));
	}

	/// <summary>
	/// Positive wiring: when a schema validator IS registered, the service MUST engage it (delegate to
	/// its <c>ValidateAsync</c>) and surface the violations it reports.
	/// </summary>
	/// <remarks>
	/// Structural RED argument: pre-fix the schema branch never called any validator, so a violation
	/// reported by the registered validator would not appear and the validator would never be invoked.
	/// </remarks>
	[Fact]
	public async Task EngageRegisteredSchemaValidator_AndSurfaceItsViolations()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ValidationOptions { Enabled = true, ValidateSchemas = true });
		var validator = A.Fake<IMessageSchemaValidator>();
		A.CallTo(() => validator.ValidateAsync(
				A<object>._, A<MessageValidationContext>._, A<ICollection<ValidationError>>._, A<CancellationToken>._))
			.Invokes((object _, MessageValidationContext _, ICollection<ValidationError> errors, CancellationToken _) =>
				errors.Add(new ValidationError("schema", "schema-violation")));

		var sut = new DefaultValidationService(options, NullLogger<DefaultValidationService>.Instance, validator);
		var context = new MessageValidationContext("msg-pfb7s4-2", typeof(SchemaEngageMessage));

		// Act
		var result = await sut.ValidateAsync(new SchemaEngageMessage(), context, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message == "schema-violation");
		A.CallTo(() => validator.ValidateAsync(
				A<object>._, A<MessageValidationContext>._, A<ICollection<ValidationError>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private sealed class SchemaEngageMessage;
}
