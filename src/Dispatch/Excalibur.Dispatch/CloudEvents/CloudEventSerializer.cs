// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Carries the CloudEvents serialization decision — reflection or source-generated — as a resolved
/// dependency, so every transport adapter shares one decided configuration instead of each making the
/// choice again.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists.</b> Each transport adapter previously constructed a parameterless
/// <see cref="JsonEventFormatter"/> of its own, which silently ignored the resolver the consumer
/// supplied at registration and serialized payloads reflectively. That is why publishing a
/// CloudEvent was not ahead-of-time publishable on any transport: not because the format needed
/// rewriting, but because the formatter was being built without the metadata the consumer had already
/// supplied.
/// </para>
/// <para>
/// <b>Why it is a dependency rather than a branch.</b> Deciding reflection-versus-source-generated
/// inside each adapter would require every adapter to carry a trimming annotation, which propagates to
/// the consumer's own build. Deciding it once, at registration, lets the decision travel as data: the
/// registration that chose reflection carries the annotation, and nothing downstream needs one. The
/// adapters receive something already decided, so there is no precondition left for them to establish.
/// </para>
/// </remarks>
public sealed class CloudEventSerializer
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudEventSerializer"/> class.
	/// </summary>
	/// <param name="options">
	/// The decided serializer options, from
	/// <see cref="CloudEventPayloadSerialization.CreateOptions"/> or
	/// <see cref="CloudEventPayloadSerialization.CreateReflectionOptions"/>.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
	public CloudEventSerializer(JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		Options = options;

		// Default JsonDocumentOptions. The formatter's document parse inherits System.Text.Json's
		// default MaxDepth of 64, which bounds nesting on untrusted inbound documents.
		Formatter = new JsonEventFormatter(options, default);
	}

	private CloudEventSerializer(JsonSerializerOptions options, CloudEventFormatter formatter)
	{
		Options = options;
		Formatter = formatter;
	}

	/// <summary>Gets the decided serializer options.</summary>
	public JsonSerializerOptions Options { get; }

	/// <summary>
	/// Gets the CloudEvents formatter for structured-mode encoding and decoding, built over
	/// <see cref="Options"/> so the consumer's payload metadata is actually used.
	/// </summary>
	public CloudEventFormatter Formatter { get; }

	/// <summary>
	/// Creates a serializer whose payload formatter uses metadata from the supplied options explicitly.
	/// </summary>
	/// <param name="options">The same options used for the envelope and payload, with an explicit resolver.</param>
	/// <returns>A serializer retaining the supplied options and their converters.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentException">The supplied options do not have an explicit resolver.</exception>
	/// <remarks>
	/// This does not add a reflection fallback or certify arbitrary consumer converters for Native AOT.
	/// The resolver must cover the payloads published through it. The legacy constructor retains the SDK formatter.
	/// </remarks>
	public static CloudEventSerializer CreateWithMetadata(JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (options.TypeInfoResolver is null)
		{
			throw new ArgumentException(
				"CloudEvents metadata serialization requires an explicit TypeInfoResolver. Use CloudEventPayloadSerialization.CreateOptions with a source-generated resolver.",
				nameof(options));
		}

		return new CloudEventSerializer(options, new MetadataCloudEventFormatter(options));
	}

	/// <summary>
	/// Serializes a CloudEvent payload to JSON text through the decided resolver.
	/// </summary>
	/// <remarks>
	/// This replaces the parameterless <c>JsonSerializer.Serialize(object)</c> overload that each
	/// adapter previously reached for as the fallback arm of its own binary-mode switch. That overload
	/// is reflective, so it required a trimming annotation on the adapter and therefore on the
	/// consumer; routing through a <see cref="JsonTypeInfo"/> obtained from the decided options needs
	/// none, and produces identical JSON.
	/// <para>
	/// When the resolver does not cover <paramref name="data"/>'s type this throws
	/// <see cref="NotSupportedException"/> naming that type, which is the diagnostic a consumer needs
	/// and is the same failure the formatter itself produces. It fails loudly rather than emitting a
	/// rendered type name as the payload.
	/// </para>
	/// </remarks>
	/// <param name="data">The payload to serialize.</param>
	/// <returns>The payload as JSON text.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
	/// <exception cref="NotSupportedException">
	/// Thrown when the configured resolver provides no metadata for the payload's type.
	/// </exception>
	public string SerializePayload(object data)
	{
		ArgumentNullException.ThrowIfNull(data);

		return JsonSerializer.Serialize(data, Options.GetTypeInfo(data.GetType()));
	}

	/// <summary>
	/// Serializes a CloudEvent payload to UTF-8 bytes through the decided resolver.
	/// </summary>
	/// <param name="data">The payload to serialize.</param>
	/// <returns>The payload as UTF-8 encoded JSON.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
	/// <exception cref="NotSupportedException">
	/// Thrown when the configured resolver provides no metadata for the payload's type.
	/// </exception>
	public byte[] SerializePayloadToUtf8Bytes(object data)
	{
		ArgumentNullException.ThrowIfNull(data);

		return JsonSerializer.SerializeToUtf8Bytes(data, Options.GetTypeInfo(data.GetType()));
	}
}
