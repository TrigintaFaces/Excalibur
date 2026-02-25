// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents an Elasticsearch index lifecycle policy configuration.
/// </summary>
public sealed class IndexLifecyclePolicy
{
	/// <summary>
	/// Gets the hot phase configuration.
	/// </summary>
	/// <value> The hot phase settings for new and active indices. </value>
	public HotPhaseConfiguration? Hot { get; init; }

	/// <summary>
	/// Gets the warm phase configuration.
	/// </summary>
	/// <value> The warm phase settings for less frequently accessed indices. </value>
	public WarmPhaseConfiguration? Warm { get; init; }

	/// <summary>
	/// Gets the cold phase configuration.
	/// </summary>
	/// <value> The cold phase settings for infrequently accessed indices. </value>
	public ColdPhaseConfiguration? Cold { get; init; }

	/// <summary>
	/// Gets the delete phase configuration.
	/// </summary>
	/// <value> The delete phase settings for index deletion. </value>
	public DeletePhaseConfiguration? Delete { get; init; }
}
