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
public sealed class DataAnnotationsValidatorResolver : IValidatorResolver
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

		var errors = results
			.Where(r => r.ErrorMessage != null)
			.Select(r => (object)new ValidationError(
				string.Join(", ", r.MemberNames),
				r.ErrorMessage))
			.ToArray();

		return SerializableValidationResult.Failed(errors);
	}
}
