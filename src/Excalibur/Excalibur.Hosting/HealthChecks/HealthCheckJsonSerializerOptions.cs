// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

using Excalibur.Dispatch.Options.Serialization;

namespace Excalibur.Hosting.HealthChecks;

/// <summary>
/// Provides a centralized configuration for JSON serialization settings specific to health checks.
/// </summary>
internal static class HealthCheckJsonSerializerOptions
{
	/// <summary>
	/// A lazily initialized default <see cref="JsonSerializerOptions" /> instance for health check serialization.
	/// </summary>
	private static readonly Lazy<JsonSerializerOptions> DefaultSettings = new(static () =>
	{
		var options = DispatchJsonSerializerOptions.Web;

		// Add custom converters for health report serialization.
		options.Converters.Add(new HealthReportEntryJsonConverter());
		options.Converters.Add(new HealthReportJsonConverter());

		return options;
	});

	/// <summary>
	/// Gets the default JSON serializer options configured for health checks.
	/// </summary>
	/// <value> The default JSON serializer options. </value>
	public static JsonSerializerOptions Default => DefaultSettings.Value;
}
