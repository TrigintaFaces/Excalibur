// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Empty dispatch message for creating message contexts.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Framework placeholder type for empty message contexts")]
internal sealed class EmptyDispatchMessage : IDispatchMessage
{
	/// <inheritdoc />
	public object Body => new();

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();

	/// <inheritdoc />
	public string MessageId { get; } = string.Empty;

	/// <inheritdoc />
	public string MessageType { get; } = string.Empty;

	/// <inheritdoc />
	public Guid Id { get; } = Guid.Empty;

	/// <inheritdoc />
	public MessageKinds Kind { get; } = MessageKinds.Action;
}
