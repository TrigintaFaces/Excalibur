// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Extension methods for <see cref="IConnectionStringProvider"/>.
/// </summary>
public static class ConnectionStringProviderExtensions
{
	/// <summary>Builds a connection string from components.</summary>
	public static string BuildConnectionString(this IConnectionStringProvider provider, IDictionary<string, string> components)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is IConnectionStringProviderAdmin admin)
		{
			return admin.BuildConnectionString(components);
		}

		return string.Empty;
	}

	/// <summary>Parses a connection string into its components.</summary>
	public static IDictionary<string, string> ParseConnectionString(this IConnectionStringProvider provider, string connectionString)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is IConnectionStringProviderAdmin admin)
		{
			return admin.ParseConnectionString(connectionString);
		}

		return new Dictionary<string, string>();
	}

	/// <summary>Validates a connection string.</summary>
	public static bool ValidateConnectionString(this IConnectionStringProvider provider, string connectionString, string providerType)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is IConnectionStringProviderAdmin admin)
		{
			return admin.ValidateConnectionString(connectionString, providerType);
		}

		return false;
	}

	/// <summary>Refreshes cached connection strings from the configuration source.</summary>
	public static Task RefreshAsync(this IConnectionStringProvider provider, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is IConnectionStringProviderAdmin admin)
		{
			return admin.RefreshAsync(cancellationToken);
		}

		return Task.CompletedTask;
	}
}
