// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Configuration;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Defines configuration management for persistence providers.
/// </summary>
public interface IPersistenceConfiguration
{
	/// <summary>
	/// Gets the configuration section for persistence settings.
	/// </summary>
	/// <value>
	/// The configuration section for persistence settings.
	/// </value>
	IConfigurationSection ConfigurationSection { get; }

	/// <summary>
	/// Gets the default provider name.
	/// </summary>
	/// <value>
	/// The default provider name.
	/// </value>
	string DefaultProviderName { get; }

	/// <summary>
	/// Gets configuration for a specific provider.
	/// </summary>
	/// <param name="providerName"> The name of the provider. </param>
	/// <returns> The provider configuration. </returns>
	IPersistenceOptions GetProviderOptions(string providerName);

	/// <summary>
	/// Gets all configured provider names.
	/// </summary>
	/// <returns> Collection of provider names. </returns>
	IEnumerable<string> GetConfiguredProviders();

	/// <summary>
	/// Validates the entire persistence configuration.
	/// </summary>
	/// <returns> Validation results. </returns>
	IEnumerable<ConfigurationValidationResult> Validate();

}
