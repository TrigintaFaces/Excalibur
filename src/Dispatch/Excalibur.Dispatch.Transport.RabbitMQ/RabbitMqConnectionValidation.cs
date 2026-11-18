// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>Checks the live connection input without creating a driver factory or opening a connection.</summary>
internal static class RabbitMqConnectionValidation
{
	/// <summary>Validates structural input and the existing explicit-URI credential policy.</summary>
	/// <param name="connection">The same connection options read by the factory.</param>
	/// <param name="transportName">The transport whose configuration is being checked.</param>
	/// <exception cref="ArgumentNullException">Thrown when connection is null.</exception>
	/// <exception cref="OptionsValidationException">Thrown when the live connection input is invalid.</exception>
	internal static void Validate(RabbitMQConnectionOptions connection, string transportName)
	{
		ArgumentNullException.ThrowIfNull(connection);

		var failure = GetFailure(connection);
		if (failure is not null)
		{
			throw new OptionsValidationException(transportName, typeof(RabbitMQConnectionOptions), [failure]);
		}
	}

	private static string? GetFailure(RabbitMQConnectionOptions connection)
	{
		if (string.IsNullOrEmpty(connection.ConnectionString))
		{
			if (string.IsNullOrWhiteSpace(connection.HostName))
			{
				return "RabbitMQ HostName must not be empty when no connection URI is supplied.";
			}

			return connection.Port is < 1 or > 65535
				? "RabbitMQ Port must be between 1 and 65535 when no connection URI is supplied."
				: null;
		}

		if (!Uri.TryCreate(connection.ConnectionString, UriKind.Absolute, out var uri) ||
			!(string.Equals(uri.Scheme, "amqp", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(uri.Scheme, "amqps", StringComparison.OrdinalIgnoreCase)) ||
			string.IsNullOrWhiteSpace(uri.Host) || uri.Port is not -1 and (< 1 or > 65535))
		{
			return "RabbitMQ connection URI must be a well-formed amqp:// or amqps:// URI with a host and valid port.";
		}

		var credentials = uri.UserInfo.Split(':', 2);
		var defaultCredentials = credentials.Length == 2 &&
			RabbitMqSecurityPosture.AreDefaultCredentials(
				Uri.UnescapeDataString(credentials[0]), Uri.UnescapeDataString(credentials[1]));
		if (defaultCredentials || connection.ConnectionString.Contains("guest:guest@", StringComparison.OrdinalIgnoreCase) ||
			connection.ConnectionString.Contains("guest%3Aguest@", StringComparison.OrdinalIgnoreCase))
		{
			return "RabbitMQ connection URI must not contain the default guest credentials.";
		}

		return null;
	}
}
