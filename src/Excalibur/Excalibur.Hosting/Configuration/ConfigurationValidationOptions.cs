// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Options for configuration validation behavior.
/// </summary>
public sealed class ConfigurationValidationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether configuration validation is enabled. Default is true.
	/// </summary>
	/// <value> <see langword="true" /> if configuration validation is enabled; otherwise, <see langword="false" />. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the application should terminate immediately if configuration validation fails. Default is
	/// true for production safety.
	/// </summary>
	/// <value> <see langword="true" /> if the application should terminate on validation failure; otherwise, <see langword="false" />. </value>
	public bool FailFast { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether exceptions thrown by validators should be treated as validation errors. Default is true.
	/// </summary>
	/// <value> <see langword="true" /> if validator exceptions should be treated as errors; otherwise, <see langword="false" />. </value>
	public bool TreatValidatorExceptionsAsErrors { get; set; } = true;

	/// <summary>
	/// Gets or sets the timeout for all validation operations. Default is 30 seconds.
	/// </summary>
	/// <value> The timeout for validation operations. </value>
	public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
