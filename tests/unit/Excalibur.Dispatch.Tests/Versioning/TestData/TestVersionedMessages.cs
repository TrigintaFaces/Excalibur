// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Versioning.TestData;

#region User Created Event Versions (v1 -> v2 -> v3 -> v4)

/// <summary>
/// V1: Original event with single Name field.
/// </summary>
public sealed record UserCreatedEventV1 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;

	public int Version => 1;
	public string MessageType => "UserCreatedEvent";
}

/// <summary>
/// V2: Split Name into FirstName and LastName.
/// </summary>
public sealed record UserCreatedEventV2 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string FirstName { get; init; } = string.Empty;
	public string LastName { get; init; } = string.Empty;

	public int Version => 2;
	public string MessageType => "UserCreatedEvent";
}

/// <summary>
/// V3: Added Email field.
/// </summary>
public sealed record UserCreatedEventV3 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string FirstName { get; init; } = string.Empty;
	public string LastName { get; init; } = string.Empty;
	public string Email { get; init; } = string.Empty;

	public int Version => 3;
	public string MessageType => "UserCreatedEvent";
}

/// <summary>
/// V4: Added CreatedAt timestamp.
/// </summary>
public sealed record UserCreatedEventV4 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string FirstName { get; init; } = string.Empty;
	public string LastName { get; init; } = string.Empty;
	public string Email { get; init; } = string.Empty;
	public DateTimeOffset CreatedAt { get; init; }

	public int Version => 4;
	public string MessageType => "UserCreatedEvent";
}

#endregion User Created Event Versions (v1 -> v2 -> v3 -> v4)

#region Order Placed Event Versions (v1 -> v2)

/// <summary>
/// V1: Original order event.
/// </summary>
public sealed record OrderPlacedEventV1 : IDispatchMessage, IVersionedMessage
{
	public Guid OrderId { get; init; }
	public decimal Total { get; init; }

	public int Version => 1;
	public string MessageType => "OrderPlacedEvent";
}

/// <summary>
/// V2: Added Currency field.
/// </summary>
public sealed record OrderPlacedEventV2 : IDispatchMessage, IVersionedMessage
{
	public Guid OrderId { get; init; }
	public decimal Total { get; init; }
	public string Currency { get; init; } = "USD";

	public int Version => 2;
	public string MessageType => "OrderPlacedEvent";
}

#endregion Order Placed Event Versions (v1 -> v2)

#region Non-Versioned Message (for testing non-versioned paths)

/// <summary>
/// A message that does not implement IVersionedMessage.
/// </summary>
public sealed record NonVersionedMessage : IDispatchMessage
{
	public string Data { get; init; } = string.Empty;
}

#endregion Non-Versioned Message (for testing non-versioned paths)

#region Mismatched MessageType (for testing type name vs property mismatch)

/// <summary>
/// V1: Message where type name doesn't follow convention (no V suffix).
/// MessageType property returns "CustomEvent" but type name is "MismatchedTypeEventV1".
/// Tests that the pipeline uses the instance's MessageType property (not derived from type name).
/// </summary>
public sealed record MismatchedTypeEventV1 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string Data { get; init; } = string.Empty;

	public int Version => 1;
	/// <summary>
	/// Returns a different value than what StripVersionSuffix would derive from type name.
	/// Type name: "MismatchedTypeEventV1" -> would derive "MismatchedTypeEvent"
	/// But this property returns "CustomEvent" (intentionally different).
	/// </summary>
	public string MessageType => "CustomEvent";
}

/// <summary>
/// V2: Target version for mismatched type upcasting.
/// Must have same MessageType as V1 for upcasting to work.
/// </summary>
public sealed record MismatchedTypeEventV2 : IDispatchMessage, IVersionedMessage
{
	public Guid Id { get; init; }
	public string Data { get; init; } = string.Empty;
	public string Extra { get; init; } = string.Empty;

	public int Version => 2;
	public string MessageType => "CustomEvent";
}

#endregion Mismatched MessageType (for testing type name vs property mismatch)
