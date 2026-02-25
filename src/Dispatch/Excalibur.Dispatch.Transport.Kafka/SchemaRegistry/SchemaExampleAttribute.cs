// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Specifies an example value for a property in the generated JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// When applied to a property, this attribute adds an <c>examples</c> array
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
///     [SchemaExample("ORD-12345")]
///     public string OrderId { get; init; }
///
///     [SchemaExample(99.99)]
///     public decimal TotalAmount { get; init; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public sealed class SchemaExampleAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaExampleAttribute"/> class.
	/// </summary>
	/// <param name="example">The example value for the JSON Schema.</param>
	public SchemaExampleAttribute(object example)
	{
		Example = example;
	}

	/// <summary>
	/// Gets the example value for the JSON Schema.
	/// </summary>
	public object? Example { get; }
}
