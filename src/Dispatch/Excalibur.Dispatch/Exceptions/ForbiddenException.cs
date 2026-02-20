// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Exception thrown when access to a resource is forbidden.
/// </summary>
/// <remarks>
/// <para>
/// This exception indicates that the authenticated user does not have permission to perform
/// the requested operation. It maps to HTTP status code 403 (Forbidden).
/// </para>
/// <para>
/// This differs from authentication failures (401 Unauthorized):
/// <list type="bullet">
///   <item><description><b>401 Unauthorized</b>: The user is not authenticated (no identity)</description></item>
///   <item><description><b>403 Forbidden</b>: The user IS authenticated but lacks permission</description></item>
/// </list>
/// </para>
/// <para>
/// Use this exception when:
/// <list type="bullet">
///   <item><description>A user lacks the required role or permission</description></item>
///   <item><description>The resource is private and the user is not the owner</description></item>
///   <item><description>A feature is disabled for the user's subscription tier</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// if (!user.HasPermission(Permission.DeleteOrders))
/// {
///     throw new ForbiddenException("Order", "Delete");
/// }
/// </code>
/// </example>
[Serializable]
public class ForbiddenException : ResourceException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ForbiddenException"/> class.
	/// </summary>
	public ForbiddenException()
		: base(403, ErrorCodes.SecurityForbidden, "Access to the requested resource is forbidden.", null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ForbiddenException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public ForbiddenException(string message)
		: base(403, ErrorCodes.SecurityForbidden, message, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ForbiddenException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public ForbiddenException(string message, Exception? innerException)
		: base(403, ErrorCodes.SecurityForbidden, message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ForbiddenException"/> class with resource and operation information.
	/// </summary>
	/// <param name="resource">The type or name of the resource.</param>
	/// <param name="operation">The operation that was denied.</param>
	public ForbiddenException(string resource, string operation)
		: base(403, ErrorCodes.SecurityForbidden, FormatForbiddenMessage(resource, operation), null)
	{
		Resource = resource;
		Operation = operation;
		_ = WithContext("resource", resource);
		_ = WithContext("operation", operation);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ForbiddenException"/> class with detailed permission information.
	/// </summary>
	/// <param name="resource">The type or name of the resource.</param>
	/// <param name="operation">The operation that was denied.</param>
	/// <param name="requiredPermission">The permission that is required.</param>
	public ForbiddenException(string resource, string operation, string requiredPermission)
		: base(403, ErrorCodes.SecurityForbidden, FormatForbiddenMessage(resource, operation, requiredPermission), null)
	{
		Resource = resource;
		Operation = operation;
		RequiredPermission = requiredPermission;
		_ = WithContext("resource", resource);
		_ = WithContext("operation", operation);
		_ = WithContext("requiredPermission", requiredPermission);
	}

	/// <summary>
	/// Gets the operation that was denied.
	/// </summary>
	/// <value>The operation name, or <see langword="null"/> if not specified.</value>
	public string? Operation { get; }

	/// <summary>
	/// Gets the permission that is required to perform the operation.
	/// </summary>
	/// <value>The required permission, or <see langword="null"/> if not specified.</value>
	public string? RequiredPermission { get; }

	/// <summary>
	/// Creates a <see cref="ForbiddenException"/> for a missing role.
	/// </summary>
	/// <param name="resource">The type or name of the resource.</param>
	/// <param name="operation">The operation that was denied.</param>
	/// <param name="requiredRole">The role that is required.</param>
	/// <returns>A new <see cref="ForbiddenException"/> instance.</returns>
	public static ForbiddenException MissingRole(string resource, string operation, string requiredRole)
	{
		var ex = new ForbiddenException(resource, operation, $"Role:{requiredRole}");
		_ = ex.WithContext("requiredRole", requiredRole);
		return ex;
	}

	/// <summary>
	/// Creates a <see cref="ForbiddenException"/> for a feature that requires a higher subscription tier.
	/// </summary>
	/// <param name="feature">The feature that is restricted.</param>
	/// <param name="requiredTier">The required subscription tier.</param>
	/// <returns>A new <see cref="ForbiddenException"/> instance.</returns>
	public static ForbiddenException SubscriptionRequired(string feature, string requiredTier)
	{
		var ex = new ForbiddenException($"This feature requires a {requiredTier} subscription.");
		_ = ex.WithContext("feature", feature);
		_ = ex.WithContext("requiredTier", requiredTier);
		return ex;
	}

	/// <summary>
	/// Formats the forbidden error message.
	/// </summary>
	private static string FormatForbiddenMessage(string resource, string operation) =>
		$"Access denied. You do not have permission to {operation} {resource}.";

	/// <summary>
	/// Formats the forbidden error message with required permission.
	/// </summary>
	private static string FormatForbiddenMessage(string resource, string operation, string requiredPermission) =>
		$"Access denied. You do not have permission to {operation} {resource}. Required permission: {requiredPermission}.";

	/// <inheritdoc/>
	protected override IDictionary<string, object?>? GetProblemDetailsExtensions()
	{
		var extensions = new Dictionary<string, object?>(StringComparer.Ordinal);

		if (Resource != null)
		{
			extensions["resource"] = Resource;
		}

		if (Operation != null)
		{
			extensions["operation"] = Operation;
		}

		if (RequiredPermission != null)
		{
			extensions["requiredPermission"] = RequiredPermission;
		}

		// Merge with any context data
		foreach (var (key, value) in Context)
		{
			_ = extensions.TryAdd(key, value);
		}

		return extensions.Count > 0 ? extensions : null;
	}
}
