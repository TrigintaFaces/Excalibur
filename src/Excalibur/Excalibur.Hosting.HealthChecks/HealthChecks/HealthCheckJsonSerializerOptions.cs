// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Options.Serialization;

namespace Excalibur.Hosting.HealthChecks;

/// <summary>
/// Provides a centralized configuration for JSON serialization settings specific to health checks.
/// </summary>
internal static class HealthCheckJsonSerializerOptions
{
	/// <summary>
	/// Backing store for <see cref="Default" />, populated on first access.
	/// </summary>
	private static JsonSerializerOptions? _defaultSettings;

	/// <summary>
	/// Gets the default JSON serializer options configured for health checks.
	/// </summary>
	/// <value> The default JSON serializer options. </value>
	/// <remarks>
	/// Built on first access rather than in a field initializer: these options derive from
	/// <see cref="DispatchJsonSerializerOptions.Web" />, which requires dynamic code, and a class
	/// constructor cannot declare that requirement -- it would silently hide it instead.
	/// </remarks>
	public static JsonSerializerOptions Default
	{
		[RequiresDynamicCode(
			"Derives from the shared web options, which add a string-enum converter built at run time.")]
		get
		{
			var existing = Volatile.Read(ref _defaultSettings);
			if (existing is not null)
			{
				return existing;
			}

			// Copy, never mutate the shared instance: DispatchJsonSerializerOptions.Web is a process-wide
			// singleton, and JsonSerializerOptions becomes read-only on first use. Adding converters to it
			// would both leak health-check converters into every other consumer and throw
			// InvalidOperationException if anything had serialized with it first.
			var created = new JsonSerializerOptions(DispatchJsonSerializerOptions.Web);

			// Add custom converters for health report serialization.
			created.Converters.Add(new HealthReportEntryJsonConverter());
			created.Converters.Add(new HealthReportJsonConverter());

			return Interlocked.CompareExchange(ref _defaultSettings, created, null) ?? created;
		}
	}
}
