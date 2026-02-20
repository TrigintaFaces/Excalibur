// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Exception thrown when validation errors occur.
/// </summary>
[Serializable]
public sealed class ValidationException : DispatchException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException" /> class.
	/// </summary>
	public ValidationException()
		: base(ErrorCodes.ValidationFailed, ErrorMessages.ValidationFailed)
	{
		DispatchStatusCode = 400;
		ValidationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> The error message that explains the reason for the exception. </param>
	public ValidationException(string message)
		: base(ErrorCodes.ValidationFailed, message)
	{
		DispatchStatusCode = 400;
		ValidationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException" /> class with validation errors.
	/// </summary>
	/// <param name="errors"> The validation errors. </param>
	public ValidationException(IDictionary<string, string[]> errors)
		: base(ErrorCodes.ValidationFailed, FormatValidationMessage(errors))
	{
		DispatchStatusCode = 400;
		ValidationErrors = new Dictionary<string, string[]>(errors, StringComparer.Ordinal);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException" /> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public ValidationException(string message, Exception? innerException) : base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException" /> class with an explicit error code.
	/// </summary>
	/// <param name="errorCode">The error code to associate with the exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public ValidationException(string errorCode, string message) : base(errorCode, message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException" /> class with an explicit error code and inner exception.
	/// </summary>
	/// <param name="errorCode">The error code to associate with the exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public ValidationException(string errorCode, string message, Exception? innerException) : base(errorCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets the validation errors by field name.
	/// </summary>
	/// <value>The current <see cref="ValidationErrors"/> value.</value>
	public IDictionary<string, string[]> ValidationErrors { get; }

	/// <summary>
	/// Creates a validation exception for a required field.
	/// </summary>
	/// <param name="fieldName"> The name of the required field. </param>
	/// <returns> A new ValidationException instance. </returns>
	public static ValidationException RequiredField(string fieldName)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal) { [fieldName] = [$"The {fieldName} field is required."] };

		var ex = new ValidationException(errors) { Data = { ["ErrorCode"] = ErrorCodes.ValidationRequiredFieldMissing } };
		return ex.WithUserMessage($"Please provide a value for {fieldName}.")
				.WithSuggestedAction($"Ensure the {fieldName} field is filled out correctly.")
			as ValidationException ?? new ValidationException();
	}

	/// <summary>
	/// Creates a validation exception for an invalid format.
	/// </summary>
	/// <param name="fieldName"> The name of the field with invalid format. </param>
	/// <param name="expectedFormat"> The expected format. </param>
	/// <returns> A new ValidationException instance. </returns>
	public static ValidationException InvalidFormat(string fieldName, string expectedFormat)
	{
		var errors = new Dictionary<string, string[]>
(StringComparer.Ordinal)
		{
			[fieldName] = [$"The {fieldName} field has an invalid format. Expected: {expectedFormat}"],
		};

		var ex = new ValidationException(errors) { Data = { ["ErrorCode"] = ErrorCodes.ValidationInvalidFormat } };
		return ex.WithUserMessage($"The {fieldName} field format is incorrect.")
				.WithSuggestedAction($"Please enter {fieldName} in the format: {expectedFormat}")
			as ValidationException ?? new ValidationException();
	}

	/// <summary>
	/// Creates a validation exception for an out-of-range value.
	/// </summary>
	/// <param name="fieldName"> The name of the field with out-of-range value. </param>
	/// <param name="min"> The minimum allowed value. </param>
	/// <param name="max"> The maximum allowed value. </param>
	/// <returns> A new ValidationException instance. </returns>
	public static ValidationException OutOfRange(string fieldName, object min, object max)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal) { [fieldName] = [$"The {fieldName} field must be between {min} and {max}."] };

		var ex = new ValidationException(errors) { Data = { ["ErrorCode"] = ErrorCodes.ValidationOutOfRange } };
		return ex.WithContext("min", min)
				.WithContext("max", max)
				.WithUserMessage($"The value for {fieldName} is out of the acceptable range.")
				.WithSuggestedAction($"Please enter a value between {min} and {max} for {fieldName}.")
			as ValidationException ?? new ValidationException();
	}

	/// <summary>
	/// Adds a validation error for a field.
	/// </summary>
	/// <param name="fieldName"> The field name. </param>
	/// <param name="errorMessage"> The error message. </param>
	/// <returns> The current exception instance for fluent configuration. </returns>
	public ValidationException AddError(string fieldName, string errorMessage)
	{
		if (ValidationErrors.TryGetValue(fieldName, out var existing))
		{
			var list = existing.ToList();
			list.Add(errorMessage);
			ValidationErrors[fieldName] = [.. list];
		}
		else
		{
			ValidationErrors[fieldName] = [errorMessage];
		}

		return this;
	}

	/// <summary>
	/// Converts the exception to a Dispatch-specific problem details representation with validation errors.
	/// </summary>
	/// <returns> A <see cref="DispatchProblemDetails"/> instance including validation errors. </returns>
	public override DispatchProblemDetails ToDispatchProblemDetails()
	{
		var baseDetails = base.ToDispatchProblemDetails();

		if (!ValidationErrors.Any())
		{
			return baseDetails;
		}

		var extensions = new Dictionary<string, object?>(baseDetails.Extensions ?? [], StringComparer.Ordinal) { ["errors"] = ValidationErrors };

		return new DispatchProblemDetails
		{
			Type = baseDetails.Type,
			Title = baseDetails.Title,
			Status = baseDetails.Status,
			Detail = baseDetails.Detail,
			Instance = baseDetails.Instance,
			ErrorCode = baseDetails.ErrorCode,
			Category = baseDetails.Category,
			Severity = baseDetails.Severity,
			CorrelationId = baseDetails.CorrelationId,
			TraceId = baseDetails.TraceId,
			SpanId = baseDetails.SpanId,
			Timestamp = baseDetails.Timestamp,
			SuggestedAction = baseDetails.SuggestedAction,
			Extensions = extensions,
		};
	}

	/// <summary>
	/// Formats validation errors into a readable message.
	/// </summary>
	private static string FormatValidationMessage(IDictionary<string, string[]> errors)
	{
		if (!errors.Any())
		{
			return "Validation failed.";
		}

		var messages = errors.SelectMany(kvp => kvp.Value.Select(v => $"{kvp.Key}: {v}"));
		return $"Validation failed: {string.Join("; ", messages)}";
	}
}
