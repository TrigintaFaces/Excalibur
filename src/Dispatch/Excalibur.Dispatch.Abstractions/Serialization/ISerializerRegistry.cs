// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Registry for managing available pluggable serializers and the currently configured serializer.
/// </summary>
/// <remarks>
/// <para>
/// This interface defines 5 core methods for serializer lifecycle and lookup.
/// Convenience query methods (<c>GetByName</c>, <c>IsRegistered</c>) are available
/// as extension methods in <see cref="SerializerRegistryExtensions"/>.
/// </para>
/// <para>
/// The serializer registry manages the set of available serializers for internal persistence
/// (Outbox, Inbox, Event Store). It maintains:
/// </para>
/// <list type="bullet">
///   <item>A mapping of serializer IDs to <see cref="IPluggableSerializer"/> implementations</item>
///   <item>A mapping of serializer names to IDs for lookup by name</item>
///   <item>The currently active serializer for new message serialization</item>
/// </list>
/// <para>
/// <b>Thread Safety:</b> Implementations must be thread-safe. The registry may be read
/// concurrently during serialization/deserialization while occasionally being written
/// to during configuration or migration.
/// </para>
/// </remarks>
public interface ISerializerRegistry
{
	/// <summary>
	/// Registers a serializer with a unique ID.
	/// </summary>
	/// <param name="id">
	/// The serializer ID (must be 1-254). Use <see cref="SerializerIds"/> constants for
	/// framework serializers or values in the custom range (200-254) for custom serializers.
	/// </param>
	/// <param name="serializer">The serializer implementation.</param>
	/// <exception cref="ArgumentNullException">Thrown when serializer is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when:
	/// <list type="bullet">
	///   <item>ID is 0 or 255 (reserved values)</item>
	///   <item>ID is already registered</item>
	///   <item>Serializer name is already registered</item>
	/// </list>
	/// </exception>
	void Register(byte id, IPluggableSerializer serializer);

	/// <summary>
	/// Sets the current serializer to use for new messages.
	/// </summary>
	/// <param name="serializerName">The name of the serializer (must be registered).</param>
	/// <exception cref="ArgumentException">Thrown when the serializer name is not registered.</exception>
	/// <exception cref="ArgumentNullException">Thrown when serializerName is null or whitespace.</exception>
	void SetCurrent(string serializerName);

	/// <summary>
	/// Gets the currently configured serializer.
	/// </summary>
	/// <returns>A tuple containing the current serializer's ID and implementation.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no current serializer is configured. Call <see cref="SetCurrent"/> first.
	/// </exception>
	(byte Id, IPluggableSerializer Serializer) GetCurrent();

	/// <summary>
	/// Gets a serializer by its ID.
	/// </summary>
	/// <param name="id">The serializer ID (from payload magic byte).</param>
	/// <returns>The serializer, or null if not registered.</returns>
	IPluggableSerializer? GetById(byte id);

	/// <summary>
	/// Gets all registered serializers.
	/// </summary>
	/// <returns>
	/// A read-only collection of tuples containing (ID, Name, Serializer) for each
	/// registered serializer.
	/// </returns>
	/// <remarks>
	/// This method is useful for diagnostics, migration tools, and error messages
	/// that need to list available serializers.
	/// </remarks>
	IReadOnlyCollection<(byte Id, string Name, IPluggableSerializer Serializer)> GetAll();
}
