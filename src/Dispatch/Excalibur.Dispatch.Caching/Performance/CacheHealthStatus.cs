// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Represents cache health status.
/// </summary>
public sealed class CacheHealthStatus
{
	/// <summary>
	/// Gets a value indicating whether the cache is healthy.
	/// </summary>
	/// <value><see langword="true"/> if the cache is healthy; otherwise, <see langword="false"/>.</value>
	public bool IsHealthy { get; init; }

	/// <summary>
	/// Gets the response time in milliseconds.
	/// </summary>
	/// <value>The response time in milliseconds.</value>
	public double ResponseTimeMs { get; init; }

	/// <summary>
	/// Gets the connection status description.
	/// </summary>
	/// <value>The connection status description.</value>
	public string ConnectionStatus { get; init; } = string.Empty;

	/// <summary>
	/// Gets the timestamp when the health check was performed.
	/// </summary>
	/// <value>The timestamp when the health check was performed.</value>
	public DateTimeOffset LastChecked { get; init; }
}
