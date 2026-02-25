// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Pre-warming strategies for the writer pool.
/// </summary>
public enum PreWarmStrategy
{
	/// <summary>
	/// Pre-warm only thread-local caches.
	/// </summary>
	ThreadLocal = 0,

	/// <summary>
	/// Pre-warm only the global pool.
	/// </summary>
	Global = 1,

	/// <summary>
	/// Pre-warm both thread-local and global pools in a balanced way.
	/// </summary>
	Balanced = 2,
}
