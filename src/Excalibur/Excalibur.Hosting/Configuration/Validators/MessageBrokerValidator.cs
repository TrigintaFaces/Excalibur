// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.Configuration.Validators;

/// <summary>
/// Base class for message broker configuration validators.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessageBrokerValidator" /> class. </remarks>
/// <param name="configurationName"> The name of the message broker configuration. </param>
public abstract class MessageBrokerValidator(string configurationName) : ConfigurationValidatorBase(configurationName, priority: 30)
{
	/// <summary>
	/// Validates a message broker endpoint URL.
	/// </summary>
	/// <param name="endpoint"> The endpoint to validate. </param>
	/// <param name="errors"> The list to add errors Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configPath"> The configuration path for error reporting. </param>
	/// <param name="expectedScheme"> The expected URI scheme (e.g., "amqp", "kafka"). </param>
	/// <returns> True if valid, false otherwise. </returns>
	protected static bool ValidateEndpoint(
		string? endpoint,
		ICollection<ConfigurationValidationError> errors,
		string configPath,
		string? expectedScheme = null)
	{
		ArgumentNullException.ThrowIfNull(errors);

		if (string.IsNullOrWhiteSpace(endpoint))
		{
			errors.Add(new ConfigurationValidationError(
				"Message broker endpoint is missing or empty",
				configPath,
				value: null,
				"Provide the broker endpoint URL"));
			return false;
		}

		if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
		{
			errors.Add(new ConfigurationValidationError(
				"Invalid endpoint URL format",
				configPath,
				endpoint,
				"Provide a valid URL (e.g., amqp://localhost:5672)"));
			return false;
		}

		if (!string.IsNullOrWhiteSpace(expectedScheme) &&
			!string.Equals(uri.Scheme, expectedScheme, StringComparison.OrdinalIgnoreCase))
		{
			errors.Add(new ConfigurationValidationError(
				$"Invalid URL scheme '{uri.Scheme}', expected '{expectedScheme}'",
				configPath,
				endpoint,
				$"Use {expectedScheme}:// for the endpoint URL"));
			return false;
		}

		return true;
	}
}
