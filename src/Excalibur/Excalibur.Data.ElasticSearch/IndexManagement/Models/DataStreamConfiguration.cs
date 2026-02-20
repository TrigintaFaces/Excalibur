// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the configuration for Elasticsearch data streams.
/// </summary>
public sealed class DataStreamConfiguration
{
	/// <summary>
	/// Gets the timestamp field for the data stream.
	/// </summary>
	/// <value> The timestamp field configuration. </value>
	public DataStreamTimestampField? TimestampField { get; init; }

	/// <summary>
	/// Gets a value indicating whether the data stream is hidden.
	/// </summary>
	/// <value> True if the data stream should be hidden, false otherwise. </value>
	public bool? Hidden { get; init; }

	/// <summary>
	/// Gets a value indicating whether to allow custom routing.
	/// </summary>
	/// <value> True to allow custom routing, false otherwise. </value>
	public bool? AllowCustomRouting { get; init; }
}
