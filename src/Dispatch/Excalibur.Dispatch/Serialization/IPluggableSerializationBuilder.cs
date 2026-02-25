// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Builder interface for configuring pluggable serialization for internal persistence.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures serializers for internal persistence stores (Outbox, Inbox, Event Store).
/// It is separate from transport serialization which handles message wire formats.
/// </para>
/// <para>
/// <b>Usage:</b>
/// </para>
/// <code>
/// services.AddDispatch()
///     .ConfigurePluggableSerialization(config =>
///     {
///         // MemoryPack is already registered by default
///         config.RegisterSystemTextJson();
///         config.RegisterMessagePack();
///
///         // Switch to a different serializer (optional)
///         config.UseSystemTextJson();
///     });
/// </code>
/// <para>
/// See the pluggable serialization architecture documentation.
/// </para>
/// </remarks>
public interface IPluggableSerializationBuilder
{
	/// <summary>
	/// Registers MemoryPack serializer (framework-assigned ID: 1).
	/// </summary>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// MemoryPack is automatically registered by default when using <c>AddDispatch()</c>.
	/// This method is provided for explicit registration scenarios.
	/// </remarks>
	IPluggableSerializationBuilder RegisterMemoryPack();

	/// <summary>
	/// Registers System.Text.Json serializer (framework-assigned ID: 2).
	/// </summary>
	/// <returns>The builder for method chaining.</returns>
	IPluggableSerializationBuilder RegisterSystemTextJson();

	/// <summary>
	/// Validates that MessagePack serializer (ID: 3) is registered.
	/// </summary>
	/// <returns>The builder for method chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the MessagePack serializer is not registered.
	/// </exception>
	/// <remarks>
	/// <para>
	/// <b>Important:</b> This method does not register the MessagePack serializer - it only
	/// validates that registration has already occurred. Actual registration happens via
	/// <c>services.AddMessagePackSerialization()</c> from the <c>Excalibur.Dispatch.Serialization.MessagePack</c>
	/// package, which must be called before <c>ConfigurePluggableSerialization()</c>.
	/// </para>
	/// <para>
	/// This design enforces explicit package dependencies - you cannot accidentally use
	/// MessagePack without adding the package reference.
	/// </para>
	/// <para>
	/// <b>Correct usage:</b>
	/// </para>
	/// <code>
	/// services.AddMessagePackSerialization();  // Registers the serializer
	/// services.AddDispatch()
	///     .ConfigurePluggableSerialization(config =>
	///     {
	///         config.RegisterMessagePack();  // Validates registration
	///         config.UseMessagePack();       // Sets as current
	///     });
	/// </code>
	/// </remarks>
	IPluggableSerializationBuilder RegisterMessagePack();

	/// <summary>
	/// Validates that Protobuf serializer (ID: 4) is registered.
	/// </summary>
	/// <returns>The builder for method chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the Protobuf serializer is not registered.
	/// </exception>
	/// <remarks>
	/// <para>
	/// <b>Important:</b> This method does not register the Protobuf serializer - it only
	/// validates that registration has already occurred. Actual registration happens via
	/// the <c>Excalibur.Dispatch.Serialization.Protobuf</c> package, which must be referenced and
	/// configured before <c>ConfigurePluggableSerialization()</c>.
	/// </para>
	/// <para>
	/// This design enforces explicit package dependencies - you cannot accidentally use
	/// Protobuf without adding the package reference.
	/// </para>
	/// </remarks>
	IPluggableSerializationBuilder RegisterProtobuf();

	/// <summary>
	/// Registers a custom serializer with an explicit ID in the custom range (200-254).
	/// </summary>
	/// <param name="serializer">The custom serializer implementation.</param>
	/// <param name="id">
	/// The serializer ID. Must be in range <see cref="SerializerIds.CustomRangeStart"/> to
	/// <see cref="SerializerIds.CustomRangeEnd"/> (200-254).
	/// </param>
	/// <returns>The builder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serializer is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when ID is outside the custom range (200-254).
	/// </exception>
	IPluggableSerializationBuilder RegisterCustom(IPluggableSerializer serializer, byte id);

	/// <summary>
	/// Sets the current serializer by name.
	/// </summary>
	/// <param name="serializerName">The name of the serializer to use for new messages.</param>
	/// <returns>The builder for method chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when the serializer name is not registered.
	/// </exception>
	IPluggableSerializationBuilder UseCurrent(string serializerName);

	/// <summary>
	/// Uses MemoryPack as the current serializer for new messages.
	/// </summary>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// MemoryPack is the default serializer. This method is useful when switching back
	/// to MemoryPack after configuring a different serializer.
	/// </remarks>
	IPluggableSerializationBuilder UseMemoryPack();

	/// <summary>
	/// Uses System.Text.Json as the current serializer for new messages.
	/// </summary>
	/// <returns>The builder for method chaining.</returns>
	IPluggableSerializationBuilder UseSystemTextJson();

	/// <summary>
	/// Uses MessagePack as the current serializer for new messages.
	/// </summary>
	/// <returns>The builder for method chaining.</returns>
	IPluggableSerializationBuilder UseMessagePack();

	/// <summary>
	/// Uses Protobuf as the current serializer for new messages.
	/// </summary>
	/// <returns>The builder for method chaining.</returns>
	IPluggableSerializationBuilder UseProtobuf();

	/// <summary>
	/// Disables automatic registration of MemoryPack serializer.
	/// </summary>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// By default, MemoryPack is auto-registered and set as the current serializer.
	/// Call this method if you want to explicitly control serializer registration
	/// and do not want MemoryPack included.
	/// </para>
	/// <para>
	/// <b>Note:</b> If you disable MemoryPack auto-registration, you must register
	/// and set a current serializer before using the serialization infrastructure.
	/// </para>
	/// </remarks>
	IPluggableSerializationBuilder DisableMemoryPackAutoRegistration();
}
