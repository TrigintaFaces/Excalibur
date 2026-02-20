// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Tests.Shared.TestTypes;

/// <summary>
/// JSON message serializer for test scenarios.
/// Implements both IJsonSerializer and IMessageSerializer.
/// </summary>
public sealed class JsonMessageSerializer : IJsonSerializer, IMessageSerializer
{
	private static readonly JsonSerializerOptions DefaultOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
		PropertyNameCaseInsensitive = true,
	};

	/// <inheritdoc/>
	public string SerializerName => "SystemTextJson";

	/// <inheritdoc/>
	public string SerializerVersion => "1.0";

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	public string Serialize(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);
		return JsonSerializer.Serialize(value, type, DefaultOptions);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	public object? Deserialize(string json, Type type)
	{
		ArgumentNullException.ThrowIfNull(json);
		ArgumentNullException.ThrowIfNull(type);
		return JsonSerializer.Deserialize(json, type, DefaultOptions);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization with runtime type requires dynamic code generation")]
	public Task<string> SerializeAsync(object value, Type type)
	{
		return Task.FromResult(Serialize(value, type));
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON deserialization with runtime type may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization with runtime type requires dynamic code generation")]
	public Task<object?> DeserializeAsync(string json, Type type)
	{
		return Task.FromResult(Deserialize(json, type));
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization may require unreferenced code")]
	[RequiresDynamicCode("JSON serialization requires dynamic code generation")]
	public byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message)
	{
		ArgumentNullException.ThrowIfNull(message);
		return JsonSerializer.SerializeToUtf8Bytes(message, DefaultOptions);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON deserialization may require unreferenced code")]
	[RequiresDynamicCode("JSON deserialization requires dynamic code generation")]
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);
		return JsonSerializer.Deserialize<T>(data, DefaultOptions)
			?? throw new InvalidOperationException($"Failed to deserialize to type {typeof(T).Name}");
	}
}
