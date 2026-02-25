// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Represents the result of a configuration validation operation.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ConfigurationValidationResult" /> class. </remarks>
/// <param name="isValid"> Indicates whether the configuration is valid. </param>
/// <param name="errors"> Collection of validation errors. </param>
public sealed class ConfigurationValidationResult(bool isValid, IReadOnlyList<ConfigurationValidationError>? errors = null)
{
	/// <summary>
	/// Gets a value indicating whether the configuration is valid.
	/// </summary>
	/// <value> <see langword="true" /> if the configuration is valid; otherwise, <see langword="false" />. </value>
	public bool IsValid { get; } = isValid;

	/// <summary>
	/// Gets the collection of validation errors.
	/// </summary>
	/// <value> The collection of validation errors. </value>
	public IReadOnlyList<ConfigurationValidationError> Errors { get; } = errors ?? [];

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	/// <returns> A successful validation result. </returns>
	public static ConfigurationValidationResult Success() => new(isValid: true);

	/// <summary>
	/// Creates a failed validation result with a single error.
	/// </summary>
	/// <param name="errorMessage"> The error message. </param>
	/// <param name="configurationPath"> The configuration path where the error occurred. </param>
	/// <returns> A failed validation result. </returns>
	public static ConfigurationValidationResult Failure(string errorMessage, string? configurationPath = null)
		=> new(isValid: false, [new ConfigurationValidationError(errorMessage, configurationPath)]);

	/// <summary>
	/// Creates a failed validation result with multiple errors.
	/// </summary>
	/// <param name="errors"> The collection of errors. </param>
	/// <returns> A failed validation result. </returns>
	public static ConfigurationValidationResult Failure(IReadOnlyList<ConfigurationValidationError> errors)
		=> new(isValid: false, errors);

	/// <summary>
	/// Combines multiple validation results into a single result.
	/// </summary>
	/// <param name="results"> The results to combine. </param>
	/// <returns> A combined validation result. </returns>
	public static ConfigurationValidationResult Combine(params ConfigurationValidationResult[] results)
	{
		var allErrors = results
			.Where(static r => !r.IsValid)
			.SelectMany(static r => r.Errors)
			.ToList();

		return allErrors.Count == 0
			? Success()
			: Failure(allErrors);
	}
}
