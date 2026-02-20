// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Message contract used to trigger cache invalidation.
/// </summary>
public interface ICacheInvalidator : IDispatchMessage
{
	/// <summary>
	/// Gets a list of cache tags that should be invalidated when this message is handled.
	/// </summary>
	/// <returns>A list of cache tags that should be invalidated when this message is handled.</returns>
	IEnumerable<string> GetCacheTagsToInvalidate();

	/// <summary>
	/// Gets a list of exact cache keys to invalidate.
	/// </summary>
	/// <returns>A list of exact cache keys to invalidate.</returns>
	IEnumerable<string> GetCacheKeysToInvalidate();
}
