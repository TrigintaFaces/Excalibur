// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Exception thrown when an operation conflicts with the current state of a resource.
/// </summary>
/// <remarks>
/// <para>
/// This exception indicates that the requested operation cannot be completed because it would
/// conflict with the current state of the target resource. It maps to HTTP status code 409 (Conflict).
/// </para>
/// <para>
/// Use this exception when:
/// <list type="bullet">
///   <item><description>Attempting to create a resource that already exists</description></item>
///   <item><description>Modifying a resource in a way that violates business rules</description></item>
///   <item><description>State transition is not allowed from the current state</description></item>
/// </list>
/// </para>
/// <para>
/// For optimistic locking failures (version mismatches), use <see cref="ConcurrencyException"/> instead.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// if (await repository.ExistsAsync(user.Email, cancellationToken))
/// {
///     throw new ConflictException("User", "email", "A user with this email already exists.");
/// }
/// </code>
/// </example>
[Serializable]
public class ConflictException : ResourceException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConflictException"/> class.
	/// </summary>
	public ConflictException()
		: base(409, ErrorCodes.ResourceConflict, "A conflict occurred with the current state of the resource.", null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConflictException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public ConflictException(string message)
		: base(409, ErrorCodes.ResourceConflict, message, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConflictException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public ConflictException(string message, Exception? innerException)
		: base(409, ErrorCodes.ResourceConflict, message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConflictException"/> class with an error code and message.
	/// </summary>
	/// <param name="errorCode">The error code associated with this exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	protected ConflictException(string errorCode, string message)
		: base(409, errorCode, message, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConflictException"/> class with resource and field information.
	/// </summary>
	/// <param name="resource">The type or name of the resource involved in the conflict.</param>
	/// <param name="field">The field that caused the conflict.</param>
	/// <param name="reason">The reason for the conflict.</param>
	public ConflictException(string resource, string field, string reason)
		: base(409, ErrorCodes.ResourceConflict, reason, null)
	{
		Resource = resource;
		Field = field;
		Reason = reason;
		_ = WithContext("resource", resource);
		_ = WithContext("field", field);
		_ = WithContext("reason", reason);
	}

	/// <summary>
	/// Creates a <see cref="ConflictException"/> with resource and reason information.
	/// </summary>
	/// <param name="resource">The type or name of the resource involved in the conflict.</param>
	/// <param name="reason">The reason for the conflict.</param>
	/// <returns>A new <see cref="ConflictException"/> instance.</returns>
	public static ConflictException WithReason(string resource, string reason)
	{
		var ex = new ConflictException(ErrorCodes.ResourceConflict, FormatConflictMessage(resource, reason))
		{
			Resource = resource,
			Reason = reason,
		};
		_ = ex.WithContext("resource", resource);
		_ = ex.WithContext("reason", reason);
		return ex;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConflictException"/> class with an error code, message, and inner exception.
	/// </summary>
	/// <param name="errorCode">The error code associated with this exception.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	protected ConflictException(string errorCode, string message, Exception? innerException)
		: base(409, errorCode, message, innerException)
	{
	}

	/// <summary>
	/// Gets the field that caused the conflict, if applicable.
	/// </summary>
	/// <value>The field name, or <see langword="null"/> if not specified.</value>
	public string? Field { get; protected init; }

	/// <summary>
	/// Gets the reason for the conflict.
	/// </summary>
	/// <value>The conflict reason, or <see langword="null"/> if not specified.</value>
	public string? Reason { get; protected init; }

	/// <summary>
	/// Creates a <see cref="ConflictException"/> for a duplicate resource scenario.
	/// </summary>
	/// <param name="resource">The type or name of the resource.</param>
	/// <param name="identifier">The identifier that already exists.</param>
	/// <returns>A new <see cref="ConflictException"/> instance.</returns>
	public static ConflictException AlreadyExists(string resource, string identifier) =>
		new(resource, $"A {resource} with identifier '{identifier}' already exists.");

	/// <summary>
	/// Creates a <see cref="ConflictException"/> for an invalid state transition.
	/// </summary>
	/// <param name="resource">The type or name of the resource.</param>
	/// <param name="currentState">The current state.</param>
	/// <param name="targetState">The desired target state.</param>
	/// <returns>A new <see cref="ConflictException"/> instance.</returns>
	public static ConflictException InvalidStateTransition(string resource, string currentState, string targetState)
	{
		var ex = new ConflictException(resource, $"Cannot transition from '{currentState}' to '{targetState}'.");
		_ = ex.WithContext("currentState", currentState);
		_ = ex.WithContext("targetState", targetState);
		return ex;
	}

	/// <summary>
	/// Formats the default error message for a conflict scenario.
	/// </summary>
	/// <param name="resource">The resource type.</param>
	/// <param name="reason">The conflict reason.</param>
	/// <returns>A formatted error message.</returns>
	private static string FormatConflictMessage(string resource, string reason) =>
		$"Conflict with {resource}: {reason}";

	/// <inheritdoc/>
	protected override IDictionary<string, object?>? GetProblemDetailsExtensions()
	{
		var extensions = new Dictionary<string, object?>(StringComparer.Ordinal);

		if (Resource != null)
		{
			extensions["resource"] = Resource;
		}

		if (Field != null)
		{
			extensions["field"] = Field;
		}

		if (Reason != null)
		{
			extensions["reason"] = Reason;
		}

		// Merge with any context data
		foreach (var (key, value) in Context)
		{
			_ = extensions.TryAdd(key, value);
		}

		return extensions.Count > 0 ? extensions : null;
	}
}
