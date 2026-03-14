// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Validation.DataAnnotations;

/// <summary>
/// Validator resolver using System.ComponentModel.DataAnnotations.
/// Zero external dependencies - uses only BCL types.
/// </summary>
internal sealed class DataAnnotationsValidatorResolver : IValidatorResolver
{
	/// <inheritdoc/>
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
			Justification = "Data annotations validation relies on reflection over message types registered at startup.")]
	public IValidationResult? TryValidate(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var validationContext = new ValidationContext(message);
		var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

		var isValid = Validator.TryValidateObject(
			message,
			validationContext,
			results,
			validateAllProperties: true);

		if (isValid)
		{
			return null; // No validation errors - pass through
		}

		var mappedErrors = new object[results.Count];
		var mappedCount = 0;
		for (var i = 0; i < results.Count; i++)
		{
			var result = results[i];
			if (result.ErrorMessage == null)
			{
				continue;
			}

			mappedErrors[mappedCount++] = new ValidationError(
				string.Join(", ", result.MemberNames),
				result.ErrorMessage);
		}

		if (mappedCount == mappedErrors.Length)
		{
			return SerializableValidationResult.Failed(mappedErrors);
		}

		var trimmedErrors = new object[mappedCount];
		Array.Copy(mappedErrors, trimmedErrors, mappedCount);
		return SerializableValidationResult.Failed(trimmedErrors);
	}
}
