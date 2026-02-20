// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Configuration builder for pipeline profiles.
/// </summary>
public interface IPipelineProfilesConfigurationBuilder
{
	/// <summary>
	/// Registers a new pipeline profile.
	/// </summary>
	/// <param name="name"> The profile name. </param>
	/// <param name="configure"> Configuration action for the profile. </param>
	/// <returns> The builder for chaining. </returns>
	IPipelineProfilesConfigurationBuilder RegisterProfile(string name, Action<IPipelineProfileBuilder> configure);

	/// <summary>
	/// Sets the default pipeline profile.
	/// </summary>
	/// <param name="name"> The profile name. </param>
	/// <param name="configure"> Configuration action for the profile. </param>
	/// <returns> The builder for chaining. </returns>
	IPipelineProfilesConfigurationBuilder SetDefaultProfile(string name, Action<IPipelineProfileBuilder> configure);
}
