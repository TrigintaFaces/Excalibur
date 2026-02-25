// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Serializable implementation of IValidationResult for AOT compatibility.
/// </summary>
/// <remarks>
/// This class does not implement the static abstract members of IValidationResult as they cannot be properly serialized. Use the static
/// factory methods instead.
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

	/// <summary>
	/// Explicit implementation of static interface member to satisfy the interface requirement.
	/// </summary>
	/// <param name="errors"> The validation errors. </param>
	/// <returns> A failed validation result. </returns>
	static IValidationResult IValidationResult.Failed(params object[] errors)
		=> Failed(errors);

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	/// <returns> A successful validation result. </returns>
	static IValidationResult IValidationResult.Success()
		=> Success();
}
