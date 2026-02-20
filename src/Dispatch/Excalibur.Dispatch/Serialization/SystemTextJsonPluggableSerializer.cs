// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// System.Text.Json implementation of <see cref="IPluggableSerializer"/> for pluggable serialization.
/// </summary>
/// <remarks>
/// <para>
/// This serializer provides human-readable JSON output, making it ideal for debugging,
/// development, and scenarios where payload inspection is required.
/// </para>
/// <para>
/// <b>Key Features:</b>
/// </para>
/// <list type="bullet">
///   <item>Human-readable JSON output</item>
///   <item>Configurable serialization options</item>
///   <item>Good for debugging and development</item>
///   <item>Built-in to .NET (no additional dependencies)</item>
/// </list>
/// <para>
/// <b>Serializer ID:</b> <see cref="SerializerIds.SystemTextJson"/> (2)
/// </para>
/// <para>
/// <b>Performance Note:</b> This serializer is slower than MemoryPack and MessagePack
/// but provides readable output. Use MemoryPack for production performance-critical scenarios.
/// </para>
/// <para>
/// See the pluggable serialization architecture documentation.
/// </para>
/// </remarks>
public sealed class SystemTextJsonPluggableSerializer : IPluggableSerializer
{
	private readonly JsonSerializerOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SystemTextJsonPluggableSerializer"/> class
	/// with default options.
	/// </summary>
	/// <remarks>
	/// Default options use camelCase property naming and compact (non-indented) output
	/// to minimize storage size while maintaining JSON compatibility.
	/// </remarks>
	public SystemTextJsonPluggableSerializer()
		: this(null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SystemTextJsonPluggableSerializer"/> class
	/// with custom options.
	/// </summary>
	/// <param name="options">
	/// Custom JSON serializer options. If null, default options are used.
	/// </param>
	public SystemTextJsonPluggableSerializer(JsonSerializerOptions? options)
	{
		_options = options ?? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false // Compact for storage
		};
	}

	/// <inheritdoc />
	/// <value>Returns "System.Text.Json".</value>
	public string Name => "System.Text.Json";

	/// <inheritdoc />
	/// <value>Returns the version of the System.Text.Json library assembly.</value>
	public string Version => typeof(JsonSerializer).Assembly
		.GetName().Version?.ToString() ?? "Unknown";

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	[RequiresUnreferencedCode(
		"JSON serialization may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode(
		"JSON serialization may require runtime code generation.")]
	public byte[] Serialize<T>(T value)
	{
		ArgumentNullException.ThrowIfNull(value);

		try
		{
			return JsonSerializer.SerializeToUtf8Bytes(value, _options);
		}
		catch (Exception ex) when (ex is not ArgumentNullException)
		{
			throw SerializationException.Wrap<T>("serialize", ex);
		}
	}

	/// <inheritdoc />
	/// <exception cref="SerializationException">Thrown when deserialization fails or returns null.</exception>
	[RequiresUnreferencedCode(
		"JSON deserialization may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode(
		"JSON deserialization may require runtime code generation.")]
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		try
		{
			return JsonSerializer.Deserialize<T>(data, _options)
				?? throw SerializationException.NullResult<T>();
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.Wrap<T>("deserialize", ex);
		}
	}

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> or <paramref name="type"/> is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	[RequiresUnreferencedCode(
		"JSON serialization may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode(
		"JSON serialization may require runtime code generation.")]
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			// System.Text.Json supports runtime type serialization natively
			return JsonSerializer.SerializeToUtf8Bytes(value, type, _options);
		}
		catch (Exception ex) when (ex is not ArgumentNullException)
		{
			throw SerializationException.WrapObject(type, "serialize", ex);
		}
	}

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
	/// <exception cref="SerializationException">Thrown when deserialization fails or returns null.</exception>
	[RequiresUnreferencedCode(
		"JSON deserialization may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode(
		"JSON deserialization may require runtime code generation.")]
	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			// System.Text.Json supports runtime type deserialization natively
			return JsonSerializer.Deserialize(data, type, _options)
				?? throw SerializationException.NullResultForType(type);
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.WrapObject(type, "deserialize", ex);
		}
	}
}
