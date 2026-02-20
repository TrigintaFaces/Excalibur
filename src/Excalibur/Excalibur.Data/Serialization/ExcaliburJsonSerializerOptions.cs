// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Data.Serialization;

/// <summary>
/// Provides predefined <see cref="JsonSerializerOptions" /> configurations for use with System.Text.Json.
/// </summary>
/// <remarks>
/// <para>
/// This class centralizes JSON serialization settings, offering consistency and reducing redundancy across the application. The provided
/// configurations include settings for ignoring streams during serialization and applying default formatting conventions.
/// </para>
/// <para>
/// This class uses the non-generic <see cref="JsonStringEnumConverter"/> which is not compatible with Native AOT.
/// For AOT scenarios, use <c>JsonStringEnumConverter&lt;TEnum&gt;</c> on individual enum types or
/// configure enum handling via <see cref="JsonSerializerContext"/> source generation instead.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("Uses JsonStringEnumConverter which requires unreferenced code for enum type handling. Use JsonStringEnumConverter<TEnum> on individual enums for AOT.")]
[RequiresDynamicCode("Uses JsonStringEnumConverter which requires dynamic code generation. Use JsonStringEnumConverter<TEnum> on individual enums for AOT.")]
public static class ExcaliburJsonSerializerOptions
{
	/// <summary>
	/// Gets a <see cref="JsonSerializerOptions" /> instance configured to ignore stream properties during serialization.
	/// </summary>
	/// <value> A <see cref="JsonSerializerOptions" /> instance that applies custom logic to exclude stream properties from serialization. </value>
	public static JsonSerializerOptions IgnoreStream { get; } = CreateIgnoreStreamOptions();

	/// <summary>
	/// Gets the default <see cref="JsonSerializerOptions" /> instance configured with standard serialization conventions.
	/// </summary>
	/// <value>
	/// A <see cref="JsonSerializerOptions" /> instance with applied default settings, including camelCase property names, ignoring null
	/// values, and using indented formatting.
	/// </value>
	public static JsonSerializerOptions Default { get; } = CreateDefaultOptions();

	/// <summary>
	/// Creates a <see cref="JsonSerializerOptions" /> instance configured to ignore stream properties during serialization.
	/// </summary>
	/// <returns> The configured <see cref="JsonSerializerOptions" /> instance. </returns>
	private static JsonSerializerOptions CreateIgnoreStreamOptions()
	{
		var options = CreateDefaultOptions();
		options.Converters.Add(new IgnoreStreamJsonConverter());
		return options;
	}

	/// <summary>
	/// Creates default serialization settings as a <see cref="JsonSerializerOptions" /> instance.
	/// </summary>
	/// <returns> The configured <see cref="JsonSerializerOptions" /> instance. </returns>
	/// <remarks>
	/// The default settings include:
	/// <list type="bullet">
	/// <item> Indented formatting for readability. </item>
	/// <item> Ignoring null values during serialization. </item>
	/// <item> Applying camelCase naming to properties. </item>
	/// <item> Using string converters for serializing enums as strings. </item>
	/// </list>
	/// </remarks>
	private static JsonSerializerOptions CreateDefaultOptions() =>
		new()
		{
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter() },
		};
}
