// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Configures environment-specific settings for index templates.
/// </summary>
public sealed class EnvironmentOptions
{
	/// <summary>
	/// Gets the current environment name.
	/// </summary>
	/// <value> The environment name (e.g., Development, Staging, Production). </value>
	public string Name { get; init; } = "Production";

	/// <summary>
	/// Gets a value indicating whether to apply development optimizations.
	/// </summary>
	/// <value> True to apply development-specific settings, false otherwise. </value>
	public bool UseDevelopmentSettings { get; init; }

	/// <summary>
	/// Gets the replica count for this environment.
	/// </summary>
	/// <value> The number of replicas to maintain. Defaults to 1. </value>
	public int ReplicaCount { get; init; } = 1;

	/// <summary>
	/// Gets the refresh interval for this environment.
	/// </summary>
	/// <value> The refresh interval for real-time search. Defaults to 1 second. </value>
	public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromSeconds(1);
}
