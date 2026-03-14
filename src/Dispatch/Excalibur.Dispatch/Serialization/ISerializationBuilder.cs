// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Builder interface for configuring serialization for internal persistence.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures serializers for internal persistence stores (Outbox, Inbox, Event Store).
/// It is separate from transport serialization which handles message wire formats.
/// </para>
/// <para>
/// <b>Core methods (3):</b> <see cref="Register"/>, <see cref="UseCurrent"/>,
/// <see cref="DisableAutoRegistration"/>. Format-specific convenience methods
/// (<c>RegisterMemoryPack</c>, <c>UseMemoryPack</c>, etc.) are available as
/// extension methods in <see cref="SerializationBuilderExtensions"/>.
/// </para>
/// <para>
/// Follows the <c>IdentityBuilder</c> pattern from Microsoft.AspNetCore.Identity
/// (3 core methods, format-specific as extensions).
/// </para>
/// <para>
/// <b>Usage:</b>
/// </para>
/// <code>
/// services.AddDispatch()
///     .ConfigureSerialization(config =>
///     {
///         config.RegisterSystemTextJson();  // extension method
///         config.UseCurrent("System.Text.Json");
///     });
/// </code>
/// </remarks>
public interface ISerializationBuilder
{
	/// <summary>
	/// Registers a serializer with a unique ID.
	/// </summary>
	/// <param name="serializer">The serializer implementation.</param>
	/// <param name="id">
	/// The serializer ID. Use <see cref="SerializerIds"/> constants for framework serializers
	/// or values in the custom range (200-254) for custom serializers.
	/// </param>
	/// <returns>The builder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serializer is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when ID is outside the valid range or already registered.
	/// </exception>
	ISerializationBuilder Register(ISerializer serializer, byte id);

	/// <summary>
	/// Sets the current serializer by name for new messages.
	/// </summary>
	/// <param name="serializerName">The name of the serializer to use.</param>
	/// <returns>The builder for method chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when the serializer name is null or whitespace.
	/// </exception>
	ISerializationBuilder UseCurrent(string serializerName);

	/// <summary>
	/// Disables automatic registration of the default serializer (MemoryPack).
	/// </summary>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// By default, MemoryPack is auto-registered and set as the current serializer.
	/// Call this method if you want to explicitly control serializer registration
	/// and do not want MemoryPack included.
	/// </para>
	/// <para>
	/// <b>Note:</b> If you disable auto-registration, you must register
	/// and set a current serializer before using the serialization infrastructure.
	/// </para>
	/// </remarks>
	ISerializationBuilder DisableAutoRegistration();
}
