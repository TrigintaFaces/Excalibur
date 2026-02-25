// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the timestamp field configuration for data streams.
/// </summary>
public sealed class DataStreamTimestampField
{
	/// <summary>
	/// Gets the name of the timestamp field.
	/// </summary>
	/// <value> The timestamp field name. Defaults to "@timestamp". </value>
	public string Name { get; init; } = "@timestamp";
}
