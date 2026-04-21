// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Provides administrative persistence configuration operations.
/// Implementations should implement this alongside <see cref="IPersistenceConfiguration"/>.
/// </summary>
public interface IPersistenceConfigurationAdmin
{
	/// <summary>Registers a provider configuration.</summary>
	void RegisterProviderConfiguration(string providerName, IPersistenceOptions options);

	/// <summary>Removes a provider configuration.</summary>
	bool RemoveProviderConfiguration(string providerName);

	/// <summary>Reloads configuration from the underlying configuration source.</summary>
	void Reload();
}
