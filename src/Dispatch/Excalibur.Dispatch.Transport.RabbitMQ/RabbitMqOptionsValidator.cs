// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Validates RabbitMQ configuration options to ensure secure and properly formatted connection strings.
/// </summary>
public sealed class RabbitMqOptionsValidator : IValidateOptions<RabbitMqOptions>
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
		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail("RabbitMQ connection string cannot be null or empty.");
		}

		// Validate connection string format - must start with amqp:// or amqps://
		if (!options.ConnectionString.StartsWith("amqp://", StringComparison.OrdinalIgnoreCase) &&
				!options.ConnectionString.StartsWith("amqps://", StringComparison.OrdinalIgnoreCase))
		{
			return ValidateOptionsResult.Fail(
				"RabbitMQ connection string must start with 'amqp://' or 'amqps://' protocol scheme.");
		}

		// Ensure no default credentials are used in production
		if (ContainsDefaultCredentials(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"RabbitMQ connection string contains default credentials (guest:guest). " +
				"Please use secure credentials for production environments.");
		}

		// Additional validation for malformed URIs
		if (!IsValidAmqpUri(options.ConnectionString))
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
		// Check for common default credential patterns
		var lowerConnectionString = connectionString.ToUpperInvariant();

		return lowerConnectionString.Contains("GUEST:GUEST@", StringComparison.Ordinal) ||
					 lowerConnectionString.Contains("GUEST%3AGUEST@", StringComparison.Ordinal) || // URL encoded version
					 lowerConnectionString.Contains("GUEST%3AGUEST@", StringComparison.Ordinal); // Mixed case URL encoding
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
			if (uri.Port is not (-1) and (<= 0 or > 65535))
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
