// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Represents the result of credential validation.
/// </summary>
/// <param name="IsValid">Whether the credential validation was successful.</param>
/// <param name="Errors">The validation error messages, or an empty array if validation was successful.</param>
public sealed record CredentialValidationResult(bool IsValid, string[] Errors)
{
	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	/// <returns> A successful credential validation result. </returns>
	public static CredentialValidationResult Success() => new(true, []);

	/// <summary>
	/// Creates a failed validation result with the specified error messages.
	/// </summary>
	/// <param name="errors"> The validation error messages. </param>
	/// <returns> A failed credential validation result with the specified errors. </returns>
	public static CredentialValidationResult Failure(params string[] errors) => new(false, errors);
}
