// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.TestFakes;

/// <summary>
/// Fake implementation of IDispatchMessage for testing purposes.
/// </summary>
public sealed class FakeDispatchMessage : IDispatchMessage
{
	public Guid Id { get; } = Guid.NewGuid();

	public string MessageId { get; } = Guid.NewGuid().ToString();

	public string Type { get; set; } = "FakeMessage";

	public string MessageType { get; set; } = "FakeMessage";

	public MessageKinds Kind { get; set; } = MessageKinds.Event;

	public object Body { get; set; } = new { Data = "test" };

	public ReadOnlyMemory<byte> Payload { get; set; } = new byte[] { 1, 2, 3, 4, 5 };

	public IReadOnlyDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();

	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	[JsonIgnore]
	public IMessageFeatures Features { get; set; } = new DefaultMessageFeatures();
}
