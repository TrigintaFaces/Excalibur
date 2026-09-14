// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware.Validation;

/// <summary>
/// Result of message validation.
/// </summary>
/// <remarks> Creates a new message validation result. </remarks>
public sealed class MessageValidationResult(bool isValid, IEnumerable<MessageValidationError> errors)
{
	private static readonly IReadOnlyList<MessageValidationError> EmptyErrors = Array.Empty<MessageValidationError>();

	/// <summary>
	/// Gets a value indicating whether validation passed.
	/// </summary>
	/// <value>The current <see cref="IsValid"/> value.</value>
	public bool IsValid { get; } = isValid;

	/// <summary>
	/// Gets the validation errors, if any.
	/// </summary>
	/// <value>
	/// The validation errors, if any.
	/// </value>
	public IReadOnlyList<MessageValidationError> Errors { get; } = CreateErrors(errors);

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	public static MessageValidationResult Success() => new(isValid: true, []);

	/// <summary>
	/// Creates a failed validation result with errors.
	/// </summary>
	public static MessageValidationResult Failure(params MessageValidationError[] errors) => new(isValid: false, errors);

	private static IReadOnlyList<MessageValidationError> CreateErrors(IEnumerable<MessageValidationError>? errors)
	{
		if (errors is null)
		{
			return EmptyErrors;
		}

		if (errors is MessageValidationError[] errorArray)
		{
			if (errorArray.Length == 0)
			{
				return EmptyErrors;
			}

			var copiedArray = new MessageValidationError[errorArray.Length];
			Array.Copy(errorArray, copiedArray, errorArray.Length);
			return copiedArray;
		}

		if (errors is ICollection<MessageValidationError> errorCollection)
		{
			if (errorCollection.Count == 0)
			{
				return EmptyErrors;
			}

			var copiedArray = new MessageValidationError[errorCollection.Count];
			errorCollection.CopyTo(copiedArray, 0);
			return copiedArray;
		}

		var buffer = new List<MessageValidationError>();
		foreach (var error in errors)
		{
			buffer.Add(error);
		}

		if (buffer.Count == 0)
		{
			return EmptyErrors;
		}

		var copied = new MessageValidationError[buffer.Count];
		buffer.CopyTo(copied, 0);
		return copied;
	}
}
