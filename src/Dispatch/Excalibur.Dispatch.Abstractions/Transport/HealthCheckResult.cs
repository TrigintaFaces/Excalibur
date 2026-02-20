// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Health check status enumeration.
/// </summary>
public enum HealthCheckStatus
{
	/// <summary>
	/// The component is healthy and operating normally.
	/// </summary>
	Healthy = 0,

	/// <summary>
	/// The component is degraded but still functional.
	/// </summary>
	Degraded = 1,

	/// <summary>
	/// The component is unhealthy and not functioning properly.
	/// </summary>
	Unhealthy = 2,
}

/// <summary>
/// Result of a message bus adapter health check operation.
/// </summary>
public sealed class HealthCheckResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="HealthCheckResult"/> class.
	/// </summary>
	/// <param name="isHealthy">Whether the adapter is healthy.</param>
	/// <param name="description">Optional description of the health status.</param>
	/// <param name="data">Optional additional health data.</param>
	public HealthCheckResult(bool isHealthy, string? description = null, IReadOnlyDictionary<string, object>? data = null)
	{
		IsHealthy = isHealthy;
		Status = isHealthy ? HealthCheckStatus.Healthy : HealthCheckStatus.Unhealthy;
		Description = description ?? (isHealthy ? "Healthy" : "Unhealthy");
		Data = data ?? new Dictionary<string, object>(StringComparer.Ordinal);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="HealthCheckResult"/> class with a status.
	/// </summary>
	/// <param name="status">The health status.</param>
	/// <param name="description">Description of the health status.</param>
	public HealthCheckResult(HealthCheckStatus status, string description)
	{
		Status = status;
		IsHealthy = status == HealthCheckStatus.Healthy;
		Description = description ?? throw new ArgumentNullException(nameof(description));
		Data = new Dictionary<string, object>(StringComparer.Ordinal);
	}

	/// <summary>
	/// Gets a value indicating whether the adapter is healthy.
	/// </summary>
	public bool IsHealthy { get; }

	/// <summary>
	/// Gets the health status.
	/// </summary>
	public HealthCheckStatus Status { get; }

	/// <summary>
	/// Gets the description of the health status.
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// Gets additional health data.
	/// </summary>
	public IReadOnlyDictionary<string, object> Data { get; }

	/// <summary>
	/// Creates a healthy result.
	/// </summary>
	/// <param name="description">Optional description.</param>
	/// <param name="data">Optional additional data.</param>
	/// <returns>A healthy health check result.</returns>
	public static HealthCheckResult Healthy(string? description = null, IReadOnlyDictionary<string, object>? data = null) =>
		new(isHealthy: true, description: description, data: data);

	/// <summary>
	/// Creates a degraded result.
	/// </summary>
	/// <param name="description">Description of the degraded status.</param>
	/// <returns>A degraded health check result.</returns>
	public static HealthCheckResult Degraded(string description) =>
		new(HealthCheckStatus.Degraded, description);

	/// <summary>
	/// Creates an unhealthy result.
	/// </summary>
	/// <param name="description">Description of the unhealthy status.</param>
	/// <returns>An unhealthy health check result.</returns>
	public static HealthCheckResult Unhealthy(string description) =>
		new(HealthCheckStatus.Unhealthy, description);
}
