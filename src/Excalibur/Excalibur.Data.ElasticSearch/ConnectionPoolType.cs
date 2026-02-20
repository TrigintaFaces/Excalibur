// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Specifies the type of connection pool to use for Elasticsearch cluster connections.
/// </summary>
public enum ConnectionPoolType
{
	/// <summary>
	/// Static connection pool with a fixed set of nodes.
	/// </summary>
	Static = 0,

	/// <summary>
	/// Sniffing connection pool that dynamically discovers cluster nodes.
	/// </summary>
	Sniffing = 1,
}
