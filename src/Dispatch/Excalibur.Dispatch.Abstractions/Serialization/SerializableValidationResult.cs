// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Validation;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Serializable implementation of IValidationResult for AOT compatibility.
/// </summary>
/// <remarks>
/// Use the concrete static factory methods to create success and failure results.
/// </remarks>
public sealed class SerializableValidationResult : IValidationResult
{
	private IReadOnlyCollection<object> _errors = [];

	/// <inheritdoc />
	public bool IsValid { get; set; }

	/// <inheritdoc />
	public IReadOnlyCollection<object> Errors
	{
		get => _errors;
		set => _errors = value ?? [];
	}

	/// <summary>
	/// Creates a failed validation result.
	/// </summary>
	/// <param name="errors"> The validation errors that occurred. </param>
	public static SerializableValidationResult Failed(params object[] errors)
		=> new() { IsValid = false, Errors = errors };

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	public static SerializableValidationResult Success()
		=> new() { IsValid = true };

}
