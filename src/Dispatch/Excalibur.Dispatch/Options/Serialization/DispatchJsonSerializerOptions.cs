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
	/// Backing store for <see cref="Web" />, populated on first access.
	/// </summary>
	private static JsonSerializerOptions? _webSettings;

	/// <summary>
	/// Gets the default <see cref="JsonSerializerOptions" /> configured for JSON serialization.
	/// </summary>
	/// <value> A <see cref="JsonSerializerOptions" /> instance using <see cref="JsonSerializerDefaults.Web" />. </value>
	public static JsonSerializerOptions Default => DefaultSettings.Value;

	/// <summary>
	/// Gets the default <see cref="JsonSerializerOptions" /> configured for web-based JSON serialization.
	/// </summary>
	/// <value> A <see cref="JsonSerializerOptions" /> instance using <see cref="JsonSerializerDefaults.Web" />. </value>
	/// <remarks>
	/// These options include a string-enum converter that is built at run time, so they cannot be
	/// produced ahead of time. Callers that publish ahead-of-time should build their own options from a
	/// <see cref="JsonSerializerContext" /> instead, or apply
	/// <see cref="JsonStringEnumConverter{TEnum}" /> to the specific enums they serialize.
	/// The value is created on first access rather than in a field initializer, because a class
	/// constructor cannot declare the dynamic-code requirement and would therefore hide it.
	/// </remarks>
	public static JsonSerializerOptions Web
	{
		[RequiresDynamicCode(
			"The shared web options add a string-enum converter that is constructed at run time.")]
		get
		{
			var existing = Volatile.Read(ref _webSettings);
			if (existing is not null)
			{
				return existing;
			}

			var created = ApplyDefaults(new JsonSerializerOptions(JsonSerializerDefaults.Web));
			return Interlocked.CompareExchange(ref _webSettings, created, null) ?? created;
		}
	}

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
