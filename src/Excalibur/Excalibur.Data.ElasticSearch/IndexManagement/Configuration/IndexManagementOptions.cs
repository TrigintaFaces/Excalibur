// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Configuration settings for Elasticsearch index management features.
/// </summary>
public sealed class IndexManagementOptions
{
	/// <summary>
	/// Gets a value indicating whether index management features are enabled.
	/// </summary>
	/// <value> True to enable index management, false otherwise. Defaults to true. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the default index template settings.
	/// </summary>
	/// <value> The default template configuration. </value>
	public DefaultTemplateOptions DefaultTemplate { get; init; } = new();

	/// <summary>
	/// Gets the index lifecycle management settings.
	/// </summary>
	/// <value> The lifecycle management configuration. </value>
	public LifecycleManagementOptions Lifecycle { get; init; } = new();

	/// <summary>
	/// Gets the index optimization settings.
	/// </summary>
	/// <value> The optimization configuration. </value>
	public OptimizationOptions Optimization { get; init; } = new();
}
