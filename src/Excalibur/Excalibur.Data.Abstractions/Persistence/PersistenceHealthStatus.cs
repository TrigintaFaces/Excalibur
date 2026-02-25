// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Represents the health status of a persistence provider.
/// </summary>
public sealed class PersistenceHealthStatus
{
	/// <summary>
	/// Gets or sets the name of the provider.
	/// </summary>
	/// <value>The current <see cref="ProviderName"/> value.</value>
	public string ProviderName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the provider is healthy.
	/// </summary>
	/// <value>The current <see cref="IsHealthy"/> value.</value>
	public bool IsHealthy { get; set; }

	/// <summary>
	/// Gets or sets the health status message.
	/// </summary>
	/// <value>The current <see cref="Message"/> value.</value>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the response time in milliseconds.
	/// </summary>
	/// <value>The current <see cref="ResponseTimeMs"/> value.</value>
	public long? ResponseTimeMs { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the health check.
	/// </summary>
	/// <value>The current <see cref="CheckedAt"/> value.</value>
	public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets additional diagnostic data.
	/// </summary>
	/// <value>The current <see cref="Data"/> value.</value>
	public Dictionary<string, object>? Data { get; }
}
