// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Provides administrative connection string operations.
/// Implementations should implement this alongside <see cref="IConnectionStringProvider"/>.
/// </summary>
public interface IConnectionStringProviderAdmin
{
	/// <summary>Builds a connection string from components.</summary>
	string BuildConnectionString(IDictionary<string, string> components);

	/// <summary>Parses a connection string into its components.</summary>
	IDictionary<string, string> ParseConnectionString(string connectionString);

	/// <summary>Validates a connection string.</summary>
	bool ValidateConnectionString(string connectionString, string providerType);

	/// <summary>Refreshes cached connection strings from the configuration source.</summary>
	Task RefreshAsync(CancellationToken cancellationToken);
}
