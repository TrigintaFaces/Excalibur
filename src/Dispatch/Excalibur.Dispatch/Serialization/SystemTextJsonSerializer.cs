// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Consolidated System.Text.Json serializer implementing <see cref="ISerializer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Replaces SystemTextJsonPluggableSerializer and SystemTextJsonMessageSerializer.
/// Provides human-readable JSON output, ideal for debugging and development.
/// </para>
/// <para>
/// <b>Serializer ID:</b> <see cref="SerializerIds.SystemTextJson"/> (2)
/// </para>
/// </remarks>
public sealed class SystemTextJsonSerializer : ISerializer
{
	private readonly JsonSerializerOptions? _options;
	private readonly JsonSerializerContext? _context;

	/// <summary>
	/// Initializes a new instance with default options (camelCase, compact).
	/// </summary>
	[RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization may require runtime code generation.")]
	public SystemTextJsonSerializer()
		: this(null)
	{
	}

	/// <summary>
	/// Initializes a new instance with custom JSON options.
	/// </summary>
	/// <param name="options">Custom JSON serializer options. If null, defaults with camelCase are used.</param>
	/// <remarks>
	/// Mutable options are snapshotted at construction; later caller mutations do not change this codec.
	/// The caller's options are never modified. Already-readonly options retain their metadata identity.
	/// </remarks>
	[RequiresUnreferencedCode("Legacy JSON construction may use reflection. Use Create(JsonSerializerContext) for generated metadata.")]
	[RequiresDynamicCode("Legacy JSON construction may require runtime code generation. Use Create(JsonSerializerContext).")]
	public SystemTextJsonSerializer(JsonSerializerOptions? options)
		: this(PrepareLegacyOptions(options), context: null)
	{
	}

	private SystemTextJsonSerializer(JsonSerializerOptions? options, JsonSerializerContext? context)
	{
		_options = options;
		_context = context;
	}

	/// <summary>Creates a codec that resolves types only through the supplied JSON metadata context.</summary>
	/// <param name="context">Application-supplied metadata, including runtime polymorphic types.</param>
	/// <returns>A System.Text.Json codec without a framework reflection fallback.</returns>
	/// <remarks>
	/// Forwarding contexts are supported; returned metadata owns the effective options. Missing metadata
	/// fails rather than falling back to reflection. Consumers own the AOT safety of supplied contexts
	/// and converters. The context is borrowed and is not modified or disposed by this codec.
	/// </remarks>
	public static SystemTextJsonSerializer Create(JsonSerializerContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return new SystemTextJsonSerializer(options: null, context);
	}

    /// <summary>Creates a codec using one explicit, owned JSON policy and generated metadata composition.</summary>
    /// <param name="options">The application wire policy with an explicit metadata resolver.</param>
    /// <returns>A codec with a frozen snapshot of the supplied policy and resolver membership.</returns>
    /// <remarks>
    /// Consumer resolvers precede generated core and CLR CloudEvents metadata fallbacks. All types use
    /// the supplied policy, including nested runtime types; context defaults are not separate policies.
    /// Resolver and converter objects are borrowed. This does not certify their NativeAOT safety or
    /// encode the standard CloudEvents wire format. Use Create(context) for context-owned metadata.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">No explicit metadata resolver is configured.</exception>
    /// <exception cref="InvalidOperationException">Required framework metadata is incompatible with the policy.</exception>
    /// <exception cref="NotSupportedException">A resolver cannot provide required framework metadata.</exception>
    public static SystemTextJsonSerializer CreateWithMetadata(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.TypeInfoResolver is null)
        {
            throw new ArgumentException("Generated JSON serialization requires an explicit TypeInfoResolver.", nameof(options));
        }

        var resolvers = options.TypeInfoResolverChain.ToArray();
        var snapshot = new JsonSerializerOptions(options)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                [.. resolvers, CoreMessageJsonContext.Default, CloudEventJsonContext.Default]),
        };
        snapshot.MakeReadOnly();
        _ = snapshot.GetTypeInfo(typeof(CloudEvent)); // Root metadata presence, not arbitrary SDK graph serialization.
        _ = snapshot.GetTypeInfo(typeof(Uri));
        _ = snapshot.GetTypeInfo(typeof(Dictionary<string, object>));
        return new SystemTextJsonSerializer(snapshot, context: null);
    }

	[RequiresUnreferencedCode("Legacy JSON options use a reflection resolver when none is supplied.")]
	[RequiresDynamicCode("The legacy reflection resolver may require runtime code generation.")]
	private static JsonSerializerOptions PrepareLegacyOptions(JsonSerializerOptions? options)
	{
		// Readonly options are already stable, and context metadata can be bound to their exact identity.
		if (options is { IsReadOnly: true }) { return options; }
		var snapshot = options is null ? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false,
			MaxDepth = 64,
		} : new JsonSerializerOptions(options);
		snapshot.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
		snapshot.MakeReadOnly();
		return snapshot;
	}

	private JsonTypeInfo ResolveTypeInfo(Type type)
	{
		var metadata = (_context is null ? _options!.GetTypeInfo(type) : _context.GetTypeInfo(type))
			?? throw new NotSupportedException($"Type '{type}' has no metadata in the supplied JSON context. Register all payload and runtime polymorphic types.");
		if (metadata.Type != type)
		{
			throw new InvalidOperationException($"The JSON context returned metadata for '{metadata.Type}' instead of '{type}'.");
		}
		return metadata;
	}

	/// <inheritdoc />
	public string Name => "System.Text.Json";

	/// <inheritdoc />
	public string Version => typeof(JsonSerializer).Assembly
		.GetName().Version?.ToString() ?? "Unknown";

	/// <inheritdoc />
	public string ContentType => "application/json";

	/// <inheritdoc />
	public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(bufferWriter);

		try
		{
			var metadata = (JsonTypeInfo<T>)ResolveTypeInfo(typeof(T));
			using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions
			{
				Indented = metadata.Options.WriteIndented,
				Encoder = metadata.Options.Encoder,
				IndentCharacter = metadata.Options.IndentCharacter,
				IndentSize = metadata.Options.IndentSize,
				NewLine = metadata.Options.NewLine,
			});
			JsonSerializer.Serialize(writer, value, metadata);
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.Wrap<T>("serialize", ex);
		}
	}

	/// <inheritdoc cref="ISerializer.Deserialize{T}"/>
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		try
		{
			return JsonSerializer.Deserialize(data, (JsonTypeInfo<T>)ResolveTypeInfo(typeof(T)))
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
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			if (!type.IsAssignableFrom(value.GetType()))
			{
				throw new ArgumentException("The value is not assignable to the declared serialization type.", nameof(value));
			}

			return JsonSerializer.SerializeToUtf8Bytes(value, ResolveTypeInfo(type));
		}
		catch (Exception ex) when (ex is not ArgumentNullException)
		{
			throw SerializationException.WrapObject(type, "serialize", ex);
		}
	}

	/// <inheritdoc />
	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			return JsonSerializer.Deserialize(data, ResolveTypeInfo(type))
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
