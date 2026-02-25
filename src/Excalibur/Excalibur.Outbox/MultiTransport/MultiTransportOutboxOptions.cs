// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Outbox.MultiTransport;

/// <summary>
/// Configuration options for multi-transport outbox message routing.
/// </summary>
/// <remarks>
/// <para>
/// Defines transport bindings that map message type patterns to specific transports.
/// When a message is staged, the outbox processor uses these bindings to determine
/// which transport(s) should deliver the message.
/// </para>
/// </remarks>
public sealed class MultiTransportOutboxOptions
{
	/// <summary>
	/// Gets the transport bindings mapping message type patterns to transport names.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Key: message type name or wildcard pattern (e.g., "OrderCreated", "Order*").
	/// Value: transport name (e.g., "kafka", "rabbitmq", "azure-servicebus").
	/// </para>
	/// </remarks>
	/// <value>The dictionary of transport bindings.</value>
	public Dictionary<string, string> TransportBindings { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets or sets the default transport name used when no specific binding matches.
	/// </summary>
	/// <value>The default transport name. Defaults to "default".</value>
	[Required]
	public string DefaultTransport { get; set; } = "default";

	/// <summary>
	/// Gets or sets a value indicating whether to throw when a message type has no matching binding.
	/// </summary>
	/// <remarks>
	/// When <see langword="false"/>, unmatched messages are routed to <see cref="DefaultTransport"/>.
	/// When <see langword="true"/>, an <see cref="InvalidOperationException"/> is thrown for unmatched messages.
	/// </remarks>
	/// <value><see langword="true"/> to require explicit bindings; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool RequireExplicitBindings { get; set; }
}
