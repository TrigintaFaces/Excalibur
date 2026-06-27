// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch;

/// <summary>
/// JSON-based event serializer implementation.
/// </summary>
public sealed class JsonEventSerializer : IEventSerializer
{
	private const int MaxTypeCacheSize = 1024;
	private readonly JsonSerializerOptions _options;
	private readonly ConcurrentDictionary<string, Type> _typeCache;
	private readonly bool _allowAssemblyScan;
	private readonly IEventTypeRegistry? _registry;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonEventSerializer" /> class.
	/// </summary>
	/// <param name="options">Optional JSON serializer options to use.</param>
	/// <param name="allowAssemblyScan">
	/// When <see langword="true"/>, an unregistered type name is resolved by scanning all loaded
	/// assemblies. This is an explicit, trusted-environment opt-in: the unbounded scan can resolve an
	/// attacker-chosen (gadget-chain) type, so it is <see langword="false"/> by default and an unknown
	/// type name is rejected with <see cref="UnknownEventTypeException"/>. The flag is symmetric with the
	/// reflection/trim opt-in this constructor already represents.
	/// </param>
	/// <remarks>
	/// This reflection-based serializer is the trim/AOT opt-in gate: constructing it surfaces the
	/// <c>IL2026</c>/<c>IL3050</c> warnings. Use <c>AotJsonEventSerializer</c> for an AOT-safe path. The
	/// <see cref="IEventSerializer"/> interface itself is intentionally annotation-free (clean contract).
	/// </remarks>
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming. Use AotJsonEventSerializer for an AOT-safe path.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation which is not compatible with AOT compilation.")]
	public JsonEventSerializer(JsonSerializerOptions? options = null, bool allowAssemblyScan = false)
		: this(registry: null, options, allowAssemblyScan)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonEventSerializer" /> class that consults a
	/// consumer-registered event-type registry (c6wd6f) before any assembly scan.
	/// </summary>
	/// <param name="registry">
	/// Optional event-type registry (populated via <c>AddEventTypes&lt;T&gt;()</c>). When provided,
	/// <see cref="ResolveType"/> resolves registered types <em>independently of the scan</em>, so the
	/// secure default (<paramref name="allowAssemblyScan"/> <see langword="false"/>) is usable for event
	/// sourcing while an unregistered/attacker-chosen type stays unresolvable without the opt-in scan.
	/// </param>
	/// <param name="options">Optional JSON serializer options to use.</param>
	/// <param name="allowAssemblyScan">See the public constructor.</param>
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming. Use AotJsonEventSerializer for an AOT-safe path.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation which is not compatible with AOT compilation.")]
	internal JsonEventSerializer(IEventTypeRegistry? registry, JsonSerializerOptions? options = null, bool allowAssemblyScan = false)
	{
		_options = options ?? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Converters = { new JsonStringEnumConverter() },
		};
		_typeCache = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
		_allowAssemblyScan = allowAssemblyScan;
		_registry = registry;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification = "Reflection-based serialization is intentional; the trim opt-in is enforced at the constructor.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Reflection-based serialization is intentional; the AOT opt-in is enforced at the constructor.")]
	public byte[] SerializeEvent(IDomainEvent domainEvent)
	{
		ArgumentNullException.ThrowIfNull(domainEvent);

		var json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), _options);
		return Encoding.UTF8.GetBytes(json);
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification = "Event deserialization requires runtime type resolution for polymorphic event types - reflection is intentional; opt-in enforced at the constructor.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Reflection-based deserialization is intentional; the AOT opt-in is enforced at the constructor.")]
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
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		Justification = "Assembly scanning for type resolution is intentional; the trim opt-in is enforced at the constructor.")]
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

		// c6wd6f: consult the consumer-registered allow-list FIRST, independent of the scan. A registered
		// type resolves securely (no reflection scan), making the secure default usable for event sourcing;
		// an unregistered type still falls through to the scan gate below (rejected unless opted in).
		var registered = _registry?.ResolveType(typeName);
		if (registered is not null)
		{
			if (_typeCache.Count < MaxTypeCacheSize)
			{
				_ = _typeCache.TryAdd(typeName, registered);
			}

			return registered;
		}

		// wpynky / S-E: the unbounded AppDomain.GetAssemblies() scan is the gadget-chain vector — it can
		// resolve an attacker-chosen type from any loaded assembly. It is OFF by default: an unregistered
		// type name is rejected unless the consumer explicitly opted into the scan (accepting the resolution
		// risk in a trusted environment, symmetric with the reflection opt-in this serializer represents).
		if (!_allowAssemblyScan)
		{
			throw new UnknownEventTypeException(
				$"Cannot resolve event type '{typeName}': it is not registered and the assembly scan is " +
				"disabled. Register your event types with AddEventTypes<T>() / AddEventTypesFromAssembly(...) " +
				"(secure, recommended), or use AotJsonEventSerializer (source-generated type map), or " +
				"construct JsonEventSerializer(allowAssemblyScan: true) to enable the reflection assembly " +
				"scan in a trusted environment.");
		}

		// Resolve from loaded assemblies to support both full names and assembly-qualified names
		var resolvedType = SearchLoadedAssemblies(typeName)
			?? throw new UnknownEventTypeException(
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
	[RequiresUnreferencedCode("Assembly scanning for type resolution requires type metadata that may be removed during trimming")]
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
