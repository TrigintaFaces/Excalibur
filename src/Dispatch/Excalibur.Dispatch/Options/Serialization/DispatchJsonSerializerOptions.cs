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
	/// <value>
	/// A <see cref="JsonSerializerOptions" /> instance using <see cref="JsonSerializerDefaults.General" />, so
	/// property names round-trip with the casing they are declared with. Use <see cref="Web" /> for the
	/// camel-cased, case-insensitive behaviour that web payloads usually expect.
	/// </value>
	public static JsonSerializerOptions Default => DefaultSettings.Value;

	/// <summary>
	/// Gets the default <see cref="JsonSerializerOptions" /> configured for web-based JSON serialization.
	/// </summary>
	/// <value> A <see cref="JsonSerializerOptions" /> instance using <see cref="JsonSerializerDefaults.Web" />. </value>
	/// <remarks>
	/// Enums serialize as numbers here, matching <see cref="JsonSerializerDefaults.Web" />. Call
	/// <see cref="ApplyDefaultsWithStringEnums" /> on your own instance for camel-cased enum names;
	/// that converter is built at run time, so those options cannot be produced ahead of time.
	/// </remarks>
	public static JsonSerializerOptions Web
	{
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
	/// <remarks>
	/// Enums are left to serialize as numbers, which is what <see cref="JsonSerializerDefaults.Web" />
	/// itself does. Call <see cref="ApplyDefaultsWithStringEnums" /> instead if you want them written as
	/// camel-cased names, and see the note there about what that costs.
	/// </remarks>
	public static JsonSerializerOptions ApplyDefaults(JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Configure property naming and handling settings.
		options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
		options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
		options.WriteIndented = true;

		return options;
	}

	/// <summary>
	/// Applies the standard settings and writes enums as camel-cased names rather than numbers.
	/// </summary>
	/// <param name="options"> The <see cref="JsonSerializerOptions" /> instance to configure. </param>
	/// <returns> The configured <see cref="JsonSerializerOptions" /> instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="options" /> is <c> null </c>. </exception>
	/// <remarks>
	/// The converter this adds builds a converter per enum type on first use, so an application that
	/// compiles ahead-of-time cannot use these options. Name the enums individually with
	/// <see cref="JsonStringEnumConverter{TEnum}" />, or supply a <see cref="JsonSerializerContext" />,
	/// if you need both string enums and ahead-of-time compilation.
	/// </remarks>
	[RequiresDynamicCode(
		"Writing enums as names uses a converter built per enum type at run time, which native AOT does not support. Use ApplyDefaults, which writes enums as numbers, or name each enum with JsonStringEnumConverter<TEnum> or supply a JsonSerializerContext.")]
	public static JsonSerializerOptions ApplyDefaultsWithStringEnums(JsonSerializerOptions options)
	{
		_ = ApplyDefaults(options);
		options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

		return options;
	}
}
