// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Options.Serialization;

/// <summary>
/// Provides predefined <see cref="JsonSerializerOptions" /> configurations for use in the application.
/// </summary>
/// <remarks>
/// This class offers centralized configurations for JSON serialization, enabling consistent settings across the application. These options
/// include settings for general web-based JSON serialization and custom configurations for specific use cases.
/// </remarks>
public static class DispatchJsonSerializerOptions
{
	/// <summary>
	/// A lazily initialized default <see cref="JsonSerializerOptions" /> instance configured with <see cref="JsonSerializerDefaults.General" />.
	/// </summary>
	private static readonly Lazy<JsonSerializerOptions> DefaultSettings =
		new(static () => new JsonSerializerOptions(JsonSerializerDefaults.General));

	/// <summary>
	/// A lazily initialized web <see cref="JsonSerializerOptions" /> instance configured with <see cref="JsonSerializerDefaults.Web" />.
	/// </summary>
	[UnconditionalSuppressMessage(
			"AotAnalysis",
			"IL3050:RequiresDynamicCode",
			Justification = "Default JSON options are configured at startup and are not used in AOT scenarios.")]
	private static readonly Lazy<JsonSerializerOptions> WebSettings = new(static () =>
			ApplyDefaults(new JsonSerializerOptions(JsonSerializerDefaults.Web)));

	/// <summary>
	/// Gets the default <see cref="JsonSerializerOptions" /> configured for JSON serialization.
	/// </summary>
	/// <value> A <see cref="JsonSerializerOptions" /> instance using <see cref="JsonSerializerDefaults.Web" />. </value>
	public static JsonSerializerOptions Default => DefaultSettings.Value;

	/// <summary>
	/// Gets the default <see cref="JsonSerializerOptions" /> configured for web-based JSON serialization.
	/// </summary>
	/// <value> A <see cref="JsonSerializerOptions" /> instance using <see cref="JsonSerializerDefaults.Web" />. </value>
	public static JsonSerializerOptions Web => WebSettings.Value;

	/// <summary>
	/// Applies standard settings to the provided <see cref="JsonSerializerOptions" /> instance.
	/// </summary>
	/// <param name="options"> The <see cref="JsonSerializerOptions" /> instance to configure. </param>
	/// <returns> The configured <see cref="JsonSerializerOptions" /> instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="options" /> is <c> null </c>. </exception>
	[RequiresDynamicCode(
		"JSON serializer options configuration with converters requires dynamic code generation for enum and type conversion.")]
	public static JsonSerializerOptions ApplyDefaults(JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Configure property naming and handling settings.
		options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
		options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
		options.WriteIndented = true;

		// Add custom converters for enums and other data types.
		options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

		return options;
	}
}
