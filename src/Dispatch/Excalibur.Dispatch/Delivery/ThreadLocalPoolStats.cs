// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Statistics for thread-local pool.
/// </summary>
public sealed class ThreadLocalPoolStats
{
	/// <summary>
	/// Gets or sets the number of cached items.
	/// </summary>
	/// <value>The current <see cref="CachedItems"/> value.</value>
	public int CachedItems { get; set; }

	/// <summary>
	/// Gets or sets the maximum size.
	/// </summary>
	/// <value>The current <see cref="MaxSize"/> value.</value>
	public int MaxSize { get; set; }
}
