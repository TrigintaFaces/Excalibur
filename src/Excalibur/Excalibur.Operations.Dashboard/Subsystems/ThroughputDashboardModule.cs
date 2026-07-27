// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Operations.Dashboard.Throughput;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard;

/// <summary>A read-only throughput rate for a single subsystem, derived from its OpenTelemetry counters.</summary>
internal sealed class ThroughputView
{
	/// <summary>Gets the subsystem this reading is for (for example <c>outbox</c>).</summary>
	/// <value>The subsystem key.</value>
	public string Subsystem { get; init; } = string.Empty;

	/// <summary>Gets a value indicating whether throughput is tracked for this subsystem.</summary>
	/// <value><see langword="true"/> when the subsystem's meters are bridged; otherwise <see langword="false"/>.</value>
	public bool Configured { get; init; }

	/// <summary>Gets the measured events-per-second since the previous read.</summary>
	/// <value>The per-second rate; <c>0</c> on the first read or when not configured.</value>
	public double RatePerSecond { get; init; }

	/// <summary>Gets the window the rate was measured over, in seconds.</summary>
	/// <value>The elapsed window; <c>0</c> on the first read.</value>
	public double WindowSeconds { get; init; }

	/// <summary>Gets the UTC instant this reading was captured.</summary>
	/// <value>The capture timestamp.</value>
	public DateTimeOffset CapturedAt { get; init; }
}

/// <summary>
/// Maps the per-subsystem throughput endpoint <c>/{subsystem}/throughput</c>. Fails open: an unknown or
/// unconfigured subsystem returns a not-configured/zero-rate payload rather than a 500.
/// </summary>
internal sealed class ThroughputDashboardModule : IDashboardEndpointModule
{
	/// <inheritdoc />
	public string Subsystem => "throughput";

	/// <inheritdoc />
	public void MapEndpoints(RouteGroupBuilder group, DashboardOptions options)
	{
		ArgumentNullException.ThrowIfNull(group);
		ArgumentNullException.ThrowIfNull(options);

		group.MapGet("/{subsystem}/throughput", static (string subsystem, ThroughputCollector? collector) =>
		{
			if (collector is null || !ThroughputCollector.IsKnownSubsystem(subsystem))
			{
				return Results.Json(
					new ThroughputView { Subsystem = subsystem, Configured = false, CapturedAt = DateTimeOffset.UtcNow },
					ThroughputJsonContext.Default.ThroughputView);
			}

			var reading = collector.Read(subsystem);
			return Results.Json(
				new ThroughputView
				{
					Subsystem = subsystem,
					Configured = true,
					RatePerSecond = reading.RatePerSecond,
					WindowSeconds = reading.WindowSeconds,
					CapturedAt = DateTimeOffset.UtcNow,
				},
				ThroughputJsonContext.Default.ThroughputView);
		});
	}
}

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ThroughputView))]
internal sealed partial class ThroughputJsonContext : JsonSerializerContext;
