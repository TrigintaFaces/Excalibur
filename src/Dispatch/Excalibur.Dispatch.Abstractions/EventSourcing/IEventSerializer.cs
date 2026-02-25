// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines serialization for domain events and type resolution for event sourcing.
/// </summary>
/// <remarks>
/// <para>
/// This interface handles event serialization and type name resolution.
/// Snapshot serialization is handled by <c>ISnapshotSerializer</c> in the
/// EventSourcing.Abstractions package.
/// </para>
/// </remarks>
public interface IEventSerializer
{
	/// <summary>
	/// Serializes an event to bytes.
	/// </summary>
	/// <param name="domainEvent"> The event to serialize. </param>
	/// <returns> Serialized event data. </returns>
	[RequiresDynamicCode("JSON serialization of events requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming")]
	byte[] SerializeEvent(IDomainEvent domainEvent);

	/// <summary>
	/// Deserializes an event from bytes.
	/// </summary>
	/// <param name="data"> The serialized event data. </param>
	/// <param name="eventType"> The type of event to deserialize. </param>
	/// <returns> The deserialized event. </returns>
	[RequiresDynamicCode("JSON deserialization of events requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON deserialization may reference types not preserved during trimming")]
	IDomainEvent DeserializeEvent(byte[] data, Type eventType);

	/// <summary>
	/// Gets the type name for serialization.
	/// </summary>
	/// <param name="type"> The type to get the name for. </param>
	/// <returns> Type name for storage. </returns>
	string GetTypeName(Type type);

	/// <summary>
	/// Resolves a type from its stored name.
	/// </summary>
	/// <param name="typeName"> The stored type name. </param>
	/// <returns> The resolved type. </returns>
	Type ResolveType(string typeName);
}
