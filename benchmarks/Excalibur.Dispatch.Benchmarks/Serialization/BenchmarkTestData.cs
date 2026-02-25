// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

namespace Excalibur.Dispatch.Benchmarks.Serialization;

#region User Created Event Versions (v1 -> v2 -> v3 -> v4)

/// <summary>
/// V1: Original event with single Name field.
/// </summary>
public sealed record BenchmarkUserCreatedEventV1 : IDispatchMessage, IVersionedMessage
{
	/// <summary>Gets or initializes the user identifier.</summary>
	public Guid Id { get; init; }

	/// <summary>Gets or initializes the user's full name.</summary>
	public string Name { get; init; } = string.Empty;

	/// <inheritdoc/>
	public int Version => 1;

	/// <inheritdoc/>
	public string MessageType => "UserCreatedEvent";
}

/// <summary>
/// V2: Split Name into FirstName and LastName.
/// </summary>
public sealed record BenchmarkUserCreatedEventV2 : IDispatchMessage, IVersionedMessage
{
	/// <summary>Gets or initializes the user identifier.</summary>
	public Guid Id { get; init; }

	/// <summary>Gets or initializes the user's first name.</summary>
	public string FirstName { get; init; } = string.Empty;

	/// <summary>Gets or initializes the user's last name.</summary>
	public string LastName { get; init; } = string.Empty;

	/// <inheritdoc/>
	public int Version => 2;

	/// <inheritdoc/>
	public string MessageType => "UserCreatedEvent";
}

/// <summary>
/// V3: Added Email field.
/// </summary>
public sealed record BenchmarkUserCreatedEventV3 : IDispatchMessage, IVersionedMessage
{
	/// <summary>Gets or initializes the user identifier.</summary>
	public Guid Id { get; init; }

	/// <summary>Gets or initializes the user's first name.</summary>
	public string FirstName { get; init; } = string.Empty;

	/// <summary>Gets or initializes the user's last name.</summary>
	public string LastName { get; init; } = string.Empty;

	/// <summary>Gets or initializes the user's email address.</summary>
	public string Email { get; init; } = string.Empty;

	/// <inheritdoc/>
	public int Version => 3;

	/// <inheritdoc/>
	public string MessageType => "UserCreatedEvent";
}

/// <summary>
/// V4: Added CreatedAt timestamp.
/// </summary>
public sealed record BenchmarkUserCreatedEventV4 : IDispatchMessage, IVersionedMessage
{
	/// <summary>Gets or initializes the user identifier.</summary>
	public Guid Id { get; init; }

	/// <summary>Gets or initializes the user's first name.</summary>
	public string FirstName { get; init; } = string.Empty;

	/// <summary>Gets or initializes the user's last name.</summary>
	public string LastName { get; init; } = string.Empty;

	/// <summary>Gets or initializes the user's email address.</summary>
	public string Email { get; init; } = string.Empty;

	/// <summary>Gets or initializes the timestamp when the user was created.</summary>
	public DateTimeOffset CreatedAt { get; init; }

	/// <inheritdoc/>
	public int Version => 4;

	/// <inheritdoc/>
	public string MessageType => "UserCreatedEvent";
}

#endregion User Created Event Versions (v1 -> v2 -> v3 -> v4)

#region Order Placed Event Versions (v1 -> v2)

/// <summary>
/// V1: Original order event.
/// </summary>
public sealed record BenchmarkOrderPlacedEventV1 : IDispatchMessage, IVersionedMessage
{
	/// <summary>Gets or initializes the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets or initializes the order total.</summary>
	public decimal Total { get; init; }

	/// <inheritdoc/>
	public int Version => 1;

	/// <inheritdoc/>
	public string MessageType => "OrderPlacedEvent";
}

/// <summary>
/// V2: Added Currency field.
/// </summary>
public sealed record BenchmarkOrderPlacedEventV2 : IDispatchMessage, IVersionedMessage
{
	/// <summary>Gets or initializes the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets or initializes the order total.</summary>
	public decimal Total { get; init; }

	/// <summary>Gets or initializes the currency code.</summary>
	public string Currency { get; init; } = "USD";

	/// <inheritdoc/>
	public int Version => 2;

	/// <inheritdoc/>
	public string MessageType => "OrderPlacedEvent";
}

#endregion Order Placed Event Versions (v1 -> v2)

#region Non-Versioned Message

/// <summary>
/// A message that does not implement IVersionedMessage.
/// Used to test passthrough performance.
/// </summary>
public sealed record BenchmarkNonVersionedMessage : IDispatchMessage
{
	/// <summary>Gets or initializes the message data.</summary>
	public string Data { get; init; } = string.Empty;
}

#endregion Non-Versioned Message
