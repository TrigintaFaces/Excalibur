using System.Text.Json;

using Excalibur.Data.Serialization;

namespace Excalibur.Hosting.HealthChecks;

/// <summary>
///     Provides a centralized configuration for JSON serialization settings specific to health checks.
/// </summary>
internal static class HealthCheckJsonSerializerSettings
{
	/// <summary>
	///     A lazily initialized default <see cref="JsonSerializerOptions" /> instance for health check serialization.
	/// </summary>
	private static readonly Lazy<JsonSerializerOptions> DefaultSettings = new(() =>
	{
		var options = ExcaliburJsonSerializerOptions.Web;

		// Add custom converters for health report serialization.
		options.Converters.Add(new HealthReportEntryJsonConverter());
		options.Converters.Add(new HealthReportJsonConverter());

		return options;
	});

	/// <summary>
	///     Gets the default JSON serializer options configured for health checks.
	/// </summary>
	public static JsonSerializerOptions Default => DefaultSettings.Value;
}
