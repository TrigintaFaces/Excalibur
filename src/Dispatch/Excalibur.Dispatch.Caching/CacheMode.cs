// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Defines the caching mode for the Dispatch caching system.
/// </summary>
public enum CacheMode
{
	/// <summary>
	/// Use only in-memory caching (IMemoryCache). Fast but not shared across servers.
	/// </summary>
	Memory = 0,

	/// <summary>
	/// Use only distributed caching (IDistributedCache). Shared across servers but has network latency.
	/// </summary>
	Distributed = 1,

	/// <summary>
	/// Use hybrid caching (HybridCache). Combines memory and distributed caching for optimal performance.
	/// </summary>
	Hybrid = 2,
}
