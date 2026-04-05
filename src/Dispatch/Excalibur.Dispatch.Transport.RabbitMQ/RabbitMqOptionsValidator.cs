// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Validates RabbitMQ configuration options to ensure secure and properly formatted connection strings.
/// </summary>
internal sealed class RabbitMqOptionsValidator : IValidateOptions<RabbitMqOptions>
{
	/// <summary>
	/// Validates the provided RabbitMQ options.
	/// </summary>
	/// <param name="name"> The name of the options instance being validated. </param>
	/// <param name="options"> The RabbitMQ options to validate. </param>
	/// <returns> A validation result indicating success or failure with appropriate error messages. </returns>
	public ValidateOptionsResult Validate(string? name, RabbitMqOptions options)
	{
		// Validate that options object is not null
		if (options is null)
		{
			return ValidateOptionsResult.Fail("RabbitMQ options cannot be null.");
		}

		// Check ConnectionString is not null or empty
		if (string.IsNullOrWhiteSpace(options.Connection.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"RabbitMqOptions.Connection.ConnectionString is required. " +
				"Set it to a valid AMQP URI (e.g., 'amqps://user:pass@host:5671/vhost') via " +
				"services.Configure<RabbitMqOptions>(config.GetSection(\"RabbitMQ\")) or in your options configuration.");
		}

		// Validate connection string format - must start with amqp:// or amqps://
		if (!options.Connection.ConnectionString.StartsWith("amqp://", StringComparison.OrdinalIgnoreCase) &&
				!options.Connection.ConnectionString.StartsWith("amqps://", StringComparison.OrdinalIgnoreCase))
		{
			return ValidateOptionsResult.Fail(
				"RabbitMQ connection string must start with 'amqp://' or 'amqps://' protocol scheme.");
		}

		// Ensure no default credentials are used in production
		if (ContainsDefaultCredentials(options.Connection.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"RabbitMQ connection string contains default credentials (guest:guest). " +
				"Please use secure credentials for production environments.");
		}

		// Additional validation for malformed URIs
		if (!IsValidAmqpUri(options.Connection.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"RabbitMQ connection string is not a valid AMQP URI. " +
				"Expected format: amqp[s]://[username:password@]host[:port][/vhost]");
		}

		return ValidateOptionsResult.Success;
	}

	/// <summary>
	/// Checks if the connection string contains default RabbitMQ credentials.
	/// </summary>
	/// <param name="connectionString"> The connection string to check. </param>
	/// <returns> True if default credentials are detected; otherwise, false. </returns>
	private static bool ContainsDefaultCredentials(string connectionString)
	{
		// Check for common default credential patterns (case-insensitive via OrdinalIgnoreCase)
		return connectionString.Contains("guest:guest@", StringComparison.OrdinalIgnoreCase) ||
					 connectionString.Contains("guest%3Aguest@", StringComparison.OrdinalIgnoreCase); // URL encoded version
	}

	/// <summary>
	/// Validates that the connection string is a properly formatted AMQP URI.
	/// </summary>
	/// <param name="connectionString"> The connection string to validate. </param>
	/// <returns> True if the URI is valid; otherwise, false. </returns>
	private static bool IsValidAmqpUri(string connectionString)
	{
		try
		{
			var uri = new Uri(connectionString);

			// Validate the scheme is amqp or amqps
			if (!string.Equals(uri.Scheme, "amqp", StringComparison.OrdinalIgnoreCase) &&
					!string.Equals(uri.Scheme, "amqps", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			// Ensure host is specified
			if (string.IsNullOrWhiteSpace(uri.Host))
			{
				return false;
			}

			// Port validation (if specified, should be valid)
			if (uri.Port is not -1 and (<= 0 or > 65535))
			{
				return false;
			}

			return true;
		}
		catch (UriFormatException)
		{
			return false;
		}
	}
}
