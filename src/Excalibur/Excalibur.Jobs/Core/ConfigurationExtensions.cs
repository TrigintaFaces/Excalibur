// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Jobs.Core;

/// <summary>
/// Provides extension methods for retrieving strongly-typed job configurations from the application's configuration.
/// </summary>
public static class ConfigurationExtensions
{
	/// <summary>
	/// Retrieves the configuration section for a specified job and maps it to a strongly-typed configuration object.
	/// </summary>
	/// <typeparam name="TConfig"> The type of the configuration object to retrieve. </typeparam>
	/// <param name="configuration"> The <see cref="IConfiguration" /> instance containing the application's configuration. </param>
	/// <param name="jobConfigSectionName"> The name of the configuration section for the job. </param>
	/// <returns> A strongly-typed <typeparamref name="TConfig" /> object populated with values from the specified configuration section. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="configuration" /> or <paramref name="jobConfigSectionName" /> is null.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown if the specified configuration section is not found or cannot be mapped to the <typeparamref name="TConfig" /> type.
	/// </exception>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public static TConfig GetJobConfiguration<TConfig>(this IConfiguration configuration, string jobConfigSectionName)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(jobConfigSectionName);

		return configuration
				   .GetSection(jobConfigSectionName)
				   .Get<TConfig>() ??
			   throw new InvalidOperationException($"Job configuration not found at {jobConfigSectionName}.");
	}
}
