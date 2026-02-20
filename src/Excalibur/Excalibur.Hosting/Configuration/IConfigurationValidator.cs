// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Defines the contract for configuration validators.
/// </summary>
public interface IConfigurationValidator
{
	/// <summary>
	/// Gets the name of the configuration section or component being validated.
	/// </summary>
	/// <value> The name of the configuration section or component. </value>
	string ConfigurationName { get; }

	/// <summary>
	/// Gets the priority of this validator. Lower values are executed first.
	/// </summary>
	/// <value> The priority of the validator. </value>
	int Priority { get; }

	/// <summary>
	/// Validates the configuration synchronously.
	/// </summary>
	/// <param name="configuration"> The configuration to validate. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A validation result containing success status and any error messages. </returns>
	Task<ConfigurationValidationResult> ValidateAsync(IConfiguration configuration, CancellationToken cancellationToken);
}
