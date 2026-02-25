// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Exception thrown when a requested resource cannot be found.
/// </summary>
/// <remarks>
/// <para>
/// This exception indicates that a specific resource (entity, record, or object) does not exist
/// or could not be located. It maps to HTTP status code 404 (Not Found).
/// </para>
/// <para>
/// Use this exception when:
/// <list type="bullet">
///   <item><description>Looking up an entity by ID that doesn't exist</description></item>
///   <item><description>Attempting to access a resource that has been deleted</description></item>
///   <item><description>Querying for a record that doesn't match any criteria</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var user = await repository.FindByIdAsync(userId, cancellationToken)
///     ?? throw new ResourceNotFoundException("User", userId.ToString());
/// </code>
/// </example>
[Serializable]
public sealed class ResourceNotFoundException : ResourceException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceNotFoundException"/> class.
	/// </summary>
	public ResourceNotFoundException()
		: base(ErrorCodes.ResourceNotFound, "The requested resource was not found.")
	{
		DispatchStatusCode = 404;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceNotFoundException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public ResourceNotFoundException(string message)
		: base(ErrorCodes.ResourceNotFound, message)
	{
		DispatchStatusCode = 404;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceNotFoundException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public ResourceNotFoundException(string message, Exception? innerException)
		: base(ErrorCodes.ResourceNotFound, message, innerException)
	{
		DispatchStatusCode = 404;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceNotFoundException"/> class with resource information.
	/// </summary>
	/// <param name="resource">The type or name of the resource that was not found.</param>
	/// <param name="resourceId">The optional identifier of the resource that was not found.</param>
	public ResourceNotFoundException(string resource, string? resourceId)
		: base(ErrorCodes.ResourceNotFound, FormatNotFoundMessage(resource, resourceId))
	{
		Resource = resource;
		ResourceId = resourceId;
		DispatchStatusCode = 404;
		_ = WithContext("resource", resource);
		if (resourceId != null)
		{
			_ = WithContext("resourceId", resourceId);
		}
	}

	/// <summary>
	/// Creates a <see cref="ResourceNotFoundException"/> for a specific entity type and ID.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <param name="id">The identifier of the entity.</param>
	/// <returns>A new <see cref="ResourceNotFoundException"/> instance.</returns>
	public static ResourceNotFoundException ForEntity<TEntity>(object id) =>
		new(typeof(TEntity).Name, id.ToString());

	/// <summary>
	/// Formats the error message for a resource not found scenario.
	/// </summary>
	/// <param name="resource">The resource type.</param>
	/// <param name="resourceId">The optional resource identifier.</param>
	/// <returns>A formatted error message.</returns>
	private static string FormatNotFoundMessage(string resource, string? resourceId) =>
		resourceId != null
			? $"The requested {resource} with ID '{resourceId}' was not found."
			: $"The requested {resource} was not found.";
}
