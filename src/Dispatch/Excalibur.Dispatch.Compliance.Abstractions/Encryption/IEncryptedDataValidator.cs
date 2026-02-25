// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides validation services for encrypted data to ensure integrity and compliance.
/// </summary>
/// <remarks>
/// <para>
/// This interface supports validation of encrypted data structures including:
/// - Structural validation (required fields, format)
/// - Key existence and validity checks
/// - Algorithm compliance verification
/// - FIPS 140-2 compliance checking
/// </para>
/// </remarks>
public interface IEncryptedDataValidator
{
	/// <summary>
	/// Validates the structural integrity of encrypted data.
	/// </summary>
	/// <param name="encryptedData"> The encrypted data to validate. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The validation result indicating success or failure details. </returns>
	Task<ValidationResult> ValidateStructureAsync(
		EncryptedData encryptedData,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates that the key referenced in the encrypted data exists and is valid.
	/// </summary>
	/// <param name="encryptedData"> The encrypted data containing key references. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The validation result indicating if the key is valid. </returns>
	Task<ValidationResult> ValidateKeyAsync(
		EncryptedData encryptedData,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates that the encrypted data can be successfully decrypted.
	/// </summary>
	/// <param name="encryptedData"> The encrypted data to validate. </param>
	/// <param name="context"> The encryption context for decryption. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The validation result indicating if decryption succeeds. </returns>
	Task<ValidationResult> ValidateDecryptabilityAsync(
		EncryptedData encryptedData,
		EncryptionContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates that the encrypted data meets compliance requirements.
	/// </summary>
	/// <param name="encryptedData"> The encrypted data to validate. </param>
	/// <param name="requirements"> The compliance requirements to check against. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The validation result indicating compliance status. </returns>
	Task<ValidationResult> ValidateComplianceAsync(
		EncryptedData encryptedData,
		ComplianceRequirements requirements,
		CancellationToken cancellationToken);

	/// <summary>
	/// Performs a comprehensive validation of all aspects of the encrypted data.
	/// </summary>
	/// <param name="encryptedData"> The encrypted data to validate. </param>
	/// <param name="context"> The encryption context for validation. </param>
	/// <param name="options"> Options controlling the validation. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The comprehensive validation result. </returns>
	Task<ComprehensiveValidationResult> ValidateAsync(
		EncryptedData encryptedData,
		EncryptionContext context,
		ValidationOptions? options,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed record ValidationResult
{
	/// <summary>
	/// Gets a value indicating whether the validation passed.
	/// </summary>
	public required bool IsValid { get; init; }

	/// <summary>
	/// Gets the validation error messages if validation failed.
	/// </summary>
	public IReadOnlyList<string>? Errors { get; init; }

	/// <summary>
	/// Gets validation warnings that don't cause failure but should be addressed.
	/// </summary>
	public IReadOnlyList<string>? Warnings { get; init; }

	/// <summary>
	/// Gets the validation code for programmatic handling.
	/// </summary>
	public string? Code { get; init; }

	/// <summary>
	/// Gets additional details about the validation.
	/// </summary>
	public IReadOnlyDictionary<string, object>? Details { get; init; }

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	/// <returns> A successful validation result. </returns>
	public static ValidationResult Success() => new() { IsValid = true };

	/// <summary>
	/// Creates a successful validation result with warnings.
	/// </summary>
	/// <param name="warnings"> The warnings to include. </param>
	/// <returns> A successful validation result with warnings. </returns>
	public static ValidationResult SuccessWithWarnings(params string[] warnings) =>
		new() { IsValid = true, Warnings = warnings };

	/// <summary>
	/// Creates a failed validation result.
	/// </summary>
	/// <param name="errors"> The error messages. </param>
	/// <returns> A failed validation result. </returns>
	public static ValidationResult Failure(params string[] errors) =>
		new() { IsValid = false, Errors = errors };

	/// <summary>
	/// Creates a failed validation result with a code.
	/// </summary>
	/// <param name="code"> The error code. </param>
	/// <param name="error"> The error message. </param>
	/// <returns> A failed validation result with a code. </returns>
	public static ValidationResult Failure(string code, string error) =>
		new() { IsValid = false, Code = code, Errors = [error] };
}

/// <summary>
/// Represents the result of a comprehensive validation operation.
/// </summary>
public sealed record ComprehensiveValidationResult
{
	/// <summary>
	/// Gets a value indicating whether all validations passed.
	/// </summary>
	public required bool IsValid { get; init; }

	/// <summary>
	/// Gets the structural validation result.
	/// </summary>
	public required ValidationResult StructureValidation { get; init; }

	/// <summary>
	/// Gets the key validation result.
	/// </summary>
	public required ValidationResult KeyValidation { get; init; }

	/// <summary>
	/// Gets the decryptability validation result, if performed.
	/// </summary>
	public ValidationResult? DecryptabilityValidation { get; init; }

	/// <summary>
	/// Gets the compliance validation result, if performed.
	/// </summary>
	public ValidationResult? ComplianceValidation { get; init; }

	/// <summary>
	/// Gets the duration of the validation.
	/// </summary>
	public TimeSpan Duration { get; init; }
}

/// <summary>
/// Requirements for compliance validation.
/// </summary>
public sealed record ComplianceRequirements
{
	/// <summary>
	/// Gets a value indicating whether FIPS 140-2 compliance is required.
	/// </summary>
	public bool RequireFips { get; init; }

	/// <summary>
	/// Gets the minimum allowed key size in bits.
	/// </summary>
	public int? MinKeySize { get; init; }

	/// <summary>
	/// Gets the allowed encryption algorithms.
	/// </summary>
	public IReadOnlySet<EncryptionAlgorithm>? AllowedAlgorithms { get; init; }

	/// <summary>
	/// Gets the maximum age allowed for encrypted data.
	/// </summary>
	public TimeSpan? MaxDataAge { get; init; }

	/// <summary>
	/// Gets a value indicating whether authentication tags are required.
	/// </summary>
	public bool RequireAuthTag { get; init; } = true;

	/// <summary>
	/// Gets the default FIPS-compliant requirements.
	/// </summary>
	public static ComplianceRequirements Fips => new()
	{
		RequireFips = true,
		MinKeySize = 256,
		AllowedAlgorithms = new HashSet<EncryptionAlgorithm>
		{
			EncryptionAlgorithm.Aes256Gcm,
			EncryptionAlgorithm.Aes256CbcHmac,
		},
		RequireAuthTag = true,
	};
}

/// <summary>
/// Options for validation operations.
/// </summary>
public sealed record ValidationOptions
{
	/// <summary>
	/// Gets a value indicating whether to validate decryptability.
	/// </summary>
	public bool ValidateDecryptability { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to validate compliance.
	/// </summary>
	public bool ValidateCompliance { get; init; } = true;

	/// <summary>
	/// Gets the compliance requirements to validate against.
	/// </summary>
	public ComplianceRequirements? ComplianceRequirements { get; init; }

	/// <summary>
	/// Gets a value indicating whether to fail fast on first error.
	/// </summary>
	public bool FailFast { get; init; }

	/// <summary>
	/// Gets the default validation options.
	/// </summary>
	public static ValidationOptions Default => new();
}
