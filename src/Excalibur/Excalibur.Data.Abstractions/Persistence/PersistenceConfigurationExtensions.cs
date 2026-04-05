// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Extension methods for <see cref="IPersistenceConfiguration"/>.
/// </summary>
public static class PersistenceConfigurationExtensions
{
	/// <summary>Registers a provider configuration.</summary>
	public static void RegisterProviderConfiguration(this IPersistenceConfiguration config, string providerName, IPersistenceOptions options)
	{
		ArgumentNullException.ThrowIfNull(config);
		if (config is IPersistenceConfigurationAdmin admin)
		{
			admin.RegisterProviderConfiguration(providerName, options);
		}
	}

	/// <summary>Removes a provider configuration.</summary>
	public static bool RemoveProviderConfiguration(this IPersistenceConfiguration config, string providerName)
	{
		ArgumentNullException.ThrowIfNull(config);
		if (config is IPersistenceConfigurationAdmin admin)
		{
			return admin.RemoveProviderConfiguration(providerName);
		}

		return false;
	}

	/// <summary>Reloads configuration from the underlying configuration source.</summary>
	public static void Reload(this IPersistenceConfiguration config)
	{
		ArgumentNullException.ThrowIfNull(config);
		if (config is IPersistenceConfigurationAdmin admin)
		{
			admin.Reload();
		}
	}
}
