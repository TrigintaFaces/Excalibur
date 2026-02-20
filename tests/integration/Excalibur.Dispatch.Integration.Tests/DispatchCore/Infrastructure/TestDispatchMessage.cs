// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Infrastructure;

/// <summary>
///     Test implementation of IDispatchMessage for use in integration tests.
/// </summary>
public sealed class TestDispatchMessage : IDispatchMessage
{
	/// <inheritdoc />
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <inheritdoc />
	public string MessageId { get; set; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public MessageKinds Kind { get; set; } = MessageKinds.Action;

	/// <inheritdoc />
	public object Body { get; set; } = "Test Body";

	/// <inheritdoc />
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();

	/// <inheritdoc />
	public string MessageType { get; set; } = "TestMessage";

	/// <inheritdoc />
	public IMessageFeatures Features { get; set; } = new DefaultMessageFeatures();
}
