// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Result of a transport health check operation.
/// </summary>
public sealed class TransportHealthCheckResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TransportHealthCheckResult"/> class.
	/// </summary>
	/// <param name="status">The health status.</param>
	/// <param name="description">Description of the health status.</param>
	/// <param name="categories">The categories that were checked.</param>
	/// <param name="duration">Duration of the health check.</param>
	/// <param name="data">Additional health data.</param>
	/// <param name="timestamp">Timestamp of the health check.</param>
	public TransportHealthCheckResult(
		TransportHealthStatus status,
		string description,
		TransportHealthCheckCategory categories,
		TimeSpan duration,
		IReadOnlyDictionary<string, object>? data = null,
		DateTimeOffset? timestamp = null)
	{
		Status = status;
		Description = description ?? throw new ArgumentNullException(nameof(description));
		Categories = categories;
		Duration = duration;
		Data = data ?? new Dictionary<string, object>(StringComparer.Ordinal);
		Timestamp = timestamp ?? DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Gets the health status.
	/// </summary>
	public TransportHealthStatus Status { get; }

	/// <summary>
	/// Gets the description of the health status.
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// Gets the categories that were checked.
	/// </summary>
	public TransportHealthCheckCategory Categories { get; }

	/// <summary>
	/// Gets the duration of the health check.
	/// </summary>
	public TimeSpan Duration { get; }

	/// <summary>
	/// Gets additional health data.
	/// </summary>
	public IReadOnlyDictionary<string, object> Data { get; }

	/// <summary>
	/// Gets the timestamp of the health check.
	/// </summary>
	public DateTimeOffset Timestamp { get; }

	/// <summary>
	/// Gets a value indicating whether the component is healthy.
	/// </summary>
	public bool IsHealthy => Status == TransportHealthStatus.Healthy;

	/// <summary>
	/// Creates a healthy health check result.
	/// </summary>
	/// <param name="description">Description of the healthy status.</param>
	/// <param name="categories">The categories that were checked.</param>
	/// <param name="duration">Duration of the health check.</param>
	/// <param name="data">Additional health data.</param>
	/// <returns>A healthy health check result.</returns>
	public static TransportHealthCheckResult Healthy(
		string description,
		TransportHealthCheckCategory categories,
		TimeSpan duration,
		IReadOnlyDictionary<string, object>? data = null) =>
		new(TransportHealthStatus.Healthy, description, categories, duration, data);

	/// <summary>
	/// Creates a degraded health check result.
	/// </summary>
	/// <param name="description">Description of the degraded status.</param>
	/// <param name="categories">The categories that were checked.</param>
	/// <param name="duration">Duration of the health check.</param>
	/// <param name="data">Additional health data.</param>
	/// <returns>A degraded health check result.</returns>
	public static TransportHealthCheckResult Degraded(
		string description,
		TransportHealthCheckCategory categories,
		TimeSpan duration,
		IReadOnlyDictionary<string, object>? data = null) =>
		new(TransportHealthStatus.Degraded, description, categories, duration, data);

	/// <summary>
	/// Creates an unhealthy health check result.
	/// </summary>
	/// <param name="description">Description of the unhealthy status.</param>
	/// <param name="categories">The categories that were checked.</param>
	/// <param name="duration">Duration of the health check.</param>
	/// <param name="data">Additional health data.</param>
	/// <returns>An unhealthy health check result.</returns>
	public static TransportHealthCheckResult Unhealthy(
		string description,
		TransportHealthCheckCategory categories,
		TimeSpan duration,
		IReadOnlyDictionary<string, object>? data = null) =>
		new(TransportHealthStatus.Unhealthy, description, categories, duration, data);
}
