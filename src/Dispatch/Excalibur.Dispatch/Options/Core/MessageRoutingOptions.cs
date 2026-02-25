// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for message routing.
/// </summary>
public sealed class MessageRoutingOptions
{
	/// <summary>
	/// Gets the message type to topic/queue mappings.
	/// </summary>
	/// <value> The mapping of message types to their routing destination. </value>
	public IDictionary<string, string> MessageTypeRouting { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the default routing key pattern.
	/// </summary>
	/// <value> The routing pattern applied when no specific mapping exists. </value>
	[Required]
	public string DefaultRoutingPattern { get; set; } = "{MessageType}";

	/// <summary>
	/// Gets or sets a value indicating whether to use message type as routing key.
	/// </summary>
	/// <value> <see langword="true" /> to use the message type as the routing key; otherwise, <see langword="false" />. </value>
	public bool UseMessageTypeAsRoutingKey { get; set; } = true;

	/// <summary>
	/// Gets custom routing key generators.
	/// </summary>
	/// <value> The map of routing key generator delegates keyed by message type. </value>
	public IDictionary<string, Func<object, string>> RoutingKeyGenerators { get; } =
		new Dictionary<string, Func<object, string>>(StringComparer.Ordinal);
}
