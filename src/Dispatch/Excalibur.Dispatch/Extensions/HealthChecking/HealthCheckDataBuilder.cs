// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Extensions.HealthChecking;

/// <summary>
/// Builder for constructing health check data dictionaries with common patterns.
/// </summary>
public sealed class HealthCheckDataBuilder
{
	private readonly Dictionary<string, object> _data = new(StringComparer.Ordinal);
	private readonly ValueStopwatch _stopwatch;

	/// <summary>
	/// Initializes a new instance of the <see cref="HealthCheckDataBuilder"/> class.
	/// </summary>
	public HealthCheckDataBuilder()
	{
		_stopwatch = ValueStopwatch.StartNew();
	}

	/// <summary>
	/// Gets the elapsed milliseconds since the builder was created.
	/// </summary>
	public long ElapsedMilliseconds => (long)_stopwatch.ElapsedMilliseconds;

	/// <summary>
	/// Adds a key-value pair to the health check data.
	/// </summary>
	/// <param name="key"> The data key. </param>
	/// <param name="value"> The data value. </param>
	/// <returns> The builder for chaining. </returns>
	public HealthCheckDataBuilder Add(string key, object value)
	{
		_data[key] = value;
		return this;
	}

	/// <summary>
	/// Adds response time data.
	/// </summary>
	/// <returns> The builder for chaining. </returns>
	public HealthCheckDataBuilder AddResponseTime()
	{
		_data["ResponseTimeMs"] = (long)_stopwatch.ElapsedMilliseconds;
		return this;
	}

	/// <summary>
	/// Adds exception information to the health check data.
	/// </summary>
	/// <param name="exception"> The exception to record. </param>
	/// <returns> The builder for chaining. </returns>
	public HealthCheckDataBuilder AddException(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		_data["Exception"] = exception.GetType().Name;
		_data["ExceptionMessage"] = exception.Message;
		return this;
	}

	/// <summary>
	/// Stops the internal stopwatch and records the response time.
	/// </summary>
	/// <returns> The builder for chaining. </returns>
	public HealthCheckDataBuilder Stop()
	{
		return AddResponseTime();
	}

	/// <summary>
	/// Builds the health check data dictionary.
	/// </summary>
	/// <returns> The constructed data dictionary. </returns>
	public IReadOnlyDictionary<string, object> Build()
	{
		return _data;
	}

	/// <summary>
	/// Creates a healthy result with the collected data.
	/// </summary>
	/// <param name="description"> The health check description. </param>
	/// <returns> A healthy health check result. </returns>
	public HealthCheckResult Healthy(string? description = null)
	{
		_ = Stop();
		return HealthCheckResult.Healthy(description, _data);
	}

	/// <summary>
	/// Creates an unhealthy result with the collected data.
	/// </summary>
	/// <param name="description"> The health check description. </param>
	/// <param name="exception"> Optional exception that caused the unhealthy state. </param>
	/// <returns> An unhealthy health check result. </returns>
	public HealthCheckResult Unhealthy(string? description = null, Exception? exception = null)
	{
		_ = Stop();
		if (exception != null)
		{
			_ = AddException(exception);
		}

		return HealthCheckResult.Unhealthy(description, exception, _data);
	}

	/// <summary>
	/// Creates a degraded result with the collected data.
	/// </summary>
	/// <param name="description"> The health check description. </param>
	/// <param name="exception"> Optional exception that caused the degraded state. </param>
	/// <returns> A degraded health check result. </returns>
	public HealthCheckResult Degraded(string? description = null, Exception? exception = null)
	{
		_ = Stop();
		if (exception != null)
		{
			_ = AddException(exception);
		}

		return HealthCheckResult.Degraded(description, exception, _data);
	}
}
