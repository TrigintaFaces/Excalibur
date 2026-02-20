// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// Marks a message type as requiring a specific permission to execute.
/// This attribute works alongside the interface-based authorization system (<see cref="IRequireAuthorization"/>).
/// When both are present, AND logic applies (both must pass).
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute for simple permission checks with clean syntax:
/// </para>
/// <code>
/// [RequirePermission("users.delete")]
/// public class DeleteUserCommand : IDispatchAction { }
/// </code>
/// <para>
/// For complex authorization with custom requirements, use <see cref="IRequireCustomAuthorization"/> instead.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : Attribute
{
	/// <summary>
	/// Gets the permission name required (e.g., "users.delete", "orders.create").
	/// </summary>
	/// <value>The permission name that must be granted for authorization to succeed.</value>
	public string Permission { get; }

	/// <summary>
	/// Gets or sets the resource types this permission applies to.
	/// </summary>
	/// <value>An array of resource type names, or <see langword="null"/> if not specified.</value>
	public string[]? ResourceTypes { get; init; }

	/// <summary>
	/// Gets or sets the property name containing the resource ID.
	/// When specified, the middleware will extract the value from this property using reflection.
	/// </summary>
	/// <value>The property name to extract the resource ID from, or <see langword="null"/> if not specified.</value>
	/// <example>
	/// <code>
	/// [RequirePermission("orders.update", ResourceIdProperty = nameof(OrderId))]
	/// public class UpdateOrderCommand : IDispatchAction
	/// {
	///     public Guid OrderId { get; set; }
	/// }
	/// </code>
	/// </example>
	public string? ResourceIdProperty { get; init; }

	/// <summary>
	/// Reserved for future conditional expressions. NOT YET IMPLEMENTED.
	/// </summary>
	/// <value>The conditional expression string, or <see langword="null"/> if not specified.</value>
	/// <remarks>
	/// This property is reserved for future use to support conditional authorization expressions.
	/// Currently, setting this property has no effect.
	/// </remarks>
	public string? When { get; init; }

	/// <summary>
	/// Initializes a new instance of the <see cref="RequirePermissionAttribute"/> class with the required permission.
	/// </summary>
	/// <param name="permission">The permission name (e.g., "users.delete").</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="permission"/> is null or whitespace.</exception>
	public RequirePermissionAttribute(string permission)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(permission);
		Permission = permission;
	}
}
