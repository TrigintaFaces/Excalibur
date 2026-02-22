// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// JSON-based event serializer implementation.
/// </summary>
public sealed class JsonEventSerializer : IEventSerializer
{
	private const int MaxTypeCacheSize = 1024;
	private readonly JsonSerializerOptions _options;
	private readonly ConcurrentDictionary<string, Type> _typeCache;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonEventSerializer" /> class.
	/// </summary>
	/// <param name="options">Optional JSON serializer options to use.</param>
	[RequiresDynamicCode("JSON serialization may require dynamic code generation which is not compatible with AOT compilation.")]
	public JsonEventSerializer(JsonSerializerOptions? options = null)
	{
		_options = options ?? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Converters = { new JsonStringEnumConverter() },
		};
		_typeCache = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation which is not compatible with AOT compilation.")]
	public byte[] SerializeEvent(IDomainEvent domainEvent)
	{
		ArgumentNullException.ThrowIfNull(domainEvent);

		var json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), _options);
		return Encoding.UTF8.GetBytes(json);
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification = "Event deserialization requires runtime type resolution for polymorphic event types - reflection is intentional")]
	[RequiresDynamicCode("JSON deserialization of events requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON deserialization may reference types not preserved during trimming")]
	public IDomainEvent DeserializeEvent(byte[] data, Type eventType)
	{
		ArgumentNullException.ThrowIfNull(data);

		ArgumentNullException.ThrowIfNull(eventType);

		var json = Encoding.UTF8.GetString(data);
		var @event = JsonSerializer.Deserialize(json, eventType, _options);

		return @event as IDomainEvent ??
			   throw new InvalidOperationException($"Deserialized object is not an IDomainEvent: {eventType}");
	}

	/// <inheritdoc />
	public string GetTypeName(Type type)
	{
		return EventTypeNameHelper.GetEventTypeName(type);
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2057:Unrecognized value passed to the parameter of method. It's not possible to guarantee the availability of the target type.", Justification = "Type resolution from strings is required for event sourcing polymorphic event deserialization. Event types are preserved through event store infrastructure.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Assembly scanning for type resolution is required for polymorphic event deserialization.")]
	public Type ResolveType(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			throw new ArgumentException(ErrorMessages.TypeNameCannotBeNullOrEmpty, nameof(typeName));
		}

		if (_typeCache.TryGetValue(typeName, out var cachedType))
		{
			return cachedType;
		}

		// Resolve from loaded assemblies to support both full names and assembly-qualified names
		var resolvedType = SearchLoadedAssemblies(typeName)
			?? throw new InvalidOperationException(
				$"Cannot resolve type: '{typeName}'. Ensure the assembly containing this type is loaded. " +
				"If using short type names, the type must be discoverable in loaded assemblies.");

		// Only cache if we haven't exceeded the capacity — prevents unbounded memory growth
		if (_typeCache.Count < MaxTypeCacheSize)
		{
			_typeCache.TryAdd(typeName, resolvedType);
		}

		return resolvedType;
	}

	/// <summary>
	/// Searches all loaded assemblies for a type matching the given name.
	/// </summary>
	/// <param name="typeName">The type name to search for (can be full name or assembly-qualified).</param>
	/// <returns>The resolved type, or <see langword="null"/> if not found.</returns>
	private static Type? SearchLoadedAssemblies(string typeName)
	{
		var simpleTypeName = typeName;
		var commaIndex = typeName.IndexOf(',', StringComparison.Ordinal);
		if (commaIndex > 0)
		{
			simpleTypeName = typeName[..commaIndex].Trim();
		}

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			// Skip dynamic assemblies that can't be searched
			if (assembly.IsDynamic)
			{
				continue;
			}

			var type = assembly.GetType(typeName) ?? assembly.GetType(simpleTypeName);
			if (type is not null)
			{
				return type;
			}
		}

		return null;
	}
}
