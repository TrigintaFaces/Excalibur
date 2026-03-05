// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Pre-computed metadata for a message type to avoid runtime reflection.
/// </summary>
public sealed class MessageTypeMetadata
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MessageTypeMetadata" /> class.
	/// </summary>
	/// <param name="type"> The message type to create metadata for. </param>
	public MessageTypeMetadata([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		Type = type;
		FullName = type.FullName ?? type.Name;
		SimpleName = type.Name;
		AssemblyQualifiedName = type.AssemblyQualifiedName ?? FullName;

		// Pre-compute common interface checks in a single pass to avoid repeated interface scans.
		var interfaces = type.GetInterfaces();
		var isEvent = false;
		var isCommand = false;
		var isDocument = false;
		var isProjection = false;
		var isIntegrationEvent = false;
		for (var i = 0; i < interfaces.Length; i++)
		{
			switch (interfaces[i].Name)
			{
				case "IDispatchEvent":
					isEvent = true;
					break;
				case "IDispatchAction":
					isCommand = true;
					break;
				case "IDispatchDocument":
					isDocument = true;
					break;
				case "IProjection":
					isProjection = true;
					break;
				case "IIntegrationEvent":
					isIntegrationEvent = true;
					break;
			}

			if (isEvent && isCommand && isDocument && isProjection && isIntegrationEvent)
			{
				break;
			}
		}

		IsEvent = isEvent;
		IsCommand = isCommand;
		IsDocument = isDocument;
		IsProjection = isProjection;

		// Cache routing hints
		if (isEvent && isIntegrationEvent)
		{
			RoutingHint = "remote";
		}
		else if (isEvent || isDocument || isProjection)
		{
			RoutingHint = "local";
		}
		else
		{
			RoutingHint = "default";
		}

		// Pre-compute hash code for fast dictionary lookups
		TypeHashCode = type.GetHashCode();
	}

	/// <summary>
	/// Gets the message type.
	/// </summary>
	/// <value> The CLR type represented by this metadata instance. </value>
	public Type Type { get; }

	/// <summary>
	/// Gets the full name of the type.
	/// </summary>
	/// <value> The fully qualified type name. </value>
	public string FullName { get; }

	/// <summary>
	/// Gets the simple name of the type.
	/// </summary>
	/// <value> The simple type name without namespace qualification. </value>
	public string SimpleName { get; }

	/// <summary>
	/// Gets the assembly-qualified name of the type.
	/// </summary>
	/// <value> The assembly-qualified type name. </value>
	public string AssemblyQualifiedName { get; }

	/// <summary>
	/// Gets a value indicating whether this is an event message.
	/// </summary>
	/// <value> <see langword="true" /> when the type implements `IDispatchEvent`; otherwise, <see langword="false" />. </value>
	public bool IsEvent { get; }

	/// <summary>
	/// Gets a value indicating whether this is a command/action message.
	/// </summary>
	/// <value> <see langword="true" /> when the type implements `IDispatchAction`; otherwise, <see langword="false" />. </value>
	public bool IsCommand { get; }

	/// <summary>
	/// Gets a value indicating whether this is a document message.
	/// </summary>
	/// <value> <see langword="true" /> when the type implements `IDispatchDocument`; otherwise, <see langword="false" />. </value>
	public bool IsDocument { get; }

	/// <summary>
	/// Gets a value indicating whether this is a projection message.
	/// </summary>
	/// <value> <see langword="true" /> when the type implements `IProjection`; otherwise, <see langword="false" />. </value>
	public bool IsProjection { get; }

	/// <summary>
	/// Gets a routing hint for this message type.
	/// </summary>
	/// <value> The routing hint determined from the message type characteristics. </value>
	public string RoutingHint { get; }

	/// <summary>
	/// Gets the pre-computed hash code for the underlying message type.
	/// </summary>
	/// <value> The hash code derived from the underlying message type. </value>
	public int TypeHashCode { get; }
}
