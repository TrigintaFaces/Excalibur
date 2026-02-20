// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;

namespace CdcJobQuartz.Infrastructure;

/// <summary>
/// Serializes and deserializes domain events.
/// </summary>
public interface IEventSerializer
{
	/// <summary>
	/// Serializes an event to bytes.
	/// </summary>
	byte[] Serialize<T>(T @event) where T : IDomainEvent;

	/// <summary>
	/// Deserializes an event from bytes.
	/// </summary>
	T? Deserialize<T>(byte[] data) where T : IDomainEvent;
}

/// <summary>
/// JSON-based event serializer.
/// </summary>
public sealed class JsonEventSerializer : IEventSerializer
{
	private readonly JsonSerializerOptions _options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
	};

	/// <inheritdoc/>
	public byte[] Serialize<T>(T @event) where T : IDomainEvent
	{
		return JsonSerializer.SerializeToUtf8Bytes(@event, _options);
	}

	/// <inheritdoc/>
	public T? Deserialize<T>(byte[] data) where T : IDomainEvent
	{
		return JsonSerializer.Deserialize<T>(data, _options);
	}
}
