// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Configuration;

namespace Excalibur.Domain.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="IConfiguration" /> interface to simplify application configuration retrieval.
/// </summary>
public static class ConfigurationExtensions
{
	/// <summary>
	/// Retrieves a dictionary of key-value pairs representing the configuration settings for the application context from the specified <see cref="IConfiguration" />.
	/// </summary>
	/// <param name="configuration"> The <see cref="IConfiguration" /> instance to retrieve the application context settings from. </param>
	/// <returns> A dictionary containing configuration key-value pairs from the "ApplicationContext" section. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="configuration" /> parameter is <c> null </c>. </exception>
	public static Dictionary<string, string?> GetApplicationContextConfiguration(this IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		return configuration
			.GetSection(nameof(ApplicationContext))
			.GetChildren()
			.ToDictionary(static c => c.Key, static c => c.Value, StringComparer.Ordinal);
	}
}
