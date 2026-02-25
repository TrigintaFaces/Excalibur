// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Persistence;

/// <summary>
/// Defines the refresh policy to apply after Elasticsearch write operations.
/// </summary>
public enum ElasticsearchRefreshPolicy
{
	/// <summary>
	/// Do not refresh after the write. Changes may not be immediately visible.
	/// Best for high-throughput scenarios where eventual visibility is acceptable.
	/// </summary>
	None = 0,

	/// <summary>
	/// Wait for the next automatic refresh (default 1 second) to make changes visible.
	/// Balances write performance with reasonable visibility guarantees.
	/// </summary>
	WaitFor = 1,

	/// <summary>
	/// Force an immediate refresh after the write. Changes are visible immediately.
	/// Use sparingly as it impacts indexing throughput.
	/// </summary>
	Immediate = 2
}
