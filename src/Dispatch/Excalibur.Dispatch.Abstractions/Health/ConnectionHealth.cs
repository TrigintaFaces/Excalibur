// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Abstractions.Health;

/// <summary>
/// Represents the health status of a connection or resource.
/// </summary>
public sealed class ConnectionHealth
{
	private ValueStopwatch _checkStopwatch;

	/// <summary>
	/// Gets or sets a value indicating whether the connection is healthy.
	/// </summary>
	/// <value> The current <see cref="IsHealthy" /> value. </value>
	public bool IsHealthy { get; set; }

	/// <summary>
	/// Gets or sets the health check timestamp ticks for high-performance scenarios.
	/// </summary>
	/// <value> The current <see cref="CheckedAtTicks" /> value. </value>
	public long CheckedAtTicks { get; set; }

	/// <summary>
	/// Gets the health check timestamp as DateTime for compatibility.
	/// </summary>
	public DateTime CheckedAt => new(CheckedAtTicks, DateTimeKind.Utc);

	/// <summary>
	/// Gets or sets the response time in milliseconds.
	/// </summary>
	/// <value> The current <see cref="ResponseTimeMs" /> value. </value>
	public double ResponseTimeMs { get; set; }

	/// <summary>
	/// Gets or sets an optional error message if the health check failed.
	/// </summary>
	/// <value> The current <see cref="ErrorMessage" /> value. </value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets additional metadata about the health check.
	/// </summary>
	/// <value> The current <see cref="Metadata" /> value. </value>
	public IDictionary<string, object>? Metadata { get; init; }

	/// <summary>
	/// Creates a healthy status.
	/// </summary>
	/// <param name="responseTimeMs"> The response time in milliseconds. </param>
	/// <returns> A healthy ConnectionHealth instance. </returns>
	public static ConnectionHealth Healthy(double responseTimeMs = 0) =>
		new()
		{
			IsHealthy = true,
			CheckedAtTicks = DateTimeOffset.UtcNow.Ticks,
			ResponseTimeMs = responseTimeMs,
			_checkStopwatch = ValueStopwatch.StartNew(),
		};

	/// <summary>
	/// Creates an unhealthy status.
	/// </summary>
	/// <param name="errorMessage"> The error message. </param>
	/// <param name="responseTimeMs"> The response time in milliseconds. </param>
	/// <returns> An unhealthy ConnectionHealth instance. </returns>
	public static ConnectionHealth Unhealthy(string errorMessage, double responseTimeMs = 0) =>
		new()
		{
			IsHealthy = false,
			CheckedAtTicks = DateTimeOffset.UtcNow.Ticks,
			ResponseTimeMs = responseTimeMs,
			ErrorMessage = errorMessage,
			_checkStopwatch = ValueStopwatch.StartNew(),
		};

	/// <summary>
	/// Starts a health check measurement.
	/// </summary>
	/// <returns> A new ConnectionHealth instance with timing started. </returns>
	public static ConnectionHealth StartHealthCheck() =>
		new()
		{
			_checkStopwatch = ValueStopwatch.StartNew(),
			CheckedAtTicks = DateTimeOffset.UtcNow.Ticks,
		};

	/// <summary>
	/// Completes the health check with success.
	/// </summary>
	public void CompleteAsHealthy()
	{
		IsHealthy = true;
		if (_checkStopwatch.IsActive)
		{
			ResponseTimeMs = _checkStopwatch.ElapsedMilliseconds;
		}
	}

	/// <summary>
	/// Completes the health check with failure.
	/// </summary>
	/// <param name="errorMessage"> The error message. </param>
	public void CompleteAsUnhealthy(string errorMessage)
	{
		IsHealthy = false;
		ErrorMessage = errorMessage;
		if (_checkStopwatch.IsActive)
		{
			ResponseTimeMs = _checkStopwatch.ElapsedMilliseconds;
		}
	}
}
