// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Options.Core;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// System.Text.Json implementation of message serializer.
/// </summary>
public sealed class SystemTextJsonMessageSerializer : IMessageSerializer
{
	private readonly JsonSerializerOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SystemTextJsonMessageSerializer" /> class.
	/// </summary>
	/// <param name="options"> The JSON serialization options. </param>
	public SystemTextJsonMessageSerializer(IOptions<JsonSerializationOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		_options = options.Value.BuildJsonSerializerOptions();
	}

	/// <inheritdoc />
	public string SerializerName => "SystemTextJson";

	/// <inheritdoc />
	public string SerializerVersion => "1.0.0";

	/// <inheritdoc />
	[RequiresUnreferencedCode(
		"Generic JSON serialization may require types that are not statically referenced and could be removed during trimming.")]
	[RequiresDynamicCode("Generic JSON serialization requires runtime code generation for type-specific serialization logic.")]
	public byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message)
	{
		ArgumentNullException.ThrowIfNull(message);
		return JsonSerializer.SerializeToUtf8Bytes(message, _options);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode(
		"Generic JSON deserialization may require types that are not statically referenced and could be removed during trimming.")]
	[RequiresDynamicCode("Generic JSON deserialization requires runtime code generation for type-specific deserialization logic.")]
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var result = JsonSerializer.Deserialize<T>(data, _options);
		return result ?? throw new InvalidOperationException(ErrorMessages.DeserializedMessageCannotBeNull);
	}
}
