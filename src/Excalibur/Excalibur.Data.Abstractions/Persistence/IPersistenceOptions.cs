// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Persistence;

/// <summary>
/// Core interface for persistence provider options. Contains connection
/// fundamentals and extensibility — at most 5 members per ISP gate.
/// </summary>
public interface IPersistenceOptions
{
	/// <summary>
	/// Gets or sets the connection string for the persistence provider.
	/// </summary>
	/// <value>
	/// The connection string for the persistence provider.
	/// </value>
	string ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>
	/// The connection timeout in seconds.
	/// </value>
	int ConnectionTimeout { get; set; }

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value>
	/// The command timeout in seconds.
	/// </value>
	int CommandTimeout { get; set; }

	/// <summary>
	/// Gets provider-specific options as key-value pairs.
	/// </summary>
	/// <value>
	/// Provider-specific options as key-value pairs.
	/// </value>
	IDictionary<string, object> ProviderSpecificOptions { get; }

	/// <summary>
	/// Validates the options and throws an exception if invalid.
	/// </summary>
	void Validate();
}
