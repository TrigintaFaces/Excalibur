// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using FluentValidation;

using Microsoft.Extensions.Options;

namespace Excalibur.Application;

/// <summary>
/// Provides extension methods for validating options using FluentValidation.
/// </summary>
public static class OptionsBuilderExtensions
{
	/// <summary>
	/// Adds validation to the options builder using a specified validator type.
	/// </summary>
	/// <typeparam name="TOptions"> The type of options being validated. </typeparam>
	/// <typeparam name="TValidator"> The type of the validator to use for validation. </typeparam>
	/// <param name="builder"> The options builder to which validation is added. </param>
	/// <exception cref="ValidationException"> Thrown when the validation of the options instance fails. </exception>
	/// <remarks>
	/// This extension integrates FluentValidation into the options validation process. It supports rule sets if the validator implements <see cref="IDetermineValidationRuleSetByConfig{T}" />.
	/// </remarks>
	public static void Validate<TOptions, TValidator>(this OptionsBuilder<TOptions> builder)
		where TOptions : class, new()
		where TValidator : IValidator<TOptions>, new()
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Validate(ValidateOptionsInstance);

		bool ValidateOptionsInstance(TOptions optionsInstance)
		{
			// Create an instance of the validator
			var validator = new TValidator();

			// Check if the validator supports rule set determination
			var ruleSetDiscriminator = validator as IDetermineValidationRuleSetByConfig<TOptions>;

			// Perform validation
			var result = validator.Validate(
				optionsInstance,
				options => _ = options.IncludeRuleSets(ruleSetDiscriminator?.WhichRuleSets(optionsInstance)));

			// If valid, return true
			if (result.IsValid)
			{
				return true;
			}

			// If validation fails, construct an error message
			var errors = result
				.Errors
				.Select(x => Environment.NewLine + " -- " + x.PropertyName + ": " + x.ErrorMessage);

			var message = $"Validation failed for {typeof(TOptions).FullName}: {string.Concat(errors)}";

			// Throw a validation exception with the errors
			throw new ValidationException(message, result.Errors);
		}
	}
}
