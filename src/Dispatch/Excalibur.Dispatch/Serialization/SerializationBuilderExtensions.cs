// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Format-specific extension methods for <see cref="ISerializationBuilder"/>.
/// </summary>
/// <remarks>
/// These methods provide convenience shortcuts for registering and selecting
/// framework-provided serializers. They delegate to the core
/// <see cref="ISerializationBuilder.Register"/> and <see cref="ISerializationBuilder.UseCurrent"/>
/// methods.
/// </remarks>
public static class SerializationBuilderExtensions
{
	/// <summary>
	/// Registers System.Text.Json serializer (framework-assigned ID: 2).
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <returns>The builder for method chaining.</returns>
	public static ISerializationBuilder RegisterSystemTextJson(this ISerializationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.Register(new SystemTextJsonSerializer(), SerializerIds.SystemTextJson);
	}

	/// <summary>
	/// Uses MemoryPack as the current serializer for new messages.
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <returns>The builder for method chaining.</returns>
	public static ISerializationBuilder UseMemoryPack(this ISerializationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.UseCurrent("MemoryPack");
	}

	/// <summary>
	/// Uses System.Text.Json as the current serializer for new messages.
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <returns>The builder for method chaining.</returns>
	public static ISerializationBuilder UseSystemTextJson(this ISerializationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.UseCurrent("System.Text.Json");
	}

	/// <summary>
	/// Uses MessagePack as the current serializer for new messages.
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <returns>The builder for method chaining.</returns>
	public static ISerializationBuilder UseMessagePack(this ISerializationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.UseCurrent("MessagePack");
	}

	/// <summary>
	/// Uses Protobuf as the current serializer for new messages.
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <returns>The builder for method chaining.</returns>
	public static ISerializationBuilder UseProtobuf(this ISerializationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.UseCurrent("Protobuf");
	}

	/// <summary>
	/// Uses Avro as the current serializer for new messages.
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <returns>The builder for method chaining.</returns>
	public static ISerializationBuilder UseAvro(this ISerializationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.UseCurrent("Avro");
	}
}
