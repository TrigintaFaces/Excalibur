// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Default settings for index templates.
/// </summary>
public sealed class DefaultTemplateOptions
{
	/// <summary>
	/// Gets the default number of shards for new indices.
	/// </summary>
	/// <value> The default shard count. Defaults to 1. </value>
	public int DefaultShards { get; init; } = 1;

	/// <summary>
	/// Gets the default number of replicas for new indices.
	/// </summary>
	/// <value> The default replica count. Defaults to 1. </value>
	public int DefaultReplicas { get; init; } = 1;

	/// <summary>
	/// Gets the default refresh interval for new indices.
	/// </summary>
	/// <value> The default refresh interval. Defaults to 1 second. </value>
	public TimeSpan DefaultRefreshInterval { get; init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the default template priority.
	/// </summary>
	/// <value> The default template priority. Defaults to 100. </value>
	public int DefaultPriority { get; init; } = 100;

	/// <summary>
	/// Gets the environment-specific settings.
	/// </summary>
	/// <value> Settings that vary by environment (dev, staging, prod). </value>
	public EnvironmentOptions Environment { get; init; } = new();
}
