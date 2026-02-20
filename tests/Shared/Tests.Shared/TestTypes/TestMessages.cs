// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Tests.Shared.TestTypes;

/// <summary>
/// Simple test message for integration testing.
/// </summary>
public class SimpleTestMessage : IDispatchMessage
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	public string MessageId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the content.
	/// </summary>
	public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Test message with payload for integration testing.
/// </summary>
public class TestMessageWithPayload : IDispatchMessage
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	public string MessageId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the payload.
	/// </summary>
	public object? Payload { get; set; }

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	public string MessageType { get; set; } = "TestMessageWithPayload";
}

/// <summary>
/// Test message with version for type resolution testing.
/// </summary>
public class VersionedTestMessage : IDispatchMessage
{
	/// <summary>
	/// Gets or sets the version.
	/// </summary>
	public int Version { get; set; } = 1;

	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	public string MessageId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the content.
	/// </summary>
	public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Test message with complex type for serialization testing.
/// </summary>
public class ComplexTestMessage : IDispatchMessage
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	public string MessageId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the nested data.
	/// </summary>
	public Dictionary<string, object?> NestedData { get; set; } = new();

	/// <summary>
	/// Gets or sets the items.
	/// </summary>
	public List<string> Items { get; set; } = new();

	/// <summary>
	/// Gets or sets the timestamp.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Generic test message for type resolution testing.
/// </summary>
/// <typeparam name="T">The payload type.</typeparam>
public class GenericMessage<T> : IDispatchMessage
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	public string MessageId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the payload.
	/// </summary>
	public T? Payload { get; set; }

	/// <summary>
	/// Gets or sets the message type name.
	/// </summary>
	public string MessageTypeName { get; set; } = typeof(T).Name;

	/// <summary>
	/// Gets or sets the timestamp.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
