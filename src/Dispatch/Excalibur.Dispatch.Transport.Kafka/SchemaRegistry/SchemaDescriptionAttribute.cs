// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Specifies a description for a property in the generated JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// When applied to a property, this attribute adds a <c>description</c> field
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
///     [SchemaDescription("The unique identifier for this order")]
///     public string OrderId { get; init; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SchemaDescriptionAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaDescriptionAttribute"/> class.
	/// </summary>
	/// <param name="description">The description text for the JSON Schema.</param>
	public SchemaDescriptionAttribute(string description)
	{
		Description = description ?? throw new ArgumentNullException(nameof(description));
	}

	/// <summary>
	/// Gets the description text for the JSON Schema.
	/// </summary>
	public string Description { get; }
}
