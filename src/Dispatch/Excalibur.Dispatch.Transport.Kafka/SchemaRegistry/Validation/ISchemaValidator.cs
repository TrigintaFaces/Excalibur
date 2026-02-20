// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Validates JSON Schema structure before registration with the Schema Registry.
/// </summary>
/// <remarks>
/// <para>
/// This validator performs lightweight local validation to catch obvious errors
/// before making a network call to the Schema Registry. It validates:
/// </para>
/// <list type="bullet">
///   <item><description>Valid JSON syntax</description></item>
///   <item><description>Required JSON Schema properties (e.g., <c>type</c>)</description></item>
///   <item><description>JSON Schema draft compliance</description></item>
/// </list>
/// <para>
/// <b>Note:</b> Compatibility checking is NOT performed locally. Use
/// <see cref="ISchemaRegistryClient.IsCompatibleAsync"/> for compatibility validation
/// against the Schema Registry.
/// </para>
/// </remarks>
public interface ISchemaValidator
{
	/// <summary>
	/// Validates the structure of a JSON Schema.
	/// </summary>
	/// <param name="schema">The JSON Schema string to validate.</param>
	/// <returns>A <see cref="SchemaValidationResult"/> indicating whether the schema is valid.</returns>
	/// <remarks>
	/// This method performs local structural validation only. It does not check
	/// compatibility with previously registered schemas.
	/// </remarks>
	SchemaValidationResult ValidateStructure(string schema);
}
