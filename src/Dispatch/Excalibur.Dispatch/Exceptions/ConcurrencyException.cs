// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Exception thrown when an optimistic concurrency conflict occurs.
/// </summary>
/// <remarks>
/// <para>
/// This exception indicates that an update operation failed because the resource was modified
/// by another process since it was last read. It maps to HTTP status code 409 (Conflict).
/// </para>
/// <para>
/// This is a specialized form of <see cref="ConflictException"/> for optimistic locking scenarios
/// where version or ETag checking is used.
/// </para>
/// <para>
/// Use this exception when:
/// <list type="bullet">
///   <item><description>The expected version doesn't match the actual version</description></item>
///   <item><description>An ETag comparison fails</description></item>
///   <item><description>A timestamp-based optimistic lock is violated</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// if (entity.Version != expectedVersion)
/// {
///     throw new ConcurrencyException("Order", orderId.ToString(), expectedVersion, entity.Version);
/// }
/// </code>
/// </example>
[Serializable]
public class ConcurrencyException : ConflictException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConcurrencyException"/> class.
	/// </summary>
	public ConcurrencyException()
		: base(ErrorCodes.ResourceConcurrency, "A concurrency conflict occurred. The resource was modified by another process.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConcurrencyException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public ConcurrencyException(string message)
		: base(ErrorCodes.ResourceConcurrency, message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConcurrencyException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public ConcurrencyException(string message, Exception? innerException)
		: base(ErrorCodes.ResourceConcurrency, message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConcurrencyException"/> class with version information.
	/// </summary>
	/// <param name="resource">The type or name of the resource.</param>
	/// <param name="resourceId">The identifier of the resource.</param>
	/// <param name="expectedVersion">The version that was expected.</param>
	/// <param name="actualVersion">The actual current version.</param>
	public ConcurrencyException(string resource, string? resourceId, long expectedVersion, long actualVersion)
		: base(ErrorCodes.ResourceConcurrency, FormatConcurrencyMessage(resource, resourceId, expectedVersion, actualVersion))
	{
		Resource = resource;
		ResourceId = resourceId;
		ExpectedVersion = expectedVersion;
		ActualVersion = actualVersion;
		_ = WithContext("resource", resource);
		if (resourceId != null)
		{
			_ = WithContext("resourceId", resourceId);
		}

		_ = WithContext("expectedVersion", expectedVersion);
		_ = WithContext("actualVersion", actualVersion);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConcurrencyException"/> class with string version information.
	/// </summary>
	/// <param name="resource">The type or name of the resource.</param>
	/// <param name="resourceId">The identifier of the resource.</param>
	/// <param name="expectedVersion">The version or ETag that was expected.</param>
	/// <param name="actualVersion">The actual current version or ETag.</param>
	public ConcurrencyException(string resource, string? resourceId, string expectedVersion, string actualVersion)
		: base(ErrorCodes.ResourceConcurrency, FormatConcurrencyMessage(resource, resourceId, expectedVersion, actualVersion))
	{
		Resource = resource;
		ResourceId = resourceId;
		ExpectedVersionString = expectedVersion;
		ActualVersionString = actualVersion;
		_ = WithContext("resource", resource);
		if (resourceId != null)
		{
			_ = WithContext("resourceId", resourceId);
		}

		_ = WithContext("expectedVersion", expectedVersion);
		_ = WithContext("actualVersion", actualVersion);
	}

	/// <summary>
	/// Gets the version that was expected when the operation was attempted.
	/// </summary>
	/// <value>The expected version, or <see langword="null"/> if not using numeric versions.</value>
	public long? ExpectedVersion { get; }

	/// <summary>
	/// Gets the actual current version of the resource.
	/// </summary>
	/// <value>The actual version, or <see langword="null"/> if not using numeric versions.</value>
	public long? ActualVersion { get; }

	/// <summary>
	/// Gets the expected version as a string (for ETag or non-numeric versions).
	/// </summary>
	/// <value>The expected version string, or <see langword="null"/> if not specified.</value>
	public string? ExpectedVersionString { get; }

	/// <summary>
	/// Gets the actual version as a string (for ETag or non-numeric versions).
	/// </summary>
	/// <value>The actual version string, or <see langword="null"/> if not specified.</value>
	public string? ActualVersionString { get; }

	/// <summary>
	/// Creates a <see cref="ConcurrencyException"/> for an aggregate version mismatch.
	/// </summary>
	/// <typeparam name="TAggregate">The type of the aggregate.</typeparam>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="expectedVersion">The expected version.</param>
	/// <param name="actualVersion">The actual version.</param>
	/// <returns>A new <see cref="ConcurrencyException"/> instance.</returns>
	public static ConcurrencyException ForAggregate<TAggregate>(object aggregateId, long expectedVersion, long actualVersion) =>
		new(typeof(TAggregate).Name, aggregateId.ToString(), expectedVersion, actualVersion);

	/// <summary>
	/// Creates a <see cref="ConcurrencyException"/> for an ETag mismatch.
	/// </summary>
	/// <param name="resource">The type or name of the resource.</param>
	/// <param name="resourceId">The resource identifier.</param>
	/// <param name="expectedETag">The expected ETag value.</param>
	/// <param name="actualETag">The actual ETag value.</param>
	/// <returns>A new <see cref="ConcurrencyException"/> instance.</returns>
	public static ConcurrencyException ETagMismatch(string resource, string resourceId, string expectedETag, string actualETag) =>
		new(resource, resourceId, expectedETag, actualETag);

	/// <summary>
	/// Formats the concurrency error message with numeric versions.
	/// </summary>
	private static string FormatConcurrencyMessage(string resource, string? resourceId, long expectedVersion, long actualVersion)
	{
		var resourcePart = resourceId != null
			? $"{resource} with ID '{resourceId}'"
			: resource;
		return $"Concurrency conflict on {resourcePart}. Expected version {expectedVersion}, but actual version is {actualVersion}.";
	}

	/// <summary>
	/// Formats the concurrency error message with string versions.
	/// </summary>
	private static string FormatConcurrencyMessage(string resource, string? resourceId, string expectedVersion, string actualVersion)
	{
		var resourcePart = resourceId != null
			? $"{resource} with ID '{resourceId}'"
			: resource;
		return $"Concurrency conflict on {resourcePart}. Expected version '{expectedVersion}', but actual version is '{actualVersion}'.";
	}

	/// <inheritdoc/>
	protected override IDictionary<string, object?>? GetProblemDetailsExtensions()
	{
		var extensions = new Dictionary<string, object?>(StringComparer.Ordinal);

		if (Resource != null)
		{
			extensions["resource"] = Resource;
		}

		if (ResourceId != null)
		{
			extensions["resourceId"] = ResourceId;
		}

		if (ExpectedVersion.HasValue)
		{
			extensions["expectedVersion"] = ExpectedVersion.Value;
			extensions["actualVersion"] = ActualVersion ?? 0;
		}
		else if (ExpectedVersionString != null)
		{
			extensions["expectedVersion"] = ExpectedVersionString;
			extensions["actualVersion"] = ActualVersionString;
		}

		// Merge with any context data
		foreach (var (key, value) in Context)
		{
			_ = extensions.TryAdd(key, value);
		}

		return extensions.Count > 0 ? extensions : null;
	}
}
