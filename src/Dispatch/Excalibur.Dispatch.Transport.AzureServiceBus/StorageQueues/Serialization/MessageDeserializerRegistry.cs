// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// AOT-safe registry that maps message type names to their typed deserialization delegates.
/// Populated at DI composition time via <see cref="MessageDeserializerRegistryPopulator"/>,
/// eliminating the need for <see cref="System.Reflection.MethodInfo.MakeGenericMethod"/>
/// at runtime.
/// </summary>
/// <remarks>
/// <para>
/// This follows the Explicit-Generic-DI Registry pattern established in Excalibur.Saga (Sprint 755)
/// and Excalibur.Dispatch.Caching (Sprint 756). During DI composition, each
/// <c>AddStorageQueueMessage&lt;TMessage&gt;()</c> call accumulates a typed registration action.
/// On first options resolution, the <see cref="MessageDeserializerRegistryPopulator"/> drains
/// the accumulated actions and freezes the registry.
/// </para>
/// </remarks>
internal sealed class MessageDeserializerRegistry
{
    private readonly ConcurrentDictionary<string, Func<IPayloadSerializer, byte[], object>> _deserializers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Type> _typeMap = new(StringComparer.Ordinal);
    private volatile bool _frozen;

    /// <summary>
    /// Registers a typed deserialization delegate for a message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type to register.</typeparam>
    /// <param name="typeNameOverride">Optional override for the type key. Defaults to <see cref="Type.AssemblyQualifiedName"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown if the registry has been frozen.</exception>
    internal void Register<TMessage>(string? typeNameOverride = null) where TMessage : class
    {
        if (_frozen)
        {
            throw new InvalidOperationException(
                $"Cannot register message deserializer for '{typeof(TMessage).Name}' after registry is frozen.");
        }

        var key = typeNameOverride ?? typeof(TMessage).AssemblyQualifiedName ?? typeof(TMessage).FullName!;
        _deserializers[key] = static (serializer, data) => serializer.Deserialize<TMessage>(data)!;
        _typeMap[key] = typeof(TMessage);

        // Also register by FullName for cross-assembly scenarios where AssemblyQualifiedName may differ
        var fullName = typeof(TMessage).FullName;
        if (fullName != null && fullName != key)
        {
            _deserializers[fullName] = static (serializer, data) => serializer.Deserialize<TMessage>(data)!;
            _typeMap[fullName] = typeof(TMessage);
        }
    }

    /// <summary>
    /// Attempts to deserialize a message using the registry.
    /// </summary>
    /// <param name="messageTypeName">The message type name from the envelope.</param>
    /// <param name="serializer">The payload serializer.</param>
    /// <param name="data">The serialized message body.</param>
    /// <returns>
    /// A tuple of the deserialized message and its <see cref="Type"/>, or <see langword="null"/>
    /// if the type name is not registered.
    /// </returns>
    internal (object Message, Type Type)? TryDeserialize(string messageTypeName, IPayloadSerializer serializer, byte[] data)
    {
        if (_deserializers.TryGetValue(messageTypeName, out var deserializer) &&
            _typeMap.TryGetValue(messageTypeName, out var type))
        {
            return (deserializer(serializer, data), type);
        }

        // Try matching by simple type name (last segment) for resilience
        // e.g., "MyNamespace.OrderCreated, MyAssembly, ..." should match a key ending with "OrderCreated"
        // This is a fallback — exact match is preferred.
        foreach (var kvp in _typeMap)
        {
            var registeredFullName = kvp.Key;
            var commaIndex = messageTypeName.IndexOf(',', StringComparison.Ordinal);
            var incomingTypeName = commaIndex > 0 ? messageTypeName[..commaIndex].Trim() : messageTypeName;

            var registeredCommaIndex = registeredFullName.IndexOf(',', StringComparison.Ordinal);
            var registeredTypePart = registeredCommaIndex > 0 ? registeredFullName[..registeredCommaIndex].Trim() : registeredFullName;

            if (string.Equals(incomingTypeName, registeredTypePart, StringComparison.Ordinal) &&
                _deserializers.TryGetValue(registeredFullName, out var fallbackDeserializer))
            {
                return (fallbackDeserializer(serializer, data), kvp.Value);
            }
        }

        return null;
    }

    /// <summary>
    /// Freezes the registry, preventing further registrations.
    /// </summary>
    internal void Freeze() => _frozen = true;
}
