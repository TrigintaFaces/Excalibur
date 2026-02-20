// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a data access event for security auditing purposes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DataAccessEvent" /> class.
/// </remarks>
/// <param name="eventId"> The unique identifier for this data access event. </param>
/// <param name="timestamp"> The timestamp when the data access event occurred. </param>
/// <param name="userId"> The identifier of the user accessing the data. </param>
/// <param name="operation"> The type of data access operation performed. </param>
/// <param name="resourceType"> The type of resource being accessed. </param>
/// <param name="resourceId"> The identifier of the specific resource accessed. </param>
public sealed class DataAccessEvent(
	string eventId,
	DateTimeOffset timestamp,
	string userId,
	DataAccessOperation operation,
	string resourceType,
	string resourceId)
{
	/// <summary>
	/// Gets the unique identifier for this data access event.
	/// </summary>
	/// <value>
	/// The unique identifier for this data access event.
	/// </value>
	public string EventId { get; } = eventId ?? throw new ArgumentNullException(nameof(eventId));

	/// <summary>
	/// Gets the timestamp when the data access event occurred.
	/// </summary>
	/// <value>
	/// The timestamp when the data access event occurred.
	/// </value>
	public DateTimeOffset Timestamp { get; } = timestamp;

	/// <summary>
	/// Gets the identifier of the user accessing the data.
	/// </summary>
	/// <value>
	/// The identifier of the user accessing the data.
	/// </value>
	public string UserId { get; } = userId ?? throw new ArgumentNullException(nameof(userId));

	/// <summary>
	/// Gets the type of data access operation performed.
	/// </summary>
	/// <value>
	/// The type of data access operation performed.
	/// </value>
	public DataAccessOperation Operation { get; } = operation;

	/// <summary>
	/// Gets the type of resource being accessed.
	/// </summary>
	/// <value>
	/// The type of resource being accessed.
	/// </value>
	public string ResourceType { get; } = resourceType ?? throw new ArgumentNullException(nameof(resourceType));

	/// <summary>
	/// Gets the identifier of the specific resource accessed.
	/// </summary>
	/// <value>
	/// The identifier of the specific resource accessed.
	/// </value>
	public string ResourceId { get; } = resourceId ?? throw new ArgumentNullException(nameof(resourceId));

	/// <summary>
	/// Gets the IP address from which the data access originated.
	/// </summary>
	/// <value>
	/// The IP address from which the data access originated.
	/// </value>
	public string? IpAddress { get; init; }

	/// <summary>
	/// Gets the application or service that initiated the data access.
	/// </summary>
	/// <value>
	/// The application or service that initiated the data access.
	/// </value>
	public string? SourceApplication { get; init; }

	/// <summary>
	/// Gets the data sensitivity classification level.
	/// </summary>
	/// <value>
	/// The data sensitivity classification level.
	/// </value>
	public string? DataClassification { get; init; }

	/// <summary>
	/// Gets the amount of data accessed or modified.
	/// </summary>
	/// <value>
	/// The amount of data accessed or modified.
	/// </value>
	public long? DataSize { get; init; }

	/// <summary>
	/// Gets a value indicating whether the data access was successful.
	/// </summary>
	/// <value>
	/// A value indicating whether the data access was successful.
	/// </value>
	public bool IsSuccessful { get; init; } = true;

	/// <summary>
	/// Gets the reason for data access failure, if applicable.
	/// </summary>
	/// <value>
	/// The reason for data access failure, if applicable.
	/// </value>
	public string? FailureReason { get; init; }

	/// <summary>
	/// Gets additional context information about the data access event.
	/// </summary>
	/// <value>
	/// Additional context information about the data access event.
	/// </value>
	public IReadOnlyDictionary<string, object>? Context { get; init; }

	/// <summary>
	/// Gets the unique identifier for this data access event (alias for EventId).
	/// </summary>
	/// <value>
	/// The unique identifier for this data access event.
	/// </value>
	public string Id => EventId;

	/// <summary>
	/// Gets the full resource path being accessed (alias for ResourceId).
	/// </summary>
	/// <value>
	/// The full resource path being accessed.
	/// </value>
	public string ResourcePath => ResourceId;
}
