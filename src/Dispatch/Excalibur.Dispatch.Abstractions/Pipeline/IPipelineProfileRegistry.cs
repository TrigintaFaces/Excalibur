// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Registry for managing pipeline profiles.
/// </summary>
public interface IPipelineProfileRegistry
{
	/// <summary>
	/// Gets a pipeline profile by name.
	/// </summary>
	/// <param name="profileName"> The name of the profile to retrieve. </param>
	/// <returns> The pipeline profile, or null if not found. </returns>
	IPipelineProfile? GetProfile(string profileName);

	/// <summary>
	/// Registers a pipeline profile.
	/// </summary>
	/// <param name="profile"> The profile to register. </param>
	void RegisterProfile(IPipelineProfile profile);

	/// <summary>
	/// Gets all registered pipeline profile names.
	/// </summary>
	/// <returns> A collection of profile names. </returns>
	IEnumerable<string> GetProfileNames();

	/// <summary>
	/// Removes a pipeline profile by name.
	/// </summary>
	/// <param name="profileName"> The name of the profile to remove. </param>
	/// <returns> True if the profile was removed, false if it was not found. </returns>
	bool RemoveProfile(string profileName);

	/// <summary>
	/// Sets the default pipeline profile for message processing.
	/// </summary>
	/// <param name="profileName"> The name of the profile to set as default. </param>
	void SetDefaultProfile(string profileName);
}
