// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Attribute to specify schema ID for a message type.
/// </summary>
/// <remarks> Creates a new schema ID attribute. </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SchemaIdAttribute(string schemaId) : Attribute
{
	/// <summary>
	/// Gets the schema identifier.
	/// </summary>
	/// <value>
	/// The schema identifier.
	/// </value>
	public string SchemaId { get; } = schemaId ?? throw new ArgumentNullException(nameof(schemaId));
}
