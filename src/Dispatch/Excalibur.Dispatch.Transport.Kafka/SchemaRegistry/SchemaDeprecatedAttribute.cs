// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Marks a property as deprecated in the generated JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// When applied to a property, this attribute adds <c>"deprecated": true</c>
/// to the property's schema definition.
/// </para>
/// <para>
/// This attribute is only processed when <see cref="JsonSchemaOptions.IncludeAnnotations"/>
/// is set to <see langword="true"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderCreated
/// {
///     public string OrderId { get; init; }
///
///     [SchemaDeprecated("Use CustomerId instead")]
///     public string LegacyCustomerCode { get; init; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SchemaDeprecatedAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaDeprecatedAttribute"/> class.
	/// </summary>
	public SchemaDeprecatedAttribute()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaDeprecatedAttribute"/> class
	/// with a deprecation message.
	/// </summary>
	/// <param name="message">A message explaining the deprecation.</param>
	public SchemaDeprecatedAttribute(string message)
	{
		Message = message;
	}

	/// <summary>
	/// Gets the optional deprecation message.
	/// </summary>
	public string? Message { get; }
}
