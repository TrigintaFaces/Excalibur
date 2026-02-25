// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Provides connection string management for persistence providers.
/// </summary>
public interface IConnectionStringProvider
{
	/// <summary>
	/// Gets a connection string by name.
	/// </summary>
	/// <param name="name"> The name of the connection string. </param>
	/// <returns> The connection string. </returns>
	string GetConnectionString(string name);

	/// <summary>
	/// Gets a connection string asynchronously, potentially from a secure store.
	/// </summary>
	/// <param name="name"> The name of the connection string. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The connection string. </returns>
	Task<string> GetConnectionStringAsync(string name, CancellationToken cancellationToken);

	/// <summary>
	/// Sets a connection string.
	/// </summary>
	/// <param name="name"> The name of the connection string. </param>
	/// <param name="connectionString"> The connection string value. </param>
	void SetConnectionString(string name, string connectionString);

	/// <summary>
	/// Checks if a connection string exists.
	/// </summary>
	/// <param name="name"> The name of the connection string. </param>
	/// <returns> True if the connection string exists; otherwise, false. </returns>
	bool ConnectionStringExists(string name);

	/// <summary>
	/// Gets all available connection string names.
	/// </summary>
	/// <returns> Collection of connection string names. </returns>
	IEnumerable<string> GetConnectionStringNames();

	/// <summary>
	/// Builds a connection string from components.
	/// </summary>
	/// <param name="components"> The connection string components. </param>
	/// <returns> The built connection string. </returns>
	string BuildConnectionString(IDictionary<string, string> components);

	/// <summary>
	/// Parses a connection string into its components.
	/// </summary>
	/// <param name="connectionString"> The connection string to parse. </param>
	/// <returns> Dictionary of connection string components. </returns>
	IDictionary<string, string> ParseConnectionString(string connectionString);

	/// <summary>
	/// Validates a connection string.
	/// </summary>
	/// <param name="connectionString"> The connection string to validate. </param>
	/// <param name="providerType"> The type of provider the connection string is for. </param>
	/// <returns> True if the connection string is valid; otherwise, false. </returns>
	bool ValidateConnectionString(string connectionString, string providerType);

	/// <summary>
	/// Refreshes cached connection strings from the configuration source.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task RefreshAsync(CancellationToken cancellationToken);
}
