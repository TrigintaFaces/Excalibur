// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Health;

/// <summary>
/// Unified health check result that consolidates all discovered health models.
/// </summary>
public sealed class UnifiedHealthResult
{
	private readonly long _checkStartTimestamp = DateTimeOffset.UtcNow.Ticks;

	/// <summary>
	/// Gets a value indicating whether the resource is healthy.
	/// </summary>
	/// <value> <see langword="true" /> when health checks passed; otherwise, <see langword="false" />. </value>
	public bool IsHealthy { get; init; }

	/// <summary>
	/// Gets the health status message.
	/// </summary>
	/// <value> The human-readable status message. </value>
	public string? Message { get; init; }

	/// <summary>
	/// Gets the timestamp when the health check was performed (high-performance ticks).
	/// </summary>
	/// <value> The UTC ticks captured when the check ran. </value>
	public long CheckedAtTicks { get; init; } = DateTimeOffset.UtcNow.Ticks;

	/// <summary>
	/// Gets the timestamp when the health check was performed.
	/// </summary>
	/// <value> The UTC timestamp based on <see cref="CheckedAtTicks" />. </value>
	public DateTimeOffset CheckedAt => new(CheckedAtTicks, TimeSpan.Zero);

	/// <summary>
	/// Gets the response time in milliseconds.
	/// </summary>
	/// <value> The response time in milliseconds. </value>
	public double ResponseTimeMs { get; init; }

	/// <summary>
	/// Gets the error message if the health check failed.
	/// </summary>
	/// <value> The error message or <see langword="null" /> when healthy. </value>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets the additional metadata about the health check.
	/// </summary>
	/// <value> The metadata dictionary or <see langword="null" />. </value>
	public IDictionary<string, object>? Metadata { get; init; }

	/// <summary>
	/// Gets the health status as an enumeration value for compatibility with enum-based systems.
	/// </summary>
	/// <value> The mapped <see cref="HealthStatus" />. </value>
	public HealthStatus Status => IsHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;

	/// <summary>
	/// Creates a healthy result.
	/// </summary>
	/// <param name="message"> Optional success message. </param>
	/// <param name="responseTimeMs"> The response time in milliseconds. </param>
	/// <param name="metadata"> Optional metadata. </param>
	/// <returns> A healthy result. </returns>
	public static UnifiedHealthResult Healthy(string? message = null, double responseTimeMs = 0,
		IDictionary<string, object>? metadata = null) => new()
		{
			IsHealthy = true,
			Message = message ?? "Resource is healthy",
			ResponseTimeMs = responseTimeMs,
			Metadata = metadata,
		};

	/// <summary>
	/// Creates an unhealthy result.
	/// </summary>
	/// <param name="message"> Optional error message. </param>
	/// <param name="exception"> Optional exception that caused the failure. </param>
	/// <param name="responseTimeMs"> The response time in milliseconds. </param>
	/// <param name="metadata"> Optional metadata. </param>
	/// <returns> An unhealthy result. </returns>
	public static UnifiedHealthResult Unhealthy(string? message = null, Exception? exception = null,
		double responseTimeMs = 0, IDictionary<string, object>? metadata = null)
	{
		var errorMessage = message ?? exception?.Message ?? "Resource is unhealthy";

		return new UnifiedHealthResult
		{
			IsHealthy = false,
			Message = errorMessage,
			ErrorMessage = exception?.ToString(),
			ResponseTimeMs = responseTimeMs,
			Metadata = metadata,
		};
	}

	/// <summary>
	/// Finishes the health check timing and returns the elapsed time.
	/// </summary>
	/// <returns> The elapsed time in milliseconds. </returns>
	public double FinishTiming()
	{
		var endTicks = DateTimeOffset.UtcNow.Ticks;
		var elapsedTicks = endTicks - _checkStartTimestamp;
		return TimeSpan.FromTicks(elapsedTicks).TotalMilliseconds;
	}
}
