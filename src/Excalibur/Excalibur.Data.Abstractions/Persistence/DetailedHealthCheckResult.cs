// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Represents a detailed health check result with additional metrics.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DetailedHealthCheckResult" /> class.
/// </remarks>
/// <param name="status"> The health status. </param>
/// <param name="description"> The description of the health check result. </param>
/// <param name="exception"> The exception that occurred during the health check, if any. </param>
/// <param name="data"> Additional data about the health check. </param>
/// <param name="responseTime"> The response time of the health check. </param>
/// <param name="metrics"> Additional performance metrics. </param>
public sealed class DetailedHealthCheckResult(
	HealthStatus status,
	string? description = null,
	Exception? exception = null,
	IReadOnlyDictionary<string, object>? data = null,
	TimeSpan? responseTime = null,
	IDictionary<string, double>? metrics = null)
{
	/// <summary>
	/// Gets the health status.
	/// </summary>
	/// <value>The current <see cref="Status"/> value.</value>
	public HealthStatus Status { get; } = status;

	/// <summary>
	/// Gets the description of the health check result.
	/// </summary>
	/// <value>The current <see cref="Description"/> value.</value>
	public string? Description { get; } = description;

	/// <summary>
	/// Gets the exception that occurred during the health check, if any.
	/// </summary>
	/// <value>The current <see cref="Exception"/> value.</value>
	public Exception? Exception { get; } = exception;

	/// <summary>
	/// Gets additional data about the health check.
	/// </summary>
	/// <value>
	/// Additional data about the health check.
	/// </value>
	public IReadOnlyDictionary<string, object> Data { get; } = data ?? new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the response time of the health check.
	/// </summary>
	/// <value>The current <see cref="ResponseTime"/> value.</value>
	public TimeSpan? ResponseTime { get; } = responseTime;

	/// <summary>
	/// Gets additional performance metrics.
	/// </summary>
	/// <value>
	/// Additional performance metrics.
	/// </value>
	public IDictionary<string, double> Metrics { get; } = metrics ?? new Dictionary<string, double>(StringComparer.Ordinal);

	/// <summary>
	/// Converts this detailed result to a standard HealthCheckResult.
	/// </summary>
	/// <returns> A standard HealthCheckResult. </returns>
	public HealthCheckResult ToHealthCheckResult() => new(Status, Description, Exception, Data);
}
