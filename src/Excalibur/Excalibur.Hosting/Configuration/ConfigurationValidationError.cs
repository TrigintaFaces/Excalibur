// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Represents a configuration validation error.
/// </summary>
public sealed class ConfigurationValidationError
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationValidationError" /> class.
	/// </summary>
	/// <param name="message"> The error message. </param>
	/// <param name="configurationPath"> The configuration path where the error occurred. </param>
	/// <param name="value"> The invalid value, if applicable. </param>
	/// <param name="recommendation"> A recommendation for fixing the error. </param>
	public ConfigurationValidationError(
		string message,
		string? configurationPath = null,
		object? value = null,
		string? recommendation = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message);

		Message = message;
		ConfigurationPath = configurationPath;
		Value = value;
		Recommendation = recommendation;
	}

	/// <summary>
	/// Gets the error message.
	/// </summary>
	/// <value> The error message. </value>
	public string Message { get; }

	/// <summary>
	/// Gets the configuration path where the error occurred.
	/// </summary>
	/// <value> The configuration path, or <see langword="null" /> if not available. </value>
	public string? ConfigurationPath { get; }

	/// <summary>
	/// Gets the invalid value, if applicable.
	/// </summary>
	/// <value> The invalid value, or <see langword="null" /> if not applicable. </value>
	public object? Value { get; }

	/// <summary>
	/// Gets a recommendation for fixing the error.
	/// </summary>
	/// <value> The recommendation, or <see langword="null" /> if not available. </value>
	public string? Recommendation { get; }

	/// <summary>
	/// Returns a string representation of the error.
	/// </summary>
	/// <returns>A string representation of the configuration validation error.</returns>
	public override string ToString()
	{
		var parts = new List<string>();

		if (!string.IsNullOrWhiteSpace(ConfigurationPath))
		{
			parts.Add($"[{ConfigurationPath}]");
		}

		parts.Add(Message);

		if (Value != null)
		{
			parts.Add($"Value: '{Value}'");
		}

		if (!string.IsNullOrWhiteSpace(Recommendation))
		{
			parts.Add($"Recommendation: {Recommendation}");
		}

		return string.Join(" - ", parts);
	}
}
