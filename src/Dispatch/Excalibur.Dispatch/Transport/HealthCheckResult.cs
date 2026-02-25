// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents the result of a health check operation for a transport adapter.
/// </summary>
public sealed class HealthCheckResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the adapter is healthy.
	/// </summary>
	/// <value>The current <see cref="IsHealthy"/> value.</value>
	public bool IsHealthy { get; set; }

	/// <summary>
	/// Gets or sets a description of the health check result.
	/// </summary>
	/// <value>The current <see cref="Description"/> value.</value>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timestamp when the health check was performed.
	/// </summary>
	/// <value>The current <see cref="CheckTimestamp"/> value.</value>
	public DateTime CheckTimestamp { get; set; }

	/// <summary>
	/// Gets or sets any exception that occurred during the health check.
	/// </summary>
	/// <value>The current <see cref="Exception"/> value.</value>
	public Exception? Exception { get; set; }

	/// <summary>
	/// Creates a healthy result.
	/// </summary>
	/// <param name="description"> Optional description of the healthy state. </param>
	/// <returns> A new healthy HealthCheckResult. </returns>
	public static HealthCheckResult Healthy(string description = "Healthy") =>
		new() { IsHealthy = true, Description = description, CheckTimestamp = CreateTimestamp(), };

	/// <summary>
	/// Creates an unhealthy result.
	/// </summary>
	/// <param name="description"> Description of the unhealthy state. </param>
	/// <param name="exception"> Optional exception that caused the unhealthy state. </param>
	/// <returns> A new unhealthy HealthCheckResult. </returns>
	public static HealthCheckResult Unhealthy(string description, Exception? exception = null) =>
		new()
		{
			IsHealthy = false,
			Description = description,
			Exception = exception,
			CheckTimestamp = CreateTimestamp(),
		};

	/// <summary>
	/// Creates a high-performance timestamp using ValueStopwatch.
	/// </summary>
	private static DateTime CreateTimestamp()
	{
		var perfTimestamp = ValueStopwatch.GetTimestamp();
		var elapsedTicks = (long)(perfTimestamp * 10_000_000.0 / ValueStopwatch.GetFrequency());
		var baseDateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		return new DateTime(baseDateTime.Ticks + elapsedTicks, DateTimeKind.Utc);
	}
}
