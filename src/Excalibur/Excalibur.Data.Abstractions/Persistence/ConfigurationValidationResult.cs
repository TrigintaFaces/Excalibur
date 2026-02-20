// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Represents a configuration validation result.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConfigurationValidationResult" /> class.
/// </remarks>
/// <param name="isValid"> Whether the configuration is valid. </param>
/// <param name="providerName"> The name of the provider. </param>
/// <param name="message"> The validation message. </param>
/// <param name="severity"> The severity of the validation issue. </param>
public sealed class ConfigurationValidationResult(
	bool isValid,
	string providerName,
	string message,
	ValidationSeverity severity = ValidationSeverity.Error)
{
	/// <summary>
	/// Gets a value indicating whether the configuration is valid.
	/// </summary>
	/// <value>The current <see cref="IsValid"/> value.</value>
	public bool IsValid { get; } = isValid;

	/// <summary>
	/// Gets the name of the provider.
	/// </summary>
	/// <value>The current <see cref="ProviderName"/> value.</value>
	public string ProviderName { get; } = providerName;

	/// <summary>
	/// Gets the validation message.
	/// </summary>
	/// <value>The current <see cref="Message"/> value.</value>
	public string Message { get; } = message;

	/// <summary>
	/// Gets the severity of the validation issue.
	/// </summary>
	/// <value>The current <see cref="Severity"/> value.</value>
	public ValidationSeverity Severity { get; } = severity;
}
